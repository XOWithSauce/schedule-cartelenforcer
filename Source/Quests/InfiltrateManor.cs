using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.RandomManorGenerator;

#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Levelling;
using ScheduleOne.NPCs;
using FishNet;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.NPCs;
using Il2CppFishNet;
using Il2CppInterop.Runtime.Injection;
#endif


namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_InfiltrateManor : ModQuestBase
    {
        protected InfiltrateManorHelper _helper;
#if MONO
        public Quest_InfiltrateManor()
        {
            _helper = new InfiltrateManorHelper(this);
        }
#else
        public Quest_InfiltrateManor(IntPtr ptr) : base(ptr) 
        {
            _helper = new InfiltrateManorHelper(this);
        }

        public Quest_InfiltrateManor() : base(ClassInjector.DerivedConstructorPointer<Quest_InfiltrateManor>())
            => ClassInjector.DerivedConstructorBody(this);

        public new string title;
        public new string Subtitle;
#endif

        private float questDifficultyScalar = 1f;
        private int manorGoonsAlive = 4;

        private readonly List<Vector3> forestSearchLocs = new()
        {
            new Vector3(164.96f, 3.10f, -32.65f),
            new Vector3(151.45f, 3.20f, -35.21f),
            new Vector3(139.41f, 1.96f, -45.71f),
            new Vector3(135.26f, 2.72f, -66.56f)
        };

        private readonly Dictionary<Vector3, bool> roomsPositions = new()
        {
            { new Vector3(166.58f, 15.61f, -52.99f), false },
            { new Vector3(160.65f, 15.61f, -52.97f), false },
            { new Vector3(160.65f, 15.61f, -61.00f), false },
            { new Vector3(166.58f, 15.61f, -61.00f), false }
        };

        // store the combat variables
        public bool hasSavedCombatVariables = false;
        public float GiveUpRange = 0f;
        public int GiveUpAfterSuccessfulHits = 0;
        public float DefaultSearchTime = 0f;

        // store bool flag for spawning randomly 1 goon in forest
        private bool forestGoonSpawned = false;

        private int forestPosSearched = 0; // indexing for search location 

        private int roomsVisited = 0; // indexing for search location 

        public bool isJukeboxPlaying = false;

        public QuestEntry QuestEntry_InvestigateWoods;
        private UnityAction _investigateAction;

        public QuestEntry QuestEntry_ReturnToRay;
        private UnityAction _returnToRayAction;

        public QuestEntry QuestEntry_WaitForNight;
        private UnityAction _waitForNightAction;

        public QuestEntry QuestEntry_BreakIn;
        private UnityAction _breakInAction;

        public QuestEntry QuestEntry_DefeatManorGoons;
        private UnityAction _defeatGoonsAction;

        public QuestEntry QuestEntry_SearchResidence;
        private UnityAction _searchAction;

        public QuestEntry QuestEntry_EscapeManor;


        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only
        public override void Complete(bool network = true)
        {
            Log("Quest_InfiltrateManor: Complete method called.");
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

                Log("Quest_InfiltrateManor: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_InfiltrateManor: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_InfiltrateManor: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_InfiltrateManor: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_InfiltrateManor: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            Log("Quest_InfiltrateManor: End method called.");
            try
            {
                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;

                var action = (Action)OnMinPass;

#if MONO
                instance.onHourPass = (Action)Delegate.Remove(instance.onHourPass, new Action(this.HourPass));
                instance.onMinutePass.Remove(action);
#else
                instance.onHourPass -= (Il2CppSystem.Action)this.HourPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)action);
#endif

                Log("Quest_InfiltrateManor: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_InfiltrateManor: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }

        #endregion

        public void SetupSelf()
        {
            // calc difficulty scalar
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
            questDifficultyScalar = 1f + allInfluenceNormalized;

            _helper.InitializeQuest("Infiltrate Manor", xp: Mathf.RoundToInt(600f * questDifficultyScalar));

            _investigateAction = (UnityAction)OnInvestigateComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_InvestigateWoods,
                name: "InvestigateWoods",
                title: "Investigate the hillside forest near Manor (0/4)",
                new PoIConfig(true, false, false, poiPosition: new Vector3(164.96f, 3.10f, -32.65f)),
                _investigateAction);

            _returnToRayAction = (UnityAction)OnReturnToRayComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_ReturnToRay,
                name: "ReturnToRay",
                title: "Return to Ray and ask for more information",
                new PoIConfig(true, false, false, poiPosition: new Vector3(77.30f, 1.46f, -12.85f)),
                _returnToRayAction);

            _waitForNightAction = (UnityAction)OnWaitForNightComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_WaitForNight,
                name: "WaitForNight",
                title: "Wait for night time (22:00)",
                new PoIConfig(false, false, false),
                _waitForNightAction);

            _breakInAction = (UnityAction)OnBreakInComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_BreakIn,
                name: "BreakIn",
                title: "Break into Manor through the back door",
                new PoIConfig(true, false, false, poiPosition: new Vector3(163.37f, 11.86f, -50.12f)),
                _breakInAction);

            _defeatGoonsAction = (UnityAction)OnDefeatGoonsComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_DefeatManorGoons,
                name: "DefeatGoons",
                title: "Defeat the Manor Goons",
                new PoIConfig(false, false, false),
                _defeatGoonsAction);

            _searchAction = (UnityAction)OnSearchResidenceCompete;
            _helper.InitializeQuestEntry(ref QuestEntry_SearchResidence,
                name: "SearchResidence",
                title: "Investigate the upstairs rooms (0/4)",
                new PoIConfig(true, false, false),
                _searchAction);

            _helper.InitializeQuestEntry(ref QuestEntry_EscapeManor,
                name: "EscapeManor",
                title: "Escape the Manor before the Police arrive",
                new PoIConfig(false, false, false));

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            var action = (Action)OnMinPass;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(HourPass));
            instance.onMinutePass.Add(action);
