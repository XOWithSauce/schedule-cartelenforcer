

using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.AlliedExtension;
using static CartelEnforcer.CartelGathering;

#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using FishNet;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_TrucedRecruits : Quest
    {
#if IL2CPP
        public Quest_TrucedRecruits(IntPtr ptr) : base(ptr) { }

        public Quest_TrucedRecruits() : base(ClassInjector.DerivedConstructorPointer<Quest_TrucedRecruits>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;

        private CartelDealer westvilleDealer = null;

        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only
        public override void Complete(bool network = true)
        {
            Log("Quest_TrucedRecruits: Complete method called.");
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

                Log("Quest_TrucedRecruits: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrucedRecruits: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_TrucedRecruits: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_TrucedRecruits: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrucedRecruits: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            Log("Quest_TrucedRecruits: End method called.");
            try
            {
                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;
#if MONO
                instance.onMinutePass.Remove((Action)this.MinPass);
#else
                instance.onMinutePass.Remove((Il2CppSystem.Action)this.MinPass);
#endif
                Log("Quest_TrucedRecruits: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrucedRecruits: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }

        #endregion

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
            Log("QuestInit");
            this.name = "Quest_TrucedRecruits";
            Expires = false;
            title = "Truced Recruits";
            CompletionXP = 200;
            Description = "Persuade Cartel Dealers to work for you";
            TrackOnBegin = true;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = false;

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

            // UI related code and the benzies logo
            RectTransform rt = MakeIcon(this.transform);
            rtIcon = rt;
            this.IconPrefab = rt;
            UiPrefab = MakeUIPrefab(this.transform);
            PoIPrefab = MakePOI(this.transform, UiPrefab);

            // Create the QuestEntry GameObjects and parent them.
            GameObject findCartelObject = new GameObject("QuestEntry_FindCartel");
            findCartelObject.transform.SetParent(this.transform);

            // Opt entry for Greeting the goons
            GameObject greetGoonsObject = new GameObject("QuestEntry_GreetGoons");
            greetGoonsObject.transform.SetParent(this.transform);

            GameObject persuadeCartelObject = new GameObject("QuestEntry_PersuadeCartelDealer");
            persuadeCartelObject.transform.SetParent(this.transform);

            GameObject hireCartelObject = new GameObject("QuestEntry_HireCartelDealer");
            hireCartelObject.transform.SetParent(this.transform);

            QuestEntry findCartel = findCartelObject.AddComponent<QuestEntry>();
            QuestEntry greetGoons = findCartelObject.AddComponent<QuestEntry>();
            QuestEntry persuadeCartel = persuadeCartelObject.AddComponent<QuestEntry>();
            QuestEntry hireCartel = hireCartelObject.AddComponent<QuestEntry>();

            this.QuestEntry_FindCartel = findCartel;
            this.QuestEntry_GreetGoons = greetGoons;
            this.QuestEntry_PersuadeCartelDealer = persuadeCartel;
            this.QuestEntry_HireCartelDealer = hireCartel;

            this.Entries = new();
            this.Entries.Add(findCartel);
            this.Entries.Add(greetGoons);
            this.Entries.Add(persuadeCartel);
            this.Entries.Add(hireCartel);

            Log("Config Entries");
            foreach (CartelDealer d in UnityEngine.Object.FindObjectsOfType<CartelDealer>(true))
            {
                if (d.Region == EMapRegion.Westville)
                {
                    westvilleDealer = d;
                    break;
                }
            }

            findCartel.SetEntryTitle("Find the Westville Cartel Dealer");
            findCartel.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            findCartel.PoILocation.name = "FindCartelEntry_POI";
            findCartel.PoILocation.transform.SetParent(findCartel.transform);
            findCartel.PoILocation.transform.position = westvilleDealer.CenterPoint;
            findCartel.AutoUpdatePoILocation = true;
            findCartel.SetState(EQuestState.Active, false);
            findCartel.ParentQuest = this;
            findCartel.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction findCartelAction = null;
            void OnFindCartelComplete()
            {
                if (findCartel != null && findCartel.State == EQuestState.Failed) return;
                if (persuadeCartel == null) return;

                persuadeCartel.Begin();
                if (persuadeCartel.PoI != null && persuadeCartel.PoI.gameObject != null)
                    persuadeCartel.PoI.gameObject.SetActive(true);

                if (findCartelAction != null)
                {
                    findCartel.onComplete.RemoveListener(findCartelAction);
                    findCartelAction = null;
                }
            }
            findCartelAction = (UnityEngine.Events.UnityAction)OnFindCartelComplete;
            findCartel.onComplete.AddListener(findCartelAction);

            greetGoons.SetEntryTitle("(Optional) Say greetings to all 3 gathering goons");
            greetGoons.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            greetGoons.PoILocation.name = "FindCartelEntry_POI";
            greetGoons.PoILocation.transform.SetParent(greetGoons.transform);
            greetGoons.PoILocation.transform.position = Vector3.zero;
            greetGoons.AutoUpdatePoILocation = true;
            greetGoons.SetState(EQuestState.Inactive, false);
            greetGoons.ParentQuest = this;
            greetGoons.CompleteParentQuest = false;

            persuadeCartel.SetEntryTitle("Try persuading the Westville Cartel Dealer");
            persuadeCartel.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            persuadeCartel.PoILocation.name = "PersuadeCartelEntry_POI";
            persuadeCartel.PoILocation.transform.SetParent(persuadeCartel.transform);
            persuadeCartel.PoILocation.transform.position = westvilleDealer.CenterPoint;
            persuadeCartel.AutoUpdatePoILocation = true;
            persuadeCartel.SetState(EQuestState.Inactive, false);
            persuadeCartel.ParentQuest = this;
            persuadeCartel.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction persuadeCartelAction = null;
            void OnPersuadeCartelComplete()
            {
                if (persuadeCartel != null && persuadeCartel.State == EQuestState.Failed) return;
                if (hireCartel == null) return;

                hireCartel.Begin();
                if (hireCartel.PoI != null && hireCartel.PoI.gameObject != null)
                    hireCartel.PoI.gameObject.SetActive(false);

                if (persuadeCartelAction != null)
                {
                    persuadeCartel.onComplete.RemoveListener(persuadeCartelAction);
                    persuadeCartelAction = null;
                }
            }
            persuadeCartelAction = (UnityEngine.Events.UnityAction)OnPersuadeCartelComplete;
            persuadeCartel.onComplete.AddListener(persuadeCartelAction);

            hireCartel.SetEntryTitle("Hire the Westville Cartel Dealer");
            hireCartel.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            hireCartel.PoILocation.name = "HireCartelEntry_POI";
            hireCartel.PoILocation.transform.SetParent(hireCartel.transform);
            hireCartel.PoILocation.transform.position = Vector3.zero;
            hireCartel.AutoUpdatePoILocation = false;
            hireCartel.SetState(EQuestState.Inactive, false);
            hireCartel.ParentQuest = this;
            hireCartel.CompleteParentQuest = false;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
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
                    this.hudUI.MainLabel.text = "Truced Recruits";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_FindCartel != null)
            {
                
                if (QuestEntry_FindCartel.compassElement != null)
                    QuestEntry_FindCartel.compassElement.Visible = true;
                else
                {
                    QuestEntry_FindCartel.CreateCompassElement();
                    QuestEntry_FindCartel.compassElement.Visible = true;
                }

                if (QuestEntry_FindCartel.PoI != null)
                    QuestEntry_FindCartel.PoI.gameObject.SetActive(true);
            }

            if (QuestEntry_GreetGoons != null)
            {
                // Make the compass and poi but non visible, setvisible when gathering starts
                if (QuestEntry_GreetGoons.PoI != null && QuestEntry_GreetGoons.PoI.gameObject != null)
                    QuestEntry_GreetGoons.PoI.gameObject.SetActive(false);

                if (QuestEntry_GreetGoons.compassElement != null)
                    QuestEntry_GreetGoons.compassElement.Visible = false;
                else
                {
                    QuestEntry_GreetGoons.CreateCompassElement();
                    QuestEntry_GreetGoons.compassElement.Visible = false;
                }
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            return;
        }

        public override void MinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || alliedQuests.alliedIntroCompleted || this.State != EQuestState.Active) return;

            if (!InstanceFinder.IsServer)
            {
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
#endif
            if (westvilleDealer == null) return;


            // Check for optional greet gathering goons quest entry
            if (QuestEntry_GreetGoons != null && QuestEntry_GreetGoons.State == EQuestState.Active)
            {
                // if gathering is not active -> set questentrystate expired, disable poi+compass
                if (!areGoonsGathering)
                {
                    // Make the compass and poi invisible
                    if (QuestEntry_GreetGoons.PoI != null && QuestEntry_GreetGoons.PoI.gameObject != null)
                    {
                        QuestEntry_GreetGoons.SetPoILocation(Vector3.zero);
                        QuestEntry_GreetGoons.PoI.gameObject.SetActive(false);
                    }
                    if (QuestEntry_GreetGoons.compassElement != null)
                        QuestEntry_GreetGoons.compassElement.Visible = false;
                    QuestEntry_GreetGoons.SetState(EQuestState.Expired, false);
                }

            }
            else if (QuestEntry_GreetGoons != null && (QuestEntry_GreetGoons.State == EQuestState.Inactive || QuestEntry_GreetGoons.State == EQuestState.Expired))
            {
                // Inactive / expire should check for gathering active -> active this + poi,compass
                if (areGoonsGathering && currentGatheringLocation != null)
                {
                    // Make the compass and poi visible
                    if (QuestEntry_GreetGoons.PoI != null && QuestEntry_GreetGoons.PoI.gameObject != null)
                    {
                        QuestEntry_GreetGoons.SetPoILocation(currentGatheringLocation.position);
                        QuestEntry_GreetGoons.PoI.gameObject.SetActive(true);
                    }
                    if (QuestEntry_GreetGoons.compassElement != null)
                        QuestEntry_GreetGoons.compassElement.Visible = true;
                }
            }

            // Find, Persuade, Hire Cartel Dealer Entries
            if (QuestEntry_FindCartel != null && QuestEntry_FindCartel.State == EQuestState.Active)
            {
                if (!westvilleDealer.isInBuilding && Player.Local.IsPointVisibleToPlayer(westvilleDealer.CenterPoint, maxDistance_Visible: 20f, minDistance_Invisible: 1f))
                {
                    QuestEntry_FindCartel.Complete();
                }
                QuestEntry_FindCartel.PoILocation.transform.position = westvilleDealer.CenterPoint;
            }

            if (QuestEntry_PersuadeCartelDealer != null && QuestEntry_PersuadeCartelDealer.State == EQuestState.Active)
            {
                if (westvilleDealer.HasBeenRecommended)
                {
                    QuestEntry_PersuadeCartelDealer.Complete();
                    return;
                }
                QuestEntry_PersuadeCartelDealer.PoILocation.transform.position = westvilleDealer.CenterPoint;
            }

            if (QuestEntry_HireCartelDealer != null && QuestEntry_HireCartelDealer.State == EQuestState.Active)
            {
                if (westvilleDealer.IsRecruited)
                {
                    if (!(SaveManager.Instance.IsSaving || isSaving))
                        alliedQuests.alliedIntroCompleted = true;
                    Complete();
                    return;
                }
            }
        }

        public QuestEntry QuestEntry_FindCartel;
        public QuestEntry QuestEntry_GreetGoons;
        public QuestEntry QuestEntry_PersuadeCartelDealer;
        public QuestEntry QuestEntry_HireCartelDealer;

    }
}
