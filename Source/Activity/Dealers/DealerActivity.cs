using System.Collections;
using MelonLoader;
using UnityEngine;
using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.AmbushOverrides;
using static CartelEnforcer.CartelInventory;

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
using ScheduleOne.UI.Handover;
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
using Il2CppScheduleOne.UI.Handover;
#endif

namespace CartelEnforcer
{
    public static class DealerActivity
    {

        private static List<Dealer> allDealers = new();
        public static CartelDealer[] allCartelDealers;
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

        // Track the Player Hired Dealer contracts that got duplicated for Cartel Dealer
        // Key: Contract GUID
        // Value: Tuple: Original Player Dealer, Original Contract XP
        public static Dictionary<string, Tuple<Dealer, int>> playerDealerStolen = new();
        public static readonly object playerDealerStolenLock = new object();
        public static List<string> consumedGUIDs = new();

        // track items in cartel dealer inventory that were awarded from stolen items inv
        public static Dictionary<CartelDealer, List<ItemInstance>> stolenInDealerInv = new();

        public static IEnumerator EvaluateDealerState()
        {
            yield return Wait5;
            Log("[DEALER ACTIVITY] Init Dealer state");

            if (DealerActivity.allCartelDealers == null)
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

                            ApplyNewEventState(d, 600, 559, 1439);
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

                        if (!d.isInBuilding && !d.Movement.HasDestination && !d.Health.IsDead && !d.Health.IsKnockedOut)
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
            Log("[DEALER ACTIVITY] Configuring Cartel Dealers");
            // Note: Brad has that broken bottle weapon whats the resource path, it could be also listed for ambush weapons and these supported?? 

            // how to unify these logics currently ambush weps load based on resource path, whereas these are properly validated...
            // Pros for this:
            // - Validates the resource path and available options, provides default fallback
            // Cons for this:
            // - In future if more weapons are added, this needs to be manually changed

            // The ambush logic pros:
            // - If new weapons are added, the user can simply just tap into the resource paths and add new ones without needing to update mod?
            //Cons:
            // - The resource path is not validated by default, if some logic allows that then it would be better... Also forces user to go through game and kind of mod the game themselves...

            // track the items that get stolen and put in dealer inventory, init lists with new() to not have them null
            foreach (CartelDealer dealer in DealerActivity.allCartelDealers)
                if (!stolenInDealerInv.ContainsKey(dealer))
                    stolenInDealerInv.Add(dealer, new());

            Log("[DEALER ACTIVITY]     Setup inventory tracking stolen items");
            string resourcePath = "";
            switch (dealerConfig.CartelDealerWeapon.ToLower())
            {
                case "m1911":
                    resourcePath = "Avatar/Equippables/M1911";
                    break;

                case "revolver":
                    resourcePath = "Avatar/Equippables/Revolver";
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

#if MONO
            GameObject gameObject = Resources.Load(resourcePath) as GameObject;
#else
            UnityEngine.Object obj = Resources.Load(resourcePath);
            GameObject gameObject = obj.TryCast<GameObject>();
#endif
            if (gameObject == null)
            {
                Log($"[DEALER ACTIVITY]     Dealer instantiated weapon is null");
            }

            AvatarEquippable equippable = UnityEngine.Object.Instantiate<GameObject>(gameObject, new Vector3(0f, -5f, 0f), Quaternion.identity, null).GetComponent<AvatarEquippable>();
            // configure weapon stats, at CartelDealerLethality 1 stats are essentially doubled/halved to make it better, at 0 default
            if (equippable is AvatarRangedWeapon rangedWep && dealerConfig.CartelDealerLethality > 0f)
            {
                rangedWep.Damage = Mathf.Lerp(rangedWep.Damage, rangedWep.Damage * 2f, dealerConfig.CartelDealerLethality);
                rangedWep.AimTime_Max = Mathf.Lerp(rangedWep.AimTime_Max, rangedWep.AimTime_Max * 0.5f, dealerConfig.CartelDealerLethality);
                rangedWep.AimTime_Min = Mathf.Lerp(rangedWep.AimTime_Min, rangedWep.AimTime_Min * 0.5f, dealerConfig.CartelDealerLethality);
                rangedWep.HitChance_MaxRange = Mathf.Lerp(rangedWep.HitChance_MaxRange, rangedWep.HitChance_MaxRange * 2f, dealerConfig.CartelDealerLethality);
                rangedWep.HitChance_MinRange = Mathf.Lerp(rangedWep.HitChance_MinRange, rangedWep.HitChance_MinRange * 2f, dealerConfig.CartelDealerLethality);
                rangedWep.MaxUseRange = Mathf.Lerp(rangedWep.MaxUseRange, rangedWep.MaxUseRange * 2f, dealerConfig.CartelDealerLethality);
                rangedWep.MinUseRange = Mathf.Lerp(rangedWep.MinUseRange, rangedWep.MinUseRange * 0.5f, dealerConfig.CartelDealerLethality);
                rangedWep.MaxFireRate = Mathf.Lerp(rangedWep.MaxFireRate, rangedWep.MaxFireRate * 0.5f, dealerConfig.CartelDealerLethality);
                rangedWep.ReloadTime = Mathf.Lerp(rangedWep.ReloadTime, rangedWep.ReloadTime * 0.5f, dealerConfig.CartelDealerLethality);
            }
            else if (equippable is AvatarMeleeWeapon meleeWep && dealerConfig.CartelDealerLethality > 0f)
            {
                meleeWep.Damage = Mathf.Lerp(meleeWep.Damage, meleeWep.Damage * 2f, dealerConfig.CartelDealerLethality);
                meleeWep.CooldownDuration = Mathf.Lerp(meleeWep.CooldownDuration, meleeWep.CooldownDuration * 0.5f, dealerConfig.CartelDealerLethality);
                meleeWep.AttackRange = Mathf.Lerp(meleeWep.AttackRange, meleeWep.AttackRange * 2f, dealerConfig.CartelDealerLethality);
                meleeWep.AttackRadius = Mathf.Lerp(meleeWep.AttackRadius, meleeWep.AttackRadius * 2f, dealerConfig.CartelDealerLethality);
                meleeWep.MaxUseRange = Mathf.Lerp(meleeWep.MaxUseRange, meleeWep.MaxUseRange * 2f, dealerConfig.CartelDealerLethality);
            }

            foreach (CartelDealer dealer in DealerActivity.allCartelDealers)
            {
                dealer.Movement.WalkSpeed = dealerConfig.CartelDealerWalkSpeed;
                dealer.Health.MaxHealth = dealerConfig.CartelDealerHP;
                dealer.Health.Health = dealerConfig.CartelDealerHP;
#if MONO
                if (equippable is AvatarWeapon weapon)
                {
                    dealer.Behaviour.CombatBehaviour.DefaultWeapon = weapon;
                    if (dealer.Avatar != null)
                        equippable.Equip(dealer.Avatar);
                    else
                        Log($"[DEALER ACTIVITY]     {dealer.Region} dealer is missing avatar field");
                }
#else
                AvatarWeapon weapon = equippable.TryCast<AvatarWeapon>();
                if (weapon != null) 
                {
                    dealer.Behaviour.CombatBehaviour.DefaultWeapon = weapon;
                    if (dealer.Avatar != null)
                        equippable.Equip(dealer.Avatar);
                    else
                        Log($"[DEALER ACTIVITY]     {dealer.Region} dealer is missing avatar field");
                }
#endif

                Log($"[DEALER ACTIVITY]     Setup {dealer.Region} weapon");

                dealer.OverrideAggression(1f); // because the dealers run away like wtf? this might have been just fleeing beh annd this wont fix it? todo fix it :D

                #region Stay Inside and Deal Signal actions
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

                    void onStayInsideEnd()
                    {
                        Log("[DEALER ACTIVITY] OnStayInsideEnd");
                        if (interceptingDeal && interceptor != null && dealer == interceptor) return;

                        if (dealer.ActiveContracts.Count == 0)
                        {
                            if (!dealer.IsAcceptingDeals)
                                dealer.SetIsAcceptingDeals(true);
                            coros.Add(MelonCoroutines.Start(WalkToInterestPoint(dealer, Wait5)));
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
                    else
                    {
                        Log("[DEALER ACTIVITY]    Dealer Action list is null!");
                    }
                }
                else
                {
                    Log("[DEALER ACTIVITY]    Failed to find dealer action list!");
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
                        randomDirection.y = 0f;
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
                Log($"[DEALER ACTIVITY]     Setup {dealer.Region} health callback");
                #endregion


            }
            Log("[DEALER ACTIVITY]    Done Configuring Cartel Dealer Event values");

            #region Ensure that the dealers have avatar settings loaded
            List<CartelDealer> missingAvatar = new();
            Log("[DEALER ACTIVITY] Ensure Cartel Dealers have avatar textures");
            foreach (CartelDealer dealer in DealerActivity.allCartelDealers) 
            {
                if (dealer.Avatar.CurrentSettings == null)
                {
                    missingAvatar.Add(dealer);
                }
            }
            if (missingAvatar.Count > 0)
            {
                Log($"[DEALER ACTIVITY]  Found {missingAvatar.Count} missing Dealer avatar settings");

                foreach (CartelDealer dealer in missingAvatar)
                {
                    dealer.RandomizeAppearance();
                }

            }
            missingAvatar.Clear();
            missingAvatar = null;
            Log("[DEALER ACTIVITY]  Done checking Cartel Dealer avatar textures");

            #endregion

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
            Dictionary<EMapRegion, Contract> validContracts = new();
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
                if (!contractGuids.Contains(contract.GUID.ToString()) && // Not used by Intercept Event
                    !validContracts.ContainsValue(contract) && // Not already in the list
                    !consumedGUIDs.Contains(contract.GUID.ToString()) && // Not already consumed
                    !playerDealerStolen.ContainsKey(contract.GUID.ToString())) // Not already assigned to be stolen by other cartel dealers
                {
                    EMapRegion reg = Map.Instance.GetRegionFromPosition(contract.DeliveryLocation.CustomerStandPoint.position);
                    if (!validContracts.ContainsKey(reg))
                        validContracts.Add(reg, contract);
                }
            }
            
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
                contract = null;

                yield return Wait2; // Short sleep to allow signals to assign contract per dealer
                if (!registered) yield break;

                if (!(TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart))
                    break;
                if (interceptor != null && interceptor == d) continue;
                if (d.Health.IsDead || d.Health.IsKnockedOut) continue;
                if (d.ActiveContracts == null) continue;
                Log("[DEALER ACTIVITY]    Conditions met");
                if (d.ActiveContracts.Count == 0)
                {
                    Log("[DEALER ACTIVITY]    Cartel dealer has 0 active contracts.");
                    bool actionTaken = false;

                    // Pick player dealers active contract, treat the config value as likelihood and if its 0.0 then disabled
                    if (validContracts.Count > 0 && UnityEngine.Random.Range(0.01f, 1f) < dealerConfig.StealDealerContractChance && dealerConfig.StealDealerContractChance != 0.0f)
                    {
                        Log($"[DEALER ACTIVITY] Checking PlayerDealer active deal");
                        if (validContracts.ContainsKey(d.Region))
                        {
                            contract = validContracts[d.Region];
                        }

                        if (contract != null && !playerDealerStolen.ContainsKey(contract.GUID.ToString()) && !consumedGUIDs.Contains(contract.GUID.ToString()))
                        {
                            actionTaken = true;
                            int originalXP = contract.CompletionXP;
                            contract.CompletionXP = 0;
                            contract.completedContractsIncremented = false;

                            Dealer originalDealer = contract.Dealer;
                            lock(playerDealerStolenLock)
                            {
                                playerDealerStolen.Add(contract.GUID.ToString(), new Tuple<Dealer, int>(originalDealer, originalXP));
                            }
                            d.AddContract(contract);
                            if (!d._attendDealBehaviour.Active)
                                d.CheckAttendStart();
                            Log($"[DEALER ACTIVITY]     Stolen Contract");
                        }
                        else
                        {
                            Log("[DEALER ACTIVITY]     No valid deal found, Deal GUID already tracked or completed, skipping.");
                        }
                    }
                    // Previous condition didnt pass another probability
                    // Pick customer with null dealer, no active deal and a pending offer
                    // Disabled by dealerConfig having it as 0.0 again
                    else if (validCustomers.Count > 0 && UnityEngine.Random.Range(0.01f, 1f) < dealerConfig.StealPlayerPendingChance && dealerConfig.StealPlayerPendingChance != 0.0f)
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
                                if (!d._attendDealBehaviour.Active)
                                    d.CheckAttendStart();
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
                        if (!d.isInBuilding && !d.Movement.HasDestination)
                        {
                            // and is outside meaning just afk standing
                            WalkToInterestPoint(d);
                        }
                    }
                }
                else
                {
                    // hAs contract
                    if (!d._attendDealBehaviour.Active)
                        d.CheckAttendStart();
                    Log("[DEALER ACTIVITY]    Dealer has pre-existing contract, skip");
                    continue;
                }

            }

            cList.Clear();
            validCustomers.Clear();
            validContracts.Clear();

            yield return null;
        }

