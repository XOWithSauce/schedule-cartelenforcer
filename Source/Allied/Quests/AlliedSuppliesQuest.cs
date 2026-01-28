

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

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;
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

            // UI related code and the benzies logo
            rtIcon = MakeIcon(this.transform);
            this.IconPrefab = rtIcon;
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
            GameObject locatePoi = UnityEngine.Object.Instantiate(PoIPrefab);
            locateSupplies.PoI = locatePoi.GetComponent<POI>();
            locateSupplies.PoILocation = locatePoi.transform;
            locateSupplies.PoILocation.name = "LocateSuppliesEntry_POI";
            locateSupplies.PoILocation.SetParent(locateSupplies.transform);
            locateSupplies.PoILocation.position = Vector3.zero;
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
            }
            locateSuppliesAction = (UnityEngine.Events.UnityAction)OnLocateSuppliesComplete;
            locateSupplies.onComplete.AddListener(locateSuppliesAction);


            gatherSupplies.SetEntryTitle("Receive the Cartel supplies");
            GameObject gatherPoi = UnityEngine.Object.Instantiate(PoIPrefab);
            gatherSupplies.PoI = gatherPoi.GetComponent<POI>();
            gatherSupplies.PoILocation = gatherPoi.transform;
            gatherSupplies.PoILocation.name = "LocateSuppliesEntry_POI";
            gatherSupplies.PoILocation.transform.SetParent(gatherSupplies.transform);
            gatherSupplies.PoILocation.transform.position = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;
            gatherSupplies.AutoUpdatePoILocation = true;
            gatherSupplies.SetState(EQuestState.Inactive, false);
            gatherSupplies.ParentQuest = this;
            gatherSupplies.CompleteParentQuest = false;

            StartQuestDetail();
        }

        private void StartQuestDetail() 
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

            // Does the bug happen even without minpass or spawn supply?
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
