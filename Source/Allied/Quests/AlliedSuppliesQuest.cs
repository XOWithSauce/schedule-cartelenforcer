

using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.SuppliesModule;

#if MONO
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using ScheduleOne.Map;
using FishNet;
#else
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Map;
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
        public RectTransform groupRt;

        public SupplyLocation location;

        public bool playerNoticed = false;
        public bool playerInterrogated = false;
        public bool interrogatingPlayer = false;

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
            Log("Reset Supply Event");
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

            interrogatingPlayer = false;
            playerInterrogated = false;
            playerNoticed = false;
            
            // Update expiry with +1 401
            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            Expiry = new GameDateTime(_elapsedDays: instance.ElapsedDays + 1, _time: 401);
            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";
            title = "Allied Supplies";

            this.gameObject.SetActive(true);

            // set questentrystates back + queststate
            QuestEntry_GatherSupplies.SetState(EQuestState.Inactive, false);
            QuestEntry_LocateSupplies.SetState(EQuestState.Active, false);
            SetQuestState(EQuestState.Active);

            if (QuestEntry_GatherSupplies.PoI != null && QuestEntry_GatherSupplies.PoI.UI != null)
            {
                QuestEntry_GatherSupplies.PoI.UI.gameObject.SetActive(false);
            }

            QuestEntry_GatherSupplies.PoILocation.transform.position = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;

            if (QuestEntry_GatherSupplies.compassElement != null && QuestEntry_GatherSupplies.compassElement.Visible)
                QuestEntry_GatherSupplies.compassElement.Visible = false;

            if (QuestEntry_LocateSupplies.compassElement != null && QuestEntry_LocateSupplies.compassElement.Visible)
                QuestEntry_LocateSupplies.compassElement.Visible = false;

            this.hudUI.gameObject.SetActive(true);
            QuestEntry_LocateSupplies.entryUI.FadeIn();

            Log("Reset Complete");

            coros.Add(MelonCoroutines.Start(SpawnSupply(this.location)));
        }

        public void SetupSelf()
        {
            alliedSuppliesActive = true;

            Log("QuestInit");
            this.name = "Quest_AlliedSupplies";
            Expires = true;
            title = "Allied Supplies";
            CompletionXP = 300;
            Description = "Pick up Cartel supplies";
            TrackOnBegin = false;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = false;
            AutoStartFirstEntry = false;
            ShouldSendExpiryReminder = true;

            Transform target = NetworkSingleton<QuestManager>.Instance.QuestContainer?.GetChild(0);
            if (target != null)
            {
                this.transform.SetParent(target);
            }

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
            this.location = supplyLocations[UnityEngine.Random.Range(0, supplyLocations.Count)];
            if (currentConfig.debugMode)
            {
                foreach (SupplyLocation loc in supplyLocations)
                {
                    if (loc.ID == "SUPPLY_DOCKS") // test barrels
                    {
                        this.location = loc;
                        break;
                    }
                }
            }

            // UI related code and the benzies logo
            base.IconPrefab = MakeIcon(this.transform);
            base.PoIPrefab = MakePOI();
            
            // Create the QuestEntry GameObjects and parent them.
            GameObject locateObject = new GameObject("QuestEntry_LocateSupplies");
            locateObject.transform.SetParent(this.transform);

            GameObject gatherObject = new GameObject("QuestEntry_GatherSupplies");
            gatherObject.transform.SetParent(this.transform);

            QuestEntry locateSupplies = locateObject.AddComponent<QuestEntry>();
            QuestEntry gatherSupplies = gatherObject.AddComponent<QuestEntry>();

            this.QuestEntry_LocateSupplies = locateSupplies;
            this.QuestEntry_GatherSupplies = gatherSupplies;

            base.Entries = new();
            base.Entries.Add(this.QuestEntry_LocateSupplies);
            base.Entries.Add(this.QuestEntry_GatherSupplies);

            Log("Config Entries");

            locateSupplies.SetEntryTitle("Read Thomas' message and locate the Cartel supplies");
            locateSupplies.AutoCreatePoI = false;
            locateSupplies.ParentQuest = this;
            locateSupplies.CompleteParentQuest = false;
            locateSupplies.PoILocation = new GameObject("LocateSuppliesEntry_POI").transform;
            locateSupplies.PoILocation.transform.SetParent(locateSupplies.transform);
            locateSupplies.SetState(EQuestState.Active, false);

            UnityEngine.Events.UnityAction locateSuppliesAction = null;
            void OnLocateSuppliesComplete()
            {
                if (locateSupplies != null && locateSupplies.State == EQuestState.Failed) return;
                if (gatherSupplies == null) return;

                gatherSupplies.Begin();
                UpdateQuestMapLogo(gatherSupplies);

                gatherSupplies.SetPoILocation(location: location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position);
            }
            locateSuppliesAction = (UnityEngine.Events.UnityAction)OnLocateSuppliesComplete;
            locateSupplies.onComplete.AddListener(locateSuppliesAction);

            gatherSupplies.SetEntryTitle("Receive the Cartel supplies");
            gatherSupplies.ParentQuest = this;
            gatherSupplies.CompleteParentQuest = false;
            gatherSupplies.PoILocation = new GameObject("GatherSuppliesEntry_POI").transform;
            gatherSupplies.PoILocation.transform.position = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;
            gatherSupplies.PoILocation.transform.SetParent(gatherSupplies.transform);
            gatherSupplies.SetState(EQuestState.Inactive, false);

            StartQuestDetail();
        }

        private void StartQuestDetail() 
        {
            SetupHUDUI();

            if (hudUI != null)
            {
                if (hudUI.MainLabel != null)
                    this.hudUI.MainLabel.text = "Allied Supplies";
                this.hudUI.gameObject.SetActive(true);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            if (QuestEntry_LocateSupplies != null)
            {
                if (QuestEntry_LocateSupplies.compassElement != null)
                    QuestEntry_LocateSupplies.compassElement.Visible = false;
                else
                {
                    QuestEntry_LocateSupplies.CreateCompassElement();
                    QuestEntry_LocateSupplies.compassElement.Visible = false;
                }
            }

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onMinutePass.Add(new Action(MinPassSupply));
#else
            instance.onMinutePass += (Il2CppSystem.Action)MinPassSupply;
#endif
            coros.Add(MelonCoroutines.Start(SpawnSupply(this.location)));
            return;
        }

        public QuestEntry QuestEntry_LocateSupplies;
        public QuestEntry QuestEntry_GatherSupplies;
    }
}
