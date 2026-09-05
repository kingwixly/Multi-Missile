using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace JUSTJ7780.kingwixly.MultiMissilePatches
{
    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "PreTerminalMode")]
    internal static class SlamErTerminalRetargetPatch
    {
        private const string SlamErJsonKey = "agm_84h_k_slam_er";
        private const float RetargetRadius = 12000f;
        private static readonly HashSet<int> AttemptedMissiles = new HashSet<int>();

        private static void Prefix(OpticalSeekerCruiseMissile __instance)
        {
            Missile missile = Traverse.Create(__instance).Field("missile").GetValue<Missile>();
            if (missile == null || !missile.IsServer || missile.definition == null
                || !string.Equals(missile.definition.jsonKey, SlamErJsonKey, StringComparison.OrdinalIgnoreCase)
                || missile.NetworkHQ == null)
            {
                return;
            }

            Unit target = Traverse.Create(__instance).Field("targetUnit").GetValue<Unit>();
            if (target != null && !target.disabled)
            {
                return;
            }

            GlobalPosition lastKnownPosition = Traverse.Create(__instance).Field("knownPos").GetValue<GlobalPosition>();
            float terminalRange = Traverse.Create(__instance).Field("terminalRange").GetValue<float>();
            if (missile.timeSinceSpawn <= 6f || !FastMath.InRange(missile.GlobalPosition(), lastKnownPosition, terminalRange)
                || !AttemptedMissiles.Add(missile.GetInstanceID()))
            {
                return;
            }

            Unit replacement = FindReplacement(missile, lastKnownPosition);
            if (replacement == null)
            {
                Plugin.Log.LogInfo("SLAM-ER terminal retarget: no valid nearby replacement target found.");
                return;
            }

            missile.NetworkHQ.TryGetKnownPosition(replacement, out GlobalPosition replacementPosition);
            Traverse seeker = Traverse.Create(__instance);
            seeker.Field("targetUnit").SetValue(replacement);
            seeker.Field("knownPos").SetValue(replacementPosition);
            seeker.Field("aimPos").SetValue(replacementPosition);
            seeker.Field("targetHQAtLaunch").SetValue(replacement.NetworkHQ);
            missile.SetTarget(replacement);
            Plugin.Log.LogInfo("SLAM-ER terminal retarget: " + replacement.unitName + ".");
        }

        private static Unit FindReplacement(Missile missile, GlobalPosition lastKnownPosition)
        {
            Unit bestCandidate = null;
            float bestDistance = RetargetRadius;

            foreach (Unit candidate in UnitRegistry.allUnits)
            {
                if (candidate == null || candidate.disabled || candidate.NetworkHQ == null
                    || candidate.NetworkHQ == missile.NetworkHQ
                    || !(candidate.definition is ShipDefinition || candidate.definition is BuildingDefinition || candidate.definition is VehicleDefinition)
                    || !missile.NetworkHQ.TryGetKnownPosition(candidate, out GlobalPosition candidatePosition))
                {
                    continue;
                }

                float distance = FastMath.Distance(lastKnownPosition, candidatePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }
    }

    // 14000 too much 
    // possibly some bad code here if your reading this future J its probably this causing issues
    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "Seek")]
    [HarmonyPriority(Priority.Last)]
    internal static class SlamErBlindFireAcquisitionPatch
    {
        private const string SlamErJsonKey = "agm_84h_k_slam_er";
        private const float ScanInterval = 0.5f;
        private const float ScanRange = 12000f;
        private const float ScanHalfAngle = 25f;
        private const float BlindFlightTimeout = 120f;
        private const float TargetSelectionWindow = 8f;
        private static readonly Dictionary<int, BlindFireState> States = new Dictionary<int, BlindFireState>();

        private static void Postfix(OpticalSeekerCruiseMissile __instance)
        {
            Missile missile = GetMissile(__instance);
            if (!IsSlamEr(missile) || !missile.IsServer || missile.disabled)
            {
                return;
            }

            BlindFireState state = GetState(missile);
            EnableTerrainAvoidanceDuringBlindFire(__instance, state);
            if (!state.IsBlindFire || state.AcquiredTarget || missile.timeSinceSpawn >= BlindFlightTimeout)
            {
                return;
            }

            if (missile.timeSinceSpawn < state.NextScanTime)
            {
                return;
            }

            state.NextScanTime = missile.timeSinceSpawn + ScanInterval;
            Unit candidate = FindBestShip(missile);
            if (candidate != null)
            {
                ConsiderCandidate(missile, state, candidate);
                if (state.SelectionEndsAt <= 0f)
                {
                    state.SelectionEndsAt = missile.timeSinceSpawn + TargetSelectionWindow;
                    Plugin.Log.LogInfo("SLAM-ER blind-fire evaluating ships for " + TargetSelectionWindow.ToString("F0") + " seconds.");
                }
            }

            if (state.SelectionEndsAt <= 0f || missile.timeSinceSpawn < state.SelectionEndsAt)
            {
                return;
            }

            if (state.BestCandidate == null || state.BestCandidate.disabled)
            {
                state.BestCandidate = null;
                state.SelectionEndsAt = 0f;
                return;
            }

            SetTarget(__instance, missile, state.BestCandidate);
            state.AcquiredTarget = true;
            Plugin.Log.LogInfo("SLAM-ER blind-fire acquired " + state.BestCandidate.unitName + ".");
        }

        private static Unit FindBestShip(Missile missile)
        {
            Unit bestCandidate = null;
            float bestValue = float.MinValue;
            float bestDistance = float.MaxValue;

            foreach (Unit candidate in UnitRegistry.allUnits)
            {
                if (candidate == null || candidate.disabled || candidate.NetworkHQ == null
                    || candidate.NetworkHQ == missile.NetworkHQ || !(candidate.definition is ShipDefinition))
                {
                    continue;
                }

                Vector3 direction = candidate.transform.position - missile.transform.position;
                float distance = direction.magnitude;
                if (distance > ScanRange || distance < 1f
                    || Vector3.Angle(direction, missile.transform.forward) > ScanHalfAngle)
                {
                    continue;
                }

                float candidateValue = candidate.definition.value;
                if (candidateValue > bestValue || (Mathf.Approximately(candidateValue, bestValue) && distance < bestDistance))
                {
                    bestCandidate = candidate;
                    bestValue = candidateValue;
                    bestDistance = distance;
                }
            }

            return bestCandidate;
        }

        private static void ConsiderCandidate(Missile missile, BlindFireState state, Unit candidate)
        {
            if (state.BestCandidate == null || state.BestCandidate.disabled)
            {
                state.BestCandidate = candidate;
                return;
            }

            float candidateValue = candidate.definition.value;
            float bestValue = state.BestCandidate.definition.value;
            if (candidateValue > bestValue)
            {
                state.BestCandidate = candidate;
                return;
            }

            if (Mathf.Approximately(candidateValue, bestValue))
            {
                float candidateDistance = Vector3.Distance(candidate.transform.position, missile.transform.position);
                float bestDistance = Vector3.Distance(state.BestCandidate.transform.position, missile.transform.position);
                if (candidateDistance < bestDistance)
                {
                    state.BestCandidate = candidate;
                }
            }
        }

        private static void SetTarget(OpticalSeekerCruiseMissile seeker, Missile missile, Unit target)
        {
            GlobalPosition targetPosition = target.GlobalPosition();
            Traverse seekerFields = Traverse.Create(seeker);
            seekerFields.Field("targetUnit").SetValue(target);
            seekerFields.Field("knownPos").SetValue(targetPosition);
            seekerFields.Field("aimPos").SetValue(targetPosition);
            seekerFields.Field("targetHQAtLaunch").SetValue(target.NetworkHQ);
            missile.SetTarget(target);
        }

        internal static bool ShouldSuppressBlindFireSlowChecks(OpticalSeekerCruiseMissile seeker)
        {
            Missile missile = GetMissile(seeker);
            if (!IsSlamEr(missile) || missile.disabled)
            {
                return false;
            }

            BlindFireState state = GetState(missile);
            return state.IsBlindFire && !state.AcquiredTarget && missile.timeSinceSpawn < BlindFlightTimeout;
        }

        private static void EnableTerrainAvoidanceDuringBlindFire(
            OpticalSeekerCruiseMissile seeker,
            BlindFireState state)
        {
            if (!state.IsBlindFire || state.AcquiredTarget)
            {
                return;
            }

            Traverse.Create(seeker).Field("terrainAvoidance").SetValue(true);
        }

        private static BlindFireState GetState(Missile missile)
        {
            BlindFireState state;
            int id = missile.GetInstanceID();
            if (States.TryGetValue(id, out state))
            {
                return state;
            }

            Unit launchTarget;
            bool hasLaunchTarget = missile.targetID.TryGetUnit(out launchTarget) && launchTarget != null && !launchTarget.disabled;
            state = new BlindFireState { IsBlindFire = !hasLaunchTarget };
            States[id] = state;
            return state;
        }

        private static Missile GetMissile(OpticalSeekerCruiseMissile seeker)
        {
            return seeker == null ? null : Traverse.Create(seeker).Field("missile").GetValue<Missile>();
        }

        private static bool IsSlamEr(Missile missile)
        {
            return missile != null && missile.definition != null
                && string.Equals(missile.definition.jsonKey, SlamErJsonKey, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class BlindFireState
        {
            internal bool IsBlindFire;
            internal bool AcquiredTarget;
            internal float NextScanTime;
            internal float SelectionEndsAt;
            internal Unit BestCandidate;
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "SlowChecks")]
    internal static class SlamErBlindFireSlowChecksPatch
    {
        private static bool Prefix(OpticalSeekerCruiseMissile __instance)
        {
            return !SlamErBlindFireAcquisitionPatch.ShouldSuppressBlindFireSlowChecks(__instance);
        }
    }
}
