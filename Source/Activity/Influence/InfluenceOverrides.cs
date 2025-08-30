using System.Collections;
using UnityEngine;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DealerRobbery;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Map;
using FishNet;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
using Il2CppFishNet;
#endif

namespace CartelEnforcer
{
    public static class InfluenceOverrides
    {
        public static List<EMapRegion> mapReg = new();
        public static bool ShouldChangeInfluence(EMapRegion region)
        {
            bool changeInfluence = false;
#if MONO
            if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                changeInfluence = true;
#else
            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                mapReg.Add(unlmapReg);
            if (InstanceFinder.IsServer && mapReg.Contains(region))
                changeInfluence = true;
            mapReg.Clear();
#endif
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
    }
}
