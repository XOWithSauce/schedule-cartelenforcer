using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.SuppliesModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.PlayerScripts;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using FishNet;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.PlayerScripts;
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
    public class Quest_AlliedSupplies : ModQuestBase
    {
        protected readonly QuestHelperBase<Quest_AlliedSupplies> _helper;
#if MONO
        public Quest_AlliedSupplies()
        {
            _helper = new QuestHelperBase<Quest_AlliedSupplies>(this);
        }
#else
        public Quest_AlliedSupplies(IntPtr ptr) : base(ptr)
        {
            _helper = new QuestHelperBase<Quest_AlliedSupplies>(this);
        }
        public Quest_AlliedSupplies() : base(ClassInjector.DerivedConstructorPointer<Quest_AlliedSupplies>())
            => ClassInjector.DerivedConstructorBody(this);

        public new string title;
        public new string Subtitle;
#endif
        public RectTransform groupRt;

        public SupplyLocation location;
        public bool playerNoticed = false;
        public bool playerInterrogated = false;
        public bool interrogatingPlayer = false;

        public QuestEntry QuestEntry_LocateSupplies;
        private UnityAction _locateSuppliesAction;

        public QuestEntry QuestEntry_GatherSupplies;

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

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            GameDateTime questExpiry = new GameDateTime(_elapsedDays: instance.ElapsedDays + 1, _time: 401);

            _helper.InitializeQuest("Allied Supplies", xp: 300, expires: true, expiry: questExpiry, trackOnBegin: false);

            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";

            this.location = supplyLocations[UnityEngine.Random.Range(0, supplyLocations.Count)];

            _locateSuppliesAction = (UnityAction)OnLocateSuppliesComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_LocateSupplies,
                name: "LocateSupplies",
                title: "Read Thomas' message and locate the Cartel supplies",
                new PoIConfig(false, false, false),
                _locateSuppliesAction);

            Vector3 gatherSuppliesPoIPosition = this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position;
            _helper.InitializeQuestEntry(ref QuestEntry_GatherSupplies,
                name: "GatherSupplies",
                title: "Receive the Cartel supplies",
                new PoIConfig(true, false, false, poiPosition: gatherSuppliesPoIPosition));

            _helper.StartQuestFromEntry(QuestEntry_LocateSupplies);

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

#if MONO
            instance.onMinutePass.Add(new Action(OnMinPass));
#else
            instance.onMinutePass += (Il2CppSystem.Action)OnMinPass;
#endif
            coros.Add(MelonCoroutines.Start(SpawnSupply(this.location)));
        }

        public override void OnMinPass()
        {
            if (!registered || Singleton<SaveManager>.Instance.IsSaving || !alliedSuppliesActive || this.State != EQuestState.Active) return;
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

            Subtitle = $"\n<color=#757575>{GetExpiryText()} until supplies vanish</color>";
#if MONO
            base.OnMinPass();
#else
            UpdateQuestHUD();
            CheckExpiry();
            if (State != EQuestState.Active) return;
#endif

            if (QuestEntry_LocateSupplies != null && QuestEntry_LocateSupplies.State == EQuestState.Active)
            {
                if (Vector3.Distance(
                    a: Player.Local.CenterPointTransform.position,
                    b: this.location.Type == ESupplyType.Van ? this.location.CarPosition : this.location.BarrelObjects[0].transform.position
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
                if (this.location.Type == ESupplyType.Barrel)
                {
                    Vector3 nextBarrel = Vector3.zero;
                    Transform currentBarrel;
                    int consumedBarrels = 0;
                    foreach (GameObject go in this.location.BarrelObjects)
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

                    if (consumedBarrels == this.location.BarrelObjects.Count)
                    {
                        suppliesClaimed = true;
                    }
                    else if (nextBarrel != Vector3.zero)
                    {
                        if (QuestEntry_GatherSupplies.PoI != null && QuestEntry_GatherSupplies.PoI.gameObject != null)
                            QuestEntry_GatherSupplies.SetPoILocation(nextBarrel);
                    }
                }

                if (suppliesClaimed)
                    Complete(false);
            }
            return;
        }
        
        public void OnLocateSuppliesComplete()
        {
            if (QuestEntry_LocateSupplies != null && QuestEntry_LocateSupplies.State == EQuestState.Failed) return;
            if (QuestEntry_GatherSupplies == null) return;

            QuestEntry_GatherSupplies.Begin();
            UpdateQuestMapLogo(QuestEntry_GatherSupplies);

            QuestEntry_GatherSupplies.SetPoILocation(location: location.Type == ESupplyType.Van ? location.CarPosition : location.BarrelObjects[0].transform.position);
        }
    }
}