        public static bool IsWalkingEnabled(CartelDealer d)
        {
            if (!currentConfig.enhancedDealers) return false;
            if (!dealerConfig.FreeTimeWalking) return false;
            if (interceptingDeal && interceptor != null && interceptor == d) return false;
            if (d.ActiveContracts.Count > 0) return false;
            if (d.Health.IsDead || d.Health.IsKnockedOut) return false;
            if (!(TimeManager.Instance.CurrentTime >= currentStayInsideEnd || TimeManager.Instance.CurrentTime <= currentStayInsideStart)) return false;
            if (d.Behaviour.GetBehaviour("Smoke Break") != null && d.Behaviour.GetBehaviour("Smoke Break").Active) return false;

            return true;
        }

        public static void WalkToInterestPoint(CartelDealer d)
        {
            if (!IsWalkingEnabled(d)) return;

            float chance = UnityEngine.Random.Range(0f, 1f);
            // Pick Random customer delivery location OR Dead Drop location
            if (chance > 0.5f) // delivery
            {
                MapRegionData mapRegionData = Singleton<Map>.instance.Regions[(int)d.Region];
                DeliveryLocation walkDest = mapRegionData.GetRandomUnscheduledDeliveryLocation();
                d.Movement.SetDestination(walkDest.CustomerStandPoint.position);
            }
            else // deaddrop
            {
#if MONO
                List<DeadDrop> regionDrops = DeadDrop.DeadDrops.FindAll((DeadDrop drop) => drop.Region == d.Region);
#else
                List<DeadDrop> regionDrops = new();
                for (int i = 0; i < DeadDrop.DeadDrops.Count; i++)
                {
                    if (DeadDrop.DeadDrops[i].Region == d.Region)
                        regionDrops.Add(DeadDrop.DeadDrops[i]);
                }
#endif
                DeadDrop drop = regionDrops[UnityEngine.Random.Range(0, regionDrops.Count)];
                Vector3 dropPos = drop.transform.position;
                Vector3 standPos = drop.transform.position + drop.transform.forward * 1.6f + Vector3.down * 1.4f; // Infront and on ground level?

                d.Movement.GetClosestReachablePoint(standPos, out Vector3 closest);
                if (closest != Vector3.zero)
                {
                    d.Movement.SetDestination(closest);
                }
                else
                {
                    Log("[DEALER ACTIVITY] Dealer could not traverse to Dead Drop");
                }
            }

            if (!d.IsAcceptingDeals)
                d.SetIsAcceptingDeals(true);
        }

