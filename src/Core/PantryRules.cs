using System;
using System.Collections.Generic;

namespace Baiye.CryogenicPantry.Core
{
    public static class PantryRules
    {
        public const float TargetK = 251.15f, GasCapacity = 2, SterileMinimum = .20f;
        public const float FoodCapacity = 500, DrinkCapacity = 50, WaterCapacity = 20;
        public const float ShellHeatCapacity = 50, CoolingKW = 240, InsulationKWPerK = .008f, ExchangeRate = .75f;
        public const float MaxGasIntakeK = 323.15f;
        public static bool GasPhaseAllowed(bool gas,bool sterile,float low,float high) => gas && sterile &&
            !float.IsNaN(low) && !float.IsInfinity(low) && low >= 0 && low < TargetK && !float.IsNaN(high) && high > MaxGasIntakeK;
        public static bool GasIntakeAllowed(float temperature,float low,float high) => !float.IsNaN(temperature) &&
            !float.IsInfinity(temperature) && temperature > low && temperature < high && temperature <= MaxGasIntakeK;
        public static int RecoverGasSelection(int requested,int stored,int fallback,Func<int,bool> supported) =>
            supported(requested)?requested:supported(stored)?stored:fallback;
        public static int? ReplacementReturnTarget(int selected,IEnumerable<KeyValuePair<int,float>> inventory,Func<int,bool> supported)
        {
            int? result=null;float largest=0;
            foreach(var stock in inventory)if(stock.Key!=selected&&stock.Value>largest&&supported(stock.Key)){result=stock.Key;largest=stock.Value;}
            return result;
        }
        public static double CoolingEnergy(double temperature, double capacity, double target, double budget) => Math.Max(0, Math.Min(Math.Max(0, budget), (temperature - target) * Math.Max(0, capacity)));
        public static double ExchangeEnergy(double itemK, double itemCapacity, double shellK, double dt)
        {
            if(itemCapacity<=0||dt<=0)return 0;
            return (itemK-shellK)*itemCapacity*ShellHeatCapacity/(itemCapacity+ShellHeatCapacity)*Math.Min(1,dt*ExchangeRate);
        }
        public static float PaidFraction(float paid, float watts, float dt) => watts > 0 && dt > 0 ? Math.Min(1, Math.Max(0, paid / (watts * dt))) : 0;
        public static bool CanSpend(float have, float cost, float reserve) => cost > 0 && have - cost >= Math.Max(0, reserve) - .00001f;
        public static float DoorLoss(float mass) => Math.Min(mass, mass * .015f);
        public static float ReplacementIntake(float have, float targetMass, float rateBudget) => Math.Min(rateBudget, Math.Max(0, GasCapacity - have) + Math.Max(0, have - targetMass));
        public static float Power(bool cooling, bool maintenance, bool drink) => (cooling ? 600 : maintenance ? 240 : 120) + (drink ? 120 : 0);
        public static bool ProcessAllowed(bool auto, bool recipeEnabled, bool co2Selected, float co2Ratio) => auto && recipeEnabled && co2Selected && co2Ratio >= .95f;
        public static float CoolingRate(bool cooling,bool maintenance)=>cooling?CoolingKW:maintenance?CoolingKW*.4f:0;
    }
    public sealed class Ingredient
    {
        public readonly string Id; public readonly float Mass;
        public Ingredient(string id, float mass) { Id = id; Mass = mass; }
    }
    public sealed class DrinkRecipe
    {
        public readonly string Id, Chinese, English, Effect;
        public readonly float Water, CarbonDioxide, Seconds;
        public readonly Ingredient[] Solids;
        // Extensions may reuse our generic bottle, supply their own animation,
        // or let another mod own the item prefab without duplicate registration.
        public string ItemAnimation { get; set; } = "baiye_pantry_drinks_kanim";
        public string ItemClip { get; set; } = "object";
        public bool RegisterItemPrefab { get; set; } = true;
        public float Mass { get { float m = Water + CarbonDioxide; foreach (var s in Solids) m += s.Mass; return m; } }
        public DrinkRecipe(string id, string zh, string en, string effect, float water, float co2, float seconds, params Ingredient[] solids)
        { Id = id; Chinese = zh; English = en; Effect = effect; Water = water; CarbonDioxide = co2; Seconds = seconds; Solids = solids ?? Array.Empty<Ingredient>(); }
    }
    public static class DrinkRecipes
    {
        static readonly List<DrinkRecipe> recipes = new List<DrinkRecipe>();
        public static IReadOnlyList<DrinkRecipe> All => recipes;
        public static void Register(DrinkRecipe recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Effect) || !Positive(recipe.Water) || !Positive(recipe.CarbonDioxide) || !Positive(recipe.Seconds)) throw new ArgumentException("Invalid drink recipe");
            var ids=new HashSet<string>();
            foreach (var s in recipe.Solids) if (s == null || string.IsNullOrWhiteSpace(s.Id) || !Positive(s.Mass) || !ids.Add(s.Id)) throw new ArgumentException("Invalid/duplicate ingredient");
            if(!Positive(recipe.Mass))throw new ArgumentException("Invalid batch mass");
            if (recipes.Exists(r => r.Id == recipe.Id)) throw new ArgumentException("Duplicate drink ID: " + recipe.Id);
            recipes.Add(recipe);
        }
        static bool Positive(float value)=>value>0&&!float.IsNaN(value)&&!float.IsInfinity(value);
        public static DrinkRecipe Find(string id) => recipes.Find(r => r.Id == id);
        static DrinkRecipes()
        {
            Register(new DrinkRecipe("BaiyeChilledSoda", "冰镇苏打水", "Chilled Soda", "BaiyeCoolBubbles", .97f, .03f, 20));
            Register(new DrinkRecipe("BaiyeBerrySoda", "浆果汽水", "Berry Soda", "BaiyeFruitSatisfaction", .77f, .03f, 25, new Ingredient("PrickleFruit", .20f)));
            Register(new DrinkRecipe("BaiyePepperSoda", "胡椒气泡饮", "Pepper Soda", "BaiyeSpicyFocus", .87f, .03f, 25, new Ingredient("SpiceNut", .10f)));
            Register(new DrinkRecipe("BaiyeEnergySoda", "能量冰饮", "Energy Soda", "BaiyeEnergyRush", .67f, .03f, 35, new Ingredient("PrickleFruit", .20f), new Ingredient("SpiceNut", .10f)));
            foreach(var r in recipes)r.ItemClip=r.Id;
        }
    }
}
