using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using KSerialization;
using UnityEngine;
using Baiye.CryogenicPantry;
using Baiye.CryogenicPantry.Core;

static class Program
{
    static string managed;static int count;
    static void Main(string[] args)
    {
        managed=args.Length>0?args[0]:@"D:\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed";
        AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{string p=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
    }
    static void Check(bool b,string n){if(!b)throw new Exception("FAIL "+n);count++;Console.WriteLine("PASS "+n);}
    static double Luminance(Color color)
    {
        double Linear(double c)=>c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
        return .2126*Linear(color.r)+.7152*Linear(color.g)+.0722*Linear(color.b);
    }
    static double Contrast(Color first,Color second)
    {double a=Luminance(first),b=Luminance(second);return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);}
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(PantryBuilding)),"building native save contract");
        Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(PantryAfterEffect)),"delayed energy low native save contract");
        Check(typeof(IWorkerPrioritizable).IsAssignableFrom(typeof(PantryDrinkWorkable)),"drink recreation priority contract");
        Check(typeof(SideScreenContent).IsAssignableFrom(typeof(PantrySideScreen)),"native settings screen");
        var inputs=(RequireInputs)RuntimeHelpers.GetUninitializedObject(typeof(RequireInputs));
        var defaultConsumer=(ConduitConsumer)RuntimeHelpers.GetUninitializedObject(typeof(ConduitConsumer));
        inputs.SetRequirements(true,true);inputs.requireConduitHasMass=true;inputs.visualizeRequirements=RequireInputs.Requirements.All;
        defaultConsumer.isOn=true;defaultConsumer.isConsuming=true;defaultConsumer.consumptionRate=float.PositiveInfinity;
        PantryConfig.ConfigureInputs(inputs,defaultConsumer);
        Check(inputs.RequiresPower&&!inputs.RequiresInputConduit,"actual native input config requires power but empty or disconnected gas pipe cannot stop cooling");
        Check(!defaultConsumer.isOn&&!defaultConsumer.isConsuming&&defaultConsumer.ConsumptionRate==0,"native default consumer cannot bypass paid filtered gas intake");
        Check(defaultConsumer.IsSatisfied,"native empty pipe consumer is not a global unsatisfied requirement");
        Check(inputs.visualizeRequirements==RequireInputs.Requirements.AllPower,"native pipe empty status is not shown as a whole-building fault");
        // Reapply the same production helper used by OnSpawn after loading.
        inputs.SetRequirements(true,true);defaultConsumer.SetOnState(true);defaultConsumer.isConsuming=true;
        PantryConfig.ConfigureInputs(inputs,defaultConsumer);
        Check(!inputs.RequiresInputConduit&&!defaultConsumer.isOn&&!defaultConsumer.isConsuming,"spawn reconfiguration removes stale default conduit requirements without consuming stock");
        Check(typeof(ConduitConsumer).GetCustomAttribute<SkipSaveFileSerialization>()!=null&&typeof(RequireInputs).GetCustomAttribute<SkipSaveFileSerialization>()!=null,"native input components have no save fields to reorder or delete");
        string Block(string flag)=>PantryBuilding.ExplainBlockingFlags(new[]{new System.Collections.Generic.KeyValuePair<Operational.Flag,bool>(new Operational.Flag(flag,Operational.Flag.Type.Requirement),false)});
        Check(Block("building_enabled").Contains("手动禁用"),"manual disable gives a specific actionable reason");
        Check(Block("not_flooded").Contains("淹没")&&Block("not_entombed").Contains("掩埋"),"flooded and entombed flags have distinct reasons");
        Check(Block("pipesHaveMass").Contains("管道为空")&&Block("inputConnected").Contains("未连接"),"actual native gas blocking flags are explained rather than generic unavailable text");
        Check(Block("external_mod_flag").Contains("external_mod_flag"),"unknown external operational flag remains visible for diagnosis");
        var mixedFlags=new[]{new System.Collections.Generic.KeyValuePair<Operational.Flag,bool>(BuildingEnabledButton.EnabledFlag,true),new System.Collections.Generic.KeyValuePair<Operational.Flag,bool>(LogicOperationalController.LogicOperationalFlag,false)};
        Check(!PantryBuilding.ExplainBlockingFlags(mixedFlags).Contains("手动禁用")&&PantryBuilding.ExplainBlockingFlags(mixedFlags).Contains("红信号"),"enabled flags never become false stop reasons and red automation still blocks");
        Check(Contrast(PantryPalette.Ink,PantryPalette.Surface)>=4.5,"body and disabled control contrast on light surface");
        Check(Contrast(PantryPalette.Ink,PantryPalette.Panel)>=4.5,"recipe button normal contrast");
        Check(Contrast(PantryPalette.Ink,PantryPalette.Accent)>=4.5,"recipe button pressed and selected tab contrast");
        Check(Contrast(PantryPalette.Ink,Color.Lerp(PantryPalette.Panel,PantryPalette.Accent,.35f))>=4.5,"recipe button hover contrast");
        Check(Contrast(PantryPalette.Muted,PantryPalette.Panel)>=4.5,"recipe status contrast");
        Check(Contrast(PantryPalette.Muted,PantryPalette.Surface)>=4.5,"secondary and scrollbar contrast");
        Check(Contrast(Color.white,PantryPalette.Ink)>=4.5,"preservation summary white text contrast");
        Check(typeof(ISecondaryInput).IsAssignableFrom(typeof(PantryPorts)),"liquid secondary port contract");
        var def=new Rottable.Def();Check(Math.Abs(def.preserveTemperature-255.15f)<.001,"native default -18 C boundary");
        foreach(var g in new[]{SimHashes.CarbonDioxide,SimHashes.ChlorineGas,SimHashes.Hydrogen})Check(Rottable.AtmosphereModifier[(int)g]==Rottable.RotAtmosphereQuality.Sterilizing,g+" native sterile mapping");
        Check(Rottable.AtmosphereModifier[(int)SimHashes.Oxygen]==Rottable.RotAtmosphereQuality.Normal,"oxygen stays normal");
        // Managed Element fixtures use the installed gas.yaml phase thresholds.
        // No ElementLoader initialization (its static path needs Unity) or world.
        Element Gas(SimHashes id,float low)
        {var e=(Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));e.id=id;e.state=Element.State.Gas;e.lowTemp=low;e.highTemp=10000;return e;}
        Check(!PantryBuilding.SupportsGas(Gas(SimHashes.CopperGas,2834)),"sterile copper vapour is not a refrigerator gas");
        Check(!PantryBuilding.SupportsGas(Gas(SimHashes.Steam,372.5f)),"steam cannot remain gas at refrigerator target");
        foreach(var pair in new[]{(SimHashes.CarbonDioxide,225f),(SimHashes.ChlorineGas,238.55f),(SimHashes.Hydrogen,21f)})
            Check(PantryBuilding.SupportsGas(Gas(pair.Item1,pair.Item2)),pair.Item1+" supported at -22 C");
        Check(!PantryBuilding.SupportsGas(Gas(SimHashes.Oxygen,90.19f)),"normal atmosphere oxygen excluded from gas menu");
        Check(!PantryBuilding.SupportsGas(null),"missing element cannot be selected");
        var modGas=Gas((SimHashes)(-192877123),200);
        Rottable.AtmosphereModifier[(int)modGas.id]=Rottable.RotAtmosphereQuality.Sterilizing;
        try{Check(PantryBuilding.SupportsGas(modGas),"third-party sterile gas accepted by native mapping and phase rather than ID allowlist");}
        finally{Rottable.AtmosphereModifier.Remove((int)modGas.id);}
        foreach(var name in new[]{"PowerFromGenerator","PowerFromBatteries"})
        {
            var method=typeof(CircuitManager).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance);
            Check(method!=null&&method.ReturnType==typeof(float)&&method.GetParameters().Any(p=>p.Name=="c"&&p.ParameterType==typeof(IEnergyConsumer)),name+" actual payment hook signature");
        }
        Check(typeof(CircuitManager).GetField("elapsedTime",BindingFlags.Instance|BindingFlags.NonPublic)?.FieldType==typeof(float),"native circuit fixed tick gate");
        Check(typeof(MinionConfig).GetMethods().Any(m=>m.Name=="CreatePrefab")&&typeof(BionicMinionConfig).GetMethods().Any(m=>m.Name=="CreatePrefab"),"normal and bionic prefab save-component hooks");
        Check(typeof(Storage).GetMethod("OnQueueDestroyObject",BindingFlags.NonPublic|BindingFlags.Instance)!=null,"native destruction vent hook");
        foreach(var name in new[]{"InternalK","SelectedGas","Progress","RecipeIds","Enabled","Targets","Unlimited","ReserveIds","Reserves","RecipeGasUsed","SealGasUsed","PurgeGasUsed"})
            Check(typeof(PantryBuilding).GetField(name).GetCustomAttribute<Serialize>()!=null,name+" is saved");
        Check(typeof(PantryAfterEffect).GetField("AwaitingLow").GetCustomAttribute<Serialize>()!=null,"energy-low timer survives save");
        Check(Storage.StandardInsulatedStorage.Contains(Storage.StoredItemModifier.Seal)&&!Storage.StandardInsulatedStorage.Contains(Storage.StoredItemModifier.Preserve),"insulation and sealing never force freshness");
        var tags=new[]{GameTags.Edible,GameTags.CookingIngredient};Check(tags.All(t=>TUNING.STORAGEFILTERS.FOOD.Contains(t)),"native food filter includes food and cooking ingredients");
        var source=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src"));
        string game=File.ReadAllText(Path.Combine(source,"Game/PantryConfig.cs"));
        Check(game.Contains("NewStorage(go, 10, false, false)")&&game.Contains("PantryRules.FoodCapacity")&&game.Contains("PantryRules.DrinkCapacity"),"production config keeps storage order and separate capacities");
        string implementation=File.ReadAllText(Path.Combine(source,"Game/PantryBuilding.cs"));
        Check(!implementation.Contains("RotValue =")&&!implementation.Contains("StoredItemModifier.Preserve"),"no freshness lock or preserve modifier");
        string ui=File.ReadAllText(Path.Combine(source,"UI/PantrySideScreen.cs"));
        Check(new[]{'\u25be','\u25b8','\u25b0','\u25b1'}.All(c=>!ui.Contains(c)),"known missing ONI triangle and block glyphs absent from pantry UI");
        Check(!ui.Contains('\u2082')&&!File.ReadAllText(Path.Combine(source,"Game/PantryMod.cs")).Contains('\u2082'),"known missing CO2 subscript removed from panel and building description");
        Check(typeof(PantrySideScreen).GetField("HorizontalButtonTemplate")?.FieldType==typeof(UnityEngine.UI.LayoutElement)&&typeof(ButtonMenuSideScreen).GetField("horizontalButtonPrefab")!=null,"native horizontal button template contract");
        // Real KSerialization on an uninitialized component exercises saved field
        // round trips only. No Unity world, GameObject identity, pipes or SimDLL.
        var b=(PantryBuilding)RuntimeHelpers.GetUninitializedObject(typeof(PantryBuilding));
        b.InternalK=251.15f;b.SelectedGas=(int)SimHashes.ChlorineGas;b.Progress=12.75f;
        b.RecipeIds=new() {"BaiyeBerrySoda"};b.Enabled=new(){true};b.Targets=new(){23};b.Unlimited=new(){false};b.ReserveIds=new(){"PrickleFruit"};b.Reserves=new(){19};
        var restored=(PantryBuilding)RuntimeHelpers.GetUninitializedObject(typeof(PantryBuilding));
        Manager.Initialize();byte[] state,dir;
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Serializer.Serialize(b,w);state=s.ToArray();}}
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Manager.SerializeDirectory(w);dir=s.ToArray();}}
        Manager.DeserializeDirectory(new FastReader(dir));Check(Deserializer.Deserialize(restored,new FastReader(state)),"native component field deserialization");
        Check(restored.InternalK==b.InternalK&&restored.SelectedGas==b.SelectedGas&&restored.Progress==b.Progress,"temperature gas and paid progress round trip");
        Check(restored.Targets.Single()==23&&restored.Reserves.Single()==19&&restored.Enabled.Single(),"recipe settings and reserves round trip");
        Console.WriteLine($"PASS {count} managed native/static contract checks; Unity/SimDLL and full save identity were not exercised.");
    }
}