        public static IEnumerator WalkToInterestPoint(CartelDealer d, WaitForSeconds delay)
        {
            yield return delay;
            WalkToInterestPoint(d);
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

                int lockedCustomerIndex = UnityEngine.Random.Range(0, regionLockedCustomers.Count);
                Customer selected = null;

                for (int j = 0; j < regionLockedCustomers.Count; j++)
                {
                    selected = regionLockedCustomers[lockedCustomerIndex];

                    // Simplified the way which customer gets awarded a contract, by default alot of calculations go into determining the deal time, weekday, customer state, MOST of relevant stuff is typed out here but some is left out just for simplicity due to being a guaranteed locked customer.
                    bool canAwardContract = true;

                    // from ShouldTryGenerateDeal only 2 relevant conditions
                    if (selected.CurrentContract != null) canAwardContract = false; // basically never true but still
                    if (!selected.NPC.IsConscious) canAwardContract = false; // what about health isdead

                    // from IsDealTime only this should be relevant, as the weekdays are kind of cosmetic addition to have player feel like this behaviour is consistent. For cartel dealers it doesnt really matter what weekday is since there is no perception of week time passing or memorizing customers.
                    int orderTime = selected.customerData.OrderTime;
                    int max = TimeManager.AddMinutesTo24HourTime(orderTime, 240); // doubled from default 120 -> 240
                    if (!NetworkSingleton<TimeManager>.Instance.IsCurrentTimeWithinRange(orderTime, max))
                        canAwardContract = false;

                    if (!canAwardContract)
                    {
                        Log($"[LOCKED CUSTOMER DEAL] Cant award contract at this moment, see below state");
                        Log($"[LOCKED CUSTOMER DEAL]     Has Contract: {selected.CurrentContract != null}");
                        Log($"[LOCKED CUSTOMER DEAL]     IsConscious: {selected.NPC.IsConscious}");
                        Log($"[LOCKED CUSTOMER DEAL]     Order Time in Range: {NetworkSingleton<TimeManager>.Instance.IsCurrentTimeWithinRange(orderTime, max)}");
                        Log($"[LOCKED CUSTOMER DEAL] Customer: {selected.NPC.fullName}");
                        Log($"[LOCKED CUSTOMER DEAL]     orderTime: {orderTime} - {max}");

                        // if all customers parsed yield break and deactivate
                        if (j == regionLockedCustomers.Count - 1)
                        {
                            dealEvent.Deactivate();
                            selected = null;
                            yield break;
                        }
                        // else check next one or loop back to start and increment
                        else
                        {
                            lockedCustomerIndex = (lockedCustomerIndex + 1) % regionLockedCustomers.Count;
                            continue;
                        }
                    }
                }
                if (selected == null) yield break;

                // Before running below we want to make sure there is something in the inventory since try generate contract can result in no acceptable items especially in higher standard customers, so based on region, we add change existing quality OR add quality item if empty?
                // Todo: this could instead/addt check product affinity data ?
                EQuality requiredQuality = selected.customerData.Standards.GetCorrespondingQuality();
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
                                Log("[LOCKED CUSTOMER DEAL] Ensured Cartel Dealer has required product quality");
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
                                Log("[LOCKED CUSTOMER DEAL] Ensured Cartel Dealer has required product quality");
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
                    Log("[LOCKED CUSTOMER DEAL] Cartel dealer doesnt have required product, inserting.");
                    
                    int productIndex = UnityEngine.Random.Range(0, dealEvent.dealer.RandomProducts.Length);
                    ProductDefinition def2 = dealEvent.dealer.RandomProducts[productIndex];
                    ItemInstance item = def2.GetDefaultInstance(4);
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
                ContractInfo contractInfo = selected.TryGenerateContract(dealEvent.dealer);

                if (contractInfo != null)
                {
                    // Here we skip the offering, instead manually set the same variables as in source, but we skip checking should accept contract to ensure probability of generating the contract succesfully at the CartelCustomerDeal event is as high as possible
                    //c.OfferContractToDealer(contractInfo, dealEvent.dealer);
                    Log("[LOCKED CUSTOMER DEAL] Awarded contract to locked customer");
                    int offeredDeals = selected.OfferedDeals;
                    selected.OfferedDeals = offeredDeals + 1;
                    selected.TimeSinceLastDealOffered = 0;
                    selected.OfferedContractInfo = contractInfo;
                    selected.OfferedContractTime = NetworkSingleton<TimeManager>.Instance.GetDateTime();
                    selected.HasChanged = true;
                    EDealWindow dealWindow = dealEvent.dealer.GetDealWindow();
                    Contract contract = selected.ContractAccepted(dealWindow, false, dealEvent.dealer);
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


        // Patch the OnUnconscious function to block Failing the stolen contracts for Cartel Dealers
        // Todo: could also add the quick revive reature later ussing this patch to IEnumerator -> sleep 30 -> respawn at hospital

        [HarmonyPatch(typeof(Dealer), "DealerUnconscious")]
        public static class Dealer_DealerUnconscious_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dealer __instance)
            {
                // only run it for cartel dealers
                if (__instance.DealerType == EDealerType.CartelDealer)
                {
#if MONO
                    List<Contract> cartelDealerContracts = new(__instance.ActiveContracts);
#else
                    Il2CppSystem.Collections.Generic.List<Contract> cartelDealerContracts = new();
                    for (int i = 0; i < __instance.ActiveContracts.Count; i++)
                    {
                        if (__instance.ActiveContracts[i] != null)
                            if (!cartelDealerContracts.Contains(__instance.ActiveContracts[i]))
                                cartelDealerContracts.Add(__instance.ActiveContracts[i]);
                    }
#endif
                    foreach (Contract contract in cartelDealerContracts)
                    {
                        lock (playerDealerStolenLock)
                        {
                            // todo: in future change the Player -> intercept events to also use this logic instead of relying on isDead/Unconscious states?
                            if (playerDealerStolen.ContainsKey(contract.GUID.ToString()))
                            {
                                // player dealer contract being intercepted by cartel dealer
                                // OR intercept deals event is active with the contract, it handles it with own mechanic
                                // dont fail this contract since its duped to player hired dealer?
                                Log("Dealer Unconscious Fix ");
                            }
                            else
                            {
                                contract.Fail(true);
                            }
                        }
                    }

                    // block original since this does the same now
                    return false;
                }
                return true;
            }
        }

