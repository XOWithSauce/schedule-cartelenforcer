using HarmonyLib;
using MelonLoader;
using System.Collections;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs;
using ScheduleOne.UI;
using ScheduleOne.Map;
using FishNet;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Map;
using Il2CppFishNet;
#endif

namespace CartelEnforcer
{
    public static class StealBackCustomer
    {
        
        public static List<StolenNPC> stolenNPCs = new();

        public class StolenNPC
        {
            public NPC npc = null;
            public int sampleChancesProcessed = -1;
        }

        public static void OnDayPassTrySteal()
        {
            if (!registered) return;
            if (!InstanceFinder.NetworkManager.IsServer)
            {
                return;
            }
#if MONO
            // Only when hostile
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
            {
                return;
            }
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
            {
                return;
            }
#endif

            // The days which cartel steals customers on should be dependant on the total cartel influence, lower influence -> higher stealing
            // so that its progressively harder towards late game regions
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;

            Log($"[STEALBACK] DayPass Influence: " + allInfluenceNormalized);

            // 0.00 - 0.50 around midgame-late, tue wed fri sun
            if (allInfluenceNormalized > 0f && allInfluenceNormalized <= 0.66f)
            {
                if (TimeManager.Instance.CurrentDay != EDay.Tuesday && TimeManager.Instance.CurrentDay != EDay.Wednesday && TimeManager.Instance.CurrentDay != EDay.Friday && TimeManager.Instance.CurrentDay != EDay.Sunday)
                {
                    return;
                }
            }
            // 0.50 - 0.70 early game, tue fri sun
            else if (allInfluenceNormalized > 0.66f && allInfluenceNormalized < 0.8f)
            {
                if (TimeManager.Instance.CurrentDay != EDay.Tuesday && TimeManager.Instance.CurrentDay != EDay.Friday && TimeManager.Instance.CurrentDay != EDay.Sunday)
                {
                    return;
                }
            }
            // else only on friday
            else if (TimeManager.Instance.CurrentDay != EDay.Friday)
            {
                return;
            }

            coros.Add(MelonCoroutines.Start(StealCustomerWithDelay(allInfluenceNormalized)));

        }

        public static IEnumerator StealCustomerWithDelay(float cartelInfluence)
        {
            yield return Wait10; // Day passed wait 10
            if (!registered) yield break;

            // Select customers that get attempted to steal
#if MONO
            List<Customer> unlocked = new(Customer.UnlockedCustomers);
#else
            List<Customer> unlocked = new();
            foreach (Customer c in Customer.UnlockedCustomers)
            {
                if (!unlocked.Contains(c))
                    unlocked.Add(c);
            }
#endif
            // Check that theres atleast 18 minimum customers unlocked (pads early game to not have it)
            if (unlocked.Count <= 18) yield break;


            int subtrahend = UnityEngine.Random.Range(3, 8);
            int i = Mathf.Clamp(unlocked.Count - subtrahend, 0, unlocked.Count - 1); // start from somewhere at the end of list
            int maxSuccesfulStolen = 2;
            int currentStolen = 0;
            while (i >= 0 && currentStolen < maxSuccesfulStolen)
            {
                Customer customer = unlocked[i];
                if (CanStealCustomer(customer.NPC, cartelInfluence))
                {
                    StealCustomer(customer.NPC);
                    currentStolen++;
                }

                i = Mathf.Clamp(i - subtrahend, 0, unlocked.Count - 1);
                subtrahend = UnityEngine.Random.Range(5, 9);
                yield return Wait05;
            }

            yield return null;
        }