#else
            instance.onHourPass += (Il2CppSystem.Action)HourPass;
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            _helper.StartQuestFromEntry(QuestEntry_InvestigateWoods);
        }

        public void SpawnManorGoons()
        {
            Log("Roompos Keys to list");
            List<Vector3> roomPositionsList = roomsPositions.Keys.ToList();

            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count == 0)
            {
                foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
                {
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count >= 3) break;
                    if (!NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(goon)) continue;

                    if (goon.IsGoonSpawned && (goon.Health.IsDead || goon.Health.IsKnockedOut))
                    {
                        goon.Despawn();
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Log("SpawnGoon");
                CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(roomPositionsList[i]);
                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
                goon.Behaviour.ScheduleManager.DisableSchedule();
                goon.IsGoonSpawned = true;
                if (!goon.gameObject.activeSelf)
                {
                    goon.gameObject.SetActive(true);
                }
                if (!goon.Avatar.enabled || !goon.Avatar.gameObject.activeSelf)
                {
                    goon.Avatar.gameObject.SetActive(true);
                    goon.Avatar.enabled = true;
                }

                SetupGoonWeapon(goon);
                goon.Inventory.AddCash(Mathf.Round(UnityEngine.Random.Range(500f * questDifficultyScalar, 1300f * questDifficultyScalar)));
                goon.NPCData.Health.MaxHealth = Mathf.Round(Mathf.Lerp(150f, 300f, questDifficultyScalar - 1f));
                goon.Health.Health = Mathf.Round(Mathf.Lerp(150f, 300f, questDifficultyScalar - 1f));

                UnityEngine.Events.UnityAction onGoonDiedAction = null;
                void onGoonDie()
                {
                    manorGoonsAlive--;
                    if (onGoonDiedAction != null)
                    {
                        goon.Health.onDieOrKnockedOut.RemoveListener(onGoonDiedAction);
                        onGoonDiedAction = null;
                    }
                }
                onGoonDiedAction = (UnityEngine.Events.UnityAction)onGoonDie;
                goon.Health.onDieOrKnockedOut.AddListener(onGoonDiedAction);

                if (!hasSavedCombatVariables)
                {
                    GiveUpRange = goon.Behaviour.CombatBehaviour.GiveUpRange;
                    GiveUpAfterSuccessfulHits = goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                    DefaultSearchTime = goon.Behaviour.CombatBehaviour.DefaultSearchTime;
                    hasSavedCombatVariables = true;
                }

                goon.Behaviour.CombatBehaviour.GiveUpRange = 60f;
                goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 60;
                goon.Behaviour.CombatBehaviour.DefaultSearchTime = 120f;

                goon.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
                goon.Behaviour.CombatBehaviour.Enable_Networked();

                float speed = Mathf.Lerp(UnityEngine.Random.Range(0.42f, 0.55f), 0.67f, questDifficultyScalar - 1f);
                goon.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, speed));
                goon.Movement.Agent.avoidancePriority = 30;
                manorGoons.Add(goon);
                manorGoonGuids.Add(goon.GUID.ToString());
            }

            manorGoonsAlive = manorGoons.Count();

            return;
        }
        private void SetupGoonWeapon(CartelGoon goon)
        {

            goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
            if (goon.Behaviour.CombatBehaviour.currentWeapon != null)
            {
                AvatarRangedWeapon wep = null;
#if MONO
                wep = goon.Behaviour.CombatBehaviour.currentWeapon as AvatarRangedWeapon;
#else
                wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
#endif
                if (wep != null)
                {
                    float dmgMin = 26f;
                    float dmgMax = 45f;
                    float t = Mathf.Clamp01(questDifficultyScalar - 1f);
                    wep.CanShootWhileMoving = true;
                    wep.AimTime_Max = 0.3f;
                    wep.AimTime_Min = 0.1f;
                    wep.HitChance_MaxRange = 65f;
                    wep.HitChance_MinRange = 85f;
                    wep.MaxFireRate = Mathf.Lerp(0.7f, 1.2f, t);
                    wep.MaxMovingShotsBeforeReposition = 3;
                    wep.MaxStationaryShotsBeforeReposition = 1;
                    wep.MaxUseRange = 7f;
                    wep.MinUseRange = 0.1f;
                    wep.CooldownDuration = Mathf.Lerp(0.7f, 1.2f, t);
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));
                }
            }
        }
        private void SpawnForestGoon()
        {
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count == 0) return;
            Log("Spawning forest goon");

            CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(forestSearchLocs[forestSearchLocs.Count - 1]);
            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goon.Behaviour.ScheduleManager.DisableSchedule();
            if (!goon.gameObject.activeSelf)
            {
                goon.gameObject.SetActive(true);
            }
            if (!goon.Avatar.enabled || !goon.Avatar.gameObject.activeSelf)
            {
                goon.Avatar.gameObject.SetActive(true);
                goon.Avatar.enabled = true;
            }

            goon.Inventory.AddCash(Mathf.Round(UnityEngine.Random.Range(500f * questDifficultyScalar, 1300f * questDifficultyScalar)));
            goon.NPCData.Health.MaxHealth = Mathf.Round(Mathf.Lerp(35f, 85f, questDifficultyScalar - 1f));
            goon.Health.Health = Mathf.Round(Mathf.Lerp(35f, 85f, questDifficultyScalar - 1f));

            goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");

            if (goon.Behaviour.CombatBehaviour.currentWeapon != null)
            {
                AvatarMeleeWeapon wep = null;
#if MONO
                wep = goon.Behaviour.CombatBehaviour.currentWeapon as AvatarMeleeWeapon;
#else
                wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarMeleeWeapon>();
#endif
                if (wep != null)
                {
                    float dmgMin = 66f;
                    float dmgMax = 87f;
                    float t = Mathf.Clamp01(questDifficultyScalar - 1f);
                    wep.MaxUseRange = 2.3f;
                    wep.MinUseRange = 0.2f;
                    wep.AttackRadius = Mathf.Lerp(2.3f, 3.2f, t);
                    wep.AttackRange = Mathf.Lerp(2.3f, 3.2f, t);
                    wep.CooldownDuration = Mathf.Lerp(0.6f, 1.2f, t);
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));
                }

            }
            goon.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
            goon.Behaviour.CombatBehaviour.Enable_Networked();

            float speed = Mathf.Lerp(UnityEngine.Random.Range(0.55f, 0.76f), 0.86f, questDifficultyScalar - 1f);
            goon.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, speed));
            goon.Movement.Agent.avoidancePriority = 30;
            goon.transform.localScale = new Vector3(0.81f, 0.81f, 0.81f);
            coros.Add(MelonCoroutines.Start(_helper.DespawnForestGoon(goon)));
        }

        public override void OnMinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || manorCompleted || this.State != EQuestState.Active) return;
