using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.FrequencyOverrides;
using static CartelEnforcer.AmbushOverrides;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Map;
using ScheduleOne.PlayerScripts;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.PlayerScripts;
#endif

namespace CartelEnforcer
{
    // Basically same as in original source code but patched to obey config cooldowns
    [HarmonyPatch(typeof(CartelActivities), "TryStartActivity")]
    public static class CartelActivities_TryStartActivityPatch
    {

        private static readonly string name = "TryStartGlobalActivity";

        [HarmonyPrefix]
        public static bool Prefix(CartelActivities __instance)
        {
            Log("TryStartGlobalActivity");

            int hours = GetActivityHours("Ambush");

            if (hours == 0) // use game default
                __instance.HoursUntilNextGlobalActivity = CartelActivities.GetNewCooldown();
            else
                __instance.HoursUntilNextGlobalActivity = hours;

            if (!__instance.CanNewActivityBegin())
            {
                Log("NewActivity Cant Begin", name);
                return false;
            }
#if MONO
            List<CartelActivity> activitiesReadyToStart = new(__instance.GetActivitiesReadyToStart());
            List<EMapRegion> validRegionsForActivity = new(__instance.GetValidRegionsForActivity());
#else
            List<CartelActivity> activitiesReadyToStart = new(); 
            List<EMapRegion> validRegionsForActivity = new();
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
                Log("No Activities or Regions ready to start", name);
                return false;
            }
            Log($"Total Activities ready to start: {activitiesReadyToStart.Count}", name);
            validRegionsForActivity.Sort((a, b) => NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(b).CompareTo(NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(a)));

            bool useOrdered = false;
            EMapRegion playerRegion = Player.Local.CurrentRegion;
            if (validRegionsForActivity.Contains(playerRegion))
            {
                useOrdered = true;
                // Set to first
                validRegionsForActivity.Remove(playerRegion);
                validRegionsForActivity.Insert(0, playerRegion);
            }

            EMapRegion region = EMapRegion.Northtown;
            bool flag = false;
            if (!currentConfig.debugMode)
            {
                foreach (EMapRegion emapRegion in validRegionsForActivity)
                {
                    float influence = NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(emapRegion);
                    float mult = 0f;
                    float result = 0f;
                    //per original source code mult config default is 0.8 
                    mult = ambushSettings.AmbushTriggerProbability;
                    result = influence * mult;
                    if (UnityEngine.Random.Range(0f, 1f) < result)
                    {
                        region = emapRegion;
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    Log("Ambush Random Roll not triggered", name);
                    return false;
                }
                Log("Check the Ambush hours", name);
            }
            else // For DEbug mode it always gets the trigger where player stands
            {
                flag = true;
                region = Player.Local.CurrentRegion;
            }

            int readyCount = activitiesReadyToStart.Count;
            Log($"Ambush Pos ReadyCount: {readyCount}", name);
            
            bool hasCheckedFirst = false;
            do
            {
                readyCount = activitiesReadyToStart.Count;
                if (readyCount == 0) break;

                // And then here also check first index and after that continue as random
                // to make the current region ambushes more preferred
                int activityIndex = 0;
                if (useOrdered)
                {
                    activityIndex = hasCheckedFirst ? UnityEngine.Random.Range(0, readyCount) : 0;
                    hasCheckedFirst = true;
                }
                else
                    activityIndex = UnityEngine.Random.Range(0, readyCount);

                if (activitiesReadyToStart[activityIndex].IsRegionValidForActivity(region))
                {
                    Log("SPAWN AMBUSH in region: " + region, name);
                    NetworkSingleton<Cartel>.Instance.Activities.StartGlobalActivity(null, region, 0);
                    // And Then because that above function overrides the cooldown we must reset
                    if (hours == 0) // use game default
                        __instance.HoursUntilNextGlobalActivity = CartelActivities.GetNewCooldown();
                    else
                        __instance.HoursUntilNextGlobalActivity = hours;
                    break;
                }
                else
                {
                    activitiesReadyToStart.Remove(activitiesReadyToStart[activityIndex]);
                }

            } while (readyCount != 0);

            Log("TryStartGlobalActivity Finished", name);
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
        private static readonly string name = "TryStartRegionActivity";

        [HarmonyPrefix]
        public static bool Prefix(CartelRegionActivities __instance)
        {
            // inst + tuple int, identifier string
            Dictionary<CartelActivity, Tuple<int, string>> enabledActivities = new();

            int hours = GetActivityHours("RegionActivity");

            if (hours == 0) // use game default
                __instance.HoursUntilNextActivity = CartelRegionActivities.GetNewCooldown(__instance.Region);
            else
                __instance.HoursUntilNextActivity = hours;

            Log("TryStartRegionalActivity", name);

            // parse activity int and identifier
            foreach (CartelActivity inRegAct in __instance.Activities)
            {
                int actInt = 0;
                string identifier = "";
#if MONO
                if (inRegAct is StealDeadDrop)
                {
                    actInt = 0;
                    identifier = "StealDeadDrop";
                }
                else if (inRegAct is CartelCustomerDeal)
                {
                    actInt = 1;
                    identifier = "CartelCustomerDeal";
                }
                else if (inRegAct is RobDealer)
                {
                    actInt = 2;
                    identifier = "RobDealer";
                }
                else // spray graffiti
                {
                    actInt = 3;
                    identifier = "SprayGraffiti";
                }
#else
                if (inRegAct.TryCast<StealDeadDrop>() != null)
                {
                    actInt = 0;
                    identifier = "StealDeadDrop";
                }
                else if (inRegAct.TryCast<CartelCustomerDeal>() != null)
                {
                    actInt = 1;
                    identifier = "CartelCustomerDeal";
                }
                else if (inRegAct.TryCast<RobDealer>() != null)
                {
                    actInt = 2;
                    identifier = "RobDealer";
                }
                else // spray graffiti
                {
                    actInt = 3;
                    identifier = "SprayGraffiti";
                }
#endif
                CartelRegActivityHours hrInstance = regActivityHours.First(x => x.cartelActivityClass == actInt);
                if (hrInstance != null && hrInstance.hoursUntilEnable <= 0)
                    enabledActivities.Add(inRegAct, new (actInt, identifier));
            }

            if (enabledActivities.Count == 0)
            {
                Log("No Regional Activities can be enabled at this moment", name);
                enabledActivities.Clear();
                return false;
            }

            int enabledCount = enabledActivities.Count;
            Log("Enabled Activities Count: " + enabledCount, name);
            do
            {
                enabledCount = enabledActivities.Count;
                if (enabledCount == 0) break;

                KeyValuePair<CartelActivity, Tuple<int, string>> selected = enabledActivities.ElementAt(UnityEngine.Random.Range(0, enabledCount));
                if (selected.Key.IsRegionValidForActivity(__instance.Region))
                {
                    __instance.StartActivity(null, __instance.Activities.IndexOf(selected.Key));

                    // User wants to use game default cooldown for the regional inner events
                    // that means its by default NOT cooldown limited at all and is only
                    // limited by the Region level activity cooldown therefore hours until enable is 0
                    // otherwise if they have custom value it will obey to those...
                    regActivityHours[selected.Value.Item1].hoursUntilEnable = GetActivityHours(selected.Value.Item2);
                    // Finally break
                    break;
                }
                else
                {
                    enabledActivities.Remove(selected.Key);
                }
            } while (enabledCount != 0);

            Log("Finished TryStartRegionalActivity", name);
            enabledActivities.Clear();
            enabledActivities = null;
            return false; 
        }
    }
}
