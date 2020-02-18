using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections;
using BattleTech.UI;
using static AbilitiesAndSkillsOverhaul.Logger;
using System.Reflection.Emit;
using HBS;
using Localize;
using SVGImporter;
using UnityEngine;

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

    //[HarmonyPatch(typeof(ToHit), "GetRefireModifier", null)]
    //public static class Gunnery_Adjustments
    //{
    //public static void Postfix(ToHit __instance, Weapon weapon, ref float __result)
    //{
    //int skillGunnery = weapon.parent.SkillGunnery;
    //bool flag = skillGunnery < 10 && skillGunnery >= 6 && weapon.RefireModifier > 0 && weapon.roundsSinceLastFire < 2;
    //if (flag)
    //{
    //__result = (float)weapon.RefireModifier - 1f;
    //}
    //bool flag2 = skillGunnery >= 10 && weapon.RefireModifier > 1 && weapon.roundsSinceLastFire < 2;
    //if (flag2)
    //{
    //__result = (float)weapon.RefireModifier - 2f;
    //}
    //bool flag3 = skillGunnery >= 10 && weapon.RefireModifier == 1 && weapon.roundsSinceLastFire < 2;
    //if (flag3)
    //{
    //__result = (float)weapon.RefireModifier - 1f;
    //}
    //}
    //}


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

    [HarmonyPatch(typeof(ToHit), "GetSelfSprintedModifier", null)]
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

    //Don't allow Called Shot to Initiative Punch without Tactics 6
    [HarmonyPatch(typeof(AttackStackSequence), "OnAdded")]
    public static class Gate_Initiative_Punch_Patch
    {
        internal static bool WasLowTacticsMoraleAttack = false;
        public static void Prefix(AttackStackSequence __instance)
        {
            if (__instance.owningActor.SkillTactics < 6 && __instance.isMoraleAttack)
            {
                __instance.isMoraleAttack = false;
                WasLowTacticsMoraleAttack = true;
            }
        }

        public static void Postfix(AttackStackSequence __instance)
        {
            if (WasLowTacticsMoraleAttack)
            {
                __instance.isMoraleAttack = true;
                __instance.owningActor.team.ModifyMorale(__instance.owningActor.OffensivePushCost * -1);
                WasLowTacticsMoraleAttack = false;
            }
        }
    }

    //Don't allow Vigilance to boost Initiative unless Tactics 4
    [HarmonyPatch(typeof(AbstractActor), "ForceUnitOnePhaseUp")]
    public static class Gate_Initiative_Boost_Defensive_Patch
    {
        public static bool Prefix(AbstractActor __instance)
        {
            if (__instance.SkillTactics < 4)
                return false;
            else
                return true;
        }
    }

    //Tiered to-hit Inspiratin bonus for different levels of resolve.
    [HarmonyPatch(typeof(ToHit), "GetAttackerAccuracyModifier")]
    public static class ToHit_GetAttackerAccuracyModifier_Patch
    {
        public static void Postfix(ref float __result, AbstractActor attacker)
        {
            if (attacker.team.Morale >= 100)
                __result -= 2;
            else if (attacker.team.Morale >= 75)
                __result -= 1;
        }
    }

    //Correct the Inspired Indicator
    [HarmonyPatch(typeof(CombatHUDStatusPanel), "ShowInspiredIndicator")]
    public static class CombatHUDStatusPanel_ShowInspiredIndicator_Patch
    {
        private static string resolveString = "Initialize";

        public static bool Prefix(CombatHUDStatusPanel __instance, Mech mech)
        {
            if (mech.IsFuryInspired)
                return true;
            if (mech.IsMoraleInspired)
            {
                var resolve = mech.pilot.Team.Morale;
                if (resolve >= 100)
                    resolveString = "This unit gets -1/-2/-3 Difficulty to all attacks for 50%/75%/100% Resolve.";
                else if (resolve >= 75)
                    resolveString = "This unit gets -1/-2/-3 Difficulty to all attacks for 50%/75%/100% Resolve.";
                else
                    resolveString = "This unit gets -1/-2/-3 Difficulty to all attacks for 50%/75%/100% Resolve.";

                Traverse showBuff = Traverse.Create(__instance).Method("ShowBuff", new Type[] { typeof(SVGAsset), typeof(Text),
                typeof(Text), typeof(Vector3), typeof(bool) });

                showBuff.GetValue(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.StatusInspiredIcon,
                    new Text("INSPIRED", Array.Empty<object>()), new Text(resolveString, Array.Empty<object>()), __instance.defaultIconScale, false);

                return false;
            }
            return true;
        }
    }


    //[HarmonyPatch(typeof(CombatHUDMechTray), "refreshMechInfo")]
    //public static class CombatHUDMechTray_refreshMechInfo_Patch
    //{
    //    public static void Postfix(CombatHUDMechTray __instance, Mech ___mech)
    //    {
    //        if (___mech != null)
    //            __instance.StatusPanel.ShowInspiredIndicator(___mech);
    //    }
    //}
    //    //Invoke dark rituals
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        var codes = instructions.ToList();
    //        for (var i = 0; i < codes.Count; i++)
    //        {
    //            Log("a");
    //            if (codes[i].opcode == OpCodes.Ldstr &&
    //                codes[i].operand.ToString().StartsWith("This unit gets"))
    //            {
    //                codes[i].operand = BonusString;
    //            }
    //        }
    //        return codes.AsEnumerable();
    //    }
    //}


        //[HarmonyPatch(typeof(CombatHUDStatusPanel), "ShowInspiredIndicator")]
        //public static class CombatHUDStatusPanel_ShowInspiredIndicator_Patch
        //{
        //    public static string MakeString(Mech mech)
        //    {
        //        try
        //        {
        //            var resolve = mech.pilot.Team.Morale;
        //            if (resolve >= 100)
        //                return "This unit gets -3 Difficulty to all attacks.";
        //            if (resolve >= 75)
        //                return "This unit gets -2 Difficulty to all attacks.";
        //            return "feces";
        //            //return "This unit gets -1 Difficulty to all attacks.";
        //        }
        //        catch { return "Bombs away"; }
        //    }

        //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        var codes = instructions.ToList();
        //        var helper = AccessTools.Method(typeof(CombatHUDStatusPanel_ShowInspiredIndicator_Patch), "MakeString", new[] { typeof(Mech) });
        //        for (var i = 0; i < codes.Count; i++)
        //        {
        //            if (codes[i].opcode == OpCodes.Ldstr &&
        //                codes[i].operand.ToString().StartsWith("This unit gets"))
        //            {
        //                codes.RemoveAt(i);
        //                codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
        //                codes.Insert(i, new CodeInstruction(OpCodes.Call, helper));
        //            }
        //        }
        //        return codes.AsEnumerable();
        //    }
        //}
    }
