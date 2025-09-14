using System.Collections;
using MelonLoader;
using UnityEngine;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InterceptEvent;


#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.Combat;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using ScheduleOne.Product;
using ScheduleOne.AvatarFramework.Equipping;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
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

                string resourcePath = "";
                switch (dealerConfig.CartelDealerWeapon.ToLower())
                {
                    case "m1911":
                        resourcePath = "Avatar/Equippables/M1911";
                        break;

                    case "knife":
                        resourcePath = "Avatar/Equippables/Knife";
                        break;

                    case "shotgun":
                        resourcePath = "Avatar/Equippables/PumpShotgun";
                        break;

                    default:
                        resourcePath = "Avatar/Equippables/M1911";
                        break;
                }

                // test to see if this works
                // AvatarEquippable equippable = dealer.SetEquippable_Return(resourcePath);
                GameObject gameObject = Resources.Load(resourcePath) as GameObject; // does the as cast work here in il2cpp??
                AvatarEquippable equippable = UnityEngine.Object.Instantiate<GameObject>(gameObject, null).GetComponent<AvatarEquippable>();
#if MONO
                if (equippable is AvatarWeapon weapon)
                    dealer.Behaviour.CombatBehaviour.DefaultWeapon = weapon;
#else
                AvatarWeapon weapon = equippable.TryCast<AvatarWeapon>();
                if (weapon != null)
                    dealer.Behaviour.CombatBehaviour.DefaultWeapon = weapon;
#endif
                // then we set the current weapon to load its own resource but default one stays... Maybe this way it wont drop the gun on beh end or it can re equip default on beh start
                dealer.Behaviour.CombatBehaviour.SetWeapon(resourcePath);

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
                        else if (TimeManager.Instance.CurrentTime > 359 && TimeManager.Instance.CurrentTime < 1600)
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
                            if (dealer.IsAcceptingDeals)
                                dealer.SetIsAcceptingDeals(false);
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
                        else if (TimeManager.Instance.CurrentTime > 359 && TimeManager.Instance.CurrentTime < 1600)
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
                            if (dealer.IsAcceptingDeals)
                                dealer.SetIsAcceptingDeals(false);
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

                // 1 callback for random roll to spawn nearby goons on dead
                void TrySpawnGoonsOnDeath()
                {
                    if (UnityEngine.Random.Range(0f, 1f) > 0.5f) return;
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.UnspawnedGoonCount < 2) return;

                    Player p = Player.GetClosestPlayer(dealer.CenterPoint, out _);

                    Vector3 randomDirection;
                    Vector3 randomPoint = Vector3.zero;
                    float randomRadius;
                    int maxAttempts = 4;
                    int i = 0;
                    do
                    {
                        if (!registered) return;
                        if (i == maxAttempts) break; // just send it

                        randomDirection = UnityEngine.Random.onUnitSphere;
                        randomDirection.y = 0;
                        randomDirection.Normalize();
                        randomRadius = UnityEngine.Random.Range(16f, 24f);
                        randomPoint = dealer.transform.position + randomDirection * randomRadius;
                        i++;

                    } while (p.IsPointVisibleToPlayer(randomPoint));

                    if (randomPoint == Vector3.zero) return;
#if MONO
                    List<CartelGoon> goons = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnMultipleGoons(randomPoint, 2, true);
#else
                    Il2CppSystem.Collections.Generic.List<CartelGoon> goons = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnMultipleGoons(randomPoint, 2, true);
#endif
                    foreach (CartelGoon goon in goons)
                    {
                        goon.Movement.WarpToNavMesh(); // just incase
                        if (UnityEngine.Random.Range(0f, 1f) > 0.7f && RangedWeapons != null && RangedWeapons.Length != 0)
                            goon.Behaviour.CombatBehaviour.DefaultWeapon = RangedWeapons[UnityEngine.Random.Range(0, RangedWeapons.Length)];
                        else if (MeleeWeapons != null && MeleeWeapons.Length != 0)
                            goon.Behaviour.CombatBehaviour.DefaultWeapon = MeleeWeapons[UnityEngine.Random.Range(0, MeleeWeapons.Length)];
                        goon.AttackEntity(p.GetComponent<ICombatTargetable>(), true);
                    }
                    coros.Add(MelonCoroutines.Start(DespawnDefenderGoonsSoon(goons)));
                    return;
                }
                dealer.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)TrySpawnGoonsOnDeath);

#endregion

            }
            Log("    Done Configuring Cartel Dealer Event values");
        }

