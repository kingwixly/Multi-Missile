using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace kingwixly.MultiMissilePatches
{
    [HarmonyPatch]
    static class BrimstoneRotaryPanelPatch
    {
        const string SourceMountName = "AGM1_rotaryLauncher_UtilityHelo1";
        const string BrimstoneJsonKey = "brimstone_12_ibis";

        static WeaponMount brimstoneMount;
        static readonly ConditionalWeakTable<Hardpoint, object> done = new ConditionalWeakTable<Hardpoint, object>();
        static readonly FieldInfo OptionsField = AccessTools.Field(typeof(Hardpoint), "pylonOptions");
        static readonly Type OptionType = OptionsField.FieldType.GetElementType();
        static readonly FieldInfo CargoField = AccessTools.Field(OptionType, "cargo");
        static readonly FieldInfo MountField = AccessTools.Field(OptionType, "mount");
        static readonly FieldInfo RendererField = AccessTools.Field(OptionType, "renderer");

        // run before every Hardpoint method so the option exists before the game checks it
        static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(Hardpoint))
                .Where(m => !m.IsStatic && !m.IsAbstract && !m.IsGenericMethodDefinition)
                .Cast<MethodBase>();

        static void Prefix(Hardpoint __instance)
        {
            if (__instance == null || done.TryGetValue(__instance, out _)) return;

            var options = OptionsField.GetValue(__instance) as Array;
            if (options == null) return;

            object agmOption = null;
            foreach (var o in options)
            {
                var m = MountField.GetValue(o) as WeaponMount;
                if (m != null && m.name == SourceMountName) agmOption = o;
            }
            if (agmOption == null) { done.Add(__instance, null); return; } // not the helo door hardpoint

            if (brimstoneMount == null)
                brimstoneMount = Resources.FindObjectsOfTypeAll<WeaponMount>()
                    .FirstOrDefault(w => Traverse.Create(w).Field("jsonKey").GetValue<string>() == BrimstoneJsonKey);
            if (brimstoneMount == null) return; // bundle not loaded yet, try again next call

            var extra = Activator.CreateInstance(OptionType);
            CargoField.SetValue(extra, false);
            MountField.SetValue(extra, brimstoneMount);
            RendererField.SetValue(extra, RendererField.GetValue(agmOption));

            var bigger = Array.CreateInstance(OptionType, options.Length + 1);
            Array.Copy(options, bigger, options.Length);
            bigger.SetValue(extra, options.Length);
            OptionsField.SetValue(__instance, bigger);

            done.Add(__instance, null);
        }
    }
}