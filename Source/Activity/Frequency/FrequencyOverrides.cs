using HarmonyLib;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.ConfigLoader;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
#endif

namespace CartelEnforcer
{
    public class CartelRegActivityHours
    {
        public int cartelActivityClass = 0; // This will hold the DeadDropSteal (0) class, CartelCustomerDeal (1) or RobDealer (2) or SprayGraffiti (3)
        public int hoursUntilEnable = 0; // ingame hours (60sec)
    }

    [Serializable]
    public class EventFrequencyConfig
    {
        public List<EventFrequency> events;

        // Must be called for empty init to populate template config file
        public void InitializeDefault()
        {
            // bunch of manual example
            events = new();

            events.Add(new()
            {
                Identifier = "Ambush",
                CooldownHours = 0, // Equivalent default is 15
                InfluenceRequirement = -1f, // base default value: 0.1f
                RandomTimeRangePercentage = 0f, // Equivalent default is 0.625f 
            });

            events.Add(new()
            {
                Identifier = "RegionActivity",
                CooldownHours = 0, // Equivalent default is 30
                InfluenceRequirement = -1f, // not used in any case
                RandomTimeRangePercentage = 0f, // Equivalent default is 0.625
            });

            // 4 inner region acts use only the cd and range of the region activity by default
            // They can use the equivalent default values for cd and random time that region activity uses
            // to cap them separately with identical random range
            events.Add(new()
            {
                Identifier = "StealDeadDrop",
                CooldownHours = 0, // No default cooldown...
                InfluenceRequirement = -1f, // Default value 0f
                RandomTimeRangePercentage = 0f, // no default range
            });

            events.Add(new()
            {
                Identifier = "CartelCustomerDeal",
                CooldownHours = 0, // No default cooldown...
                InfluenceRequirement = -1f, // Default value 0f
                RandomTimeRangePercentage = 0f, // no default range
            });

            events.Add(new()
            {
                Identifier = "RobDealer",
                CooldownHours = 0, // No default cooldown...
                InfluenceRequirement = -1f, // Default value 0.2f
                RandomTimeRangePercentage = 0f, // no default range
            });

            events.Add(new()
            {
                Identifier = "SprayGraffiti",
                CooldownHours = 0, // No default cooldown...
                InfluenceRequirement = -1f, // Default value 0f
                RandomTimeRangePercentage = 0f, // no default range
            });


            events.Add(new()
            {
                Identifier = "CartelPlayerDeal",
                CooldownHours = 0, // Use game default calculation
                InfluenceRequirement = -1f, // not used in any case
                RandomTimeRangePercentage = 0f, // Use game default calculation so this can be 0
            });

            events.Add(new()
            {
                Identifier = "DriveBy",
                CooldownHours = 0, // Use mod default calculation
                InfluenceRequirement = -1f, // Dont use influence requirement at -1f
                RandomTimeRangePercentage = 0f, // Use game default calculation so this can be 0
            });

            events.Add(new()
            {
                Identifier = "InterceptDeals",
                CooldownHours = 0, // Use mod default calculation that is 2hrs
                InfluenceRequirement = -1f, // Dont use influence requirement at -1f
                RandomTimeRangePercentage = 0f, // Use game default calculation so this can be 0
            });

            events.Add(new()
            {
                Identifier = "Gathering",
                CooldownHours = 0, // Use mod default calculation that is bound to dealer activity
                InfluenceRequirement = -1f, // Dont use influence requirement at -1f
                RandomTimeRangePercentage = 0f, // Use game default calculation so this can be 0
            });

            events.Add(new()
            {
                Identifier = "Sabotage",
                CooldownHours = 0, // Use mod default calculation
                InfluenceRequirement = -1f, // Dont use influence requirement at -1f
                RandomTimeRangePercentage = 0f, // Use game default calculation so this can be 0
            });

        }
    }

    [Serializable]
    public class EventFrequency
    {
        public string Identifier = "";

        public int CooldownHours; // int 0 - int.max
        public float InfluenceRequirement; // 0...1, and for values below 0, its unused

