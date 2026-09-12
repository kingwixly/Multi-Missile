using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace JUSTJ7780.kingwixly.MultiMissilePatches
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.justj7780.kingwixly.multimissilepatches";
        public const string PluginName = "Multi Missile Add on Pack";
        public const string PluginVersion = "1.0.2";

        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            Log.LogInfo("Multi Missile Patches loaded.");
        }

        // Hello whoever you are looking into the code small disclaimer from me justj
        // i wired up the entire mod for the author for free
        // any models were provided to me by the author under the confirmation all licenses were in order
        // for any model issues please contact the github user that posted this mod
        // for a more in depth disclaimer please decompile the nobp file
        // thank you for reading and have a nice day :)
        

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
