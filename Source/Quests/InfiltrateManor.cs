

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
    public class Quest_InfiltrateManor : Quest
    {
#if IL2CPP
        public Quest_InfiltrateManor(IntPtr ptr) : base(ptr) { }

        public Quest_InfiltrateManor() : base(ClassInjector.DerivedConstructorPointer<Quest_InfiltrateManor>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;
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
        public float GiveUpRange = 0f;
        public int GiveUpAfterSuccessfulHits = 0;
        public float DefaultSearchTime = 0f;

        // store bool flag for spawning randomly 1 goon in forest
        private bool forestGoonSpawned = false;

        private int forestPosSearched = 0; // indexing for search location 

        private int roomsVisited = 0; // indexing for search location 

        public bool isJukeboxPlaying = false;

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


#if IL2CPP
        // Because by default the property uses this.Entries in Enumberable.Count, which probably causes the bug when the this.entries is il2cpp system ienumerable but expecting system ienumerable?
        public new int ActiveEntryCount
        {
            get
            {
                if (this.Entries == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (var entry in this.Entries)
                {
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
            Log("SetupSelfStart");
            // calc difficulty scalar
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
            questDifficultyScalar = 1f + allInfluenceNormalized;

            Log("QuestInit");
            this.name = "Quest_InfiltrateManor";
            Expires = false;
            title = "Infiltrate Manor";
            CompletionXP = Mathf.RoundToInt(600f * questDifficultyScalar);
            Description = "Find info about Thomas Benzie and break into Manor";
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

            GameObject investigateWoodsObject = new GameObject("QuestEntry_InvestigateWoods");
            investigateWoodsObject.transform.SetParent(this.transform);

            GameObject returnToRayObject = new GameObject("QuestEntry_ReturnToRay");
            returnToRayObject.transform.SetParent(this.transform);

            GameObject waitForNightObject = new GameObject("QuestEntry_WaitForNight");
            waitForNightObject.transform.SetParent(this.transform);

            GameObject breakInObject = new GameObject("QuestEntry_BreakIn");
            breakInObject.transform.SetParent(this.transform);

            GameObject defeatGoonsObject = new GameObject("QuestEntry_DefeatManorGoons");
            defeatGoonsObject.transform.SetParent(this.transform);

            GameObject searchResidenceObject = new GameObject("QuestEntry_SearchResidence");
            searchResidenceObject.transform.SetParent(this.transform);

            GameObject escapeManorObject = new GameObject("QuestEntry_EscapeManor");
            escapeManorObject.transform.SetParent(this.transform);

            QuestEntry investigate = investigateWoodsObject.AddComponent<QuestEntry>();
            QuestEntry returnToRay = returnToRayObject.AddComponent<QuestEntry>();
            QuestEntry waitForNight = waitForNightObject.AddComponent<QuestEntry>();
            QuestEntry breakIn = breakInObject.AddComponent<QuestEntry>();
            QuestEntry defeatGoons = defeatGoonsObject.AddComponent<QuestEntry>();
            QuestEntry searchResidence = searchResidenceObject.AddComponent<QuestEntry>();
            QuestEntry escapeManor = escapeManorObject.AddComponent<QuestEntry>();

            Log("Setting Entries");
            this.QuestEntry_InvestigateWoods = investigate;
            this.QuestEntry_ReturnToRay = returnToRay;
            this.QuestEntry_WaitForNight = waitForNight;
            this.QuestEntry_BreakIn = breakIn;
            this.QuestEntry_DefeatManorGoons = defeatGoons;
            this.QuestEntry_SearchResidence = searchResidence;
            this.QuestEntry_EscapeManor = escapeManor;
#if MONO
            this.Entries = new()
                {
                    investigate, returnToRay, waitForNight, breakIn, defeatGoons, searchResidence, escapeManor
                };
#else
            this.Entries = new();
            this.Entries.Add(investigate);
            this.Entries.Add(returnToRay);
            this.Entries.Add(waitForNight);
            this.Entries.Add(breakIn);
            this.Entries.Add(defeatGoons);
            this.Entries.Add(searchResidence);
            this.Entries.Add(escapeManor);
#endif

            investigate.SetEntryTitle("Investigate the hillside forest near Manor (0/4)");
            investigate.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            investigate.PoILocation.name = "InvestigateEntry_POI";
            investigate.PoILocation.transform.SetParent(investigate.transform);
            investigate.PoILocation.transform.position = new Vector3(164.96f, 3.10f, -32.65f);
            investigate.AutoUpdatePoILocation = true;
            investigate.SetState(EQuestState.Active, true);
            investigate.ParentQuest = this;
            investigate.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction investigateCompleteAction = null;
            void OnInvestigateComplete()
            {
                if (investigate != null && investigate.State == EQuestState.Failed) return;
                if (returnToRay == null) return;

                returnToRay.Begin();
                returnToRay.CreateCompassElement();
                returnToRay.PoI.gameObject.SetActive(true);
                returnToRay.compassElement.Visible = true;
                returnToRay.SetPoILocation(ray.transform.position);

                MelonCoroutines.Start(GenRaySecondDialog(QuestEntry_ReturnToRay.Complete));
                if (investigateCompleteAction != null)
                {
                    investigate.onComplete.RemoveListener(investigateCompleteAction);
                    investigateCompleteAction = null;
                }
            }
            investigateCompleteAction = (UnityEngine.Events.UnityAction)OnInvestigateComplete;
            investigate.onComplete.AddListener(investigateCompleteAction);

            returnToRay.SetEntryTitle("Return to Ray and ask for more information");
            returnToRay.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            returnToRay.PoILocation.name = "ReturnToRayEntry_POI";
            returnToRay.PoILocation.transform.SetParent(returnToRay.transform);
            returnToRay.PoILocation.transform.position = new Vector3(77.30f, 1.46f, -12.85f);
            returnToRay.AutoUpdatePoILocation = true;
            returnToRay.SetState(EQuestState.Inactive, false);
            returnToRay.ParentQuest = this;
            returnToRay.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction returnToRayAction = null;
            void OnReturnToRayComplete()
            {
                if (returnToRay != null && returnToRay.State == EQuestState.Failed) return;
                if (waitForNight == null) return;

                waitForNight.Begin();
                waitForNight.PoI.gameObject.SetActive(false);
                if (waitForNight.compassElement != null)
                    waitForNight.compassElement.Visible = false;
                coros.Add(MelonCoroutines.Start(RandomManorGenerator.SetupManor()));
                if (returnToRayAction != null)
                {
                    returnToRay.onComplete.RemoveListener(returnToRayAction);
                    returnToRayAction = null;
                }
            }
            returnToRayAction = (UnityEngine.Events.UnityAction)OnReturnToRayComplete;
            returnToRay.onComplete.AddListener(returnToRayAction);

            waitForNight.SetEntryTitle("Wait for night time (22:00)");
            waitForNight.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            waitForNight.PoILocation.name = "WaitForNightEntry_POI";
            waitForNight.PoILocation.transform.SetParent(waitForNight.transform);
            waitForNight.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            waitForNight.AutoUpdatePoILocation = true;
            waitForNight.SetState(EQuestState.Inactive, false);
            waitForNight.ParentQuest = this;
            waitForNight.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction waitForNightAction = null;
            void OnWaitForNightComplete()
            {
                if (waitForNight != null && waitForNight.State == EQuestState.Failed) return;
                if (breakIn == null) return;

                breakIn.Begin();
                breakIn.CreateCompassElement();
                breakIn.PoI.gameObject.SetActive(true);
                if (breakIn.compassElement != null)
                    breakIn.compassElement.Visible = true;
                if (waitForNightAction != null)
                {
                    waitForNight.onComplete.RemoveListener(waitForNightAction);
                    waitForNightAction = null;
                }
            }
            waitForNightAction = (UnityEngine.Events.UnityAction)OnWaitForNightComplete;
            waitForNight.onComplete.AddListener(waitForNightAction);

            breakIn.SetEntryTitle("Break into Manor through the back door");
            breakIn.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            breakIn.PoILocation.name = "BreakInEntry_POI";
            breakIn.PoILocation.transform.SetParent(breakIn.transform);
            breakIn.PoILocation.transform.position = new Vector3(163.37f, 11.86f, -50.12f);
            breakIn.AutoUpdatePoILocation = true;
            breakIn.SetState(EQuestState.Inactive, false);
            breakIn.ParentQuest = this;
            breakIn.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction breakInAction = null;
            void OnBreakInComplete()
            {
                if (breakIn != null && breakIn.State == EQuestState.Failed) return;
                if (defeatGoons == null) return;

                SpawnManorGoons();
                defeatGoons.Begin();
                defeatGoons.PoI.gameObject.SetActive(false);
                if (defeatGoons.compassElement != null)
                    defeatGoons.compassElement.Visible = false;
                if (breakInAction != null)
                {
                    breakIn.onComplete.RemoveListener(breakInAction);
                    breakInAction = null;
                }
            }
            breakInAction = (UnityEngine.Events.UnityAction)OnBreakInComplete;
            breakIn.onComplete.AddListener(breakInAction);

            defeatGoons.SetEntryTitle("Defeat the Manor Goons");
            defeatGoons.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            defeatGoons.PoILocation.name = "DefeatGoonsEntry_POI";
            defeatGoons.PoILocation.transform.SetParent(defeatGoons.transform);
            defeatGoons.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            defeatGoons.AutoUpdatePoILocation = true;
            defeatGoons.SetState(EQuestState.Inactive, false);
            defeatGoons.ParentQuest = this;
            defeatGoons.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction defeatGoonsAction = null;
            void OnDefeatGoonsComplete()
            {
                if (defeatGoons != null && defeatGoons.State == EQuestState.Failed) return;
                if (searchResidence == null) return;

                searchResidence.Begin();
                searchResidence.CreateCompassElement();
                searchResidence.PoI.gameObject.SetActive(true);
                if (searchResidence.compassElement != null)
                    searchResidence.compassElement.Visible = true;
                searchResidence.SetPoILocation(roomsPositions.Keys.FirstOrDefault());

                if (defeatGoonsAction != null)
                {
                    defeatGoons.onComplete.RemoveListener(defeatGoonsAction);
                    defeatGoonsAction = null;
                }
            }
            defeatGoonsAction = (UnityEngine.Events.UnityAction)OnDefeatGoonsComplete;
            defeatGoons.onComplete.AddListener(defeatGoonsAction);

            searchResidence.SetEntryTitle("Investigate the upstairs rooms (0/4)");
            searchResidence.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            searchResidence.PoILocation.name = "SearchResidenceEntry_POI";
            searchResidence.PoILocation.transform.SetParent(searchResidence.transform);
            searchResidence.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            searchResidence.AutoUpdatePoILocation = true;
            searchResidence.SetState(EQuestState.Inactive, false);
            searchResidence.ParentQuest = this;
            searchResidence.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction searchResidenceAction = null;
            void OnSearchResidenceComplete()
            {
                if (searchResidence != null && searchResidence.State == EQuestState.Failed) return;
                if (escapeManor == null) return;
                Log("Escape Begin");
                escapeManor.Begin();
                escapeManor.PoI.gameObject.SetActive(false);
                if (escapeManor.compassElement != null)
                    escapeManor.compassElement.Visible = false;
                Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Investigating);
                // Note: not promised that will dispatch
#if MONO
                PoliceStation.PoliceStations.FirstOrDefault().Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#else
                PoliceStation.PoliceStations[0].Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#endif
                if (searchResidenceAction != null)
                {
                    searchResidence.onComplete.RemoveListener(searchResidenceAction);
                    searchResidenceAction = null;
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
            searchResidenceAction = (UnityEngine.Events.UnityAction)OnSearchResidenceComplete;
            searchResidence.onComplete.AddListener(searchResidenceAction);

            escapeManor.SetEntryTitle("Escape the Manor before the Police arrive");
            escapeManor.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            escapeManor.PoILocation.name = "EscapeManorEntry_POI";
            escapeManor.PoILocation.transform.SetParent(escapeManor.transform);
            escapeManor.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            escapeManor.AutoUpdatePoILocation = true;
            escapeManor.SetState(EQuestState.Inactive, false);
            escapeManor.ParentQuest = this;
            escapeManor.CompleteParentQuest = false;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            var action = (Action)OnMinPass;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
            instance.onMinutePass.Add(action);
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif

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
                    this.hudUI.MainLabel.text = "Infiltrate Manor";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_InvestigateWoods != null)
            {
                QuestEntry_InvestigateWoods.CreateCompassElement();
                if (QuestEntry_InvestigateWoods.compassElement != null)
                    QuestEntry_InvestigateWoods.compassElement.Visible = true;

                if (QuestEntry_InvestigateWoods.PoI != null)
                    QuestEntry_InvestigateWoods.PoI.gameObject.SetActive(true);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);
            return;
        }

        public void SpawnManorGoons()
        {
            Log("Roompos Keys to list");
            List<Vector3> roomPositionsList = roomsPositions.Keys.ToList();

            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 3)
            {
                // because below dowhile not limited by max iter added this
                int maxIter = 5;
                int currentIter = 0;
                do
                {
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Count == 0) break;
                    int count = NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Count - 1; // list pos to last
                    CartelGoon target = NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons[count];
                    target.Health.Revive();
                    target.Despawn();
                    // If combat behaviour is active then the goon will be invis but fight player ensure disable
                    if (target.Behaviour.CombatBehaviour.Active)
                        target.Behaviour.CombatBehaviour.Disable_Networked(null);

                    currentIter++;
                } while (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 3 || currentIter >= maxIter);
            }

            for (int i = 0; i < NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count; i++)
            {
                if (i > 2) break;
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
                goon.Health.MaxHealth = Mathf.Round(Mathf.Lerp(150f, 300f, questDifficultyScalar - 1f));
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

                if (GiveUpRange == 0f)
                {
                    GiveUpRange = goon.Behaviour.CombatBehaviour.GiveUpRange;
                    GiveUpAfterSuccessfulHits = goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                    DefaultSearchTime = goon.Behaviour.CombatBehaviour.DefaultSearchTime;
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
#if MONO
                if (goon.Behaviour.CombatBehaviour.currentWeapon is AvatarRangedWeapon wep)
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

                    if (currentConfig.debugMode)
                        wep.Damage = 0f;
                }
#else
                AvatarRangedWeapon wep = null;
                try
                {
                    wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
                } 
                catch (InvalidCastException ex)
                {
                    MelonLogger.Warning("Failed to Cast Manor Goon Weapon Instance: " + ex);
                }

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
                    wep.MaxFireRate = Mathf.Round(Mathf.Lerp(0.7f, 1.2f, t));
                    wep.MaxMovingShotsBeforeReposition = 3;
                    wep.MaxStationaryShotsBeforeReposition = 1;
                    wep.MaxUseRange = 7f;
                    wep.MinUseRange = 0.1f;
                    wep.CooldownDuration = Mathf.Round(Mathf.Lerp(0.7f, 1.2f, t));
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));

                    if (currentConfig.debugMode)
                        wep.Damage = 0f;
                }
#endif

                if (goon.Behaviour.CombatBehaviour.currentWeapon != null && goon.Behaviour.CombatBehaviour.DefaultWeapon == null)
                    goon.Behaviour.CombatBehaviour.DefaultWeapon = goon.Behaviour.CombatBehaviour.currentWeapon;

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
            goon.Health.MaxHealth = Mathf.Round(Mathf.Lerp(35f, 85f, questDifficultyScalar - 1f));
            goon.Health.Health = Mathf.Round(Mathf.Lerp(35f, 85f, questDifficultyScalar - 1f));

            goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");
            if (goon.Behaviour.CombatBehaviour.currentWeapon != null)
            {
#if MONO
                if (goon.Behaviour.CombatBehaviour.currentWeapon is AvatarMeleeWeapon wep)
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

                    if (currentConfig.debugMode)
                        wep.Damage = 0f;
                }
#else
                AvatarMeleeWeapon wep = null;
                try
                {
                    wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarMeleeWeapon>();
                } 
                catch (InvalidCastException ex)
                {
                    MelonLogger.Warning("Failed to Cast Manor Goon Weapon Instance: " + ex);
                }

                if (wep != null)
                {
                    float dmgMin = 66f;
                    float dmgMax = 87f;
                    float t = Mathf.Clamp01(questDifficultyScalar - 1f);
                    wep.MaxUseRange = 2.7f;
                    wep.MinUseRange = 0.2f;
                    wep.AttackRadius = Mathf.Lerp(2.3f, 3.7f, t);
                    wep.AttackRange = Mathf.Lerp(2.3f, 3.7f, t);
                    wep.CooldownDuration = Mathf.Lerp(0.6f, 1.2f, t);
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));

                    if (currentConfig.debugMode)
                        wep.Damage = 0f;
                }