        // ProcessHandover for deciding from customer perspective when the contract is completed by cartel and when its completed by player dealer
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
                if (handoverByPlayer) return true;
                if (contract.Dealer == null) return true;
                if (!playerDealerStolen.ContainsKey(contract.GUID.ToString())) return true;

                playerDealerStolen.TryGetValue(contract.GUID.ToString(), out Tuple<Dealer, int> stored);
                if (stored == null) return true;
                Dealer originalDealer = stored.Item1;
                int originalXP = stored.Item2;
                // after this contract is guaranteed to be the stolen contracts thing this mod implements

                float distanceToCartelDealer = Vector3.Distance(__instance.NPC.CenterPoint, contract.Dealer.CenterPoint);
                float distanceToPlayerDealer = Vector3.Distance(__instance.NPC.CenterPoint, originalDealer.CenterPoint);

                // This function is still problematic it seems this can return both false when something happens and is indecisive
                // only at longer distances, needs outcome logged too to figure out what happens with it

                Log($"STOLEN HANDOVER CUSTOMER: ${__instance.NPC.fullName}");
                Log($"Handover: {contract.title} {contract.Entries[0].name} - {outcome}");
                Log($"    Completed by CartelDealer: {distanceToCartelDealer < distanceToPlayerDealer && distanceToCartelDealer < 2f}");
                Log($"    Completed by PlayerDealer: {distanceToPlayerDealer < distanceToCartelDealer && distanceToPlayerDealer < 2f}");

