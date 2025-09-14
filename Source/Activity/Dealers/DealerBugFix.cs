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
    // Fix a bug where cartel dealer unlocks locked region suppliers and dealers
    [HarmonyPatch(typeof(Customer), "ProcessHandover")]
    public static class Customer_ProcessHandover_Patch
    {
        [HarmonyPrefix]
#if MONO
        public static bool Prefix(Customer __instance, HandoverScreen.EHandoverOutcome outcome, Contract contract, List<ItemInstance> items, bool handoverByPlayer, bool giveBonuses = true)
#else
        public static bool Prefix(Customer __instance, HandoverScreen.EHandoverOutcome outcome, Contract contract, Il2CppSystem.Collections.Generic.List<ItemInstance> items, bool handoverByPlayer, bool giveBonuses = true)
#endif
        {

            // Original source code copy, removed functions where the nested if clause is always "player", commented out rela change, also total payment to 1
            if (contract.Dealer != null && contract.Dealer.DealerType == EDealerType.CartelDealer)
            {
                float num;
                EDrugType drugType;
                int num2;
                float satisfaction = Mathf.Clamp01(__instance.EvaluateDelivery(contract, items, out num, out drugType, out num2));
                __instance.ChangeAddiction(num / 5f);
                float relationshipChange = CustomerSatisfaction.GetRelationshipChange(satisfaction);
                float change = relationshipChange * 0.2f * Mathf.Lerp(0.75f, 1.5f, num);
                __instance.AdjustAffinity(drugType, change);
                // __instance.NPC.RelationData.ChangeRelationship(relationshipChange, true); <--- this caused bugs

                __instance.TimeSinceLastDealCompleted = 0;
                __instance.NPC.SendAnimationTrigger("GrabItem");
                NetworkObject networkObject = null;
                if (contract.Dealer != null)
                {
                    networkObject = contract.Dealer.NetworkObject;
                }
                float totalPayment = 0; // Because this seems to be displayed to player after each day, increments the value of total dealer sum gained
                __instance.ProcessHandoverServerSide(outcome, items, handoverByPlayer, totalPayment, contract.ProductList, satisfaction, networkObject);

                return false;
            }

            // run original
            return true;
        }
    }

    // Fix a bug where the cartel dealer sends messages to player
    [HarmonyPatch(typeof(Dealer), "CustomerContractEnded")]
    public static class Dealer_CustomerContractEnded_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Dealer __instance, Contract contract)
        {
            if (__instance.DealerType == EDealerType.CartelDealer)
            {
                Log("Cartel Dealer Bug Fix Working");
                if (!__instance.ActiveContracts.Contains(contract))
                {
                    return false;
                }
                __instance.ActiveContracts.Remove(contract);
                contract.SetDealer(null);
                __instance.Invoke("SortContracts", 0.05f);

                if (currentConfig.enhancedDealers)
                {
#if MONO
                    WalkToInterestPoint(__instance as CartelDealer);
#else
                    WalkToInterestPoint(__instance.TryCast<CartelDealer>());
#endif
                }
                return false;
            }

            return true;
        }
    }
    // The customer contract ended patch could use another addition to make the dealer start walking again? It starts standing still when contracts end and max wait time is 1 minute before it can start walking again?


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
