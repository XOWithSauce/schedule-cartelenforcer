using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InfluenceOverrides;
using static CartelEnforcer.InterceptEvent;


#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using ScheduleOne.UI;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.NPCs.Other;
using ScheduleOne.NPCs;
using ScheduleOne.AvatarFramework.Equipping;
#else
using Il2Cpp;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.NPCs.Other;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.AvatarFramework.Equipping;
#endif

namespace CartelEnforcer
{
    public static class DealerActivity
    {

        private static List<Dealer> allDealers = new();
        private static CartelDealer[] allCartelDealers;
        public static CartelDealerConfig dealerConfig;

        public static float currentDealerActivity = 0f;
        public static float previousDealerActivity = 0f;

        private static float maxActivity = 1f;
        private static float minActivity = -1f;

        // Defaults to scale against with user config
        // Calculate new action start and end times
        private static int defaultStayInsideStart = 200;
        private static int defaultStayInsideEnd = 2000;
        private static int defaultStayInsideDur = 1080;

        // Current 
        public static int currentStayInsideStart = 0;
        public static int currentStayInsideEnd = 0;
        public static int currentStayInsideDur = 0;

        public static IEnumerator EvaluateDealerState()
        {
            yield return Wait5;
            Log("[DEALER ACTIVITY] Init Dealer state");

            DealerActivity.allCartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            Dealer[] allSceneDealers = UnityEngine.Object.FindObjectsOfType<Dealer>(true);

            foreach (Dealer d in allSceneDealers)
            {
                if (d.DealerType == EDealerType.PlayerDealer)
                    allDealers.Add(d);
            }
            allSceneDealers = null;

            currentStayInsideStart = defaultStayInsideStart;
            currentStayInsideEnd = defaultStayInsideEnd;
            currentStayInsideDur = defaultStayInsideDur;

            SetupDealers();

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onDayPass = (Action)Delegate.Combine(instance.onDayPass, new Action(OnDayPassChange));
#else
            instance.onDayPass += (Il2CppSystem.Action)OnDayPassChange;
#endif
            Log("[DEALER ACTIVITY] Starting evaluation");

            while (registered)
            {
                yield return Wait60;
                if (!registered) yield break;
                // from 4pm to 4am only
                if (!(TimeManager.Instance.CurrentTime >= 1620 || TimeManager.Instance.CurrentTime <= 359))
                    continue;
                bool isHostile = true;
#if MONO
                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    isHostile = false;
#else

                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                    isHostile = false;
#endif
                

                // Calculate new action start and end times only if the activity has changed
                if (currentDealerActivity != previousDealerActivity)
                {
                    int newStayInsideStart = 0;
                    int newStayInsideEnd = 0;
                    int newStayInsideDur = 0;

                    if (currentDealerActivity > 0f)
                    {
                        Log("[DEALER ACTIVITY] Decrement Safety Status");

                        // Calculate stay inside Start time when does dealer go back inside, range 00:01 - 03:59
                        int oldStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideStart);
                        int maxStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(0359);

                        int increasedStayInsideStartMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideStartMins, (float)maxStayInsideStartMins, currentDealerActivity));

                        newStayInsideStart = TimeManager.Get24HourTimeFromMinSum(increasedStayInsideStartMins);
                        if (newStayInsideStart == 400) // because math round to int sucks ass
                        {
                            newStayInsideStart = 359;
                        }
                        // Stay inside starting time is now increased towards 04:00

                        // Calculate stay inside end time, when does dealer go out of building, range 16:20 - 23:59
                        int oldStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideEnd);
                        int maxStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(1620);

