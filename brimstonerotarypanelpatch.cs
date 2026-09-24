using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace kingwixly.MultiMissilePatches
{
    [HarmonyPatch]
    static class BrimstoneRotaryPanelPatch
    {
        const string SourceMountName = "AGM1_rotaryLauncher_UtilityHelo1";
        const string BrimstoneJsonKey = "brimstone_12_ibis";

        static readonly FieldInfo HardpointMountField = AccessTools.Field(typeof(Hardpoint), "mount");
        static readonly FieldInfo OptionsField = AccessTools.Field(typeof(Hardpoint), "pylonOptions");
        static readonly Type OptionType = OptionsField.FieldType.GetElementType();
        static readonly FieldInfo OptionMountField = AccessTools.Field(OptionType, "mount");
        static readonly FieldInfo OptionRendererField = AccessTools.Field(OptionType, "renderer");

        // only hook SpawnMount, that's where the game toggles the door panels
        static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(Hardpoint))
                .Where(m => m.Name == "SpawnMount")
                .Cast<MethodBase>();

        // after the mount spawns, if it's the brimstone x12, turn the agm-48 panel back on
        static void Postfix(Hardpoint __instance)
        {
            if (__instance == null) return;

            var mount = HardpointMountField.GetValue(__instance) as WeaponMount;
            if (mount == null) return;
            if (Traverse.Create(mount).Field("jsonKey").GetValue<string>() != BrimstoneJsonKey) return;

            var options = OptionsField.GetValue(__instance) as Array;
            if (options == null) return;

            foreach (var o in options)
            {
                var m = OptionMountField.GetValue(o) as WeaponMount;
                if (m == null || m.name != SourceMountName) continue;

                var r = OptionRendererField.GetValue(o) as Renderer;
                if (r != null)
                {
                    r.enabled = true;
                    Plugin.Log.LogInfo("Brimstone x12: enabled " + r.name);
                }
                return;
            }
            Plugin.Log.LogWarning("Brimstone x12: no AGM-48 panel option on this hardpoint");
        }
    }
}