#if MONO
            base.OnMinPass();
#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_InvestigateWoods != null && QuestEntry_InvestigateWoods.State == EQuestState.Active)
            {
                QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor ({forestPosSearched}/4)");

                if (forestPosSearched > 0 && forestPosSearched < 3 && !forestGoonSpawned && UnityEngine.Random.Range(0f, 1f) > 0.90f)
                {
                    forestGoonSpawned = true;
                    SpawnForestGoon();
                }

                if (forestSearchLocs == null) return;

                if (forestPosSearched == 4)
                {
                    QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor (4/4)");
                    QuestEntry_InvestigateWoods.Complete();
                    return;
                }

                if (forestPosSearched < forestSearchLocs.Count && Vector3.Distance(Player.Local.CenterPointTransform.position, forestSearchLocs[forestPosSearched]) < 5f)
                {
                    forestPosSearched++;
                    QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor ({forestPosSearched}/4)");

                    if (forestPosSearched < forestSearchLocs.Count)
                    {
                        QuestEntry_InvestigateWoods.SetPoILocation(forestSearchLocs[forestPosSearched]);
                    }
                    else
                    {
                        QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor (4/4)");
                        QuestEntry_InvestigateWoods.Complete();
                        return;
                    }
                }

                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200)
                {
                    manorCompleted = true;
                    QuestEntry_InvestigateWoods.SetState(EQuestState.Failed);
                    coros.Add(MelonCoroutines.Start(ResetRayAFK()));
                    this.Fail();
                    return;
                }

                return;
            }
            else if (QuestEntry_ReturnToRay != null && QuestEntry_ReturnToRay.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200)
                {
                    manorCompleted = true;
                    QuestEntry_ReturnToRay.SetState(EQuestState.Failed);
                    coros.Add(MelonCoroutines.Start(ResetRayAFK()));
                    this.Fail();
                    return;
                }
                return;
            }
            else if (QuestEntry_DefeatManorGoons != null && QuestEntry_DefeatManorGoons.State == EQuestState.Active)
            {
                if (manorGoons != null && manorGoons.Count > 0 && manorGoonsAlive == 0)
                {
                    QuestEntry_DefeatManorGoons.Complete();
                }
                if (Vector3.Distance(QuestEntry_BreakIn.PoILocation.position, Player.Local.CenterPointTransform.position) > 70f)
                {
                    manorCompleted = true;

                    coros.Add(MelonCoroutines.Start(CleanupManor()));
                    this.Fail();
                    return;
                }

                if (TimeManager.Instance.CurrentTime >= 2200 || TimeManager.Instance.CurrentTime <= 359)
                {
                    // in time window do nothing
                }
                else
                {
                    manorCompleted = true;
                    QuestEntry_DefeatManorGoons.SetState(EQuestState.Failed);
                    Log("Fail Timeout Manor Infiltration Quest");
                    coros.Add(MelonCoroutines.Start(CleanupManor()));
                    this.Fail();
                    return;
                    // Means that from state start you have atleast 6 hours to complete from 22:00 to 3:59
                }
                return;
            }
            else if (QuestEntry_SearchResidence != null && QuestEntry_SearchResidence.State == EQuestState.Active)
            {
                QuestEntry_SearchResidence.SetEntryTitle($"Investigate the upstairs rooms ({roomsVisited}/4)");

                bool allRoomsVisited = true;
                foreach (var roomEntry in roomsPositions)
                {
                    Vector3 roomPosition = roomEntry.Key;
                    bool hasBeenVisited = roomEntry.Value;

                    if (!hasBeenVisited)
                    {
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, roomPosition) < 1.85f)
                        {
                            roomsPositions[roomPosition] = true;
                            roomsVisited++;
                            break;
                        }
                    }
                }

                foreach (var roomEntry in roomsPositions)
                {
                    if (!roomEntry.Value)
                    {
                        QuestEntry_SearchResidence.SetPoILocation(roomEntry.Key);
                        allRoomsVisited = false;
                        break;
                    }
                }

                if (allRoomsVisited)
                {
                    QuestEntry_SearchResidence.Complete();
                    return;
                }

                if (TimeManager.Instance.CurrentTime >= 2200 || TimeManager.Instance.CurrentTime <= 359)
                {
                    // in time window do nothing
                }
                else
                {
                    manorCompleted = true;
                    QuestEntry_SearchResidence.SetState(EQuestState.Failed);
                    Log("Fail Timeout Manor Infiltration Quest");
                    coros.Add(MelonCoroutines.Start(CleanupManor()));
                    this.Fail();
                    return;
                    // Means that from state start you have atleast 6 hours to complete from 22:00 to 3:59
                }
                return;
            }
            else if (QuestEntry_EscapeManor != null && QuestEntry_EscapeManor.State == EQuestState.Active)
            {
                if (Player.Local.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None)
                {
                    // Not being hunted
                    if (Vector3.Distance(QuestEntry_BreakIn.PoILocation.position, Player.Local.CenterPointTransform.position) > 70f)
                    {
                        manorCompleted = true;
                        // Far enough escaped from the back door position
                        coros.Add(MelonCoroutines.Start(CleanupManor()));
                        coros.Add(MelonCoroutines.Start(QuestManorReward()));
                        this.Complete();
                        return;
                    }
                }

                if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None && Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.Investigating)
                {
                    manorCompleted = true;

                    QuestEntry_EscapeManor.SetState(EQuestState.Failed);
                    // Not none and not investigating means that player has been spotted by police atleast once
                    coros.Add(MelonCoroutines.Start(CleanupManor()));
                    this.Fail();
                    return;
                }

                return;
            }
        }
        private void HourPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || manorCompleted || this.State != EQuestState.Active) return;

            Log("HourPass In Quest");
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_WaitForNight != null && QuestEntry_WaitForNight.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 2359)
                {
                    QuestEntry_WaitForNight.Complete();
                }
            }
        }

        private void OnInvestigateComplete()
        {
            if (QuestEntry_InvestigateWoods != null && QuestEntry_InvestigateWoods.State == EQuestState.Failed) return;
            if (QuestEntry_ReturnToRay == null) return;

            QuestEntry_ReturnToRay.Begin();
            UpdateQuestMapLogo(QuestEntry_ReturnToRay);
            QuestEntry_ReturnToRay.SetPoILocation(ray.transform.position);

            coros.Add(MelonCoroutines.Start(GenRaySecondDialog(QuestEntry_ReturnToRay.Complete)));
            if (_investigateAction != null)
            {
                QuestEntry_InvestigateWoods.onComplete.RemoveListener(_investigateAction);
                _investigateAction = null;
            }
        }
        private void OnReturnToRayComplete()
        {
            if (QuestEntry_ReturnToRay != null && QuestEntry_ReturnToRay.State == EQuestState.Failed) return;
            if (QuestEntry_WaitForNight == null) return;

            QuestEntry_WaitForNight.Begin();
            if (QuestEntry_WaitForNight.compassElement != null)
                QuestEntry_WaitForNight.compassElement.Visible = false;
            coros.Add(MelonCoroutines.Start(RandomManorGenerator.SetupManor()));
            if (_returnToRayAction != null)
            {
                QuestEntry_ReturnToRay.onComplete.RemoveListener(_returnToRayAction);
                _returnToRayAction = null;
            }
        }
        private void OnWaitForNightComplete()
        {
            if (QuestEntry_WaitForNight != null && QuestEntry_WaitForNight.State == EQuestState.Failed) return;
            if (QuestEntry_BreakIn == null) return;

            QuestEntry_BreakIn.Begin();
            UpdateQuestMapLogo(QuestEntry_BreakIn);
            if (_waitForNightAction != null)
            {
                QuestEntry_WaitForNight.onComplete.RemoveListener(_waitForNightAction);
                _waitForNightAction = null;
            }
        }
        private void OnBreakInComplete()
        {
            if (QuestEntry_BreakIn != null && QuestEntry_BreakIn.State == EQuestState.Failed) return;
            if (QuestEntry_DefeatManorGoons == null) return;

            SpawnManorGoons();
            QuestEntry_DefeatManorGoons.Begin();
            if (QuestEntry_DefeatManorGoons.compassElement != null)
                QuestEntry_DefeatManorGoons.compassElement.Visible = false;
            if (_breakInAction != null)
            {
                QuestEntry_BreakIn.onComplete.RemoveListener(_breakInAction);
                _breakInAction = null;
            }
        }
        private void OnDefeatGoonsComplete()
        {
            if (QuestEntry_DefeatManorGoons != null && QuestEntry_DefeatManorGoons.State == EQuestState.Failed) return;
            if (QuestEntry_SearchResidence == null) return;

            QuestEntry_SearchResidence.Begin();
            UpdateQuestMapLogo(QuestEntry_SearchResidence);
            QuestEntry_SearchResidence.SetPoILocation(roomsPositions.Keys.FirstOrDefault());

            if (_defeatGoonsAction != null)
            {
                QuestEntry_DefeatManorGoons.onComplete.RemoveListener(_defeatGoonsAction);
                _defeatGoonsAction = null;
            }
        }
        private void OnSearchResidenceCompete()
        {
            if (QuestEntry_SearchResidence != null && QuestEntry_SearchResidence.State == EQuestState.Failed) return;
            if (QuestEntry_EscapeManor == null) return;

            QuestEntry_EscapeManor.Begin();
            if (QuestEntry_EscapeManor.compassElement != null)
                QuestEntry_EscapeManor.compassElement.Visible = false;
            Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Investigating);

            // Note: not promised that will dispatch
