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
        static readonly FieldInfo SpawnedPrefabField = AccessTools.Field(typeof(Hardpoint), "spawnedPrefab");
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
        // and attach a keeper so nothing later in the spawn flips it back off
        static void Postfix(Hardpoint __instance)
        {
            if (__instance == null) return;

            var mount = HardpointMountField.GetValue(__instance) as WeaponMount;
            if (mount == null) { Plugin.Log.LogInfo("Brimstone x12 patch: SpawnMount ran, no mount set yet"); return; }
            if (Traverse.Create(mount).Field("jsonKey").GetValue<string>() != BrimstoneJsonKey) return;

            var options = OptionsField.GetValue(__instance) as Array;
            if (options == null) return;

            Renderer panel = null;
            foreach (var o in options)
            {
                var m = OptionMountField.GetValue(o) as WeaponMount;
                if (m != null && m.name == SourceMountName) { panel = OptionRendererField.GetValue(o) as Renderer; break; }
            }
            if (panel == null) { Plugin.Log.LogWarning("Brimstone x12 patch: no AGM-48 panel option on this hardpoint"); return; }

            panel.enabled = true;

            var spawned = SpawnedPrefabField.GetValue(__instance) as GameObject;
            if (spawned == null) { Plugin.Log.LogWarning("Brimstone x12 patch: panel enabled, but no spawned mount to attach keeper to"); return; }

            var keeper = spawned.GetComponent<BrimstonePanelKeeper>();
            if (keeper == null) keeper = spawned.AddComponent<BrimstonePanelKeeper>();
            keeper.panel = panel;
            keeper.hardpoint = __instance;
            Plugin.Log.LogInfo("Brimstone x12 patch: enabled " + panel.name + " and attached keeper");
        }
    }

    // lives on the spawned brimstone x12 mount, keeps the panel on while this mount is the one fitted
    class BrimstonePanelKeeper : MonoBehaviour
    {
        static readonly FieldInfo SpawnedPrefabField = AccessTools.Field(typeof(Hardpoint), "spawnedPrefab");

        public Renderer panel;
        public Hardpoint hardpoint;

        void LateUpdate()
        {
            if (panel == null || hardpoint == null) { Destroy(this); return; }

            // stop once this mount is no longer the one on the hardpoint (swapped or removed)
            if (!ReferenceEquals(SpawnedPrefabField.GetValue(hardpoint), gameObject)) { Destroy(this); return; }

            if (!panel.enabled) panel.enabled = true;
        }
    }
}