                // Player dealer completes contract
                if (distanceToPlayerDealer < distanceToCartelDealer && distanceToPlayerDealer < 2f)
                {
                    // Now because the contract gets auto assigned for Cartel Dealer instead of the original before evaluating the result
                    // it must be reset back to original if player dealer completed it. Otherwise it wont process the deal as "succesful" for player

                    // First remove it from Cartel Dealer
                    if (contract.Dealer.ActiveContracts.Count != 0 && contract.Dealer.ActiveContracts[0] == contract)
                        contract.Dealer.ActiveContracts[0].Fail();
                    // Assign to original
                    contract.Dealer = originalDealer;

                    // Reset xp + increment contract state
                    contract.CompletionXP = originalXP;
                    contract.completedContractsIncremented = true;

                }
                // Cartel dealer completes contract
                else if (distanceToCartelDealer < distanceToPlayerDealer && distanceToPlayerDealer < 2f)
                {
                    // Remove it from Player dealer?
                    if (originalDealer.ActiveContracts.Count != 0 && originalDealer.ActiveContracts[0] == contract)
                        originalDealer.ActiveContracts[0].Fail();

                    if (currentConfig.stealBackCustomers)
                    {
                        // cap the lowering to 1.0 (0.0-5.0)
                        if (__instance.NPC.RelationData.RelationDelta > 1.0f)
                        {
                            // Lower by 15% or 0.20 whichever is higher (0.0-5.0 rang)
                            float relationChange = Mathf.Max(__instance.NPC.RelationData.RelationDelta * 0.85f, 0.20f);
                            float result = Mathf.Clamp(relationChange, min: 0f, max: 5f);

                            Log($"[STEALBACK] {__instance.NPC.fullName} Relation Down: -{relationChange} now: {result}");
                            __instance.NPC.RelationData.RelationDelta = result;
                        }
                    }
                    
                }

