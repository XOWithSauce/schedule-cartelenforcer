using HarmonyLib;
using MelonLoader;
using System.Collections;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Persistence;
using ScheduleOne.Messaging;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs;
using ScheduleOne.UI;
using ScheduleOne.Map;
using FishNet;
#else
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Messaging;
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

        public static List<string> playerMessageTemplates = new()
        {
            "I won't be hustling with you anymore. Got better quality from another person...",
            "Sorry, but I've got to change my local. These prices ain't it.",
            "Just letting you know that I won't be hitting you up anymore...",
            "These last deals have been a bit pricey. I'll get my stuff elsewhere.",
            "You've been cutting me short. I'm done buying from you.",
            "Thought we were cool, but you've been ripping me off. Peace.",
            "That last one was too light and quality was trash... I'll find someone else."
        };

        public static void OnDayPassTrySteal()
        {
            coros.Add(MelonCoroutines.Start(WaitSleepEndTrySteal()));
        }
        public static IEnumerator WaitSleepEndTrySteal()
        {
            yield return Wait30;
#if MONO
            yield return new WaitUntil(() => !isSaving && !SaveManager.Instance.IsSaving);
#else
            yield return new WaitUntil((Il2CppSystem.Func<bool>)(() => !isSaving && !SaveManager.Instance.IsSaving));
#endif
            if (!registered) yield break;
            if (!InstanceFinder.NetworkManager.IsServer) yield break;
            // Only when hostile
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile) yield break;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile) yield break;
#endif
            // The days which cartel steals customers on should be dependant on the total cartel influence, lower influence -> higher stealing
            // so that its progressively harder towards late game regions
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;

            // 0.00 - 0.66 around midgame-late, tue wed fri sun
            if (allInfluenceNormalized > 0f && allInfluenceNormalized <= 0.66f)
            {
                if (TimeManager.Instance.CurrentDay != EDay.Tuesday && TimeManager.Instance.CurrentDay != EDay.Friday && TimeManager.Instance.CurrentDay != EDay.Sunday)
                {
                    yield break;
                }
            }
            // 0.66 - 0.8 early game, tue fri sun
            else if (allInfluenceNormalized > 0.66f && allInfluenceNormalized < 0.8f)
            {
                if (TimeManager.Instance.CurrentDay != EDay.Tuesday && TimeManager.Instance.CurrentDay != EDay.Friday)
                {
                    yield break;
                }
            }
            // else only on friday, most likely never runs?
            else if (TimeManager.Instance.CurrentDay != EDay.Friday)
            {
                yield break;
            }
            coros.Add(MelonCoroutines.Start(StealCustomerWithDelay(allInfluenceNormalized)));
        }

        public static IEnumerator StealCustomerWithDelay(float cartelInfluence)
        {
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

            int subtrahend = UnityEngine.Random.Range(3, 6);
            int i = Mathf.Clamp(unlocked.Count - subtrahend, 0, unlocked.Count - 1); // start from somewhere at the end of list
            int maxCustomersChecked = 5;
            int maxSuccesfulStolen = 2;
            int currentCustomersChecked = 0;
            int currentStolen = 0;
            
            EMapRegion region1StolenFrom = EMapRegion.Northtown; // default

            while (registered && i >= 0 && currentStolen < maxSuccesfulStolen && currentCustomersChecked < maxCustomersChecked)
            {
                Customer customer = unlocked[i];
                if (CanStealCustomer(customer.NPC, cartelInfluence))
                {
                    if (region1StolenFrom == EMapRegion.Northtown)
                    {
                        // Store the first region npc reg
                        region1StolenFrom = customer.NPC.Region;
                    }
                    else if (region1StolenFrom != customer.NPC.Region)
                    {
                        // Checked that another customer wasnt stolen in the same region as previous
                        StealCustomer(customer.NPC);
                        currentStolen++;
                    }
                    else
                    {
                        // Same region as previous continue next
                    }

                }

                i = Mathf.Clamp(i - subtrahend, 0, unlocked.Count - 1);
                subtrahend = UnityEngine.Random.Range(3, 6);
                currentCustomersChecked++;
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

            // If the region is westville then 50% chance to not steal regardless
            if (npc.Region == EMapRegion.Westville && chance > 0.50f) return false;

            // Relation delta has to be tied to the cartel total influence here just like with missions
            // Lower total cartel influence = lower relation delta to steal it succesfully

            // t = ~0 = a low cartel influence total and higher relations are now only ones which wont run chance
            // t = ~1 = b high cartel influence total lower threshold pass
            float thresholdRelation = Mathf.Lerp(a: 4.5f, b: 1.5f, cartelInfluence);
            Log("[STEALBACK]   Evaluate Steal Threshold: " + thresholdRelation);
            // If relation delta is above the threshold then 90% chance to not steal
            if (npc.RelationData.RelationDelta > thresholdRelation && chance > 0.10f) return false;
            Customer c = npc.GetComponent<Customer>();
            if (Customer.LockedCustomers.Contains(c)) return false;

            // Active contract with someone?
            if (c.CurrentContract != null) return false;
            // If addiction is above 0.8 (0.0-1.0) then 80% chance to not steal
            if (c.CurrentAddiction > 0.8f && chance > 0.20f) return false;

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
            coros.Add(MelonCoroutines.Start(LateSendMessage(npc)));

            return;
        }

        public static IEnumerator LateSendMessage(NPC npc)
        {
            if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
            {
                yield return Wait30;
            }
            else
            {
                yield return Wait10;
            }
            if (!registered) yield break;
            npc.MSGConversation.SendMessage(
                new Message(
                    _text: playerMessageTemplates[UnityEngine.Random.Range(0, playerMessageTemplates.Count)],
                    _type: Message.ESenderType.Other,
                    _endOfGroup: true, 
                    _messageId: -1
                ), 
                notify: true, 
                network: true
            );
            yield break;
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
