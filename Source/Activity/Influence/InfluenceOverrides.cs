using System.Collections;
using UnityEngine;
using HarmonyLib;
using MelonLoader;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelGathering;
using static CartelEnforcer.StealBackCustomer;

#if MONO
using ScheduleOne.Graffiti;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.Levelling;
using ScheduleOne.Map;
using ScheduleOne.NPCs.Relation;
using ScheduleOne.PlayerScripts;
using FishNet;
#else
using Il2CppScheduleOne.Graffiti;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppFishNet;
#endif

namespace CartelEnforcer
{
    public static class InfluenceOverrides
    {
        public static bool ShouldChangeInfluence(EMapRegion region)
        {
            bool changeInfluence = false;
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                changeInfluence = false;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                changeInfluence = false;
#endif

#if MONO
            if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                changeInfluence = true;
#else
            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
            {
                if (unlmapReg == region && InstanceFinder.IsServer)
                {
                    changeInfluence = true;
                    break;
                }
            }
#endif
            if (region == EMapRegion.Northtown)
                changeInfluence = false;
            return changeInfluence;
        }
        
        public static IEnumerator ApplyInfluenceConfig()
        {
            yield return Wait2;
            if (!registered) yield break;
            if (currentConfig.activityInfluenceMin != 0.0f)
            {
                Log("Changing Activity Influence Requirements");
                // Change Activity Influence requirements
                CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;

                foreach (CartelActivity act in instanceActivities.GlobalActivities)
                {
                    float result = 0f;
                    if (currentConfig.activityInfluenceMin > 0.0f && act.InfluenceRequirement > 0.0f)
                        result = Mathf.Lerp(act.InfluenceRequirement, 1.0f, currentConfig.activityInfluenceMin);
                    else
                        result = Mathf.Lerp(act.InfluenceRequirement, 0.0f, -currentConfig.activityInfluenceMin);

                    Log($"Changing Global Activity Influence from {act.InfluenceRequirement} to {result}");
                    act.InfluenceRequirement = result;
                }

                CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
                foreach (CartelRegionActivities act in regAct)
                {
                    foreach (CartelActivity activity in act.Activities)
                    {
                        float result = 0f;
                        if (currentConfig.activityInfluenceMin > 0.0f && activity.InfluenceRequirement > 0.0f)
                            result = Mathf.Lerp(activity.InfluenceRequirement, 1.0f, currentConfig.activityInfluenceMin);
                        else
                            result = Mathf.Lerp(activity.InfluenceRequirement, 0.0f, -currentConfig.activityInfluenceMin);

                        Log($"Changing Regional Activity Influence from {activity.InfluenceRequirement} to {result}");
                        activity.InfluenceRequirement = result;
                    }
                }

            }
            Log("Finished changing Activity Influence Requirements");
            yield return null;
        }

        public static IEnumerator OnDayChangeApplyPassiveGain()
        {
            foreach (MapRegionData region in Singleton<Map>.Instance.Regions)
            {
                yield return Wait05;
                if (ShouldChangeInfluence(region.Region))
                {
                    // flip the original influence and apply the mod one
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region.Region, -0.02f + influenceConfig.passiveInfluenceGainPerDay);
                }
            }
            yield return null;
        }