        //15% range, Example Cooldown hours now == Rand range min: hours*1f-mult max: hours*1f+mult
        public float RandomTimeRangePercentage = 0.15f;
    }

    // DriveBy, Sabotage, inner regional activities all persist
    [Serializable] 
    public class CurrentEventCooldowns
    {
        public int StealDeadDropCooldown;
        public int CartelCustomerDealCooldown;
        public int RobDealerCooldown;
        public int SprayGraffitiCooldown;

        public int DriveByCooldown;
        public int GatheringCooldown;
        public int InterceptDealsCooldown;

        // key Property Name and cooldown hours
        public Dictionary<string, int> SabotageCooldowns;

        // Must be called on empty init or error
        public void InitializeDefault()
        {
            // These dont use cooldowns at all by default so its fine to have 0 here
            // Since its always additionally limited by the regional activity cooldown
            StealDeadDropCooldown = FrequencyOverrides.GetActivityHours("StealDeadDrop");
            CartelCustomerDealCooldown = FrequencyOverrides.GetActivityHours("CartelCustomerDeal");
            RobDealerCooldown = FrequencyOverrides.GetActivityHours("RobDealer");
            SprayGraffitiCooldown = FrequencyOverrides.GetActivityHours("SprayGraffiti");

            // Mod additional events need non zero default inits
            int driveByHrs = FrequencyOverrides.GetActivityHours("DriveBy");
            DriveByCooldown = driveByHrs != 0 ? driveByHrs : UnityEngine.Random.Range(24, 68);

            int gatheringHrs = FrequencyOverrides.GetActivityHours("Gathering");
            GatheringCooldown = gatheringHrs != 0 ? gatheringHrs : UnityEngine.Random.Range(6, 15);

            int interceptHrs = FrequencyOverrides.GetActivityHours("InterceptDeals");
            InterceptDealsCooldown = interceptHrs != 0 ? interceptHrs : UnityEngine.Random.Range(2, 8);

            int sabotageHrs = FrequencyOverrides.GetActivityHours("Sabotage");
            sabotageHrs = sabotageHrs != 0 ? sabotageHrs : UnityEngine.Random.Range(16, 64);

            SabotageCooldowns = new()
            {
                {"Laundromat", sabotageHrs },
                {"Post Office", sabotageHrs },
                {"Taco Ticklers", sabotageHrs },
            };
        }
    }

    public static class FrequencyOverrides
    {
        public static EventFrequencyConfig frequencyConfig;
        public static List<CartelRegActivityHours> regActivityHours = new();

        public static readonly List<string> SupportedEventIdentifiers = new()
        {
            "Ambush",
            "RegionActivity",
            "StealDeadDrop",
            "CartelCustomerDeal",
            "RobDealer",
            "SprayGraffiti",
            "CartelPlayerDeal",
            "DriveBy",
            "InterceptDeals",
            "Gathering",
            "Sabotage"
        };

