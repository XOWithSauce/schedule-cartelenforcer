using HarmonyLib;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.FrequencyOverrides;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Map;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
#endif

namespace CartelEnforcer
{
    // Basically same as in original source code but patched to obey the global activity frequency cap of mod
    [HarmonyPatch(typeof(CartelActivities), "TryStartActivity")]
    public static class CartelActivities_TryStartActivityPatch
    {
#if IL2CPP
        public static List<CartelActivity> activitiesReadyToStart = new(); 
        public static List<EMapRegion> validRegionsForActivity = new();
#endif
        [HarmonyPrefix]
        public static bool Prefix(CartelActivities __instance)
        {
            Log("[GLOBACT] TryStartGlobalActivity");
            __instance.HoursUntilNextGlobalActivity = CartelActivities.GetNewCooldown();
            if (!__instance.CanNewActivityBegin())
            {
                Log("[GLOBACT]    NewActivity Cant Begin");
                return false;
            }
#if MONO
            List<CartelActivity> activitiesReadyToStart = NetworkSingleton<Cartel>.Instance.Activities.GetActivitiesReadyToStart();
            List<EMapRegion> validRegionsForActivity = NetworkSingleton<Cartel>.Instance.Activities.GetValidRegionsForActivity();
#else
            foreach (CartelActivity actItem in __instance.GetActivitiesReadyToStart())
            {
                activitiesReadyToStart.Add(actItem);
            }
            foreach (EMapRegion reg in __instance.GetValidRegionsForActivity())
            {
                validRegionsForActivity.Add(reg);
            }
#endif
            if (activitiesReadyToStart.Count == 0 || validRegionsForActivity.Count == 0)
            {
                Log("[GLOBACT]    No Activities or Regions ready to start");
                return false;
            }
            Log($"[GLOBACT]    Total Activities ready to start: {activitiesReadyToStart.Count}");
            validRegionsForActivity.Sort((a, b) => NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(b).CompareTo(NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(a)));
            EMapRegion region = EMapRegion.Northtown;
            bool flag = false;
            foreach (EMapRegion emapRegion in validRegionsForActivity)
            {
                float influence = NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(emapRegion);
                // This part is modified to obey the influence mod
                float mult = 0f;
                float result = 0f;
                if (currentConfig.activityInfluenceMin == 0.0f)
                {
                    //per original source code
                    mult = 0.8f;
                    result = influence * mult; // this is actually division, only 80% of original influence
                                               // And then if result is higher than 0..1 ranged rand
                                               // Per original source code Random value 0..1 is smaller than result
                    if (UnityEngine.Random.Range(0f, 1f) < result)
                    {
                        region = emapRegion;
                        flag = true;
                        break;
                    }
                }
                else if (currentConfig.activityInfluenceMin > 0.0f)
                {
                    result = Mathf.Lerp(influence * 0.8f, 1f, currentConfig.activityInfluenceMin);
                    if (UnityEngine.Random.Range(0f, 1f) < result)
                    {
                        region = emapRegion;
                        flag = true;
                        break;
                    }
                }
                else
                {
                    // flip because negative
                    float t = -currentConfig.activityInfluenceMin;
                    // now if activityInfluenceMin was -1.0, it becomes t=1 so that multiplier is always 0f
                    // meaning that the random range check will always return true
                    mult = Mathf.Lerp(1f, 0f, currentConfig.activityInfluenceMin);
                    result = influence * mult;
                    if (UnityEngine.Random.Range(0f, 1f) < result)
                    {
                        region = emapRegion;
                        flag = true;
                        break;
                    }
                }

            }
            if (!flag)
            {
                Log("[GLOBACT]    Ambush Random Roll not triggered");
                return false;
            }
            Log("[GLOBACT]    Check the Ambush hours");
            // Now we check that the activity activation obeys to the ambush in config
            // Element at 0 is always the timer for Ambush global activity
            if (regActivityHours[0].hoursUntilEnable > 0)
            {
                Log("[GLOBACT]    Ambush not ready");
                return false;
            }


            int readyCount = activitiesReadyToStart.Count;
            Log($"[GLOBACT]    Ambush Pos ReadyCount: {readyCount}");
            do
            {
                readyCount = activitiesReadyToStart.Count;
                if (readyCount == 0) break;

                int activityIndex = UnityEngine.Random.Range(0, readyCount);
                if (activitiesReadyToStart[activityIndex].IsRegionValidForActivity(region))
                {
                    Log("[GLOBACT]    Start Global Activity");
                    NetworkSingleton<Cartel>.Instance.Activities.StartGlobalActivity(null, region, 0);
                    regActivityHours[0].hoursUntilEnable = GetActivityHours(currentConfig.ambushFrequency);
                    break;
                }
                else
                {
                    activitiesReadyToStart.Remove(activitiesReadyToStart[activityIndex]);
                }

            } while (readyCount != 0);

            Log("[GLOBACT] TryStartGlobalActivity Finished");
#if IL2CPP
            activitiesReadyToStart.Clear();
            validRegionsForActivity.Clear();
#endif
            return false; // Always block since patch handles the original source code
        }
    }

