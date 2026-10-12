using System.Collections.Generic;
using Baiye.CryogenicPantry.Core;
using TUNING;
using UnityEngine;

namespace Baiye.CryogenicPantry
{
    public sealed class PantryConfig : IBuildingConfig
    {
        public const string ID = "BaiyeCryogenicPantry", Anim = "baiye_cryogenic_pantry_kanim";
        public override BuildingDef CreateBuildingDef()
        {
            var d = BuildingTemplates.CreateBuildingDef(ID, 2, 3, Anim, 100, 60,
                new float[] { 400, 200 }, new string[] { "RefinedMetal", "Plastic" }, 1600, BuildLocationRule.OnFloor,
                noise: NOISE_POLLUTION.NOISY.TIER1, decor: BUILDINGS.DECOR.BONUS.TIER1);
            d.RequiresPowerInput = true; d.EnergyConsumptionWhenActive = 720;
            d.PowerInputOffset = new CellOffset(0, 0); d.InputConduitType = ConduitType.Gas;
            d.UtilityInputOffset = new CellOffset(0, 2); d.AddLogicPowerPort = true;
            d.Overheatable = true; d.OverheatTemperature = 348.15f;
            d.SelfHeatKilowattsWhenActive = 0; d.ExhaustKilowattsWhenActive = 0;
            d.Floodable = true; d.Entombable = true; d.AudioCategory = "Metal";
            d.ViewMode = OverlayModes.Power.ID; return d;
        }
        static Storage NewStorage(GameObject go, float capacity, bool visible, bool removable)
        {
            var s = go.AddComponent<Storage>(); s.capacityKg = capacity; s.showInUI = visible;
            s.allowItemRemoval = removable; s.showDescriptor = visible;
            s.SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage); return s;
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            // This order is a save-format contract. Never reorder storages.
            var food = NewStorage(go, PantryRules.FoodCapacity, true, true);
            food.storageFilters = new List<Tag>(STORAGEFILTERS.FOOD); food.fetchCategory = Storage.FetchCategory.GeneralStorage;
            food.storageFullMargin = .01f; food.showCapacityStatusItem = true;
            NewStorage(go, PantryRules.DrinkCapacity, true, true);
            NewStorage(go, PantryRules.GasCapacity, true, false);
            NewStorage(go, PantryRules.WaterCapacity, true, false);
            NewStorage(go, 10, false, false); // physically reserved recipe input chunks
            NewStorage(go, 10, false, false); // physically reserved serving, returned after interrupted drinking
            Prioritizable.AddRef(go); go.AddOrGet<TreeFilterable>(); go.AddOrGet<FoodStorage>();
            go.AddOrGet<CopyBuildingSettings>().copyGroupTag = new Tag(ID);
            go.AddOrGet<UserNameable>();
            go.AddOrGet<PantryPorts>(); go.AddOrGet<PantryBuilding>(); go.AddOrGet<PantryDrinkWorkable>();go.AddOrGet<PantryStatus>();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.KitchenRefrigerator);
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            ConfigureInputs(go.GetComponent<RequireInputs>(),go.GetComponent<ConduitConsumer>());
            go.GetComponent<KBatchedAnimController>().initialAnim = "off";
            GeneratedBuildings.RemoveLoopingSounds(go);
        }
        public static void ConfigureInputs(RequireInputs inputs,ConduitConsumer consumer)
        {
            // BuildingLoader adds these after ConfigureBuildingTemplate whenever
            // the definition exposes a gas port. Only our paid Intake may consume.
            if(!object.ReferenceEquals(inputs,null))
            {inputs.SetRequirements(true,false);inputs.requireConduitHasMass=false;inputs.visualizeRequirements=RequireInputs.Requirements.AllPower;}
            if(!object.ReferenceEquals(consumer,null))
            {consumer.SetOnState(false);consumer.isConsuming=false;consumer.consumptionRate=0;consumer.forceAlwaysSatisfied=true;consumer.IsSatisfied=true;}
        }
    }
    // Secondary markers and network endpoint must both be registered.
    public sealed class PantryPorts : KMonoBehaviour, ISecondaryInput
    {
        FlowUtilityNetwork.NetworkItem endpoint;
        public bool HasSecondaryConduitType(ConduitType type) => type == ConduitType.Liquid;
        public CellOffset GetSecondaryConduitOffset(ConduitType type) => new CellOffset(1, 0);
        protected override void OnSpawn()
        {
            base.OnSpawn(); int cell = Grid.OffsetCell(GetComponent<Building>().NaturalBuildingCell(), new CellOffset(1, 0));
            endpoint = new FlowUtilityNetwork.NetworkItem(ConduitType.Liquid, Endpoint.Sink, cell, gameObject);
            Conduit.GetNetworkManager(ConduitType.Liquid).AddToNetworks(cell, endpoint, true);
        }
        protected override void OnCleanUp()
        {
            if (endpoint != null) Conduit.GetNetworkManager(ConduitType.Liquid).RemoveFromNetworks(endpoint.Cell, endpoint, true);
            endpoint = null; base.OnCleanUp();
        }
    }
}
