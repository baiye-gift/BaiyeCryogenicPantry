using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Baiye.CryogenicPantry.Core;
using HarmonyLib;
using KMod;
using UnityEngine;

namespace Baiye.CryogenicPantry
{
    public sealed class PantryMod : UserMod2
    {
        public override void OnLoad(Harmony harmony) { base.OnLoad(harmony); PantryText.Register(); }
    }
    public static class PantryText
    {
        public static string T(string zh, string en) => Localization.GetLocale()?.Code?.StartsWith("zh") != false ? zh : en;
        public static void Register()
        {
            string key = "STRINGS.BUILDINGS.PREFABS." + PantryConfig.ID.ToUpperInvariant();
            Strings.Add(key + ".NAME", T("恒温气调保鲜柜", "Atmosphere-Controlled Cryogenic Pantry"));
            Strings.Add(key + ".DESC", T("将精密深冷与密封气调整合到一座紧凑的未来食品设施中。", "A compact future food facility integrating precision cooling and a sealed atmosphere."));
            Strings.Add(key + ".EFFECT", T("食品 500 kg、饮料 50 kg。连接电力和低温下仍保持气态的所选无菌气体，输入气体不超过50°C，柜内目标 -22°C。食品实际深冻与有效无菌气氛同时满足时原生腐败率为零。接入水和 CO2 后可自动加工四种饮料，复制人在休闲时到柜前饮用。制冷热量和耗电转为环境热量；断电自然回温，自动化红信号停止运行。无菌气氛不消除食物病菌。", "Stores 500 kg food and 50 kg beverages. Connect power and selected sterile gas that remains gaseous when cold; gas intake max 50 C, cabinet target -22 C. Native spoilage reaches zero when the food itself is frozen and its atmosphere is valid and sterile. Water + CO2 enables four drinks, consumed at the cabinet during recreation. Cooling and electrical heat enter the surroundings. Retains thermal inertia without power; red automation stops operation. Sterile atmosphere does not remove germs."));
            foreach (var r in DrinkRecipes.All)
            {
                Strings.Add("STRINGS.MISC.TAGS." + r.Id.ToUpperInvariant(), T(r.Chinese, r.English));
                Strings.Add("STRINGS.ITEMS.FOOD." + r.Id.ToUpperInvariant() + ".NAME", T(r.Chinese,r.English));
                Strings.Add("STRINGS.ITEMS.FOOD." + r.Id.ToUpperInvariant() + ".DESC", T("保鲜柜加工的瓶装冰饮；每瓶质量 1 kg。存于饮料仓，休闲时在柜前饮用，实际消耗后生效。", "A bottled iced serving made by the pantry. Stored in its drink chamber and consumed there during recreation."));
            }
        }
    }
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class PantryRegistration
    {
        static void Postfix()
        {
            PantryText.Register(); ModUtil.AddBuildingToPlanScreen((HashedString)"Food", PantryConfig.ID);
            var tech = Db.Get().Techs.Get("LiquidTemperature");
            if (tech != null && !tech.unlockedItemIDs.Contains(PantryConfig.ID)) tech.unlockedItemIDs.Add(PantryConfig.ID);
            PantryEffects.Register();
        }
    }
    // Observe actual power debits, including partial supply, without changing vanilla circuits.
    [HarmonyPatch(typeof(CircuitManager), nameof(CircuitManager.Sim200msLast))]
    internal static class PantryPowerTick
    {
        static void Prefix(float dt, float ___elapsedTime, out bool __state)
        { __state = dt > 0 && ___elapsedTime + dt >= .2f; if (__state) foreach (var b in PantryBuilding.Live.Values.ToArray()) if (b != null && b.Ready) b.PreparePower(.2f); }
        static void Postfix(bool __state)
        { if (__state) foreach (var b in PantryBuilding.Live.Values.ToArray()) if (b != null && b.Ready) b.SettlePower(.2f); }
    }
    [HarmonyPatch]
    internal static class PantryPayment
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CircuitManager), "PowerFromGenerator");
            yield return AccessTools.Method(typeof(CircuitManager), "PowerFromBatteries");
        }
        static void Prefix(float joules_needed, out float __state) { __state = joules_needed; }
        static void Postfix(IEnergyConsumer c, float __result, float __state)
        {
            var consumer = c as EnergyConsumer;
            if (consumer != null && PantryBuilding.Live.TryGetValue(consumer.GetInstanceID(), out var b) && b.Ready)
                b.PaidJoules += Math.Max(0, __state - __result);
        }
    }
    [HarmonyPatch(typeof(Rottable), nameof(Rottable.AtmosphereQuality))]
    internal static class PantryAtmosphere
    {
        static bool Prefix(IRottable rottable, ref Rottable.RotAtmosphereQuality __result)
        {
            var b = PantryBuilding.Owner(rottable?.gameObject);
            if (b == null) return true;
            __result = b.Sterile ? Rottable.RotAtmosphereQuality.Sterilizing : Rottable.RotAtmosphereQuality.Normal;
            return false;
        }
    }
    [HarmonyPatch(typeof(Rottable), nameof(Rottable.RefrigerationLevel))]
    internal static class PantryTemperature
    {
        static bool Prefix(IRottable rottable, ref Rottable.RotRefrigerationLevel __result)
        {
            var b = PantryBuilding.Owner(rottable?.gameObject);
            if (b == null) return true;
            double t = rottable.gameObject.GetComponent<PrimaryElement>().Temperature;
            __result = t < rottable.PreserveTemperature ? Rottable.RotRefrigerationLevel.Frozen :
                t < rottable.RotTemperature ? Rottable.RotRefrigerationLevel.Refrigerated : Rottable.RotRefrigerationLevel.Normal;
            return false;
        }
    }
    [HarmonyPatch(typeof(Storage), "OnQueueDestroyObject")]
    internal static class PantryDestroying
    {
        static void Prefix(Storage __instance) { __instance.GetComponent<PantryBuilding>()?.MarkDestroyed(); }
    }
}
