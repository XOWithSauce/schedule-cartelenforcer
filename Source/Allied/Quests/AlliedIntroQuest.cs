

using UnityEngine;
using UnityEngine.Events;
using MelonLoader;

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
    [RegisterTypeInIl2Cpp]
    public class Quest_TrucedRecruits : ModQuestBase
    {
        protected readonly QuestHelperBase<Quest_TrucedRecruits> _helper;
#if MONO
        public Quest_TrucedRecruits()
        {
            _helper = new QuestHelperBase<Quest_TrucedRecruits>(this);
        }
#else
        public Quest_TrucedRecruits(IntPtr ptr) : base(ptr) 
        { 
            _helper = new QuestHelperBase<Quest_TrucedRecruits>(this);
        }
        public Quest_TrucedRecruits() : base(ClassInjector.DerivedConstructorPointer<Quest_TrucedRecruits>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private CartelDealer westvilleDealer = null;

        public QuestEntry QuestEntry_FindCartel;
        private UnityAction _findCartelAction;

        public QuestEntry QuestEntry_GreetGoons;

        public QuestEntry QuestEntry_PersuadeCartelDealer;
        private UnityAction _persuadeCartelAction;

        public QuestEntry QuestEntry_HireCartelDealer;

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
            _helper.InitializeQuest("Truced Recruits", xp: 200);

            foreach (CartelDealer d in UnityEngine.Object.FindObjectsOfType<CartelDealer>(true))
            {
                if (d.Region == EMapRegion.Westville)
                {
                    westvilleDealer = d;
                    break;
                }
            }

            _findCartelAction = (UnityEngine.Events.UnityAction)OnFindCartelComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_FindCartel,
                name: "FindCartel",
                title: "Find the Westville Cartel Dealer",
                new PoIConfig(true, false, false, poiPosition: westvilleDealer.CenterPoint),
                _findCartelAction);

            _helper.InitializeQuestEntry(ref QuestEntry_GreetGoons,
                name: "GreetGoons",
                title: "(Optional) Say greetings to all 3 gathering goons",
                new PoIConfig(true, false, false));

            _persuadeCartelAction = (UnityEngine.Events.UnityAction)OnPersuadeComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_PersuadeCartelDealer,
                name: "PersuadeCartel",
                title: "Try persuading the Westville Cartel Dealer",
                new PoIConfig(true, true, true, westvilleDealer.transform, Vector3.zero),
                _persuadeCartelAction);

            _helper.InitializeQuestEntry(ref QuestEntry_HireCartelDealer,
                name: "HireCartel",
                title: "Hire the Westville Cartel Dealer",
                new PoIConfig(true, true, true, westvilleDealer.transform, Vector3.zero));

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;

            var action = OnMinPass;
#if MONO
            instance.onMinutePass.Add(new Action(action));
#else
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif

            _helper.StartQuestFromEntry(QuestEntry_FindCartel);
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

        public void OnFindCartelComplete()
        {
            if (QuestEntry_FindCartel != null && QuestEntry_FindCartel.State == EQuestState.Failed) return;
            if (QuestEntry_PersuadeCartelDealer == null) return;

            QuestEntry_PersuadeCartelDealer.SetState(EQuestState.Active, false);
            UpdateQuestMapLogo(QuestEntry_PersuadeCartelDealer);

            if (_findCartelAction != null)
            {
                QuestEntry_FindCartel.onComplete.RemoveListener(_findCartelAction);
                _findCartelAction = null;
            }

        }

        public void OnPersuadeComplete()
        {
            if (QuestEntry_PersuadeCartelDealer != null && QuestEntry_PersuadeCartelDealer.State == EQuestState.Failed) return;
            if (QuestEntry_HireCartelDealer == null) return;

            QuestEntry_HireCartelDealer.Begin();
            UpdateQuestMapLogo(QuestEntry_HireCartelDealer);

            if (QuestEntry_PersuadeCartelDealer != null)
            {
                QuestEntry_PersuadeCartelDealer.onComplete.RemoveListener(_persuadeCartelAction);
                _persuadeCartelAction = null;
            }
        }
    }

}