        public static void OnDayPassChangePassive()
        {
            Log("On Day Pass Change Passive Influence");
            coros.Add(MelonCoroutines.Start(OnDayChangeApplyPassiveGain()));
        }
    }

    // Passive influence gain is now no longer default source and purely additive by mod (0.4.1f13)
    [Serializable]
    public class InfluenceConfig
    {
        // Mod added
        public float interceptFail = 0.025f;
        public float interceptSuccess = -0.050f;

        public float deadDropFail = 0.025f;
        public float deadDropSuccess = -0.050f;

        public float gatheringFail = 0.025f;
        public float gatheringSuccess = -0.080f;

        public float robberyPlayerEscape = 0.025f; // Player out of range
        public float robberyGoonEscapeSuccess = 0.025f; // goon escapes to dealer succesfully
        public float robberyGoonDead = -0.080f; // Player kills before goon kills dealer
        public float robberyGoonEscapeDead = -0.050f; // Player kills while goon is escaping

        public float sabotageBombDefused = -0.150f;
        public float sabotageGoonKilled = -0.050f;
        public float sabotageBombExploded = 0.200f;

        // Truced interaction
        public float cartelDealerPersuaded = -0.100f;
        public float trucedGreetingChallenge = -0.150f; // Player greets all 3 gather goons in short timeframe

        // used to be in source
        public float passiveInfluenceGainPerDay = 0.025f;

        // Game defaults here
        public float cartelDealerDied = -0.100f;
        public float ambushDefeated = -0.100f;
        public float graffitiInfluenceReduction = -0.050f;
        public float customerUnlockInfluenceChange = -0.075f;


    }

    // Patch the cartel dealer on died to have modifiable influence
    [HarmonyPatch(typeof(CartelDealer), "DiedOrKnockedOut")]
    public static class CartelDealer_Died_Patch
    {
        public static bool Prefix(CartelDealer __instance)
        {
            if (!InstanceFinder.IsServer)
                return false;
            // Hostile status
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
#endif
            {
                return false;
            }


            NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(__instance.Region, influenceConfig.cartelDealerDied);
            return false;
        }
    }

    // Custom patch to monitor ambush so that there is no need to do opcodes or other shenanigans for the nested ienumerator
    [HarmonyPatch(typeof(Ambush), "SpawnAmbush")]
    public static class Ambush_SpawnAmbush_Patch
    {
        public static bool Prefix(Ambush __instance, Player target, Vector3[] potentialSpawnPoints)
        {
            List<CartelGoon> currentlySpawned = [.. NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons];
            coros.Add(MelonCoroutines.Start(OverMonitorAmbush(potentialSpawnPoints, currentlySpawned, __instance.Region)));
            return true;
        }

        public static IEnumerator OverMonitorAmbush(Vector3[] potentialSpawnPoints, List<CartelGoon> currentlySpawned, EMapRegion region)
        {
            float preMaxElapsed = 1f;
            float preElapsed = 0f;
            List<CartelGoon> ambushSpawned = new();

            while (registered && preElapsed < preMaxElapsed)
            {
                yield return Wait025;
                preElapsed += 0.25f;
                if (NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Count != currentlySpawned.Count)
                {
                    foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons)
                    {
                        if (!currentlySpawned.Contains(goon) && !ambushSpawned.Contains(goon) && !spawnedGatherGoons.Contains(goon))
                        {
                            ambushSpawned.Add(goon);
                        }
                    }
                }
            }

            // if nothing was found ambush didnt run
            if (ambushSpawned.Count == 0)
            {
                yield break;
            }

            // Monitor same state as the original enumerator if there are spawned ambushers
            float maxAmbushElapsed = (float)Ambush.CANCEL_AMBUSH_AFTER_MINS;
            float elapsed = 0f;
            List<CartelGoon> deadGoons = new();
            int spawnedCount = ambushSpawned.Count;

            while (registered && elapsed < maxAmbushElapsed)
            {
                if (ambushSpawned.Count == 0 || deadGoons.Count == spawnedCount) break;

                foreach (CartelGoon goon in ambushSpawned)
                {
                    yield return Wait05;
                    elapsed += 0.5f;
                    if (deadGoons.Contains(goon)) continue;

                    if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                    {
                        deadGoons.Add(goon);
                    }
                }

                foreach (CartelGoon goon in deadGoons)
                {
                    yield return Wait05;
                    elapsed += 0.5f;
                    if (ambushSpawned.Contains(goon))
                        ambushSpawned.Remove(goon);
                }
            }

            if ((ambushSpawned.Count == 0 || deadGoons.Count == spawnedCount) && InstanceFinder.IsServer)
            {
                // flip the original influence and apply the mod one
                float change = -(Ambush.AMBUSH_DEFEATED_INFLUENCE_CHANGE) + influenceConfig.ambushDefeated;
                if (change != 0f)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, change);
            }

        }

    }


    // Patch graffiti influence reward function as blocking, so its basically same as in source but bound to config with influence
    [HarmonyPatch(typeof(WorldSpraySurface), "Reward")]
    public static class WorldSpraySurface_Reward_Patch
    {
        public static bool Prefix(WorldSpraySurface __instance)
        {
            NetworkSingleton<LevelManager>.Instance.AddXP(50);

            if (!InstanceFinder.IsServer)
                return false;
            
            // If cartel is hostile OR if cartel is truced and allied extensions are enabled -> change influence after graffiti
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile || (currentConfig.alliedExtensions && NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced))
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile || (currentConfig.alliedExtensions && NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced))
#endif
            {
                NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(__instance.Region, influenceConfig.graffitiInfluenceReduction);
            }
            return false;
        }
    }

    // Patch OnCustomerUnlocked to allow config applied influence changes
    [HarmonyPatch(typeof(Customer), "OnCustomerUnlocked")]
    public static class Customer_OnCustomerUnlocked_Patch
    {
        public static bool Prefix(Customer __instance, NPCRelationData.EUnlockType unlockType, bool notify)
        {
            // based on source the influence is guarded as follows
            if (!notify || !NetworkSingleton<Cartel>.InstanceExists) return true;
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
#endif
            {
                return true;
            }

            // original function is guaranteed to change influence, prefix overrides that change

            // Since customers can be unlocked and locked with the steal back customer feature
            // the influence awarded from re unlocking is half that of config customerUnlockInfluenceChange
            // note: doesnt persist
            if (currentConfig.stealBackCustomers) // config value to enable steal back is true
            {
                List<StealBackCustomer.StolenNPC> currentStolen = new(StealBackCustomer.stolenNPCs);
                StealBackCustomer.StolenNPC stolen = null;
                for (int i = 0; i < currentStolen.Count; i++)
                {
                    if (currentStolen[i].npc == __instance.NPC)
                    {
                        stolen = currentStolen[i];
                        break;
                    }
                }
                if (stolen != null) 
                {

                    float change = -(Customer.CUSTOMER_UNLOCKED_CARTEL_INFLUENCE_CHANGE) + influenceConfig.customerUnlockInfluenceChange * 0.5f;
                    if (change != 0f && __instance.NPC.Region != EMapRegion.Northtown)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(__instance.NPC.Region, change);
                }
            }
            else // Unlocked customer for the first time (this session)
            {
                // flip the original influence and apply the mod one
                float change = -(Customer.CUSTOMER_UNLOCKED_CARTEL_INFLUENCE_CHANGE) + influenceConfig.customerUnlockInfluenceChange;
                if (change != 0f && __instance.NPC.Region != EMapRegion.Northtown)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(__instance.NPC.Region, change);
            }
            
            return true;
        }
    }


}