#if MONO
            PoliceStation.PoliceStations.FirstOrDefault().Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#else
            PoliceStation.PoliceStations[0].Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#endif
            if (_searchAction != null)
            {
                QuestEntry_SearchResidence.onComplete.RemoveListener(_searchAction);
                _searchAction = null;
            }

            Log("Escape Begun");
            try
            {
                if (activeJukebox.IsPlaying)
                    OnJukeboxStateChange();
            }
            catch (Exception ex) // Because it seems it can fail silently in mono?
            {
                Log(ex.Message);
            }
        }

    }

    // Quest coroutine wrapper
    public class InfiltrateManorHelper : QuestHelperBase<Quest_InfiltrateManor>
    {
        public InfiltrateManorHelper(Quest_InfiltrateManor quest) : base(quest) { }
        public IEnumerator DespawnForestGoon(CartelGoon goon)
        {
            int maxWaitMins = 2;
            for (int i = 0; i < 60 * maxWaitMins; i++)
            {
                yield return Wait1;
                if (!registered) yield break;
                if (goon.Health.IsDead || goon.Health.IsKnockedOut) break;
                if (goon.Behaviour.activeBehaviour == null || goon.Behaviour.activeBehaviour != goon.Behaviour.CombatBehaviour) break;
            }
            yield return Wait30;
            goon.NPCData.Health.MaxHealth = 100f;
            goon.Health.Health = 100f;
            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
            goon.Behaviour.ScheduleManager.EnableSchedule();
            if (goon.IsGoonSpawned)
                goon.Despawn();
            goon.Movement.SpeedController.RemoveSpeedControl("combat");
            yield break;
        }

    }
}
