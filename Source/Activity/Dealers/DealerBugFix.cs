using UnityEngine;
using HarmonyLib;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.DealerActivity;
using static CartelEnforcer.CartelEnforcer;


#if MONO
using FishNet.Object;
using ScheduleOne.Cartel;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Handover;

#else
using Il2CppFishNet.Object;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Handover;

#endif


namespace CartelEnforcer
{

    [HarmonyPatch(typeof(Dealer), "CustomerContractEnded")]
    public static class Dealer_CustomerContractEnded_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Dealer __instance, Contract contract)
        {
            if (__instance.DealerType == EDealerType.CartelDealer)
            {
                if (currentConfig.enhancedDealers)
                {
#if MONO
                    WalkToInterestPoint(__instance as CartelDealer);
#else
                    WalkToInterestPoint(__instance.TryCast<CartelDealer>());
#endif
                }
            }
            return true;
        }
    }

    // Fix a bug where current activity can be null and throw errors if callback function is registered
    [HarmonyPatch(typeof(CartelRegionActivities), "ActivityEnded")]
    public static class CartelRegionActivities_Patch
    {
        public static bool Prefix(CartelRegionActivities __instance)
        {
            if (__instance.CurrentActivity == null)
                return false;

            return true;
        }
    }


}
