using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections;
using HBS;
using BattleTech.UI;
using static AbilitiesAndSkillsOverhaul.Logger;

namespace AbilitiesAndSkillsOverhaul
{
    public static class Core
    {
        #region Init

        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("dZ.donZappo.AbilitiesAndSkillsOverhaul");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // read settings
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
                Settings.modDirectory = modDir;
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }

            // blank the logfile
            Clear();
            // PrintObjectFields(Settings, "Settings");
        }

        // logs out all the settings and their values at runtime
        internal static void PrintObjectFields(object obj, string name)
        {
            LogDebug($"[START {name}]");

            var settingsFields = typeof(ModSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in settingsFields)
            {
                if (field.GetValue(obj) is IEnumerable &&
                    !(field.GetValue(obj) is string))
                {
                    LogDebug(field.Name);
                    foreach (var item in (IEnumerable)field.GetValue(obj))
                    {
                        LogDebug("\t" + item);
                    }
                }
                else
                {
                    LogDebug($"{field.Name,-30}: {field.GetValue(obj)}");
                }
            }

            LogDebug($"[END {name}]");
        }

        #endregion

        internal static ModSettings Settings;
    }

    [HarmonyPatch(typeof(ToHit), "GetRefireModifier", null)]
    public static class Gunnery_Adjustments
    {
        public static void Postfix(ToHit __instance, Weapon weapon, ref float __result)
        {
            int skillGunnery = weapon.parent.SkillGunnery;
            bool flag = skillGunnery < 10 && skillGunnery >= 6 && weapon.RefireModifier > 0 && weapon.roundsSinceLastFire < 2;
            if (flag)
            {
                __result = (float)weapon.RefireModifier - 1f;
            }
            bool flag2 = skillGunnery >= 10 && weapon.RefireModifier > 1 && weapon.roundsSinceLastFire < 2;
            if (flag2)
            {
                __result = (float)weapon.RefireModifier - 2f;
            }
            bool flag3 = skillGunnery >= 10 && weapon.RefireModifier == 1 && weapon.roundsSinceLastFire < 2;
            if (flag3)
            {
                __result = (float)weapon.RefireModifier - 1f;
            }
        }
    }
    [HarmonyPatch(typeof(ToHit), "GetSelfSpeedModifier", null)]
    public static class Tactics_Adjustments
    {
        public static void Postfix(ToHit __instance, AbstractActor attacker, ref float __result)
        {
            Pilot pilot = attacker.GetPilot();
            int tactics = pilot.Tactics;
            bool flag = tactics >= 6;
            if (flag)
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(ToHit), "GetSelfSprintModifier", null)]
    public static class Tactics_Sprint_Adjustments
    {
        public static void Postfix(ToHit __instance, AbstractActor attacker, ref float __result)
        {
            Pilot pilot = attacker.GetPilot();
            int tactics = pilot.Tactics;
            bool flag = tactics >= 8;
            if (flag)
            {
                __result = 0f;
            }
        }
    }


    [HarmonyPatch(typeof(Team), "CollectUnitBaseline")]
    public static class Resolve_Reduction_Patch
    {
        public static void Postfix(Team __instance, ref int __result)
        {
            foreach (AbstractActor actor in __instance.units)
            {
                Pilot pilot = actor.GetPilot();
                int tactics = pilot.Tactics;
            
                if (tactics >= 7)
                    __result++;
                if (tactics >= 10)
                    __result++;
            }
        }
    }
}