#if MONO
        public static IEnumerator DespawnDefenderGoonsSoon(List<CartelGoon> goons)
#else
        public static IEnumerator DespawnDefenderGoonsSoon(Il2CppSystem.Collections.Generic.List<CartelGoon> goons)
#endif
        {
            int maxHoursWaited = 2;
            int goonsDead = 0;
            for (int i = 0; i < maxHoursWaited; i++)
            {
                yield return Wait60;
                foreach (CartelGoon goon in goons)
                {
                    if ((goon.Health.IsDead || goon.Health.IsKnockedOut) && goon.IsGoonSpawned)
                    {
                        goonsDead++;
                        goon.Health.Revive();
                        goon.Despawn();
                        goon.Behaviour.CombatBehaviour.Disable_Networked(null);
                    }
                }
                if (goonsDead == goons.Count) break;
            }
            foreach (CartelGoon goon in goons)
            {
                if (goon.IsGoonSpawned)
                {
                    goon.Despawn();
                    goon.Behaviour.CombatBehaviour.Disable_Networked(null);
                }
            }

            yield return null;
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
            Log("[DEALER ACTIVITY] Parse customers");
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

            Log("[DEALER ACTIVITY] Parse dealers");
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
                            contract.CompletionXP = 0;
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
                                contract.CompletionXP = 0;
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
            if (!currentConfig.enhancedDealers) return;
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

        // 1 callback function to monitor the change of CartelCustomerDeal type event calls this function to insta-generate new locked customer contracts
        // so that the event can end earlier, by default it only enables the isAccepting deals boolean and awards the contracts but this should make it more reliable
        // When the contract is generated from this, it should always result in the event activity disable without errors.

        // Note this is the only function in the mod that can add contracts to dealer even if they have active contracts. This is to make the base functionality "more" preferrable. Any other mod added contracts obey to limit 1 contract at any given state.
        public static IEnumerator OnCartelCustomerDeal(CartelCustomerDeal dealEvent, bool started)
        {
            yield return Wait2;

            if (started)
            {
                List<Customer> regionLockedCustomers = new();
                // Make list traverse safe for modification
                int i = 0;
                do
                {
                    yield return Wait025;
                    if (i >= Customer.LockedCustomers.Count) break;
                    if (Customer.LockedCustomers[i].NPC.Region == dealEvent.Region && !regionLockedCustomers.Contains(Customer.LockedCustomers[i]))
                        regionLockedCustomers.Add(Customer.LockedCustomers[i]);
                    i++;
                } while (i < Customer.LockedCustomers.Count && registered);

                if (regionLockedCustomers.Count == 0)
                {
                    // should deactivate, since the callback will only run when the activity will only time out
                    dealEvent.Deactivate();
                    Log("[LOCKED CUSTOMER DEAL] Customer deal deactivated due to region not having locked customers");
                    yield break;
                }

                Customer c = regionLockedCustomers[UnityEngine.Random.Range(0, regionLockedCustomers.Count)];

                // Simplified the way which customer gets awarded a contract, by default alot of calculations go into determining the deal time, weekday, customer state, MOST of relevant stuff is typed out here but some is left out just for simplicity due to being a guaranteed locked customer.
                bool canAwardContract = true;

                // from ShouldTryGenerateDeal only 2 relevant conditions
                if (c.CurrentContract != null) canAwardContract = false; // basically never true but still
                if (!c.NPC.IsConscious) canAwardContract = false;

                // from IsDealTime only this should be relevant, as the weekdays are kind of cosmetic addition to have player feel like this behaviour is consistent. For cartel dealers it doesnt really matter what weekday is since there is no perception of week time passing or memorizing customers.
                int orderTime = c.customerData.OrderTime;
                int max = TimeManager.AddMinutesTo24HourTime(orderTime, 240); // doubled from default 120 -> 240
                if (!NetworkSingleton<TimeManager>.Instance.IsCurrentTimeWithinRange(orderTime, max))
                    canAwardContract = false;

                if (!canAwardContract) 
                {
                    Log($"[LOCKED CUSTOMER DEAL] Cant award contract at this moment, see below state");   
                    Log($"[LOCKED CUSTOMER DEAL]     Has Contract: {c.CurrentContract != null}");
                    Log($"[LOCKED CUSTOMER DEAL]     IsConscious: {c.NPC.IsConscious}");
                    Log($"[LOCKED CUSTOMER DEAL]     Order Time in Range: {NetworkSingleton<TimeManager>.Instance.IsCurrentTimeWithinRange(orderTime, max)}");
                    dealEvent.Deactivate();
                    yield break;
                }


                // Before running below we want to make sure there is something in the inventory since try generate contract can result in no acceptable items especially in higher standard customers, so based on region, we add change existing quality OR add quality item if empty?
                EQuality requiredQuality = c.customerData.Standards.GetCorrespondingQuality();
                List<ProductDefinition> list = new List<ProductDefinition>();
                bool hasMinItems = false;

#if IL2CPP
                ProductItemInstance temp = null;
                QualityItemInstance qtInst = null;
#endif

                foreach (ItemSlot itemSlot in dealEvent.dealer.GetAllSlots())
                {
                    if (itemSlot.ItemInstance != null)
                    {
#if MONO
                        if (itemSlot.ItemInstance is ProductItemInstance product)
                        {
                            if ((int)product.Quality < (int)requiredQuality)
                            {
                                hasMinItems = true;
                                product.Quality = requiredQuality;
                                break;
                            }
                            else
                            {
                                hasMinItems = true;
                                break;
                            }
                        }
#else
                        temp = itemSlot.ItemInstance.TryCast<ProductItemInstance>();
                        if (temp != null) 
                        {
                            if ((int)temp.Quality < (int)requiredQuality)
                            {
                                hasMinItems = true;
                                temp.Quality = requiredQuality;
                                break;
                            }
                            else
                            {
                                hasMinItems = true;
                                break;
                            }
                        }
                        temp = null;
#endif
                    }
                }
                // Now if hasMinItems is still false it means nothing in inventory is of required quality or item type to sell
                // by default we add into inventory of required type
                if (!hasMinItems)
                {
#if MONO
                    ItemDefinition def = ScheduleOne.Registry.GetItem("cocaine");
#else
                    ItemDefinition def = Il2CppScheduleOne.Registry.GetItem("cocaine");
#endif
                    ItemInstance item = def.GetDefaultInstance(4);
#if MONO
                    if (item is QualityItemInstance inst)
                        inst.Quality = requiredQuality;
#else
                    qtInst = item.TryCast<QualityItemInstance>();
                    if (qtInst != null)
                        qtInst.Quality = requiredQuality;
#endif

                    if (dealEvent.dealer.Inventory.CanItemFit(item))
                    {
                        dealEvent.dealer.Inventory.InsertItem(item);
                    }
                }

                // Now try generate contract has better chance always to award items from inventory
                ContractInfo contractInfo = c.TryGenerateContract(dealEvent.dealer);

                if (contractInfo != null)
                {
                    // Here we skip the offering, instead manually set the same variables as in source, but we skip checking should accept contract to ensure probability of generating the contract succesfully at the CartelCustomerDeal event is as high as possible
                    //c.OfferContractToDealer(contractInfo, dealEvent.dealer);
                    Log("[LOCKED CUSTOMER DEAL] Awarded contract to locked customer");
                    int offeredDeals = c.OfferedDeals;
                    c.OfferedDeals = offeredDeals + 1;
                    c.TimeSinceLastDealOffered = 0;
                    c.OfferedContractInfo = contractInfo;
                    c.OfferedContractTime = NetworkSingleton<TimeManager>.Instance.GetDateTime();
                    c.HasChanged = true;
                    EDealWindow dealWindow = dealEvent.dealer.GetDealWindow();
                    Contract contract = c.ContractAccepted(dealWindow, false, dealEvent.dealer);
                    contract.CompletionXP = 0;
                    contract.completedContractsIncremented = false;
                    dealEvent.dealer.AddContract(contract); // Running this should instantly deactivate CartelCustomerDeal activity since the callbacks _should_ have the Deactivate still added
                }
                else
                {
                    Log("[LOCKED CUSTOMER DEAL] Locked customer did not generate a contract");
                    dealEvent.Deactivate();
                }

            }

            yield return null;
        }

    }

    [Serializable]
    public class CartelDealerConfig
    {
        public float CartelDealerMoveSpeedMultiplier = 1.65f;
        public float CartelDealerHP = 200.0f;
        public string CartelDealerWeapon = "M1911"; // "Knife" "Shotgun", default unknown M1911
        public float StealDealerContractChance = 0.06f;
        public float StealPlayerPendingChance = 0.08f;
        public float DealerActivityDecreasePerKill = 0.10f;
        public float DealerActivityIncreasePerDay = 0.25f;
        public float SafetyThreshold = -0.85f;
        public bool SafetyEnabled = true;
        public bool FreeTimeWalking = true;
    }

}