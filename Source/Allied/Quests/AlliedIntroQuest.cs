

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

                var action = (Action)OnMinPass;

                if (instance == null) return;
#if MONO
                instance.onMinutePass.Remove(action);
#else
                instance.onMinutePass.Remove((Il2CppSystem.Action)action);
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
            base.IconPrefab = MakeIcon(this.transform);
            base.PoIPrefab = MakePOI();

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
            QuestEntry greetGoons = greetGoonsObject.AddComponent<QuestEntry>();
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
            findCartel.ParentQuest = this;
            findCartel.CompleteParentQuest = false;
            findCartel.PoILocation = new GameObject("FindCartelEntry_POI").transform;
            findCartel.PoILocation.transform.SetParent(findCartel.transform);
            findCartel.PoILocation.transform.position = westvilleDealer.CenterPoint;
            findCartel.AutoUpdatePoILocation = true;
            findCartel.SetState(EQuestState.Active, false);

            UnityEngine.Events.UnityAction findCartelAction = null;
            void OnFindCartelComplete()
            {
                if (findCartel != null && findCartel.State == EQuestState.Failed) return;
                if (persuadeCartel == null) return;

                persuadeCartel.Begin();
                UpdateQuestMapLogo(persuadeCartel);

                if (findCartelAction != null)
                {
                    findCartel.onComplete.RemoveListener(findCartelAction);
                    findCartelAction = null;
                }
            }
            findCartelAction = (UnityEngine.Events.UnityAction)OnFindCartelComplete;
            findCartel.onComplete.AddListener(findCartelAction);

            greetGoons.SetEntryTitle("(Optional) Say greetings to all 3 gathering goons");
            greetGoons.ParentQuest = this;
            greetGoons.CompleteParentQuest = false;
            greetGoons.PoILocation = new GameObject("GreetGoonsEntry_POI").transform;
            greetGoons.PoILocation.transform.SetParent(greetGoons.transform);
            greetGoons.AutoUpdatePoILocation = true;
            greetGoons.SetState(EQuestState.Inactive, false);

            persuadeCartel.SetEntryTitle("Try persuading the Westville Cartel Dealer");
            persuadeCartel.ParentQuest = this;
            persuadeCartel.CompleteParentQuest = false;
            persuadeCartel.PoILocation = new GameObject("PersuadeCartelEntry_POI").transform;
            persuadeCartel.PoILocation.SetParent(westvilleDealer.transform);
            persuadeCartel.PoILocation.transform.localPosition = Vector3.zero;
            persuadeCartel.AutoUpdatePoILocation = true;
            persuadeCartel.SetState(EQuestState.Inactive, false);
           
            UnityEngine.Events.UnityAction persuadeCartelAction = null;
            void OnPersuadeCartelComplete()
            {
                if (persuadeCartel != null && persuadeCartel.State == EQuestState.Failed) return;
                if (hireCartel == null) return;

                hireCartel.Begin();
                UpdateQuestMapLogo(hireCartel);

                if (persuadeCartelAction != null)
                {
                    persuadeCartel.onComplete.RemoveListener(persuadeCartelAction);
                    persuadeCartelAction = null;
                }
            }
            persuadeCartelAction = (UnityEngine.Events.UnityAction)OnPersuadeCartelComplete;
            persuadeCartel.onComplete.AddListener(persuadeCartelAction);

            hireCartel.SetEntryTitle("Hire the Westville Cartel Dealer");
            hireCartel.ParentQuest = this;
            hireCartel.CompleteParentQuest = false;
            hireCartel.PoILocation = new GameObject("HireCartelEntry_POI").transform;
            hireCartel.PoILocation.SetParent(westvilleDealer.transform);
            hireCartel.PoILocation.transform.localPosition = Vector3.zero;
            hireCartel.AutoUpdatePoILocation = true;
            hireCartel.SetState(EQuestState.Inactive, false);

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;

            var action = OnMinPass;

#if MONO
            instance.onMinutePass.Add(new Action(action));
#else
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            StartQuestDetail();
        }

        private void StartQuestDetail()
        {
            SetupHUDUI();

            if (hudUI != null)
            {
                if (hudUI.MainLabel != null)
                    this.hudUI.MainLabel.text = "Truced Recruits";
                this.hudUI.gameObject.SetActive(true);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            UpdateQuestMapLogo(QuestEntry_FindCartel);
            return;
        }

        public override void OnMinPass()
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
            base.OnMinPass();
#endif
            if (westvilleDealer == null) return;


            // Check for optional greet gathering goons quest entry
            if (QuestEntry_GreetGoons != null && QuestEntry_GreetGoons.State == EQuestState.Active)
            {
                // if gathering is not active -> set questentrystate expired, disable poi+compass
                if (!areGoonsGathering)
                {
                    QuestEntry_GreetGoons.SetState(EQuestState.Expired, false);
                }
            }
            else if (QuestEntry_GreetGoons != null && (QuestEntry_GreetGoons.State == EQuestState.Inactive || QuestEntry_GreetGoons.State == EQuestState.Expired))
            {
                // Inactive / expire should check for gathering active -> active this + poi,compass
                if (areGoonsGathering && currentGatheringLocation != null)
                {
                    QuestEntry_GreetGoons.Begin();
                    UpdateQuestMapLogo(QuestEntry_GreetGoons);
                    if (currentGatheringLocation != null && currentGatheringLocation.position != null)
                        QuestEntry_GreetGoons.SetPoILocation(currentGatheringLocation.position);
                }
            }

            // Find, Persuade, Hire Cartel Dealer Entries
            if (QuestEntry_FindCartel != null && QuestEntry_FindCartel.State == EQuestState.Active)
            {
                if (!westvilleDealer.isInBuilding && Player.Local.IsPointVisibleToPlayer(westvilleDealer.CenterPoint, maxDistance_Visible: 20f, minDistance_Invisible: 1f))
                {
                    QuestEntry_FindCartel.Complete();
                    return;
                }
            }

            if (QuestEntry_PersuadeCartelDealer != null && QuestEntry_PersuadeCartelDealer.State == EQuestState.Active)
            {
                if (westvilleDealer.HasBeenRecommended)
                {
                    QuestEntry_PersuadeCartelDealer.Complete();
                    return;
                }
            }

            if (QuestEntry_HireCartelDealer != null && QuestEntry_HireCartelDealer.State == EQuestState.Active)
            {
                if (westvilleDealer.IsRecruited)
                {
                    if (!(SaveManager.Instance.IsSaving || isSaving))
                    {
                        alliedQuests.alliedIntroCompleted = true;
                        Complete();
                    }
                }
            }
        }

        public QuestEntry QuestEntry_FindCartel;
        public QuestEntry QuestEntry_GreetGoons;
        public QuestEntry QuestEntry_PersuadeCartelDealer;
        public QuestEntry QuestEntry_HireCartelDealer;

    }
}
