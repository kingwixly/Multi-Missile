using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace kingwixly.MultiMissilePatches
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kingwixly.multimissilepatches";
        public const string PluginName = "Multi Missile Add on Pack";
        public const string PluginVersion = "1.4.0";

        // Changed every x.*.x
        public const string VersionCodename = "CENTER_MASS";
        /* they call it center mass
        that part they aim for */

        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            Log.LogInfo("Multi Missile Patches " + PluginVersion + " \"" + VersionCodename + "\" loaded.");
        }

        

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
                harmony = null;
            }
        }
    }
}