        public static bool CanStealCustomer(NPC npc, float cartelInfluence)
        {
            if (npc.Region == EMapRegion.Northtown) return false;
            if (!npc.RelationData.Unlocked) return false;
            Log($"[STEALBACK] Evaluate {npc.Region} Customer: {npc.fullName}");

            float chance = UnityEngine.Random.Range(0f, 1f);

            // Relation delta has to be tied to the cartel total influence here just like with missions
            // Lower total cartel influence = lower relation delta to steal it succesfully
            
            // t = ~1 high cartel influence total lower threshold pass
            // t = ~0 low cartel influence total and higher relations are now only ones which wont run chance
            float thresholdRelation = Mathf.Lerp(5f, 1.5f, cartelInfluence);
            Log("[STEALBACK]   Evaluate Steal Threshold: " + thresholdRelation);
            // If relation delta is above the threshold then 80% chance to not steal
            if (npc.RelationData.RelationDelta > thresholdRelation && chance > 0.20f) return false;

            Customer c = npc.GetComponent<Customer>();
            if (Customer.LockedCustomers.Contains(c)) return false;

            // Active contract with someone?
            if (c.CurrentContract != null) return false;

            // If addiction is above 0.8 (0.0-1.0) then 70% chance to not steal
            if (c.CurrentAddiction > 0.8f && chance > 0.30f) return false;

            // If only 1 connection then dont lock it
            if (npc.RelationData.Connections.Count < 2) return false;
            Log("[STEALBACK]   Evaluate Steal Connections: " + npc.RelationData.Connections.Count);
            // If atleast one of the connections of this customer are locked (prevent perma locking everyone thus cant re unlock)
            for (int i = 0; i < npc.RelationData.Connections.Count; i++)
            {
                if (!npc.RelationData.Connections[i].RelationData.Unlocked)
                {
                    return false;
                }
            }
            
            // If during this session customer has been already stolen once -> resets after loading to menu
            List<StealBackCustomer.StolenNPC> currentStolen = new(StealBackCustomer.stolenNPCs);
            for (int i = 0; i < currentStolen.Count; i++)
            {
                if (currentStolen[i].npc == npc)
                {
                    return false;
                }
            }


            return true;
        }

        public static void StealCustomer(NPC npc)
        {
            npc.RelationData.Unlocked = false;
            Customer c = npc.GetComponent<Customer>();
            if (!Customer.LockedCustomers.Contains(c))
                Customer.LockedCustomers.Add(c);
            if (Customer.UnlockedCustomers.Contains(c))
                Customer.UnlockedCustomers.Remove(c);

            c.minsSinceUnlocked = 0;
            c.customerData.GuaranteeFirstSampleSuccess = false;
            c.UpdatePotentialCustomerPoI();

            StolenNPC stolen = new();
            stolen.npc = npc;
            stolen.sampleChancesProcessed = 1;

            stolenNPCs.Add(stolen);

            if (c.AssignedDealer != null)
            {
                c.AssignedDealer.RemoveCustomer(c);
            }

            // notify popup
            // todo test how to make the popup not glitch ingame on first time unlock?
            Singleton<NewCustomerPopup>.Instance.PlayPopup(c);
            Singleton<NewCustomerPopup>.Instance.Title.text = "Customer has been stolen by Cartel!";

            Log($"[STEALBACK]     Stole customer: {npc.fullName}");
            Log($"[STEALBACK]     RelationDelta: {npc.RelationData.RelationDelta}");

            return;
        }
    }

    // Controls the accept of dialogue choice, higher addiction provides higher chance to get them back with samples
    // Also successive samples increment probability
    // And after 3-5 its 100% again?
    [HarmonyPatch(typeof(Customer), "GetSampleSuccess")]
    public static class Customer_GetSampleSuccess_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Customer __instance, ref float __result)
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

            if (stolen != null && stolen.sampleChancesProcessed != -1) 
            {
                float t = (float)stolen.sampleChancesProcessed / 9f;
                if (t >= 1f)
                {
                    __result = 0.95f;
                }
                else
                {
                    __result = Mathf.Lerp(0.05f, 0.75f, Mathf.Clamp01(t * (1f + __instance.CurrentAddiction)));
                }
                stolen.sampleChancesProcessed++;
            }
        }

    }


}
