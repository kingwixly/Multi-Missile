using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace JUSTJ7780.kingwixly.MultiMissilePatches
{
    internal static class AircraftCruiseVlsState
    {
        internal const string ExocetJsonKey = "exocet_am39_block3c";
        internal const string ZirconJsonKey = "zircon_3m22";
        internal const string ZirconNuclearJsonKey = "zircon_3m22_nuclear";
        internal const string AtmacaJsonKey = "atmaca_al";
        internal const string Agm84JsonKey = "agm_84_harpoon";

        internal static bool IsAircraftLaunchedSupportedCruiseMissile(Missile missile)
        {
            if (missile == null || !(missile.owner is Aircraft) || missile.definition == null)
            {
                return false;
            }

            string jsonKey = missile.definition.jsonKey;
            return string.Equals(jsonKey, ExocetJsonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(jsonKey, ZirconJsonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(jsonKey, ZirconNuclearJsonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(jsonKey, AtmacaJsonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(jsonKey, Agm84JsonKey, StringComparison.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch(typeof(VLSBooster), "Awake")]
    [HarmonyPriority(Priority.First)]
    internal static class DecorativeVlsAwakeGuardPatch
    {
        private static bool Prefix(Missile ___missile)
        {
            if (___missile != null)
            {
                return true;
            }

            Plugin.Log.LogDebug("Skipped VLSBooster.Awake on a decorative VLS object.");
            return false;
        }
    }

    // find a better way to kep boosters
    // add some debugging 
    [HarmonyPatch(typeof(VLSBooster), "VLSBooster_OnInitialize")]
    internal static class AircraftCruiseVlsPatch
    {
        private static bool Prefix(Missile ___missile)
        {
            if (!AircraftCruiseVlsState.IsAircraftLaunchedSupportedCruiseMissile(___missile))
            {
                return true;
            }

            ___missile.boosterIsAttached = true;
            Plugin.Log.LogInfo("Kept VLS booster for an aircraft-launched cruise missile.");
            return false;
        }
    }

    [HarmonyPatch(typeof(VLSBooster), "Burnout")]
    internal static class AircraftCruiseVlsBurnoutPatch
    {
        private static bool Prefix(VLSBooster __instance, Missile ___missile)
        {
            if (!AircraftCruiseVlsState.IsAircraftLaunchedSupportedCruiseMissile(___missile))
            {
                return true;
            }

            try
            {
                ParticleSystem[] particleSystems = Traverse.Create(__instance).Field("particleSystems").GetValue<ParticleSystem[]>();
                if (particleSystems != null)
                {
                    foreach (ParticleSystem particleSystem in particleSystems)
                    {
                        if (particleSystem != null)
                        {
                            particleSystem.Stop();
                        }
                    }
                }

                AudioSource[] audioSources = Traverse.Create(__instance).Field("audioSources").GetValue<AudioSource[]>();
                if (audioSources != null)
                {
                    foreach (AudioSource audioSource in audioSources)
                    {
                        if (audioSource != null && audioSource.loop)
                        {
                            audioSource.Stop();
                        }
                    }
                }

                Light[] lights = Traverse.Create(__instance).Field("lights").GetValue<Light[]>();
                if (lights != null)
                {
                    foreach (Light light in lights)
                    {
                        if (light != null)
                        {
                            light.enabled = false;
                        }
                    }
                }

                Traverse.Create(__instance).Field("separated").SetValue(true);
                ___missile.boosterIsAttached = false;
                __instance.transform.SetParent(null, true);

                Rigidbody boosterBody = __instance.GetComponent<Rigidbody>();
                if (boosterBody == null)
                {
                    boosterBody = __instance.gameObject.AddComponent<Rigidbody>();
                }

                boosterBody.mass = Traverse.Create(__instance).Field("dryMass").GetValue<float>();
                boosterBody.drag = 0.1f;
                boosterBody.angularDrag = 0.01f;
                boosterBody.isKinematic = false;
                boosterBody.interpolation = RigidbodyInterpolation.Interpolate;
                boosterBody.velocity = ___missile.rb == null || ___missile.rb.isKinematic ? Vector3.zero : ___missile.rb.velocity;
                Traverse.Create(__instance).Field("rb").SetValue(boosterBody);

                UnityEngine.Object.Destroy(__instance.gameObject, 10f);
                Plugin.Log.LogInfo("Separated VLS booster safely for an aircraft-launched cruise missile.");
                return false;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError("Aircraft cruise-missile VLS burnout fallback failed: " + exception);
                return true;
            }
        }
    }
}