    [HarmonyPatch(typeof(CartelRegionActivities), "TryStartActivity")]
    public static class CartelRegionActivities_TryStartActivityPatch
    {
        public static Dictionary<CartelActivity, List<int>> enabledActivities = new();
        [HarmonyPrefix]
        public static bool Prefix(CartelRegionActivities __instance)
        {
            __instance.HoursUntilNextActivity = CartelRegionActivities.GetNewCooldown(__instance.Region);
            Log("[REGACT] TryStartRegionalActivity");
            // Maps out indexes in the reg act hours
            List<int> foundMatch = new();
            for (int i = 0; i < regActivityHours.Count; i++)
            {
                if (regActivityHours[i].region == (int)__instance.Region)
                {
                    foundMatch.Add(i);
                }
            }
            // parse activity int
            foreach (CartelActivity inRegAct in __instance.Activities)
            {
                int actInt = 0;
#if MONO
                if (inRegAct is StealDeadDrop)
                    actInt = 0;
                else if (inRegAct is CartelCustomerDeal)
                    actInt = 1;
                else // else its RobDealer class
                    actInt = 2;
#else
                if (inRegAct.TryCast<StealDeadDrop>() != null)
                    actInt = 0;
                else if (inRegAct.TryCast<CartelCustomerDeal>() != null)
                    actInt = 1;
                else // else its RobDealer class
                    actInt = 2;
#endif

                for (int i = 0; i < foundMatch.Count; i++)
                {
                    if (regActivityHours[foundMatch[i]].cartelActivityClass == actInt)
                    {
                        if (regActivityHours[foundMatch[i]].hoursUntilEnable <= 0)
                        {
                            if (!enabledActivities.ContainsKey(inRegAct))
                            {
                                //Log("Hours Until Enable Satisfied - IDX: " + foundMatch[i]);
                                List<int> indexAndActInt = new() { foundMatch[i], actInt };
                                enabledActivities.Add(inRegAct, indexAndActInt);
                            }
                        }
                    }
                }
            }

            if (enabledActivities.Count == 0)
            {
                Log("[REGACT]    No Regional Activities can be enabled at this moment");
                enabledActivities.Clear();
                return false;
            }

            int enabledCount = enabledActivities.Count;
            Log("[REGACT]    Enabled Activities Count: " + enabledCount);
            do
            {
                enabledCount = enabledActivities.Count;
                if (enabledCount == 0) break;

                KeyValuePair<CartelActivity, List<int>> selected = enabledActivities.ElementAt(UnityEngine.Random.Range(0, enabledCount));
                if (selected.Key.IsRegionValidForActivity(__instance.Region))
                {
                    __instance.StartActivity(null, __instance.Activities.IndexOf(selected.Key));
                    Log("[REGACT]    Starting Activity!");
                    if (selected.Value[1] == 0)// StealDeadDrop
                    {
                        regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.deadDropStealFrequency);
                    }
                    else if (selected.Value[1] == 1)// CartelCustomerDeals
                    {
                        regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.cartelCustomerDealFrequency);
                    }
                    else if (selected.Value[1] == 2)// RobDealer
                    {
                        regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.cartelRobberyFrequency);
                    }

                    // Finally break
                    break;
                }
                else
                {
                    enabledActivities.Remove(selected.Key);
                }
            } while (enabledCount != 0);

            Log("[REGACT] Finished TryStartRegionalActivity");
            enabledActivities.Clear();
            return false; // Just block running the original function with shuffle as described in comments
        }
    }
}
