using System.Collections;
using MelonLoader;
using UnityEngine;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.DealerActivity;


#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using FishNet;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppFishNet;
#endif

namespace CartelEnforcer
{

    public class CartelRegActivityHours
    {
        public int region; // Identifier integer maps out to region, but -1 is global
        public int cartelActivityClass = 0; // This will hold the DeadDropSteal (0) class, CartelCustomerDeal (1) or RobDealer (2)
        public int hoursUntilEnable = 0; // ingame hours (60sec)

    }

    public class HrPassParameterMap
    {
        public string itemDesc { get; set; }
        public Func<int> Getter { get; set; }
        public Action<int> Setter { get; set; }
        public Action HourPassAction { get; set; }
        public int modTicksPassed { get; set; }
        public int currentModHours { get; set; }
        public Func<bool> CanPassHour { get; set; }
    }
    public static class FrequencyOverrides
    {
        public static List<HrPassParameterMap> actFreqMapping = new();
        public static List<CartelRegActivityHours> regActivityHours = new();

        public static int GetActivityHours(float configValue)
        {
            Log("Get Activity Hours");
            int hours = 0;
            if (configValue > 0.0f) // 2 days at 0.0 -> every hour at 1.0
            {
                int startValue = 48;
                int endValue = 1;

                hours = Mathf.RoundToInt(Mathf.Lerp(startValue, endValue, configValue));
            }
            else if (configValue < 0.0f) // 2 days at 0.0 -> every 4 days at -1.0
            {
                int startValue = 48;
                int endValue = 96;
                // we flip because its negative
                float t = -configValue;
                hours = Mathf.RoundToInt(Mathf.Lerp(startValue, endValue, t));
            }
            else
            {
                hours = 48;
            }
            return hours;
        }


        public static IEnumerator PopulateParameterMap()
        {
            yield return Wait2;
            if (!registered) yield break;
            Log("Populating Activity Frequency Parameters");

            int indexCurrent = 0;

            CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemDesc = "Global Activities",
                Getter = () => instanceActivities.HoursUntilNextGlobalActivity,
                Setter = (value) => instanceActivities.HoursUntilNextGlobalActivity = value,
                HourPassAction = () => instanceActivities.HourPass(),
                modTicksPassed = 0,
                currentModHours = instanceActivities.HoursUntilNextGlobalActivity,
#if MONO
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile
#else
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
#endif
            });
            indexCurrent++;
            // Add above to the custom activity hours
            CartelRegActivityHours activityGlobalHrs = new();
            activityGlobalHrs.region = -1; // Global Activity Ambush has region index -1
            activityGlobalHrs.hoursUntilEnable = GetActivityHours(currentConfig.ambushFrequency);
            activityGlobalHrs.cartelActivityClass = -1; // -1 reserved for the global ambushes
            regActivityHours.Add(activityGlobalHrs); // Always first element
            // Also populate the weapon arrays
            foreach (CartelActivity globalActivity in instanceActivities.GlobalActivities)
            {
#if MONO
                if (globalActivity is Ambush ambush)
                {
                    if ((MeleeWeapons == null || MeleeWeapons.Length == 0) && (ambush.MeleeWeapons != null && ambush.MeleeWeapons.Length > 0))
                    {
                        MeleeWeapons = ambush.MeleeWeapons;
                    }
                    if ((RangedWeapons == null || RangedWeapons.Length == 0) && (ambush.RangedWeapons != null && ambush.RangedWeapons.Length > 0))
                    {
                        RangedWeapons = ambush.RangedWeapons;
                    }
                }
#else
                Ambush temp = globalActivity.TryCast<Ambush>();
                if (temp != null)
                {
                    if ((MeleeWeapons == null || MeleeWeapons.Length == 0) && (temp.MeleeWeapons != null && temp.MeleeWeapons.Length > 0))
                    {
                        MeleeWeapons = temp.MeleeWeapons;
                    }
                    if ((RangedWeapons == null || RangedWeapons.Length == 0) && (temp.RangedWeapons != null && temp.RangedWeapons.Length > 0))
                    {
                        RangedWeapons = temp.RangedWeapons;
                    }
                }
#endif
            }

