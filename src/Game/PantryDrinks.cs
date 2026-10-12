using System;
using System.Collections.Generic;
using System.Linq;
using Baiye.CryogenicPantry.Core;
using Klei.AI;
using KSerialization;
using UnityEngine;

namespace Baiye.CryogenicPantry
{
    public sealed class PantryDrinkEntities : IMultiEntityConfig
    {
        public List<GameObject> CreatePrefabs()
        {
            var list=new List<GameObject>();
            foreach(var r in DrinkRecipes.All)
            {
                if(!r.RegisterItemPrefab)continue;
                var go=EntityTemplates.CreateLooseEntity(r.Id,PantryText.T(r.Chinese,r.English),
                    PantryText.T("休闲时在保鲜柜前饮用；每瓶实际消耗后生效。","Consumed at a pantry during recreation. Effects require an actual serving."),
                    r.Mass,false,Assets.GetAnim(r.ItemAnimation),r.ItemClip,Grid.SceneLayer.Front,EntityTemplates.CollisionShape.RECTANGLE,.3f,.5f,true,
                    element:SimHashes.Creature,additionalTags:new List<Tag>{new Tag("BaiyePantryDrink"),GameTags.MiscPickupable});
                go.AddOrGetDef<Rottable.Def>().spoilTime=4800;go.AddOrGetDef<Rottable.Def>().staleTime=2400;
                list.Add(go);
            }
            return list;
        }
        public void OnPrefabInit(GameObject inst) { }
        public void OnSpawn(GameObject inst) { }
    }
    internal static class PantryEffects
    {
        public const string Cooldown="BaiyeRecentlyDrank", Crash="BaiyeEnergyLow";
        public static void Register()
        {
            Add("BaiyeCoolBubbles","清凉气泡","Cool Bubbles",300,new[]{Mod("QualityOfLife",1),Mod("StressDelta",-5f/600)});
            Add("BaiyeFruitSatisfaction","果味满足","Fruit Satisfaction",450,new[]{Mod("QualityOfLife",3),Mod("StressDelta",-10f/600)});
            Add("BaiyeSpicyFocus","辛辣提神","Spicy Focus",300,new[]{Mod("Athletics",2),Mod("Machinery",2)});
            Add("BaiyeEnergyRush","高度亢奋","Energy Rush",180,new[]{Mod("Machinery",3),Mod("Construction",3),Mod("Digging",3),Mod("Athletics",3)});
            Add(Crash,"能量低谷","Energy Low",90,new[]{Mod("Machinery",-1),Mod("Athletics",-1)});
            Add(Cooldown,"已饮用冰饮","Recently Drank",300,Array.Empty<AttributeModifier>());
        }
        static AttributeModifier Mod(string id,float value)=>new AttributeModifier(id,value,PantryText.T("气调保鲜柜饮料","Pantry beverage"));
        static void Add(string id,string zh,string en,float duration,AttributeModifier[] mods)
        {
            if(Db.Get().effects.TryGet(id)!=null)return;
            string name=PantryText.T(zh,en);
            Strings.Add("STRINGS.DUPLICANTS.MODIFIERS."+id.ToUpperInvariant()+".NAME",name);
            string desc=id=="BaiyeEnergyRush"?PantryText.T("短时提升工作属性；结束后进入 90 秒能量低谷。","Short skill boost followed by a 90-second energy low."):PantryText.T("保鲜柜冰饮带来的暂时效果；同类饮料不叠加。","Temporary pantry drink effect. Repeated drinks do not stack.");
            Strings.Add("STRINGS.DUPLICANTS.MODIFIERS."+id.ToUpperInvariant()+".DESCRIPTION",desc);
            var e=new Effect(id,name,desc,duration,true,true,id==Crash);foreach(var m in mods)e.Add(m);Db.Get().effects.Add(e);
        }
    }
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PantryAfterEffect : KMonoBehaviour, ISaveLoadable, ISim1000ms
    {
        [Serialize] public bool AwaitingLow;
        public void Sim1000ms(float dt)
        {
            if(!AwaitingLow)return;var effects=GetComponent<Effects>();if(effects==null||effects.HasEffect("BaiyeEnergyRush"))return;
            AwaitingLow=false;effects.Add(PantryEffects.Crash,true);
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(MinionConfig),"CreatePrefab")]
    internal static class PantryMinionEffect
    {
        static void Postfix(GameObject __result){__result.AddOrGet<PantryAfterEffect>();}
    }
    [HarmonyLib.HarmonyPatch(typeof(BionicMinionConfig),"CreatePrefab")]
    internal static class PantryBionicEffect
    {
        static void Postfix(GameObject __result){__result.AddOrGet<PantryAfterEffect>();}
    }
    public sealed class PantryDrinkWorkable : Workable, IWorkerPrioritizable
    {
        PantryBuilding pantry;
        Chore chore;
        DrinkRecipe serving;
        bool busy;
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();SetWorkTime(20);showProgressBar=true;resetProgressOnStop=true;synchronizeAnims=false;
            SetReportType(ReportManager.ReportType.PersonalTime);
            overrideAnims=new[]{Assets.GetAnim("anim_interacts_sodamaker_kanim")};SetOffsets(new[]{new CellOffset(1,0)});
        }
        protected override void OnSpawn(){base.OnSpawn();pantry=GetComponent<PantryBuilding>();}
        public override AnimInfo GetAnim(WorkerBase worker)
        {
            overrideAnims=new[]{Assets.GetAnim(worker.PrefabID()==BionicMinionConfig.ID?"anim_bionic_interacts_sodamaker_kanim":"anim_interacts_sodamaker_kanim")};
            return base.GetAnim(worker);
        }
        public void RefreshChore()
        {
            if(pantry==null||!pantry.Ready)return;
            bool available=pantry.Working&&DrinkRecipes.All.Any(r=>pantry.Servings(r)>=1-.00001f);
            if(!available&&!busy&&chore!=null){chore.Cancel("No drink available");chore=null;}
            if(available&&!busy&&(chore==null||chore.isComplete))
            {
                var c=new WorkChore<PantryDrinkWorkable>(Db.Get().ChoreTypes.Relax,this,null,true,null,null,null,false,Db.Get().ScheduleBlockTypes.Recreation,false,true,null,false,true,false,PriorityScreen.PriorityClass.high);
                c.AddPrecondition(ChorePreconditions.instance.CanDoWorkerPrioritizable,this);chore=c;
            }
        }
        public bool GetWorkerPriority(WorkerBase worker,out int priority)
        {
            priority=4;var effects=worker.GetComponent<Effects>();
            return pantry!=null&&pantry.Working&&effects!=null&&!effects.HasEffect(PantryEffects.Cooldown)&&!effects.HasEffect(PantryEffects.Crash)&&!effects.HasEffect("BaiyeEnergyRush");
        }
        protected override void OnStartWork(WorkerBase worker){busy=true;serving=pantry.ReserveServing();}
        protected override bool OnWorkTick(WorkerBase worker,float dt)
        {
            if(serving==null||!pantry.Working||pantry.Serving.GetMassAvailable(new Tag(serving.Id))+.00001f<serving.Mass)
            {chore?.Cancel("Drink interrupted");return false;}return false;
        }
        protected override void OnCompleteWork(WorkerBase worker)
        {
            if(serving==null||!pantry.Working||pantry.Serving.GetMassAvailable(new Tag(serving.Id))+.00001f<serving.Mass)return;
            pantry.Serving.ConsumeAndGetDisease(new Tag(serving.Id),serving.Mass,out float used,out var disease,out float temperature);
            if(used+.00001f<serving.Mass)return;
            var exposure=worker.GetSMI<GermExposureMonitor.Instance>();exposure?.TryInjectDisease(disease.idx,disease.count,new Tag(serving.Id),Sickness.InfectionVector.Digestion);
            var effects=worker.GetComponent<Effects>();foreach(var r in DrinkRecipes.All)effects.Remove(r.Effect);
            effects.Add(serving.Effect,true);effects.Add(PantryEffects.Cooldown,true);
            if(serving.Id=="BaiyeEnergySoda")worker.gameObject.AddOrGet<PantryAfterEffect>().AwaitingLow=true;
        }
        protected override void OnStopWork(WorkerBase worker){pantry?.ReturnServing();serving=null;busy=false;chore=null;}
        protected override void OnCleanUp(){chore?.Cancel("Pantry removed");chore=null;base.OnCleanUp();}
    }
}
