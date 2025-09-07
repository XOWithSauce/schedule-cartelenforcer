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

        private static int defaultDealSignalStart = 2000;
        private static int defaultDealSignalDur = 360;


        // Current 
        public static int currentStayInsideStart = 0;
        public static int currentStayInsideEnd = 0;
        public static int currentStayInsideDur = 0;

        public static int currentDealSignalStart = 0;
        public static int currentDealSignalDur = 0;

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

            currentDealSignalStart = defaultDealSignalStart;
            currentDealSignalDur = defaultDealSignalDur;

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
                // from 4pm to 4am only
                if (!(TimeManager.Instance.CurrentTime >= 1620 || TimeManager.Instance.CurrentTime <= 359))
                    continue;

#if MONO
                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    continue;
#else

                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                    continue;
#endif
                // Calculate current

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
                            ApplyNewEventState(d, 0, 2359, 1440, 0, 1);
                        }
                    }
                }
                if (safetyThresholdMet) continue;

                // Calculate new action start and end times only if the activity has changed
                if (currentDealerActivity != previousDealerActivity)
                {
                    int newStayInsideStart = 0;
                    int newStayInsideEnd = 0;
                    int newStayInsideDur = 0;
                    int newDealSignalStart = 0;
                    int newDealSignalDur = 0;

                    if (currentDealerActivity > 0f)
                    {
                        // Calculate stay inside Start time when does dealer go back inside, range 00:01 - 03:59
                        int oldStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideStart);
                        int maxStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(359);

                        int increasedStayInsideStartMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideStartMins, (float)maxStayInsideStartMins, currentDealerActivity));

                        newStayInsideStart = TimeManager.Get24HourTimeFromMinSum(increasedStayInsideStartMins);
                        // Stay inside starting time is now increased towards 04:00

                        // Calculate stay inside end time, when does dealer go out of building, range 16:20 - 23:59
                        int oldStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideEnd);
                        int maxStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(1620);

                        int decreasedStayInsideEndMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideEndMins, (float)maxStayInsideEndMins, currentDealerActivity));

                        newStayInsideEnd = TimeManager.Get24HourTimeFromMinSum(decreasedStayInsideEndMins);
                        // Stay inside ending time is now decreased towards 16:20

                        // New Stay inside event duration in minutes
                        newStayInsideDur = decreasedStayInsideEndMins - increasedStayInsideStartMins;

                        // Then the deal handle signal, start time is always the same as stay inside end time
                        newDealSignalStart = newStayInsideEnd;
                        // Then duration,
                        int dayMins = TimeManager.GetMinSumFrom24HourTime(2359);
                        newDealSignalDur = dayMins - newStayInsideDur;
                    }
                    else if (currentDealerActivity < 0f)
                    {
                        // flip t
                        float t = -currentDealerActivity;

                        // Calculate stay inside Start time when does dealer go back inside, range 00:01 - 03:59
                        int oldStayInsideStartMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideStart);
                        int minStayInsideStartMins = 1; // 00:01

                        int decreasedStayInsideStartMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideStartMins, (float)minStayInsideStartMins, currentDealerActivity));

                        newStayInsideStart = TimeManager.Get24HourTimeFromMinSum(decreasedStayInsideStartMins);
                        // Stay inside starting time is now decreased towards 00:01

                        // Calculate stay inside end time, when does dealer go out of building, range 16:20 - 23:59
                        int oldStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(currentStayInsideEnd);
                        int minStayInsideEndMins = TimeManager.GetMinSumFrom24HourTime(2359);

                        int increasedStayInsideEndMins = Mathf.RoundToInt(Mathf.Lerp((float)oldStayInsideEndMins, (float)minStayInsideEndMins, currentDealerActivity));

                        newStayInsideEnd = TimeManager.Get24HourTimeFromMinSum(increasedStayInsideEndMins);
                        // Stay inside ending time is now increased towards 23:59

                        // New Stay inside event duration in minutes
                        newStayInsideDur = increasedStayInsideEndMins - decreasedStayInsideStartMins;

                        // Then the deal handle signal, start time is always the same as stay inside end time
                        newDealSignalStart = newStayInsideEnd;
                        // Then duration,
                        int dayMins = TimeManager.GetMinSumFrom24HourTime(2359);
                        newDealSignalDur = dayMins - newStayInsideDur;
                    }
                    else // Activity at 0.0f
                    {
                        newStayInsideStart = defaultStayInsideStart;
                        newStayInsideEnd = defaultStayInsideEnd;
                        newStayInsideDur = defaultStayInsideDur;

                        newDealSignalStart = defaultDealSignalStart;
                        newDealSignalDur = defaultDealSignalDur;
                    }

                    Log("[DEALER ACTIVITY] Current Dealer activity: " + currentDealerActivity);
                    Log($"[DEALER ACTIVITY] StayInsideStart changed {currentStayInsideStart} -> {newStayInsideStart}");
                    Log($"[DEALER ACTIVITY] StayInsideEnd changed {currentStayInsideEnd} -> {newStayInsideEnd}");
                    Log($"[DEALER ACTIVITY] StayInsideDur changed {currentStayInsideDur} -> {newStayInsideDur}");
                    Log($"[DEALER ACTIVITY] DealSignalStart changed {currentDealSignalStart} -> {newDealSignalStart}");
                    Log($"[DEALER ACTIVITY] DealSignalDur changed {currentDealSignalDur} -> {newDealSignalDur}");

                    currentStayInsideStart = newStayInsideStart;
                    currentStayInsideEnd = newStayInsideEnd;
                    currentStayInsideDur = newStayInsideDur;
                    currentDealSignalStart = newDealSignalStart;
                    currentDealSignalDur = newDealSignalDur;


                    // Apply
                    foreach (CartelDealer d in DealerActivity.allCartelDealers)
                    {
                        Log("[DEALER ACTIVITY] Apply Event state:");
                        yield return Wait05;
                        ApplyNewEventState(
                            d,
                            currentStayInsideStart,
                            currentStayInsideEnd,
                            currentStayInsideDur,
                            currentDealSignalStart,
                            currentDealSignalDur
                        );
                    }
                }

                previousDealerActivity = currentDealerActivity;

                // Now that new ones are applied we can check if the signal should be toggled
                if (TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart)
                {
                    // Current time is in deal signal window
                    coros.Add(MelonCoroutines.Start(StartActiveSignal()));
                }
            }

            yield return null;
        }

        public static void ApplyNewEventState(CartelDealer dealer, int inStart, int inEnd, int inDur, int sigStart, int sigDur)
        {
            // we dont wanna update this dealers values yet since they are actively partaking intercept deal event
            if (interceptor != null && interceptor == dealer) return;

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
            }

            if (event1 != null)
            {
                event1.StartTime = inStart;
                event1.EndTime = inEnd;
                event1.Duration = inDur;
            }
            if (event2 != null)
            {
                event2.MaxDuration = sigDur;
                event2.StartTime = sigStart;
            }
        }

        public static void SetupDealers()
        {
            Log("[DEALER ACTIVITY] Configuring Cartel Dealer Event values");

            foreach (CartelDealer dealer in DealerActivity.allCartelDealers)
            {
                dealer.Movement.MoveSpeedMultiplier = dealerConfig.CartelDealerMoveSpeedMultiplier;
                dealer.Health.MaxHealth = dealerConfig.CartelDealerHP;

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
                        dealer.SetIsAcceptingDeals(true);
                        if (!dealer.IsAcceptingDeals)
                            dealer.SetIsAcceptingDeals(true);
                        WalkToInterestPoint(dealer);
                    }

                    void onHandOverComplete()
                    {
                        if (dealer.ActiveContracts.Count == 0)
                        {
                            WalkToInterestPoint(dealer);
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
                        event2.MaxDuration = defaultDealSignalDur;
                        event2.StartTime = defaultDealSignalStart;
#if MONO
                        event2.onEnded = (Action)Delegate.Combine(event2.onEnded, new Action(onHandOverComplete));
#else
                        event2.onEnded += (Il2CppSystem.Action)onHandOverComplete;
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
            List<string> guidAssignedContracts = new();

            // Store valid contracts
            List<Contract> validContracts = new();
            Contract contract = null;
            foreach (Dealer playerDealer in allDealers)
            {
                yield return Wait05;
                if (playerDealer.ActiveContracts.Count == 0)
                {
                    continue;
                }
                // We have ActiveContracts List atleast one element, get one
                contract = playerDealer.ActiveContracts[UnityEngine.Random.Range(0, playerDealer.ActiveContracts.Count)];
                validContracts.Add(contract);
            }
            contract = null;

            foreach (CartelDealer d in DealerActivity.allCartelDealers)
            {
                yield return Wait5; // Short sleep to allow signals to assign contract per dealer
                if (interceptor != null && interceptor == d) continue;
                if (d.Health.IsDead || d.Health.IsKnockedOut) continue;
                if (d.ActiveContracts.Count == 0)
                {
                    bool actionTaken = false;

                    // Pick player dealers active contract
                    if (validContracts.Count > 0 && UnityEngine.Random.Range(0f, 1f) < dealerConfig.StealDealerContractChance)
                    {
                        Log("[DEALER ACTIVITY] Checking PlayerDealer active deal");

                        contract = validContracts[UnityEngine.Random.Range(0, validContracts.Count)];

                        if (contract != null)
                        {
                            // This one is hard because this contract now awards xp to player even when cartel completes it
                            // We dont really want to fail the original contract either to have the player dealer compete against cartel dealer
                            actionTaken = true;
                            guidAssignedContracts.Add(contract.GUID.ToString());
                            contract.CompletionXP = 1;
                            d.AddContract(contract);
                            d.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                            validContracts.Remove(contract);
                        }
                    }
                    // Pick customer with null dealer, no active deal and a pending offer
                    else if (UnityEngine.Random.Range(0f, 1f) < dealerConfig.StealPlayerPendingChance)
                    {
                        Log("Search for pending");
                        List<Customer> cList = new();
                        for (int i = 0; i < Customer.UnlockedCustomers.Count; i++)
                        {
                            //Log("Add Customer");
                            yield return Wait01;
                            cList.Add(Customer.UnlockedCustomers[i]);
                        }
                        yield return Wait05;
                        //Log("Parse customers");
                        do
                        {
                            yield return Wait01;
                            Customer c = cList[UnityEngine.Random.Range(0, cList.Count)];
                            if (c.CurrentContract == null && c.AssignedDealer == null && c.offeredContractInfo != null)
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
                                c.offeredContractInfo = contractInfo;

                                Log("[DEALER ACTIVITY]   Taking pending offer to dealer");
                                EDealWindow window = d.GetDealWindow();
                                contract = c.ContractAccepted(window, false, d);
                                if (contract != null)
                                {
                                    contract.CompletionXP = 1;
                                    d.AddContract(contract);
                                    d.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                                    actionTaken = true;
                                }
                                break; // out of do While
                            }
                            else
                                cList.Remove(c);
                        } while (cList.Count > 0);
                    }

                    // For when no valid contracts were found for this cartel dealer
                    if (!actionTaken)
                    {
                        Log("[DEALER ACTIVITY] No action taken");
                        // Has no contract but is in time window, and random roll didnt award the contract
                        if (!d.isInBuilding && !d.Movement.hasDestination)
                        {
                            Log("[DEALER ACTIVITY] Not in building no destination");
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

            yield return null;
        }

        public static void WalkToInterestPoint(CartelDealer d)
        {
            if (!dealerConfig.FreeTimeWalking) return;

            Log("[DEALER ACTIVITY] Cartel dealer walking to Interest point");
            MapRegionData mapRegionData = Singleton<Map>.instance.Regions[UnityEngine.Random.Range(0, Singleton<Map>.instance.Regions.Length)];
            Log("[DEALER ACTIVITY] Got region data");
            DeliveryLocation walkDest = mapRegionData.GetRandomUnscheduledDeliveryLocation();
            Log("[DEALER ACTIVITY] Got Walk Destination");
            d.Movement.SetDestination(walkDest.CustomerStandPoint.position);
            Log("[DEALER ACTIVITY] Set Destination");
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
        public float CartelDealerHP = 200;
        public string CartelDealerWeapon = "M1911"; // "Knife" "Shotgun", default unknown M1911
        public float StealDealerContractChance = 0.2f;
        public float StealPlayerPendingChance = 0.2f;
        public float DealerActivityDecreasePerKill = 0.1f;
        public float DealerActivityIncreasePerDay = 0.1f;
        public float SafetyThreshold = -0.5f;
        public bool SafetyEnabled = true;
        public bool FreeTimeWalking = true;
    }

}