            CartelRegionActivities[] regInstanceActivies = NetworkSingleton<Cartel>.Instance.Activities.RegionalActivities;
            foreach (CartelRegionActivities act in regInstanceActivies)
            {
                actFreqMapping.Add(new HrPassParameterMap
                {
                    itemDesc = $"Cartel Regional Activities ({act.Region.ToString()})",
                    Getter = () => act.HoursUntilNextActivity,
                    Setter = (value) => act.HoursUntilNextActivity = value,
                    HourPassAction = () => act.HourPass(),
                    modTicksPassed = 0,
                    currentModHours = act.HoursUntilNextActivity,
#if MONO
                    CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile
#else
                    CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
#endif
                });
                indexCurrent++;
                Log($"  {act.Region.ToString()} - Parsing Inner Activities");
                foreach (CartelActivity inRegAct in act.Activities)
                {
                    CartelRegActivityHours activityHrs = new();
                    activityHrs.region = (int)act.Region;

                    int hours = 0;

#if MONO
                    // Now determine class
                    if (inRegAct is StealDeadDrop)
                    {
                        activityHrs.cartelActivityClass = 0;
                        hours = GetActivityHours(currentConfig.deadDropStealFrequency);
                    }
                    else if (inRegAct is CartelCustomerDeal cartelCustomerDeal)
                    {
                        activityHrs.cartelActivityClass = 1;
                        hours = GetActivityHours(currentConfig.cartelCustomerDealFrequency);

                        if (currentConfig.enhancedDealers)
                        {
                            void OnCustomerDealActive()
                            {
                                Log("[LOCKED CUSTOMER DEAL] On Activated");
                                coros.Add(MelonCoroutines.Start(OnCartelCustomerDeal(cartelCustomerDeal, true)));
                            }
                            cartelCustomerDeal.onActivated = (Action)Delegate.Combine(cartelCustomerDeal.onActivated, new Action(OnCustomerDealActive));
                        }
                    }
                    else // else its RobDealer class
                    {
                        activityHrs.cartelActivityClass = 2;
                        hours = GetActivityHours(currentConfig.cartelRobberyFrequency);
                    }
#else
                    if (inRegAct.TryCast<StealDeadDrop>() != null)
                    {
                        activityHrs.cartelActivityClass = 0;
                        hours = GetActivityHours(currentConfig.deadDropStealFrequency);
                    }
                    else if (inRegAct.TryCast<CartelCustomerDeal>() != null)
                    {
                        activityHrs.cartelActivityClass = 1;
                        hours = GetActivityHours(currentConfig.cartelCustomerDealFrequency);

                        if (currentConfig.enhancedDealers) 
                        {
                            CartelCustomerDeal cartelCustomerDeal = inRegAct.TryCast<CartelCustomerDeal>();
                            void OnCustomerDealActive()
                            {
                                Log("[LOCKED CUSTOMER DEAL] On Activated");
                                coros.Add(MelonCoroutines.Start(OnCartelCustomerDeal(cartelCustomerDeal, true)));
                            }
                            cartelCustomerDeal.onActivated += (Il2CppSystem.Action)OnCustomerDealActive;
                        }

                    }
                    else if (inRegAct.TryCast<RobDealer>() != null)
                    {
                        activityHrs.cartelActivityClass = 2;
                        hours = GetActivityHours(currentConfig.cartelRobberyFrequency);
                    }
                    else
                    {
                        Log("Failed to parse Cartel Activity Hours");
                        continue;
                    }
#endif


                    activityHrs.hoursUntilEnable = hours;

                    regActivityHours.Add(activityHrs);
                    Log($"    {act.Region.ToString()} - {activityHrs.cartelActivityClass} class Added to regActivityHours");
                }
            }