                lock(playerDealerStolenLock)
                {
                    playerDealerStolen.Remove(contract.GUID.ToString());
                }
                consumedGUIDs.Add(contract.GUID.ToString()); // beacuse it seems that it can be double checked later???

                return true; // after all this run original????
            }
        }

        // Patch the CartelDealers randomize inventory function to allow for saving possibly stolen items back to inventory system before the inventory clears
        // With this also the dealer inv can be now used to return stolen items
        [HarmonyPatch(typeof(CartelDealer), "RandomizeInventory")]
        public static class CartelDealer_RandomizeInventory_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(CartelDealer __instance)
            {
                // If allied extensions enabled, cartel truced and dealer recruited, dont randomize inv
#if MONO
                if (currentConfig.alliedExtensions && NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced && __instance.IsRecruited)
                    return false;
#else
                if (currentConfig.alliedExtensions && NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced && __instance.IsRecruited)
                    return false;
#endif

                if (stolenInDealerInv.TryGetValue(__instance, out List<ItemInstance> items))
                {
                    if (items == null || items.Count == 0)
                    {
                        // no items that were stolen from player originally
                    }
                    else
                    {
                        // Else there were items stolen, parse them for the dealer check
                        // if they still exist??
                        List<ItemInstance> returnable = new();
                        for (int i = 0; i < items.Count; i++) 
                        {
                            for (int j = 0; j < __instance.Inventory.ItemSlots.Count; j++)
                            {
                                // Check if the item was untouched basically, this wont work if the dealer sold or got more of the item in inv...
                                if (__instance.Inventory.ItemSlots[j].ItemInstance == items[i])
                                {
                                    returnable.Add(items[i]);
                                }
                            }
                        }

                        if (returnable.Count > 0)
                        {
                            CartelStealsItems(returnable);
                            // Remove from List iteminstance tracking
                            stolenInDealerInv[__instance].Clear();
                        }
                    }


                }

                // Continue to randomize inventory (also clear now in the proceeding function)
                return true;
            }

        }

    }

    [Serializable]
    public class CartelDealerConfig
    {
        public float CartelDealerWalkSpeed = 2.8f;
        public float CartelDealerHP = 200.0f;
        public float CartelDealerLethality = 0.5f; // 0.0-1.0
        public string CartelDealerWeapon = "M1911"; // "Knife" "Shotgun", "Revolver", default unknown "M1911"
        public float StealDealerContractChance = 0.06f;
        public float StealPlayerPendingChance = 0.08f;
        public float DealerActivityDecreasePerKill = 0.10f;
        public float DealerActivityIncreasePerDay = 0.25f;
        public float SafetyThreshold = -0.85f;
        public bool SafetyEnabled = true;
        public bool FreeTimeWalking = true;
    }

}