                        int decreasedStayInsideEndMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideEndMins, (float)maxStayInsideEndMins, currentDealerActivity));

                        newStayInsideEnd = TimeManager.Get24HourTimeFromMinSum(decreasedStayInsideEndMins);
                        // Stay inside ending time is now decreased towards 16:20

                        // New Stay inside event duration in minutes
                        newStayInsideDur = decreasedStayInsideEndMins - increasedStayInsideStartMins;
                    }
                    else if (currentDealerActivity < 0f)
                    {
                        Log("[DEALER ACTIVITY] Increment Safety Status");

                        // flip t ex -0.3 Becomes 0.3
                        float t = -currentDealerActivity;

                        // Calculate stay inside Start time when does dealer go back inside, range 00:01 - 03:59
                        int oldStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideStart);
                        int minStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(0002);

                        int decreasedStayInsideStartMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideStartMins, (float)minStayInsideStartMins, t));

                        newStayInsideStart = TimeManager.Get24HourTimeFromMinSum(decreasedStayInsideStartMins);
                        // Stay inside starting time is now decreased towards 00:01

                        // Calculate stay inside end time, when does dealer go out of building, range 16:20 - 23:59
                        int oldStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideEnd);
                        int minStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(2359);

                        int increasedStayInsideEndMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideEndMins, (float)minStayInsideEndMins, t));

                        newStayInsideEnd = TimeManager.Get24HourTimeFromMinSum(increasedStayInsideEndMins);
                        // Stay inside ending time is now increased towards 23:59

                        // New Stay inside event duration in minutes
                        newStayInsideDur = increasedStayInsideEndMins - decreasedStayInsideStartMins;
                    }
                    else // Activity at 0.0f
                    {
                        Log("[DEALER ACTIVITY] Default Activity Set");
                        newStayInsideStart = defaultStayInsideStart;
                        newStayInsideEnd = defaultStayInsideEnd;
                        newStayInsideDur = defaultStayInsideDur;
                    }

                    Log("[DEALER ACTIVITY] Current Dealer activity: " + currentDealerActivity);
                    Log($"[DEALER ACTIVITY] StayInsideStart changed {currentStayInsideStart} -> {newStayInsideStart}");
                    Log($"[DEALER ACTIVITY] StayInsideEnd changed {currentStayInsideEnd} -> {newStayInsideEnd}");
                    Log($"[DEALER ACTIVITY] StayInsideDur changed {currentStayInsideDur} -> {newStayInsideDur}");

                    currentStayInsideStart = newStayInsideStart;
                    currentStayInsideEnd = newStayInsideEnd;
                    currentStayInsideDur = newStayInsideDur;


                    // Apply
                    foreach (CartelDealer d in DealerActivity.allCartelDealers)
                    {
                        Log("[DEALER ACTIVITY] Apply Event state:");
                        yield return Wait05;
                        if (!registered) yield break;

                        ApplyNewEventState(
                            d,
                            currentStayInsideStart,
                            currentStayInsideEnd,
                            currentStayInsideDur
                        );
                    }
                }

                previousDealerActivity = currentDealerActivity;

                
                // Check safety first, if safety enabled set Stay Inside event to last for entire day if requirement met
                bool safetyThresholdMet = false;
                if (dealerConfig.SafetyEnabled)
                {
                    if (currentDealerActivity <= dealerConfig.SafetyThreshold)
                    {
                        safetyThresholdMet = true;
                        // Current dealer activity indicates that alot of dealers have died
                        // Since safety is enabled we must modify the StayInside event time frame
                        foreach (CartelDealer d in DealerActivity.allCartelDealers)
                        {
                            yield return Wait05;
                            if (!registered) yield break;

                            ApplyNewEventState(d, 0, 2359, 1440);
                            d.SetIsAcceptingDeals(false);
                        }
                    }
                }
                if (safetyThresholdMet) continue;

                if (!isHostile)
                {
                    // Sleep longer and just trigger the walking, state will continue after extra minute or they go back inside
                    foreach (CartelDealer d in allCartelDealers)
                    {
                        yield return Wait2;
                        if (!registered) yield break;

                        if (!d.isInBuilding && !d.Movement.hasDestination && !d.Health.IsDead && !d.Health.IsKnockedOut)
                        {
                            WalkToInterestPoint(d);
                        }
                    }
                    yield return Wait60;
                    if (!registered) yield break;

                    continue;
                }

                // Now that new ones are applied we can check if the signal should be toggled
                if (TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart)
                {
                    // Current time is in deal signal window
                    coros.Add(MelonCoroutines.Start(StartActiveSignal()));
                }
            }

            yield return null;
        }

        public static void ApplyNewEventState(CartelDealer dealer, int inStart, int inEnd, int inDur)
        {
            // we dont wanna update this dealers values yet since they are actively partaking intercept deal event
            if (interceptor != null && interceptor == dealer) return;

            NPCEvent_StayInBuilding event1 = null;
            if (dealer.Behaviour.ScheduleManager.ActionList != null)
            {
                foreach (NPCAction action in dealer.Behaviour.ScheduleManager.ActionList)
                {
#if MONO
                    if (action is NPCEvent_StayInBuilding ev1)
                        event1 = ev1;

#else
                    NPCEvent_StayInBuilding ev1_temp = action.TryCast<NPCEvent_StayInBuilding>();
                    if (ev1_temp != null)
                    {
                        event1 = ev1_temp;
                    }
#endif
                }
            }

            if (event1 != null)
            {
                event1.StartTime = inStart;
                event1.EndTime = inEnd;
                event1.Duration = inDur;
            }
        }

        public static void SetupDealers()
        {
            Log("[DEALER ACTIVITY] Configuring Cartel Dealer Event values");

            foreach (CartelDealer dealer in DealerActivity.allCartelDealers)
            {
                dealer.Movement.MoveSpeedMultiplier = dealerConfig.CartelDealerMoveSpeedMultiplier;
                dealer.Health.MaxHealth = dealerConfig.CartelDealerHP;
                dealer.Health.Health = dealerConfig.CartelDealerHP;

                switch (dealerConfig.CartelDealerWeapon.ToLower())
                {
                    case "m1911":
                        dealer.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
                        break;

                    case "knife":
                        dealer.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");
                        break;

                    case "shotgun":
                        dealer.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/PumpShotgun");
                        break;

                    default:
                        dealer.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");
                        break;
                }

                if (dealer.Behaviour.CombatBehaviour.currentWeapon != null)
                    dealer.Behaviour.CombatBehaviour.DefaultWeapon = dealer.Behaviour.CombatBehaviour.currentWeapon;

                dealer.OverrideAggression(1f); // because the dealers run away like wtf?

                #region Stay Inside and Deal Signal actions
                NPCEvent_StayInBuilding event1 = null;
                NPCSignal_HandleDeal event2 = null;
                if (dealer.Behaviour.ScheduleManager.ActionList != null)
                {
                    foreach (NPCAction action in dealer.Behaviour.ScheduleManager.ActionList)
                    {
#if MONO
                        if (action is NPCEvent_StayInBuilding ev1)
                            event1 = ev1;

                        else if (action is NPCSignal_HandleDeal ev2)
                            event2 = ev2;
#else
                        NPCEvent_StayInBuilding ev1_temp = action.TryCast<NPCEvent_StayInBuilding>();
                        if (ev1_temp != null)
                        {
                            event1 = ev1_temp;
                        }
                        else
                        {
                            NPCSignal_HandleDeal ev2_temp = action.TryCast<NPCSignal_HandleDeal>();
                            if (ev2_temp != null)
                            {
                                event2 = ev2_temp;
                            }
                        }
#endif
                    }

                    void onStayInsideEnd()
                    {
                        if (interceptingDeal && interceptor != null && dealer == interceptor) return;

                        if (TimeManager.Instance.CurrentTime <= 359 || TimeManager.Instance.CurrentTime >= 1600) // because the 400 bugs out
                        {
                            if (!dealer.IsAcceptingDeals)
                                dealer.SetIsAcceptingDeals(true);
                            WalkToInterestPoint(dealer);
                        }
                        else if (TimeManager.Instance.CurrentTime > 359 && TimeManager.Instance.CurrentTime < 402)
                        {
                            if (dealer.ActiveContracts.Count > 0)
                            {
                                if (interceptor != null)
                                {
                                    if (dealer == interceptor) return;
                                }
                                // Anybody else but the interceptor must fail contracts past 3:59 or they start bugging out
                                dealer.ActiveContracts[0].Fail();
                            }
                        }
                    }

                    void onDealSignalEnd()
                    {
                        if (interceptingDeal && interceptor != null && dealer == interceptor) return;

                        if (TimeManager.Instance.CurrentTime <= 359 || TimeManager.Instance.CurrentTime >= 1600) // because the 400 bugs out
                        {
                            if (dealer.ActiveContracts.Count == 0)
                            {
                                if (!dealer.IsAcceptingDeals)
                                    dealer.SetIsAcceptingDeals(true);
                                WalkToInterestPoint(dealer);
                            }
                        }
                        else if (TimeManager.Instance.CurrentTime > 359 && TimeManager.Instance.CurrentTime < 402)
                        {
                            if (dealer.ActiveContracts.Count > 0)
                            {
                                if (interceptor != null)
                                {
                                    if (dealer == interceptor) return;
                                }
                                // Anybody else but the interceptor must fail contracts past 3:59 or they start bugging out
                                dealer.ActiveContracts[0].Fail();
                            }
                        }
                    }

                    if (event1 != null)
                    {
                        event1.StartTime = defaultStayInsideStart;
                        event1.EndTime = defaultStayInsideEnd;
                        event1.Duration = defaultStayInsideDur;
#if MONO
                        event1.onEnded = (Action)Delegate.Combine(event1.onEnded, new Action(onStayInsideEnd));

#else
                        event1.onEnded += (Il2CppSystem.Action)onStayInsideEnd;
#endif
                    }
                    if (event2 != null)
                    {
                        event2.MaxDuration = 60;
                        // event2.StartTime = defaultDealSignalStart; This is not needed
#if MONO
                        event2.onEnded = (Action)Delegate.Combine(event2.onEnded, new Action(onDealSignalEnd));
#else
                        event2.onEnded += (Il2CppSystem.Action)onDealSignalEnd;
#endif
                    }
                }
                #endregion

                #region Health based callbacks
                dealer.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)OnDealerDied);
                #endregion

            }
            Log("    Done Configuring Cartel Dealer Event values");
        }

        public static IEnumerator StartActiveSignal()
        {
            // Store valid contracts from dealers
            List<Contract> validContracts = new();
            Contract contract = null;
            foreach (Dealer playerDealer in allDealers)
            {
                yield return Wait01;
                if (!registered) yield break;

                if (playerDealer.ActiveContracts.Count == 0)
                {
                    continue;
                }
                // We have ActiveContracts List atleast one element, get one
                contract = playerDealer.ActiveContracts[UnityEngine.Random.Range(0, playerDealer.ActiveContracts.Count)];
                if (!contractGuids.Contains(contract.GUID.ToString()) && !validContracts.Contains(contract))
                    validContracts.Add(contract);
            }
            contract = null;
            
            // Store valid contracts from customers
            List<Customer> cList = new();
            for (int i = 0; i < Customer.UnlockedCustomers.Count; i++)
            {
                //Log("Add Customer");
                yield return Wait01;
                if (!registered) yield break;

                cList.Add(Customer.UnlockedCustomers[i]);
            }

            List<Customer> validCustomers = new();
            //Log("Parse customers");
            do
            {
                yield return Wait01;
                if (!registered) yield break;

                Customer c = cList[UnityEngine.Random.Range(0, cList.Count)];
                if (c.CurrentContract == null && c.AssignedDealer == null && c.offeredContractInfo != null)
                {
                    validCustomers.Add(c);
                }
                cList.Remove(c);
            } while (cList.Count > 0);


            foreach (CartelDealer d in DealerActivity.allCartelDealers)
            {
                yield return Wait2; // Short sleep to allow signals to assign contract per dealer
                if (!registered) yield break;

                if (!(TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart))
                    break;
                if (interceptor != null && interceptor == d) continue;
                if (d.Health.IsDead || d.Health.IsKnockedOut) continue;
                if (d.ActiveContracts.Count == 0)
                {
                    bool actionTaken = false;
                    

                    // Pick player dealers active contract
                    if (validContracts.Count > 0 && UnityEngine.Random.Range(0.01f, 1f) < dealerConfig.StealDealerContractChance)
                    {
                        Log("[DEALER ACTIVITY] Checking PlayerDealer active deal");

                        contract = validContracts[UnityEngine.Random.Range(0, validContracts.Count)];
                        if (contract != null)
                        {
                            // This one is hard because this contract now awards xp to player even when cartel completes it
                            // We dont really want to fail the original contract either to have the player dealer compete against cartel dealer
                            actionTaken = true;
                            contract.CompletionXP = 1;
                            contract.completedContractsIncremented = false;
                            d.AddContract(contract);
                            d.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                        }
                        validContracts.Remove(contract);

                    }
                    // Pick customer with null dealer, no active deal and a pending offer
                    else if (validCustomers.Count > 0 && UnityEngine.Random.Range(0.01f, 1f) < dealerConfig.StealPlayerPendingChance)
                    {
                        Customer c = validCustomers[UnityEngine.Random.Range(0, validCustomers.Count)];
                        if (c.CurrentContract == null && c.AssignedDealer == null && c.offeredContractInfo != null) // because of delay we have to verify again a bit redundant but needed
                        {
                            ContractInfo contractInfo = new();
                            contractInfo.DeliveryLocation = c.offeredContractInfo.DeliveryLocation;
                            contractInfo.DeliveryLocationGUID = c.offeredContractInfo.DeliveryLocationGUID;
                            contractInfo.Payment = c.offeredContractInfo.Payment;
                            contractInfo.Expires = c.offeredContractInfo.Expires;
                            contractInfo.DeliveryWindow = c.offeredContractInfo.DeliveryWindow;
                            contractInfo.PickupScheduleIndex = c.offeredContractInfo.PickupScheduleIndex;
                            contractInfo.ExpiresAfter = c.offeredContractInfo.ExpiresAfter;
                            contractInfo.IsCounterOffer = c.offeredContractInfo.IsCounterOffer;
                            contractInfo.Products = c.offeredContractInfo.Products;

                            c.ExpireOffer();
                            yield return Wait01;
                            if (!registered) yield break;

                            c.offeredContractInfo = contractInfo;

                            Log("[DEALER ACTIVITY]   Taking pending offer to dealer");
                            EDealWindow window = d.GetDealWindow();
                            contract = c.ContractAccepted(window, false, d);
                            if (contract != null)
                            {
                                contract.CompletionXP = 1;
                                contract.completedContractsIncremented = false;
                                d.AddContract(contract);
                                d.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                                actionTaken = true;
                            }
                        }
                        validCustomers.Remove(c);
                    }

                    // For when no valid contracts were found for this cartel dealer
                    if (!actionTaken)
                    {
                        Log("[DEALER ACTIVITY] No action taken");
                        // Has no contract but is in time window, and random roll didnt award the contract
                        if (!d.isInBuilding && !d.Movement.hasDestination)
                        {
                            // and is outside meaning just afk standing
                            WalkToInterestPoint(d);
                        }
                    }
                }
                else
                {
                    // hAs contract
                    if (!d.Behaviour.ScheduleManager.ActionList[0].gameObject.activeSelf)
                        d.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                    continue;
                }

            }

            cList.Clear();
            validCustomers.Clear();
            validContracts.Clear();

            yield return null;
        }

        public static void WalkToInterestPoint(CartelDealer d)
        {
            if (!dealerConfig.FreeTimeWalking) return;
            if (interceptingDeal && interceptor != null && interceptor == d) return;
            if (d.ActiveContracts.Count > 0) return;
            if (d.Health.IsDead || d.Health.IsKnockedOut) return;
            if (!(TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart)) return;
            MapRegionData mapRegionData = Singleton<Map>.instance.Regions[(int)d.Region];
            DeliveryLocation walkDest = mapRegionData.GetRandomUnscheduledDeliveryLocation();
            d.Movement.SetDestination(walkDest.CustomerStandPoint.position);
            if (!d.IsAcceptingDeals)
                d.SetIsAcceptingDeals(true);
        }

        // 2 callbacks for changing the activity state
        public static void OnDealerDied()
        {
            currentDealerActivity = Mathf.Clamp(currentDealerActivity - dealerConfig.DealerActivityDecreasePerKill, minActivity, maxActivity);
        }

        public static void OnDayPassChange()
        {
            currentDealerActivity = Mathf.Clamp(currentDealerActivity + dealerConfig.DealerActivityIncreasePerDay, minActivity, maxActivity);
        }

    }

    [Serializable]
    public class CartelDealerConfig
    {
        public float CartelDealerMoveSpeedMultiplier = 1.65f;
        public float CartelDealerHP = 200.0f;
        public string CartelDealerWeapon = "M1911"; // "Knife" "Shotgun", default unknown M1911
        public float StealDealerContractChance = 0.03f;
        public float StealPlayerPendingChance = 0.03f;
        public float DealerActivityDecreasePerKill = 0.25f;
        public float DealerActivityIncreasePerDay = 0.15f;
        public float SafetyThreshold = -0.7f;
        public bool SafetyEnabled = true;
        public bool FreeTimeWalking = true;
    }

}