            // NORMAL DEALS WHEN TRUCED
            CartelDealManager instanceDealMgr = NetworkSingleton<Cartel>.Instance.DealManager;
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemDesc = "Cartel Deal Manager (Truced only)",
                Getter = () => instanceDealMgr.HoursUntilNextDealRequest,
                Setter = (value) => instanceDealMgr.HoursUntilNextDealRequest = value,
                HourPassAction = () => instanceDealMgr.HourPass(),
                modTicksPassed = 0,
                currentModHours = instanceDealMgr.HoursUntilNextDealRequest,
#if MONO
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced
#else
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced
#endif
            });
            indexCurrent++;


            // MY DRIVE BY
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemDesc = "Drive-By Events",
                Getter = () => hoursUntilDriveBy,
                Setter = (value) => hoursUntilDriveBy = value,
                HourPassAction = () => hoursUntilDriveBy = Mathf.Clamp(hoursUntilDriveBy - 1, 0, 48),
                modTicksPassed = 0,
                currentModHours = hoursUntilDriveBy,
#if MONO
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile
#else
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
#endif
            });

            Log("Finished populating Activity Frequency Parameters");
            Log($"{regActivityHours.Count} items in RegActivityHours");
            yield return null;
        }


        // Tie the new class into basic hour pass
        public static void OnHourPassReduceCartelRegActHours()
        {
            if (regActivityHours.Count > 0)
            {
                foreach (CartelRegActivityHours regActHrs in regActivityHours)
                {
                    if (regActHrs.hoursUntilEnable > 0)
                        regActHrs.hoursUntilEnable = regActHrs.hoursUntilEnable - 1;
                }
            }
        }

        public static IEnumerator TickOverrideHourPass()
        {
            yield return Wait5;
            if (!registered) yield break;
            if (currentConfig.activityFrequency == 0.0f) yield break;

            float defaultRate = 60f; // By default hour pass is 60sec
            float tickRate = 60f;
            if (currentConfig.activityFrequency > 0.0f) // If number is higher than 0, set tick rate to be rougly 10 times faster at 1.0
                tickRate = Mathf.Lerp(defaultRate, defaultRate / 10, currentConfig.activityFrequency);
            WaitForSeconds WaitCalculated = new WaitForSeconds(tickRate / actFreqMapping.Count);// So we arrive at the end of list around the full time length of tick rate, less cluttering big chunk changes more like overtime one by one we adjust the hourpass cooldowns

            // Else condition here is that Activity Frequency is at minimum -1.0, where tick rate should be 10 times slower
            // But this doesnt work for tickrate because HourPass functions in classes add automatically
            // Therefore we must have an "hour" pass in 600 seconds, handled in the helper set function

            Log("[HOURPASS] Starting HourPass Override, Tick once every " + tickRate + " seconds");
            while (registered)
            {
                if (!registered) yield break;
                if (actFreqMapping.Count == 0) continue;
                foreach (HrPassParameterMap item in actFreqMapping)
                {
                    yield return WaitCalculated;
                    if (!registered) yield break;
                    MelonCoroutines.Start(HelperSet(item));
                }
            }
            yield return null;
        }

        public static IEnumerator HelperSet(HrPassParameterMap hpmap)
        {
            // based on source code these guards needed
            if (!hpmap.CanPassHour())
                yield break;
            if (!InstanceFinder.IsServer)
                yield break;

            if (currentConfig.activityFrequency > 0.0f)
            {
                hpmap.HourPassAction();
            }
            else
            {
                // Because we are decreasing / resetting the hours value, we must avoid changing the value while its at 1 to avoid repeating the same
                // state change from 1->0 by normal hourpass function logic
                if (hpmap.Getter() < 2) yield break;

                float ticksReqForPass = Mathf.Lerp(1, 10, -currentConfig.activityFrequency);

                if (hpmap.Getter() < hpmap.currentModHours)
                    hpmap.Setter(hpmap.currentModHours);
                else
                {
                    // Now because we avoid setting the hourpass to 0, we let higher values reset current "hour" ticks and reassign the randomised next value
                    hpmap.modTicksPassed = 0;
                    hpmap.currentModHours = hpmap.Getter();
                }

                hpmap.modTicksPassed++;

                if (hpmap.modTicksPassed >= ticksReqForPass)
                {
                    hpmap.HourPassAction();
                    hpmap.modTicksPassed = 0;
                    hpmap.currentModHours = hpmap.currentModHours - 1; // Update value now since we ticked
                }
            }

            yield return null;
        }

    }

}
