

using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.SuppliesModule;


#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using ScheduleOne.UI;
using ScheduleOne.UI.Handover;
using ScheduleOne.Dialogue;
using FishNet;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Handover;
using Il2CppScheduleOne.Dialogue;
using Il2CppFishNet;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_AlliedSupplies : Quest
    {
#if IL2CPP
        public Quest_AlliedSupplies(IntPtr ptr) : base(ptr) { }

        public Quest_AlliedSupplies() : base(ClassInjector.DerivedConstructorPointer<Quest_AlliedSupplies>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;
        private SupplyLocation location;

        public bool playerNoticed = false;
        public bool playerInterrogated = false;

        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions

        public override void Complete(bool network = true)
        {
            Log("Quest_AlliedSupplies: Complete method called.");
            try
            {
                if (this.State == EQuestState.Completed)
                {
                    return;
                }
                if (InstanceFinder.IsServer && !Singleton<LoadManager>.Instance.IsLoading)
                    NetworkSingleton<LevelManager>.Instance.AddXP(this.CompletionXP);

                this.SetQuestState(EQuestState.Completed, false);

                NetworkSingleton<QuestManager>.Instance.PlayCompleteQuestSound();
                this.End();

                Log("Quest_AlliedSupplies: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_AlliedSupplies: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_AlliedSupplies: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_AlliedSupplies: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_AlliedSupplies: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End() // so now complete, fail, expire should all call this
        {
            Log("Quest_AlliedSupplies: End method called with state " + this.State);
            try
            {
                // Instead of Complete calling destroy just disable the ui
                if (hudUI != null)
                    hudUI.gameObject.SetActive(false);


                /* Commented out to test if not removing minpass would allow replay after complete
                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;
#if MONO
                instance.onMinutePass.Remove((Action)this.MinPass);
#else
                instance.onMinutePass.Remove((Il2CppSystem.Action)this.MinPass); also this doesnt work
#endif
                */
                coros.Add(MelonCoroutines.Start(CleanupTruceSuppliesQuest(this.location)));

                Log("Quest_AlliedSupplies: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_AlliedSupplies: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }

        #endregion

        public void ResetSelf()
        {
            Log("[ALLIEDEXT] Reset Supply Event");
            alliedSuppliesActive = true;
            if (activeAlliedSupplies == null) return;
            // First load in save instantiates it and completes
            // so that if status == complete OR Expire -> can re enable
            if (!(this.State == EQuestState.Completed || this.State == EQuestState.Expired)) return;
            // if status was fail then cartel is not truced anymore
            
            // Pick next random location that is not the same as previous
            int newLocationIndex = UnityEngine.Random.Range(0, supplyLocations.Count);
            if (supplyLocations[newLocationIndex] == this.location)
            {
                this.location = supplyLocations[(newLocationIndex + 1) % supplyLocations.Count];
            }
            else
            {
                this.location = supplyLocations[newLocationIndex];
            }

            playerInterrogated = false;
            playerNoticed = false;
            
            // Update expiry with +1 401
            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            Expiry = new GameDateTime(_elapsedDays: instance.ElapsedDays + 1, _time: 401);
            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";
            title = "Allied Supplies";

            this.gameObject.SetActive(true);

            // set questentrystates back + queststate
            QuestEntry_GatherSupplies.SetState(EQuestState.Active, false);
            QuestEntry_LocateSupplies.SetState(EQuestState.Inactive, false);
            SetQuestState(EQuestState.Active);
            if (QuestEntry_GatherSupplies.PoI != null && QuestEntry_GatherSupplies.PoI.gameObject != null)
            {
                QuestEntry_GatherSupplies.PoI.gameObject.SetActive(false);
            }

            QuestEntry_GatherSupplies.PoILocation.transform.position = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;

            if (QuestEntry_GatherSupplies.compassElement != null)
                QuestEntry_GatherSupplies.compassElement.Visible = false;

            this.hudUI.gameObject.SetActive(true);

            Log("[ALLIEDEXT] Reset Complete");

            coros.Add(MelonCoroutines.Start(SpawnSupply(this.location)));

        }


#if IL2CPP
        // Because by default the property uses this.Entries in Enumberable.Count, which probably causes the bug when the this.entries
        // il2cpp system ienumerable but expecting system ienumerable?
        public new int ActiveEntryCount
        {
            get
            {
                if (this.Entries == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (QuestEntry entry in this.Entries)
                {
                    if (entry == null)
                    {
                        MelonLogger.Warning("Quest Entry got GC'd");
                    }
                    if (entry.State == EQuestState.Active)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
#endif

        public void SetupSelf()
        {
            alliedSuppliesActive = true;

            Log("QuestInit");
            this.name = "Quest_AlliedSupplies";
            Expires = true;
            title = "Allied Supplies";
            CompletionXP = 300;
            Description = "Pick up Cartel supplies";
            TrackOnBegin = true;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = false;
            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            Expiry = new GameDateTime(_elapsedDays: instance.ElapsedDays + 1, _time: 401);
            ExpiryVisibility = EExpiryVisibility.Always;
            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";

            onActiveState = new UnityEvent();
            onComplete = new UnityEvent();
            onInitialComplete = new UnityEvent();
            onQuestBegin = new UnityEvent();
            onQuestEnd = new UnityEvent<EQuestState>();
            onTrackChange = new UnityEvent<bool>();
#if MONO
            this.SetGUID(Guid.NewGuid());
#else
            this.SetGUID(Il2CppSystem.Guid.NewGuid());
#endif
            Transform target = NetworkSingleton<QuestManager>.Instance.QuestContainer?.GetChild(0);
            if (target != null)
            {
                this.transform.SetParent(target);
            }

            this.location = supplyLocations[UnityEngine.Random.Range(0, supplyLocations.Count)];

            // UI related code and the benzies logo
            RectTransform rt = MakeIcon(this.transform);
            rtIcon = rt;
            this.IconPrefab = rt;
            UiPrefab = MakeUIPrefab(this.transform);
            PoIPrefab = MakePOI(this.transform, UiPrefab);

            // Create the QuestEntry GameObjects and parent them.
            GameObject locateObject = new GameObject("QuestEntry_LocateSupplies");
            locateObject.transform.SetParent(this.transform);

            GameObject gatherObject = new GameObject("QuestEntry_GatherSupplies");
            gatherObject.transform.SetParent(this.transform);

            QuestEntry locateSupplies = locateObject.AddComponent<QuestEntry>();
            QuestEntry gatherSupplies = gatherObject.AddComponent<QuestEntry>();

            this.QuestEntry_LocateSupplies = locateSupplies;
            this.QuestEntry_GatherSupplies = gatherSupplies;

            this.Entries = new();
            this.Entries.Add(this.QuestEntry_LocateSupplies);
            this.Entries.Add(this.QuestEntry_GatherSupplies);

            Log("Config Entries");

            locateSupplies.SetEntryTitle("Read Thomas' message and locate the Cartel supplies");
            locateSupplies.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            locateSupplies.PoILocation.name = "LocateSuppliesEntry_POI";
            locateSupplies.PoILocation.transform.SetParent(locateSupplies.transform);
            locateSupplies.PoILocation.transform.position = Vector3.zero;
            locateSupplies.AutoUpdatePoILocation = true;
            locateSupplies.SetState(EQuestState.Active, false);
            locateSupplies.ParentQuest = this;
            locateSupplies.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction locateSuppliesAction = null;
            void OnLocateSuppliesComplete()
            {
                if (locateSupplies != null && locateSupplies.State == EQuestState.Failed) return;
                if (gatherSupplies == null) return;

                gatherSupplies.Begin();
                
                gatherSupplies.SetPoILocation(location: location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position);
                if (gatherSupplies.PoI != null && gatherSupplies.PoI.gameObject != null)
                    gatherSupplies.PoI.gameObject.SetActive(true);

                if (gatherSupplies.compassElement != null)
                    gatherSupplies.compassElement.Visible = true;
                else
                {
                    gatherSupplies.CreateCompassElement();
                    gatherSupplies.compassElement.Visible = true;
                }

                // commented out to make it replayable multiple times per session
                //if (locateSuppliesAction != null) 
                //{
                //    locateSupplies.onComplete.RemoveListener(locateSuppliesAction);
                //    locateSuppliesAction = null;
                //}
            }
            locateSuppliesAction = (UnityEngine.Events.UnityAction)OnLocateSuppliesComplete;
            locateSupplies.onComplete.AddListener(locateSuppliesAction);

            gatherSupplies.SetEntryTitle("Receive the Cartel supplies");
            gatherSupplies.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            gatherSupplies.PoILocation.name = "LocateSuppliesEntry_POI";
            gatherSupplies.PoILocation.transform.SetParent(gatherSupplies.transform);
            gatherSupplies.PoILocation.transform.position = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;
            gatherSupplies.AutoUpdatePoILocation = true;
            gatherSupplies.SetState(EQuestState.Inactive, false);
            gatherSupplies.ParentQuest = this;
            gatherSupplies.CompleteParentQuest = false;

#if MONO
            instance.onMinutePass.Add(new Action(this.MinPass));
#else
            instance.onMinutePass += (Il2CppSystem.Action)this.MinPass;
#endif
            StartQuestDetail();
        }

        private void StartQuestDetail() // todo fixme this dumb
        {
            if (this.IconPrefab == null)
                this.IconPrefab = this.transform.Find("BenziesLogoQuest").GetComponent<RectTransform>();
            SetupHUDUI();

            if (hudUI != null)
            {
                if (hudUI.MainLabel != null)
                    this.hudUI.MainLabel.text = "Allied Supplies";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_LocateSupplies != null)
            {
                if (QuestEntry_LocateSupplies.compassElement != null)
                    QuestEntry_LocateSupplies.compassElement.Visible = false;

                if (QuestEntry_LocateSupplies.PoI != null)
                    QuestEntry_LocateSupplies.PoI.gameObject.SetActive(false);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);
            coros.Add(MelonCoroutines.Start(SpawnSupply(this.location)));
            return;
        }

        public override void MinPass()
        {
            // test logging it
            if (!registered || Singleton<SaveManager>.Instance.IsSaving || this.State != EQuestState.Active) return;
            Log("SupplyQuestMinPass");
            if (!InstanceFinder.IsServer)
            {
                Log("Not server instance");
                return;
            }

#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced)
#endif
            {
                Fail();
            }

#if MONO
            base.MinPass(); // Is this necessary in mono or does cause recursion??
#else
            // because calling base.MinPass in il2cpp crashes instantly
            this.CheckExpiry();
            this.UpdateHUDUI();
            // re-evaluate quest state?
            if (this.State != EQuestState.Active) return;
#endif

            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";

            // If player noticed and interrogated dont evaluate guard witth non return
            if (!(playerNoticed && playerInterrogated))
            {
                Log($"Guard: {!alliedGuard.Health.IsDead} {!alliedGuard.Health.IsKnockedOut} {!alliedGuard.isInBuilding} {alliedGuard.Behaviour.activeBehaviour != alliedGuard.Behaviour.CombatBehaviour} {Vector3.Distance(Player.Local.CenterPointTransform.position, location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position) < 20f}");

                // Check if guard not dead, in building, in combat and within 20 units range
                if (alliedGuard != null 
                        && !alliedGuard.Health.IsDead 
                        && !alliedGuard.Health.IsKnockedOut 
                        && !alliedGuard.isInBuilding 
                        && alliedGuard.Behaviour.activeBehaviour != alliedGuard.Behaviour.CombatBehaviour
                        && Vector3.Distance(Player.Local.CenterPointTransform.position, location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position) < 20f
                        )
                {
                    // Guard look for player + rot towards randomly
                    if (!playerNoticed && !playerInterrogated)
                    {
                        Log("Look For Player");

                        // if guard is guaranteed to be visible to player then higher chance for it to face player
                        if (Player.Local.IsPointVisibleToPlayer(alliedGuard.CenterPointTransform.position))
                        {
                            if (UnityEngine.Random.Range(0f, 1f) > 0.80f)
                            {
                                alliedGuard.Movement.FacePoint(Player.Local.CenterPointTransform.position, lerpTime: 0.7f);
                            }
                        }

                        if (alliedGuard.Awareness.VisionCone.IsPlayerVisible(Player.Local))
                        {
                            playerNoticed = true;
                        }
                    }
                    // Guard notice player and start walking towards it to start dialogue
                    else if (playerNoticed && !playerInterrogated)
                    {
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, alliedGuard.CenterPoint) < 3f)
                        {
                            Log("Interrogate");
                            // start dialogue if possible
                            if (!Singleton<DialogueCanvas>.Instance.isActive && !Singleton<HandoverScreen>.Instance.IsOpen && PlayerSingleton<PlayerCamera>.Instance.activeUIElementCount <= 0 && !alliedGuard.DialogueHandler.IsDialogueInProgress)
                            {
                                if (alliedGuard.DialogueHandler != null)
                                {
                                    DialogueController controller = alliedGuard.DialogueHandler.gameObject.GetComponent<DialogueController>();
                                    if (controller != null)
                                        controller.StartGenericDialogue(false);
                                    Log("Player interrogated");
                                    playerInterrogated = true;
                                }
                            }
                        }
                        else
                        {
                            Log("Traverse");
                            Vector3 closestPoint = Vector3.zero;
                            alliedGuard.Movement.GetClosestReachablePoint(Player.Local.CenterPointTransform.position, out closestPoint);
                            if (closestPoint != Vector3.zero)
                                alliedGuard.Movement.SetDestination(closestPoint);
                        }
                    }
                }
            }
            // else if noticed and interrogated try return to original position facing original rotation
            else
            {
                // If not has destination and can move and is not in guard position, move to position
                if (!alliedGuard.Movement.HasDestination && alliedGuard.Movement.CanMove() && Vector3.Distance(alliedGuard.CenterPoint, location.GuardPosition) > 1.5f)
                {
                    // set destination with NPCMovement walkresult callback to
                    // rotate back 2 original
                    void OnGuardArrivedToPos(NPCMovement.WalkResult result)
                    {
                        if (result == NPCMovement.WalkResult.Success)
                        {
                            // Calculate forward position from guard pos facing guard rot and then face point
                            Vector3 fwdFromPos = location.GuardPosition + (Quaternion.Euler(location.GuardRotation) * Vector3.fwd * 1.5f);
                            alliedGuard.Movement.FacePoint(fwdFromPos);
                        }
                    }
#if MONO
                    Action<NPCMovement.WalkResult> walkCallback = (Action<NPCMovement.WalkResult>)OnGuardArrivedToPos;
#else
                    Il2CppSystem.Action<NPCMovement.WalkResult> walkCallback = (Il2CppSystem.Action<NPCMovement.WalkResult>)OnGuardArrivedToPos;
#endif
                    alliedGuard.Movement.SetDestination(this.location.GuardPosition, walkCallback, interruptExistingCallback: true);
                }
            }

            // Evaluate quest entries with return

            if (QuestEntry_LocateSupplies != null && QuestEntry_LocateSupplies.State == EQuestState.Active)
            {
                if (Vector3.Distance(
                    a: Player.Local.CenterPointTransform.position,
                    b: location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position
                ) < 14f)
                {
                    QuestEntry_LocateSupplies.SetState(EQuestState.Completed, false);
                    return;
                }
            }

            if (QuestEntry_GatherSupplies != null && QuestEntry_GatherSupplies.State == EQuestState.Active)
            {
                // If barrel update barrel poi and compass pos
                bool suppliesClaimed = false;
                // Check unclaimed barrels, update poi
                if (location.Type == ESupplyType.Barrel)
                {
                    Vector3 nextBarrel = Vector3.zero;
                    Transform currentBarrel;
                    int consumedBarrels = 0;
                    foreach (GameObject go in location.BarrelObjects)
                    {
                        currentBarrel = go.transform.Find("CE_SUPPLY"); // find the interactable child object
                        if (currentBarrel == null)
                        {
                            consumedBarrels++;
                            continue;
                        }
                        else
                        {
                            nextBarrel = currentBarrel.position;
                        }
                    }

                    if (consumedBarrels == location.BarrelObjects.Count)
                    {
                        suppliesClaimed = true;
                    }
                    else if (nextBarrel != Vector3.zero)
                    {
                        if (QuestEntry_GatherSupplies.PoI != null && QuestEntry_GatherSupplies.PoI.gameObject != null)
                        {
                            QuestEntry_GatherSupplies.SetPoILocation(nextBarrel);
                        }
                    }
                }

                if (suppliesClaimed)
                {
                    this.Complete(false);
                    return;
                }
            }

            return;
        }

        public QuestEntry QuestEntry_LocateSupplies;
        public QuestEntry QuestEntry_GatherSupplies;
    }
}