#endif
                if (goon.Behaviour.CombatBehaviour.currentWeapon != null && goon.Behaviour.CombatBehaviour.DefaultWeapon == null)
                    goon.Behaviour.CombatBehaviour.DefaultWeapon = goon.Behaviour.CombatBehaviour.currentWeapon;
            }
            goon.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
            goon.Behaviour.CombatBehaviour.Enable_Networked();

            float speed = Mathf.Lerp(UnityEngine.Random.Range(0.55f, 0.76f), 0.86f, questDifficultyScalar - 1f);
            goon.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, speed));
            goon.Movement.Agent.avoidancePriority = 30;
            goon.transform.localScale = new Vector3(0.81f, 0.81f, 0.81f);
            coros.Add(MelonCoroutines.Start(DespawnForestGoon(goon)));
        }
        private IEnumerator DespawnForestGoon(CartelGoon goon)
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
            goon.Health.MaxHealth = 100f;
            goon.Health.Health = 100f;
            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
            goon.Behaviour.ScheduleManager.EnableSchedule();
            if (goon.Health.IsDead)
                goon.Health.Revive();
            if (goon.IsGoonSpawned)
                goon.Despawn();
            if (goon.Behaviour.CombatBehaviour.Active)
                goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            goon.Movement.SpeedController.RemoveSpeedControl("combat");
            yield break;
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


        public QuestEntry QuestEntry_InvestigateWoods;
        public QuestEntry QuestEntry_ReturnToRay;
        public QuestEntry QuestEntry_WaitForNight;
        public QuestEntry QuestEntry_BreakIn;
        public QuestEntry QuestEntry_DefeatManorGoons;
        public QuestEntry QuestEntry_SearchResidence;
        public QuestEntry QuestEntry_EscapeManor;

    }

}