        public static void InitFrequencyOverrides()
        {

            // Now it is required to map the region inner events to custom class
            // because the game doesnt implement hoursUntilEnabled for those
            // everything else should be able to calculate the hourpass

            // 0 steal dead drop
            regActivityHours.Add(
                new() { cartelActivityClass = 0, hoursUntilEnable = eventCooldowns.StealDeadDropCooldown }
            );

            // 1 CartelCustomerDeal
            regActivityHours.Add(
                new() { cartelActivityClass = 1, hoursUntilEnable = eventCooldowns.CartelCustomerDealCooldown }
            );

            // 2 RobDealer
            regActivityHours.Add(
                new() { cartelActivityClass = 2, hoursUntilEnable = eventCooldowns.RobDealerCooldown }
            );

            // 3 Spray Graffiti
            regActivityHours.Add(
                new() { cartelActivityClass = 3, hoursUntilEnable = eventCooldowns.SprayGraffitiCooldown }
            );

            // set the mod added events based on persistent config
            DriveByEvent.hoursUntilDriveBy = eventCooldowns.DriveByCooldown;
            CartelGathering.hoursUntilNextGathering = eventCooldowns.GatheringCooldown;
            InterceptEvent.hoursUntilInterceptEvent = eventCooldowns.InterceptDealsCooldown;

            if (eventCooldowns.SabotageCooldowns != null && eventCooldowns.SabotageCooldowns.Count > 0)
            {
                // if it works it works
                // foreach find matching property name and then assing hours until enabled
                // and this crashes 100% of the time if sabotage cooldowns doesnt contain the propertyname
                // but thats never going to happen surely...
                // just wrap the ugly code in trycatch and all is well
                try
                {
                    SabotageEvent.locations.ForEach(x => x.hoursUntilEnabled = eventCooldowns.SabotageCooldowns.First(y => y.Key == x.business.propertyName).Value);
                } catch (Exception ex) { }
            }

            CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;

            Log("Setting ambush influence requirement");
            foreach (CartelActivity act in instanceActivities.GlobalActivities)
            {
                float influenceRequirement = GetByID("Ambush").InfluenceRequirement;
                if (influenceRequirement >= 0f)
                    act.InfluenceRequirement = influenceRequirement;
            }

            Log("Setting inner region activities influence requirements");
            // Then set the instance influence requirements based on config for the inner region activities
            CartelRegionActivities[] regInstanceActivies = NetworkSingleton<Cartel>.Instance.Activities.RegionalActivities;
            foreach (CartelRegionActivities act in regInstanceActivies)
            {
                foreach (CartelActivity inRegAct in act.Activities)
                {
#if MONO
                    string type = inRegAct.GetType().Name;
#else
                    string type = inRegAct.GetIl2CppType().Name;
#endif
                    float influenceRequirement = GetByID(type).InfluenceRequirement;
                    if (influenceRequirement >= 0f)
                        inRegAct.InfluenceRequirement = influenceRequirement;

                }
            }
        }

        public static int GetActivityHours(string identifier)
        {
            // update based on config  
            // +-15% range, Example Cooldown hours now == Rand range min: hours*1f-0.15 max: hours*1f+0.15
            EventFrequency freq = GetByID(identifier);
            float percentage = freq.RandomTimeRangePercentage;

            int hoursMax = Mathf.RoundToInt((float)freq.CooldownHours * (1f + freq.RandomTimeRangePercentage));
            int hoursMin = Mathf.Clamp(Mathf.RoundToInt((float)freq.CooldownHours * (1f - freq.RandomTimeRangePercentage)), 1, hoursMax-1);

            // Use game default or mod defined default value if 0, else mod AND config defined random range
            int result = freq.CooldownHours == 0 ? freq.CooldownHours : UnityEngine.Random.Range(hoursMin, hoursMax);
            Log($"New activity cooldown for {identifier} with result: {result}");

            return result;
        }

        public static EventFrequency GetByID(string identifier)
        {
            return frequencyConfig.events.First(x => SupportedEventIdentifiers.Contains(x.Identifier) && x.Identifier == identifier);
        }

        public static void OnHourPassReduceCartelRegActHours()
        {
            if (regActivityHours == null || regActivityHours.Count == 0) return;
            if (SaveManager.Instance.IsSaving || isSaving) return;

            foreach (CartelRegActivityHours regActHrs in regActivityHours)
            {
                if (regActHrs.hoursUntilEnable > 0)
                    regActHrs.hoursUntilEnable = regActHrs.hoursUntilEnable - 1;
            }
        }
    }

    // Harmony patch to override the hours until next deal request time
    // with custom time, IF the returned value is not set to 0
    [HarmonyPatch(typeof(CartelDealManager), "CompleteDeal")]
    public static class CartelDealManager_CompleteDeal_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CartelDealManager __instance)
        {
            int hours = FrequencyOverrides.GetActivityHours("CartelPlayerDeal");
            __instance.HoursUntilNextDealRequest = hours != 0 ? hours : __instance.HoursUntilNextDealRequest;
            return;
        }
    }

}