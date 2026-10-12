using System;
using System.Collections.Generic;
using System.Linq;
using Baiye.CryogenicPantry.Core;
using KSerialization;
using UnityEngine;

namespace Baiye.CryogenicPantry
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PantryBuilding : KMonoBehaviour, ISaveLoadable
    {
        internal static readonly Dictionary<int, PantryBuilding> Live = new Dictionary<int, PantryBuilding>();
        [Serialize] public double InternalK = 293.15;
        [Serialize] public int SelectedGas = (int)SimHashes.CarbonDioxide;
        [Serialize] public bool AutoGas = true, AutoDrinks = true;
        [Serialize] public List<string> RecipeIds = new List<string>();
        [Serialize] public List<bool> Enabled = new List<bool>();
        [Serialize] public List<float> Targets = new List<float>();
        [Serialize] public List<bool> Unlimited = new List<bool>();
        [Serialize] public List<string> ReserveIds = new List<string> { "PrickleFruit", "SpiceNut" };
        [Serialize] public List<float> Reserves = new List<float> { 20, 10 };
        [Serialize] string pendingId;
        [Serialize] public float Progress;
        [Serialize] public double RecipeGasUsed, SealGasUsed, PurgeGasUsed, ElectricalJoules, HeatRejected;
        [Serialize] bool initialized;
        Storage[] stores;
        FilteredStorage filtered;
        EnergyConsumer power;
        Operational operational;
        BuildingHP hp;
        KBatchedAnimController anim;
        string clip;
        float lastFood, lastDrinks, doorCooldown, doorAnim;
        bool internalMove, cooling, plannedOperational, plannedProduction;
        float requestedWatts, plannedCoolingKW;
        string lastBlockingFlags;
        int nextRecipe;
        public bool Ready { get; private set; }
        public float PaidJoules;
        public float LastWatts, LastHeatKW, LastPaidFraction;
        public Storage Food => stores[0]; public Storage Drinks => stores[1]; public Storage Gas => stores[2];
        public Storage Water => stores[3]; public Storage Batch => stores[4]; public Storage Serving => stores[5];
        public DrinkRecipe Pending => DrinkRecipes.Find(pendingId);
        public bool Working => Ready && operational.IsOperational && hp.HitPoints > 0;
        public bool Cooling => cooling && LastPaidFraction > 0;
        public bool Processing => Pending != null && ProcessEnabled && LastPaidFraction > 0 && Working;
        bool ProcessEnabled => Pending != null && PantryRules.ProcessAllowed(AutoDrinks,Enabled[RecipeIds.IndexOf(Pending.Id)],SelectedGas==(int)SimHashes.CarbonDioxide,GasRatio);
        public bool Broken => hp != null && hp.IsBroken;
        public bool Sterile => Ready && !Broken && Gas.MassStored() >= PantryRules.SterileMinimum &&
            Gas.items.All(i => i == null || IsSterileGas(i.GetComponent<PrimaryElement>().ElementID));
        public bool Replacing => Ready && Gas.MassStored() > .0001f && Gas.GetMassAvailable(new Tag(((SimHashes)SelectedGas).ToString())) / Gas.MassStored() < .95f;
        public float GasRatio => Gas.MassStored() <= 0 ? 0 : Gas.GetMassAvailable(new Tag(((SimHashes)SelectedGas).ToString())) / Gas.MassStored();
        public static bool IsSterileGas(SimHashes gas) => SupportsGas(ElementLoader.FindElementByHash(gas));
        public static bool SupportsGas(Element element) => element!=null && PantryRules.GasPhaseAllowed(element.IsGas,
            Rottable.AtmosphereModifier.TryGetValue((int)element.id, out var quality) && quality == Rottable.RotAtmosphereQuality.Sterilizing,element.lowTemp,element.highTemp);
        public static string GasName(SimHashes id)
        {var e=ElementLoader.FindElementByHash(id);return e==null?"?":global::STRINGS.UI.StripLinkFormatting(e.name);}
        IEnumerable<KeyValuePair<int,float>> GasInventory => Gas.items.Where(g=>g!=null).Select(g=>g.GetComponent<PrimaryElement>())
            .GroupBy(p=>(int)p.ElementID).Select(g=>new KeyValuePair<int,float>(g.Key,g.Sum(p=>p.Mass)));
        public SimHashes StoredGas => Ready?(SimHashes)GasInventory.OrderByDescending(g=>g.Value).Select(g=>g.Key).FirstOrDefault():SimHashes.Vacuum;
        SimHashes StoredSupportedGas => (SimHashes)GasInventory.Where(g=>IsSterileGas((SimHashes)g.Key)).OrderByDescending(g=>g.Value).Select(g=>g.Key).FirstOrDefault();
        int? ReplacementReturnGas => PantryRules.ReplacementReturnTarget(SelectedGas,GasInventory,id=>IsSterileGas((SimHashes)id));
        public bool SelectGas(SimHashes gas)
        {if(!Ready||!IsSterileGas(gas))return false;SelectedGas=(int)gas;return true;}
        public void CancelGasReplacement()
        {if(Ready&&Replacing){var gas=ReplacementReturnGas;if(gas.HasValue)SelectGas((SimHashes)gas.Value);}}
        public bool CanCancelGasReplacement => Ready&&Replacing&&ReplacementReturnGas.HasValue;
        public float? WarmestFoodK => Ready?Food.items.Where(g=>g!=null&&g.GetSMI<Rottable.Instance>()!=null)
            .Select(g=>(float?)g.GetComponent<PrimaryElement>().Temperature).Max():null;
        public string StopReason
        {
            get
            {
                if(!Ready)return PantryText.T("初始化中","Initializing");
                if(Broken)return PantryText.T("设备损坏：维修建筑","Damaged: repair building");
                if(operational.Flags.TryGetValue(BuildingEnabledButton.EnabledFlag,out bool enabled)&&!enabled)
                    return ExplainBlockingFlag(BuildingEnabledButton.EnabledFlag.Name);
                if(GetComponent<LogicOperationalController>()!=null&&!operational.GetFlag(LogicOperationalController.LogicOperationalFlag))
                    return PantryText.T("自动化红信号：提供绿信号或断开自动化线","Red automation signal: send green or disconnect automation wire");
                if(!power.HasWire)return PantryText.T("未接电线：连接电力输入","No wire: connect power input");
                if(!power.IsPowered)return PantryText.T("供电不足：检查电源和电路负载","Insufficient power: check supply and circuit load");
                if(!operational.IsOperational)return ExplainBlockingFlags(operational.Flags);
                if(LastPaidFraction<=0)return PantryText.T("等待电路实际供电","Waiting for circuit payment");
                return "";
            }
        }
        public static string ExplainBlockingFlags(IEnumerable<KeyValuePair<Operational.Flag,bool>> flags)
        {
            var reasons=flags.Where(f=>!f.Value).OrderBy(f=>f.Key.Name).Select(f=>ExplainBlockingFlag(f.Key.Name)).ToArray();
            return reasons.Length>0?string.Join("\n",reasons):PantryText.T("运行状态尚未就绪：等待设备检查完成","Operational state not ready: waiting for building checks");
        }
        static string ExplainBlockingFlag(string name)
        {
            switch(name)
            {
                case "building_enabled":return PantryText.T("建筑已手动禁用：点击“启用建筑”","Manually disabled: enable building");
                case "not_flooded":return PantryText.T("建筑被淹没：排走建筑所在格的液体","Flooded: drain liquid from the building cells");
                case "not_entombed":return PantryText.T("建筑被掩埋：清理占据建筑的固体","Entombed: clear solids occupying the building");
                case "healthy":return PantryText.T("建筑损坏：维修建筑","Damaged: repair building");
                case "powered":return PantryText.T("供电不足：检查电源和电路负载","Insufficient power: check supply and circuit load");
                case "LogicOperational":return PantryText.T("自动化红信号：提供绿信号或断开自动化线","Red automation signal: send green or disconnect automation wire");
                case "inputConnected":return PantryText.T("气体输入未连接：检查管道连接","Gas input disconnected: check pipe connection");
                case "pipesHaveMass":return PantryText.T("气体输入管道为空：检查供气","Gas input pipe empty: check gas supply");
                default:return PantryText.T("未满足运行条件：","Unmet operational condition: ")+name;
            }
        }
        public string GasSupplyStatus
        {
            get
            {
                if(!Ready)return "";
                if(!AutoGas)return PantryText.T("自动补气已关闭；选择气体不会启动置换","Auto gas off; selecting a gas does not start replacement");
                if(!Working||LastPaidFraction<=0)return PantryText.T("补气暂停：","Gas intake paused: ")+StopReason;
                int cell=GetComponent<Building>().GetUtilityInputCell();var flow=Game.Instance.gasConduitFlow;
                if(!flow.HasConduit(cell))return PantryText.T("未接气管：连接顶部气体输入","No gas pipe: connect top gas input");
                var packet=flow.GetContents(cell);
                if(packet.mass<=0)return PantryText.T("等待气管供气","Waiting for gas supply");
                if((int)packet.element!=SelectedGas)return PantryText.T("管内是","Pipe contains ")+GasName(packet.element)+PantryText.T("；需要","; needs ")+GasName((SimHashes)SelectedGas);
                var element=ElementLoader.FindElementByHash(packet.element);
                if(element==null||!PantryRules.GasIntakeAllowed(packet.temperature,element.lowTemp,element.highTemp))
                    return PantryText.T($"拒收不适温度气体：{packet.temperature-273.15f:0.0}°C，先冷却到50°C以内并保持气态",$"Rejected gas at {packet.temperature-273.15f:0.0} C: cool to 50 C or less and keep gaseous");
                if(!Replacing&&Gas.MassStored()>=PantryRules.GasCapacity-.0001f)return PantryText.T("气罐已满，停止补气","Gas tank full; intake stopped");
                return Replacing?PantryText.T("正在接入目标气体并置换旧气体","Receiving target gas and replacing old gas"):PantryText.T("正在补充气体","Receiving gas");
            }
        }
        public static PantryBuilding Owner(GameObject go)
        {
            var s = go?.GetComponent<Pickupable>()?.storage;
            var b = s?.GetComponent<PantryBuilding>();
            return b != null && b.Ready && (s == b.Food || s == b.Drinks || s == b.Batch || s == b.Serving) ? b : null;
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            PantryConfig.ConfigureInputs(GetComponent<RequireInputs>(),GetComponent<ConduitConsumer>());
            stores = GetComponents<Storage>();
            if (stores.Length != 6) { Debug.LogError("[BaiyeCryogenicPantry] Invalid storage contract"); return; }
            power = GetComponent<EnergyConsumer>(); operational = GetComponent<Operational>(); hp = GetComponent<BuildingHP>(); anim = GetComponent<KBatchedAnimController>();
            foreach (var r in DrinkRecipes.All) if (!RecipeIds.Contains(r.Id))
            { RecipeIds.Add(r.Id); Enabled.Add(false); Targets.Add(r.Id == "BaiyeEnergySoda" ? 5 : 20); Unlimited.Add(false); }
            while (Enabled.Count < RecipeIds.Count) Enabled.Add(false);
            while (Targets.Count < RecipeIds.Count) Targets.Add(20);
            while (Unlimited.Count < RecipeIds.Count) Unlimited.Add(false);
            for (int i = 0; i < Targets.Count; i++) if (float.IsNaN(Targets[i]) || float.IsInfinity(Targets[i]) || Targets[i] < 0) Targets[i] = 0;
            if (!initialized) { InternalK = GetComponent<PrimaryElement>().Temperature; initialized = true; }
            SelectedGas=PantryRules.RecoverGasSelection(SelectedGas,(int)StoredSupportedGas,(int)SimHashes.CarbonDioxide,id=>IsSterileGas((SimHashes)id));
            Ready = true;
            internalMove = true; ReturnServing(); if (Pending == null && Batch.MassStored() > 0) ReturnBatch(); internalMove = false;
            lastFood = Food.MassStored(); lastDrinks = Drinks.MassStored();
            Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            Subscribe((int)GameHashes.CopySettings, CopySettings);
            Subscribe((int)GameHashes.BuildingReceivedDamage, OnDamage);
            filtered = new FilteredStorage(this, new[] { GameTags.Compostable, new Tag("BaiyePantryDrink") }, null, false, Db.Get().ChoreTypes.FoodFetch);
            filtered.SetHasMeter(false); var foodRules=GetComponent<FoodStorage>();foodRules.FilteredStorage=filtered;
            foodRules.SpicedFoodOnly=foodRules.SpicedFoodOnly; filtered.FilterChanged();
            Live[power.GetInstanceID()] = this; UpdateAnimation();
        }
        public float Reserve(string id)
        {
            int i = ReserveIds.IndexOf(id); return i >= 0 && i < Reserves.Count ? Math.Max(0, Reserves[i]) : 0;
        }
        public void SetReserve(string id, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) return;
            int i = ReserveIds.IndexOf(id); if (i < 0) { ReserveIds.Add(id); Reserves.Add(value); } else Reserves[i] = value;
        }
        public float Servings(DrinkRecipe r) => Drinks.GetMassAvailable(new Tag(r.Id)) / r.Mass;
        public float DrinkMass => Drinks.MassStored() + Serving.MassStored();
        public string RecipeStatus(DrinkRecipe r)
        {
            int n = RecipeIds.IndexOf(r.Id);
            if (n < 0 || !Enabled[n] || !AutoDrinks) return PantryText.T("生产已关闭", "Production disabled");
            if (Pending == r)
            {
                if(!Working||LastPaidFraction<=0)return PantryText.T("加工暂停：", "Processing paused: ")+StopReason;
                if(!ProcessEnabled)return PantryText.T("加工暂停：需要二氧化碳气氛", "Processing paused: needs carbon dioxide atmosphere");
                return PantryText.T($"加工中 {Progress / r.Seconds:P0}", $"Processing {Progress / r.Seconds:P0}");
            }
            if (!Unlimited[n] && Servings(r) + (Pending?.Id == r.Id ? 1 : 0) >= Targets[n] - .0001f) return PantryText.T("已达目标库存", "Target reached");
            if (!Working) return PantryText.T("设备停机：", "Stopped: ")+StopReason;
            if (DrinkMass + (Pending?.Mass ?? 0) + r.Mass > PantryRules.DrinkCapacity + .0001f) return PantryText.T("饮料仓已满", "Drink chamber full");
            if (SelectedGas != (int)SimHashes.CarbonDioxide || GasRatio < .95f || Gas.GetMassAvailable(SimHashes.CarbonDioxide.CreateTag()) < r.CarbonDioxide)
                return PantryText.T("需要二氧化碳", "Needs carbon dioxide");
            if (Water.GetMassAvailable(GameTags.Water) + .00001f < r.Water) return PantryText.T("缺少水：连接净水管", "Needs water: connect clean water");
            foreach (var s in r.Solids) if (!PantryRules.CanSpend(Food.GetMassAvailable(new Tag(s.Id)), s.Mass, Reserve(s.Id)))
                return PantryText.T("原料不足或达到保留量：", "Missing ingredient or reserve: ") + Proper(s.Id);
            return PantryText.T("原料充足", "Ingredients ready");
        }
        public static string Proper(string id)
        { var p = Assets.GetPrefab(new Tag(id)); return p == null ? id : global::STRINGS.UI.StripLinkFormatting(p.GetProperName()); }
        bool CanStart(DrinkRecipe r) => RecipeStatus(r) == PantryText.T("原料充足", "Ingredients ready");
        void OnStorageChanged(object data)
        {
            if (!Ready) return;
            float food = Food.MassStored(), drinks = Drinks.MassStored();
            if (!internalMove && (Math.Abs(food-lastFood) > .00001f || Math.Abs(drinks-lastDrinks) > .00001f)) OpenDoor();
            lastFood = food; lastDrinks = drinks;
        }
        public void OpenDoor()
        {
            if (!Ready || doorCooldown > 0) return;
            doorCooldown = 1; doorAnim = 1;
            LoseGas(PantryRules.DoorLoss(Gas.MassStored()), false);
            float wall = GetComponent<PrimaryElement>().Temperature;
            double q = Math.Max(0, (wall-InternalK) * .05); InternalK += q / PantryRules.ShellHeatCapacity;
            AddHeat((float)-q, .2f);
        }
        void CopySettings(object data)
        {
            var src = (data as GameObject)?.GetComponent<PantryBuilding>(); if (src == null || src == this || !src.Ready) return;
            AutoGas = src.AutoGas; AutoDrinks = src.AutoDrinks;
            SelectGas((SimHashes)src.SelectedGas);
            foreach (var r in DrinkRecipes.All) { int a = RecipeIds.IndexOf(r.Id), b = src.RecipeIds.IndexOf(r.Id); if (a >= 0 && b >= 0) { Enabled[a] = src.Enabled[b]; Targets[a] = src.Targets[b]; Unlimited[a] = src.Unlimited[b]; } }
            ReserveIds = new List<string>(src.ReserveIds); Reserves = new List<float>(src.Reserves);
        }
        void OnDamage(object data) { if (Broken && Ready) Gas.DropAll(true, false); }
        public void MarkDestroyed() { internalMove = true; Ready = false; LastPaidFraction = 0; }
        protected override void OnCleanUp()
        {
            Ready = false; if (power != null) Live.Remove(power.GetInstanceID());
            filtered?.CleanUp(); filtered = null; base.OnCleanUp();
        }
        public void PreparePower(float dt)
        {
            PaidJoules = 0;
            string blocked=string.Join(",",operational.Flags.Where(f=>!f.Value).Select(f=>f.Key.Name).OrderBy(n=>n));
            if(blocked!=lastBlockingFlags)
            {
                lastBlockingFlags=blocked;
                Debug.Log($"[BaiyeCryogenicPantry] {GetInstanceID()} operational={operational.IsOperational}, blockingFlags=[{blocked}], paidPrevious={LastPaidFraction:0.000}");
            }
            cooling = InternalK > PantryRules.TargetK + .75f || (cooling && InternalK > PantryRules.TargetK + .02f);
            plannedProduction = AutoDrinks && (Pending != null ? ProcessEnabled : DrinkRecipes.All.Any(CanStart));
            bool maintenance=InternalK > PantryRules.TargetK + .1f;
            plannedCoolingKW=PantryRules.CoolingRate(cooling,maintenance);
            requestedWatts = PantryRules.Power(cooling, maintenance, plannedProduction);
            power.BaseWattageRating = requestedWatts;
            plannedOperational = Working; operational.SetActive(plannedOperational);
        }
        public void SettlePower(float dt)
        {
            if (!Ready || dt <= 0) return;
            doorCooldown = Math.Max(0, doorCooldown-dt); doorAnim = Math.Max(0, doorAnim-dt);
            LastPaidFraction = plannedOperational && !Broken ? PantryRules.PaidFraction(PaidJoules, requestedWatts, dt) : 0;
            LastWatts = PaidJoules / dt; ElectricalJoules += PaidJoules;
            double rejected = 0;
            float wall = GetComponent<PrimaryElement>().Temperature;
            double leak = (wall-InternalK) * PantryRules.InsulationKWPerK * dt;
            InternalK += leak / PantryRules.ShellHeatCapacity; rejected -= leak;
            foreach (var store in new[] { Food, Drinks, Gas, Batch, Serving })
                foreach (var go in store.items.ToArray())
                {
                    if (go == null) continue; var p = go.GetComponent<PrimaryElement>();
                    float c = p.Mass * p.Element.specificHeatCapacity;
                    if (c <= .000001f) continue;
                    double q = PantryRules.ExchangeEnergy(p.Temperature,c,InternalK,dt);
                    float before=p.Temperature;p.Temperature=(float)(before-q/c);
                    // Native item temperature is float. Transfer only its actual
                    // representable energy change to the double-precision shell.
                    q=((double)before-p.Temperature)*c; InternalK += q / PantryRules.ShellHeatCapacity;
                }
            float workDt = dt * LastPaidFraction;
            if (workDt > 0)
            {
                if (plannedCoolingKW > 0)
                {
                    float budget = plannedCoolingKW * workDt;
                    double q = PantryRules.CoolingEnergy(InternalK, PantryRules.ShellHeatCapacity, PantryRules.TargetK, budget);
                    InternalK -= q / PantryRules.ShellHeatCapacity; rejected += q;
                }
                Intake(workDt);
                if (Pending == null && AutoDrinks) for (int k = 0; k < DrinkRecipes.All.Count; k++)
                {
                    int i = (nextRecipe+k) % DrinkRecipes.All.Count; var r = DrinkRecipes.All[i];
                    if (CanStart(r) && ReserveBatch(r)) { nextRecipe=(i+1)%DrinkRecipes.All.Count; break; }
                }
                var job = Pending;
                if (job != null && ProcessEnabled && plannedProduction)
                {
                    if (!BatchValid(job)) ReturnBatch();
                    else { Progress = Math.Min(job.Seconds, Progress + workDt); if (Progress >= job.Seconds && DrinkMass+job.Mass <= PantryRules.DrinkCapacity+.0001f) FinishBatch(job); }
                }
            }
            rejected += PaidJoules / 1000f;
            LastHeatKW = (float)(rejected / dt); HeatRejected += rejected;
            AddHeat((float)rejected, dt); UpdateAnimation();
            GetComponent<PantryDrinkWorkable>().RefreshChore();
        }
        void AddHeat(float energy, float dt)
        {
            var temperatures = GameComps.StructureTemperatures;
            temperatures?.ProduceEnergy(temperatures.GetHandle(gameObject), energy, "Cryogenic Pantry", dt);
        }
        void Intake(float dt)
        {
            var building = GetComponent<Building>(); int cell = building.GetUtilityInputCell();
            if (AutoGas && Game.Instance.gasConduitFlow.HasConduit(cell))
            {
                var flow = Game.Instance.gasConduitFlow; var packet = flow.GetContents(cell);
                var packetElement=ElementLoader.FindElementByHash(packet.element);
                if ((int)packet.element == SelectedGas && SupportsGas(packetElement) && packet.mass > 0 &&
                    PantryRules.GasIntakeAllowed(packet.temperature,packetElement.lowTemp,packetElement.highTemp))
                {
                    float have = Gas.MassStored(), selected = Gas.GetMassAvailable(packet.element.CreateTag());
                    float amount = Math.Min(packet.mass, PantryRules.ReplacementIntake(have, selected, .10f * dt));
                    if (amount > .000001f)
                    {
                        var receipt = flow.RemoveElement(cell, amount);
                        // Incoming mass is authoritative; never purge before a real receipt.
                        if (receipt.mass > 0)
                        {
                            float replace = Math.Max(0, have+receipt.mass-PantryRules.GasCapacity);
                            foreach (var old in Gas.items.ToArray()) if (old != null && old.GetComponent<PrimaryElement>().ElementID != packet.element)
                            { float n=Math.Min(replace,old.GetComponent<PrimaryElement>().Mass); Gas.ConsumeIgnoringDisease(old.PrefabID(),n); PurgeGasUsed+=n; replace-=n; if(replace<=.000001f)break; }
                            Gas.AddGasChunk(receipt.element,receipt.mass,receipt.temperature,receipt.diseaseIdx,receipt.diseaseCount,false);
                            if (GasRatio >= .95f) foreach (var old in Gas.items.ToArray()) if (old != null && (int)old.GetComponent<PrimaryElement>().ElementID != SelectedGas)
                            { PurgeGasUsed+=old.GetComponent<PrimaryElement>().Mass; Gas.ConsumeIgnoringDisease(old); }
                        }
                    }
                }
            }
            int liquidCell=Grid.OffsetCell(building.NaturalBuildingCell(),new CellOffset(1,0));
            if (AutoDrinks && Game.Instance.liquidConduitFlow.HasConduit(liquidCell))
            {
                var flow=Game.Instance.liquidConduitFlow;var packet=flow.GetContents(liquidCell);
                if(packet.element==SimHashes.Water)
                { float n=Math.Min(packet.mass,Math.Min(Water.RemainingCapacity(),dt));if(n>.000001f){var r=flow.RemoveElement(liquidCell,n);if(r.mass>0)Water.AddLiquid(r.element,r.mass,r.temperature,r.diseaseIdx,r.diseaseCount);} }
            }
        }
        void LoseGas(float mass, bool recipe)
        {
            float total=Gas.MassStored();if(total<=0||mass<=0)return;
            foreach(var go in Gas.items.ToArray())if(go!=null){float n=Math.Min(go.GetComponent<PrimaryElement>().Mass,mass*go.GetComponent<PrimaryElement>().Mass/total);Gas.ConsumeIgnoringDisease(go.PrefabID(),n);if(recipe)RecipeGasUsed+=n;else SealGasUsed+=n;}
        }
        bool MoveMass(Storage from, Storage to, Tag tag, float mass)
        {
            foreach (var go in from.items.ToArray()) if (go != null && go.HasTag(tag))
            {
                float amount=Math.Min(mass,go.GetComponent<PrimaryElement>().Mass); if(amount<=0)continue;
                var item=go.GetComponent<Pickupable>().Take(amount);if(item==null)continue;
                float moved=item.PrimaryElement.Mass;to.Store(item.gameObject);mass-=moved;if(mass<=.00001f)return true;
            }
            return mass<=.00001f;
        }
        bool ReserveBatch(DrinkRecipe r)
        {
            internalMove=true;bool ok=MoveMass(Water,Batch,GameTags.Water,r.Water)&&MoveMass(Gas,Batch,SimHashes.CarbonDioxide.CreateTag(),r.CarbonDioxide);
            if(ok)foreach(var s in r.Solids)if(!MoveMass(Food,Batch,new Tag(s.Id),s.Mass)){ok=false;break;}
            if(ok){pendingId=r.Id;Progress=0;RecipeGasUsed+=r.CarbonDioxide;}
            else ReturnBatch();internalMove=false;return ok;
        }
        bool BatchValid(DrinkRecipe r) => Batch.GetMassAvailable(GameTags.Water)+.00001f>=r.Water && Batch.GetMassAvailable(SimHashes.CarbonDioxide.CreateTag())+.00001f>=r.CarbonDioxide && r.Solids.All(s=>Batch.GetMassAvailable(new Tag(s.Id))+.00001f>=s.Mass);
        void FinishBatch(DrinkRecipe r)
        {
            var prefab=Assets.GetPrefab(new Tag(r.Id)); if(prefab==null)return;
            float mass=Batch.MassStored();if(Math.Abs(mass-r.Mass)>.0001f){ReturnBatch();return;}
            var product=Util.KInstantiate(prefab,transform.GetPosition());var p=product.GetComponent<PrimaryElement>();
            p.Mass=mass;float energyAboveTarget=0;
            foreach(var item in Batch.items)if(item!=null){var input=item.GetComponent<PrimaryElement>();energyAboveTarget+=input.Mass*input.Element.specificHeatCapacity*(input.Temperature-PantryRules.TargetK);p.AddDisease(input.DiseaseIdx,input.DiseaseCount,"Pantry batch");}
            p.Temperature=PantryRules.TargetK+energyAboveTarget/(mass*p.Element.specificHeatCapacity); product.SetActive(true);
            internalMove=true;Batch.ConsumeAllIgnoringDisease();Drinks.Store(product);pendingId=null;Progress=0;internalMove=false;
            LoseGas(PantryRules.DoorLoss(Gas.MassStored())*.2f,false);
        }
        public void ReturnBatch()
        {
            bool previous=internalMove;internalMove=true;
            foreach(var go in Batch.items.ToArray())if(go!=null)
            {var id=go.PrefabID();Batch.Remove(go);(id==SimHashes.CarbonDioxide.CreateTag()?Gas:id==GameTags.Water?Water:Food).Store(go);}
            if(Pending!=null)RecipeGasUsed=Math.Max(0,RecipeGasUsed-Pending.CarbonDioxide);
            pendingId=null;Progress=0;internalMove=previous;
        }
        public DrinkRecipe ReserveServing()
        {
            if(Serving.MassStored()>0)return null;
            var r=DrinkRecipes.All.FirstOrDefault(d=>Drinks.GetMassAvailable(new Tag(d.Id))+.00001f>=d.Mass);
            if(r==null)return null;
            bool previous=internalMove;internalMove=true;bool ok=MoveMass(Drinks,Serving,new Tag(r.Id),r.Mass);internalMove=previous;
            if(!ok){ReturnServing();return null;}OpenDoor();return r;
        }
        public void ReturnServing()
        {
            bool previous=internalMove;internalMove=true;
            foreach(var go in Serving.items.ToArray())if(go!=null){Serving.Remove(go);Drinks.Store(go);}
            internalMove=previous;
        }
        public string PreservationStatus
        {
            get
            {
                bool cold=Food.items.Where(g=>g!=null).Select(g=>g.GetSMI<Rottable.Instance>()).Where(r=>r!=null).All(r=>r.primaryElement.Temperature<r.PreserveTemperature);
                if(Food.MassStored()<=0)cold=InternalK<255.15f;
                if(Broken)return PantryText.T("保鲜失效 · 设备损坏", "Preservation lost · damaged");
                if(cold&&Sterile)return PantryText.T("永久保鲜", "Permanent preservation");
                if(cold)return PantryText.T("深度冷冻 · 需要无菌气体", "Deep frozen · needs sterile gas");
                bool refrigerated=Food.items.Where(g=>g!=null).Select(g=>g.GetSMI<Rottable.Instance>()).Where(r=>r!=null).All(r=>r.primaryElement.Temperature<r.RotTemperature);
                if(Food.MassStored()<=0)refrigerated=InternalK<277.15f;
                if(refrigerated)return Cooling?PantryText.T("普通冷藏 · 正在降温", "Refrigerated · cooling"):PantryText.T("普通冷藏 · 依靠余冷", "Refrigerated · retained cold");
                if(InternalK<277.15f)return Working&&LastPaidFraction>0?PantryText.T("食品降温中 · 尚未深冻", "Food cooling · not yet frozen"):PantryText.T("食品尚未深冻", "Food not yet frozen");
                return PantryText.T("保鲜失效 · 温度偏高", "Preservation lost · too warm");
            }
        }
        void UpdateAnimation()
        {
            string next=Broken?"broken":doorAnim>0?"door":LastPaidFraction<=0?"off":Processing?"processing":Cooling?"cooling":Sterile&&InternalK<255.15f?"preserved":"idle";
            if(clip==next)return;clip=next;anim.Play(next,KAnim.PlayMode.Loop);
        }
    }
}
