

using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.ItemFramework;
using ScheduleOne.Interaction;
using ScheduleOne.Storage;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Property;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Levelling;
using FishNet;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
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
        private static Quaternion doorOrigRot;
        private GameObject spawnedSafe = null;
        private float questDifficultyScalar = 1f;
        private int manorGoonsAlive = 4;

        // store the combat variables
        private float GiveUpRange = 0f;
        private int GiveUpAfterSuccessfulHits = 0;
        private float DefaultSearchTime = 0f;


        private List<Vector3> forestSearchLocs = new()
        {
            new Vector3(164.96f, 3.10f, -32.65f),
            new Vector3(151.45f, 3.20f, -35.21f),
            new Vector3(139.41f, 1.96f, -45.71f),
            new Vector3(135.26f, 2.72f, -66.56f)
        };
        private int forestPosSearched = 0; // indexing for above search location one by one update compass

        private Dictionary<Vector3, bool> roomsPositions = new()
        {
            { new Vector3(166.58f, 15.61f, -52.99f), false },
            { new Vector3(160.65f, 15.61f, -52.97f), false },
            { new Vector3(160.65f, 15.61f, -61.00f), false },
            { new Vector3(166.58f, 15.61f, -61.00f), false }
        };
        private int roomsVisited = 0;

        class SafeSpawnSpot
        {

            public Vector3 spawnSpot;
            public Vector3 eulerAngles;
            public SafeSpawnSpot(Vector3 pos, Vector3 rot)
            {
                spawnSpot = pos;
                eulerAngles = rot;
            }
        }

        private List<SafeSpawnSpot> lootSafeSpawns = new()
        {
            new SafeSpawnSpot(new Vector3(153.63f, 14.24f, -56.13f), new Vector3(0f, 0f, 0f)),
            new SafeSpawnSpot(new Vector3(159.43f, 14.24f, -57.73f), new Vector3(0f, 90f, 0f)),
            new SafeSpawnSpot(new Vector3(173.43f, 14.24f, -57.89f), new Vector3(0f, 180f, 0f)),
            new SafeSpawnSpot(new Vector3(166.56f, 14.24f, -51.41f), new Vector3(0f, 75f, 0f)),
            new SafeSpawnSpot(new Vector3(153.46f, 15.28f, -54.77f), new Vector3(0f, 335f, 0f)),
        };

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
            MelonLogger.Msg("Quest_InfiltrateManor: End method called.");
            try
            {
                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;
#if MONO
                instance.onHourPass = (Action)Delegate.Remove(instance.onHourPass, new Action(this.HourPass));
                instance.onMinutePass.Remove((Action)this.MinPass);
#else
                instance.onHourPass -= (Il2CppSystem.Action)this.HourPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)this.MinPass);
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
            Log($"Entries list type: {this.Entries.GetType()}");
            Log("Config Entries");

            investigate.SetEntryTitle("• Investigate the hillside forest near Manor (0/4)");
            investigate.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            investigate.PoILocation.name = "InvestigateEntry_POI";
            investigate.PoILocation.transform.SetParent(investigate.transform);
            investigate.PoILocation.transform.position = new Vector3(164.96f, 3.10f, -32.65f);
            investigate.AutoUpdatePoILocation = true;
            investigate.SetState(EQuestState.Active, true);
            investigate.ParentQuest = this;
            investigate.CompleteParentQuest = false;
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
                investigate.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);
            }
            investigate.onComplete.AddListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);

            returnToRay.SetEntryTitle("• Return to Ray and ask for more information");
            returnToRay.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            returnToRay.PoILocation.name = "ReturnToRayEntry_POI";
            returnToRay.PoILocation.transform.SetParent(returnToRay.transform);
            returnToRay.PoILocation.transform.position = new Vector3(77.30f, 1.46f, -12.85f);
            returnToRay.AutoUpdatePoILocation = true;
            returnToRay.SetState(EQuestState.Inactive, false);
            returnToRay.ParentQuest = this;
            returnToRay.CompleteParentQuest = false;
            void OnReturnToRayComplete()
            {
                if (returnToRay != null && returnToRay.State == EQuestState.Failed) return;
                if (waitForNight == null) return;

                waitForNight.Begin();
                waitForNight.PoI.gameObject.SetActive(false);
                if (waitForNight.compassElement != null)
                    waitForNight.compassElement.Visible = false;
                coros.Add(MelonCoroutines.Start(this.SetupManor()));

                returnToRay.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnReturnToRayComplete);
            }
            returnToRay.onComplete.AddListener((UnityEngine.Events.UnityAction)OnReturnToRayComplete);

            waitForNight.SetEntryTitle("• Wait for night time");
            waitForNight.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            waitForNight.PoILocation.name = "WaitForNightEntry_POI";
            waitForNight.PoILocation.transform.SetParent(waitForNight.transform);
            waitForNight.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            waitForNight.AutoUpdatePoILocation = true;
            waitForNight.SetState(EQuestState.Inactive, false);
            waitForNight.ParentQuest = this;
            waitForNight.CompleteParentQuest = false;
            void OnWaitForNightComplete()
            {
                if (waitForNight != null && waitForNight.State == EQuestState.Failed) return;
                if (breakIn == null) return;

                breakIn.Begin();
                breakIn.CreateCompassElement();
                breakIn.PoI.gameObject.SetActive(true);
                if (breakIn.compassElement != null)
                    breakIn.compassElement.Visible = true;
                waitForNight.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnWaitForNightComplete);
            }
            waitForNight.onComplete.AddListener((UnityEngine.Events.UnityAction)OnWaitForNightComplete);

            breakIn.SetEntryTitle("• Break into Manor through the back door");
            breakIn.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            breakIn.PoILocation.name = "BreakInEntry_POI";
            breakIn.PoILocation.transform.SetParent(breakIn.transform);
            breakIn.PoILocation.transform.position = new Vector3(163.37f, 11.86f, -50.12f);
            breakIn.AutoUpdatePoILocation = true;
            breakIn.SetState(EQuestState.Inactive, false);
            breakIn.ParentQuest = this;
            breakIn.CompleteParentQuest = false;
            void OnBreakInComplete()
            {
                if (breakIn != null && breakIn.State == EQuestState.Failed) return;
                if (defeatGoons == null) return;

                SpawnManorGoons();
                defeatGoons.Begin();
                defeatGoons.PoI.gameObject.SetActive(false);
                if (defeatGoons.compassElement != null)
                    defeatGoons.compassElement.Visible = false;
                breakIn.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnBreakInComplete);
            }
            breakIn.onComplete.AddListener((UnityEngine.Events.UnityAction)OnBreakInComplete);

            defeatGoons.SetEntryTitle("• Defeat the Manor Goons");
            defeatGoons.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            defeatGoons.PoILocation.name = "DefeatGoonsEntry_POI";
            defeatGoons.PoILocation.transform.SetParent(defeatGoons.transform);
            defeatGoons.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            defeatGoons.AutoUpdatePoILocation = true;
            defeatGoons.SetState(EQuestState.Inactive, false);
            defeatGoons.ParentQuest = this;
            defeatGoons.CompleteParentQuest = false;
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
                defeatGoons.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnDefeatGoonsComplete);
            }
            defeatGoons.onComplete.AddListener((UnityEngine.Events.UnityAction)OnDefeatGoonsComplete);

            searchResidence.SetEntryTitle("• Investigate the upstairs rooms (0/4)");
            searchResidence.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            searchResidence.PoILocation.name = "SearchResidenceEntry_POI";
            searchResidence.PoILocation.transform.SetParent(searchResidence.transform);
            searchResidence.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            searchResidence.AutoUpdatePoILocation = true;
            searchResidence.SetState(EQuestState.Inactive, false);
            searchResidence.ParentQuest = this;
            searchResidence.CompleteParentQuest = false;
            void OnSearchResidenceComplete()
            {
                if (searchResidence != null && searchResidence.State == EQuestState.Failed) return;
                if (escapeManor == null) return;

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
                searchResidence.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);
            }
            searchResidence.onComplete.AddListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);

            escapeManor.SetEntryTitle("• Escape the Manor before the Police arrive");
            escapeManor.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            escapeManor.PoILocation.name = "EscapeManorEntry_POI";
            escapeManor.PoILocation.transform.SetParent(escapeManor.transform);
            escapeManor.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            escapeManor.AutoUpdatePoILocation = true;
            escapeManor.SetState(EQuestState.Inactive, false);
            escapeManor.ParentQuest = this;
            escapeManor.CompleteParentQuest = false;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
#endif
            instance.onMinutePass.Add(new Action(this.MinPass));

            StartQuestDetail();
        }

        private void StartQuestDetail()
        {
            if (this.IconPrefab == null)
                this.IconPrefab = this.transform.Find("BenziesLogoQuest").GetComponent<RectTransform>();
            SetupHudUI();

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

        private IEnumerator SetupManor()
        {
            Log("SETUP MANOR");
            Manor manor = UnityEngine.Object.FindObjectOfType<Manor>(true);
            if (manor == null)
            {
                Log("Manor is null");
                yield break;
            }

            // Disable the dupe doors for now, remember to re-enable later
            Transform doorsTr = manor.transform.Find("Doors");
            if (doorsTr != null)
            {
                doorsTr.gameObject.SetActive(false);
            }
            else
            {
                Log("Manor Doors is null");
                yield break;
            }

            Log("Fetching original Door Container");
            // First setup original door to work and also the trigger to open because it doesnt have it
            Transform door = manor.OriginalContainer.transform.Find("MansionDoor (1)");
            if (door == null)
            {
                Log("Manor DOOR in orig container is NULL");
                yield break;
            }

            Transform doorContainer = door.Find("Container"); // Lerp rotation on this one because it works with hinges
            if (doorContainer == null)
            {
                Log("Manor DOORCONTAINER in container is NULL");
                yield break;
            }
            doorOrigRot = doorContainer.transform.localRotation;
            Log("Fetched original Door Container");
            InteractableObject exteriorIntObj = doorContainer.GetChild(0).GetComponent<InteractableObject>();
            InteractableObject interiorIntObj = doorContainer.GetChild(1).GetComponent<InteractableObject>();
            Log("Fetched INT objects");

            void onDoorInteracted()
            {
                if (QuestEntry_BreakIn.State == EQuestState.Active)
                {
                    coros.Add(MelonCoroutines.Start(LerpDoorRotation(doorContainer, QuestEntry_BreakIn.Complete)));
                    exteriorIntObj.onInteractStart.RemoveListener((UnityEngine.Events.UnityAction)onDoorInteracted);
                }
            }
            exteriorIntObj.message = "Open Door";
            exteriorIntObj.onInteractStart.AddListener((UnityEngine.Events.UnityAction)onDoorInteracted);
            interiorIntObj.message = "Open Door";
            Log("Configured int objects");

            // then setup lights because they are buggy if we dont disable the optimize light feature
            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };
            Log("Setup rooms done");

            foreach (Transform room in rooms)
            {
                room.gameObject.SetActive(true);
                LightOptimizer lightOptimizer = room.gameObject.GetComponent<LightOptimizer>();
                Log("LightOpt fetched");
                if (lightOptimizer == null)
                {
                    Log("Light Optimizer is null!");
                    continue;
                }
                if (lightOptimizer.lights == null || lightOptimizer.lights.Count() == 0)
                {
                    Log("Light optimizer light list is empty or null");
                    continue;
                }
                yield return Wait05;
                if (!registered) yield break;

                foreach (OptimizedLight optimizedLight in lightOptimizer.lights)
                {
                    yield return Wait05;
                    if (!registered) yield break;

                    Log("LightOpt Swapping");
                    optimizedLight.Enabled = false;
                    optimizedLight.enabled = false;
                    if (optimizedLight._Light != null)
                    {
                        optimizedLight._Light.enabled = true;
                    }
                    Log("LightOpt Done");
                }
            }

            int index = UnityEngine.Random.Range(0, lootSafeSpawns.Count);
            spawnedSafe = UnityEngine.Object.Instantiate(safePrefab, Map.Instance.transform);

            spawnedSafe.transform.position = lootSafeSpawns[index].spawnSpot;
            spawnedSafe.transform.rotation = Quaternion.Euler(lootSafeSpawns[index].eulerAngles);
            yield return Wait2;
            if (!registered) yield break;

            if (!spawnedSafe.activeSelf)
                spawnedSafe.SetActive(true);
            yield return Wait2;
            if (!registered) yield break;

            spawnedSafe.SetActive(true);

            Safe safeComp = spawnedSafe.GetComponent<Safe>();
            StorageDoorAnimation doorAnim = spawnedSafe.GetComponent<StorageDoorAnimation>();
            doorAnim.Open();
            yield return Wait05;
            if (!registered) yield break;

            doorAnim.Close();

            void SecondaryTrigger()
            {
                if (QuestEntry_SearchResidence.State == EQuestState.Active)
                    QuestEntry_SearchResidence.Complete();
                safeComp.onOpened.RemoveListener((UnityEngine.Events.UnityAction)SecondaryTrigger);
            }
            safeComp.onOpened.AddListener((UnityEngine.Events.UnityAction)SecondaryTrigger);
            safeComp.StorageEntitySubtitle = "Thomas' Safe";

            Func<string, ItemDefinition> GetItem;

#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
            int maxSlotsToFill = 5;
            int currentSlots = 0;
            if (UnityEngine.Random.Range(0f, 1f) > 0.666f)
            {
                ItemDefinition defGun = GetItem("m1911");
                ItemInstance gunInst = defGun.GetDefaultInstance(1);
                safeComp.InsertItem(gunInst, true);
                currentSlots++;
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.90f)
            {
                ItemDefinition defCoke = GetItem("cocaine");
                ItemInstance cokeInst = defCoke.GetDefaultInstance(UnityEngine.Random.Range(12, 20));
                safeComp.InsertItem(cokeInst, true);
                currentSlots++;
            }

            ItemDefinition defMag = GetItem("m1911mag");
            ItemInstance magInst = defMag.GetDefaultInstance(1);
            safeComp.InsertItem(magInst, true);
            currentSlots++;

            bool goldBarInserted = false;
            if (UnityEngine.Random.Range(0f, 1f) > 0.95f && currentSlots <= maxSlotsToFill)
            {
                int goldBarQty = UnityEngine.Random.Range(1, 4);
                ItemDefinition goldBarDef = GetItem("goldbar");
                ItemInstance goldBar = goldBarDef.GetDefaultInstance(goldBarQty);
                safeComp.InsertItem(goldBar, true);
                goldBarInserted = true;
                currentSlots++;
            }
            if (currentSlots <= maxSlotsToFill && !goldBarInserted)
            {
                ItemDefinition defCash = GetItem("cash");
                int qty = 0;
                if (UnityEngine.Random.Range(0f, 1f) > 0.666f)
                    qty = UnityEngine.Random.Range(1000, 4000);
                else
                    qty = UnityEngine.Random.Range(800, 3000);

                qty = (int)Math.Round((double)qty / 100) * 100;
                if (qty > 2000)
                    defCash.StackLimit = qty;

                ItemInstance cashInstance = defCash.GetDefaultInstance(1);

#if MONO
                if (cashInstance is CashInstance inst)
                {
                    inst.Balance = qty;
                }
#else
                CashInstance tempInst = cashInstance.TryCast<CashInstance>();
                if (tempInst != null)
                {
                    tempInst.Balance = qty;
                }
#endif
                safeComp.InsertItem(cashInstance, true);
                currentSlots++;
            }

            if (cartelStolenItems.Count > 0 && currentSlots <= maxSlotsToFill && UnityEngine.Random.Range(0f, 1f) > 0.20f)
            {
                List<ItemInstance> fromPool = GetFromPool(1);
                if (fromPool.Count > 0)
                {
                    safeComp.InsertItem(fromPool[0], true);
                    currentSlots++;
                }
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.80f && currentSlots <= maxSlotsToFill)
            {
                ItemDefinition defWatch = GetItem("silverwatch");
                ItemInstance watchInst = defWatch.GetDefaultInstance(1);
                safeComp.InsertItem(watchInst, true);
                currentSlots++;
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.80f && currentSlots <= maxSlotsToFill)
            {
                ItemDefinition defChain = GetItem("silverchain");
                ItemInstance chainInst = defChain.GetDefaultInstance(1);
                safeComp.InsertItem(chainInst, true);
                currentSlots++;
            }


            yield return null;
        }

        private IEnumerator CleanupManor()
        {
            yield return Wait60;
            if (!registered) yield break;

            Manor manor = UnityEngine.Object.FindObjectOfType<Manor>(true);
            Transform doorsTr = manor.transform.Find("Doors");
            if (doorsTr != null)
            {
                doorsTr.gameObject.SetActive(true);
            }
            else
            {
                Log("Manor Doors is null");
            }

            Transform door = manor.OriginalContainer.transform.Find("MansionDoor (1)");
            if (door == null)
            {
                Log("Manor DOOR in orig container is NULL");
                Transform doorContainer = door.Find("Container");
                if (doorContainer == null)
                {
                    Log("Manor DOORCONTAINER in container is NULL");
                }
                else
                {
                    if (doorOrigRot != null)
                        doorContainer.transform.localRotation = doorOrigRot;
                }
            }

            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };
            foreach (Transform room in rooms)
            {
                LightOptimizer lightOptimizer = room.gameObject.GetComponent<LightOptimizer>();
                yield return Wait05;
                if (!registered) yield break;

                foreach (OptimizedLight optimizedLight in lightOptimizer.lights)
                {
                    yield return Wait05;
                    if (!registered) yield break;

                    optimizedLight.Enabled = true;
                    optimizedLight.enabled = true;
                    if (optimizedLight._Light != null)
                    {
                        optimizedLight._Light.enabled = false;
                    }
                }
            }

            foreach (CartelGoon goon in manorGoons)
            {
                yield return Wait05;
                if (!registered) yield break;

                goon.Movement.MoveSpeedMultiplier = 1f;
                goon.Health.MaxHealth = 100f;
                goon.Health.Health = 100f;

                goon.Behaviour.CombatBehaviour.GiveUpRange = GiveUpRange;
                goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = GiveUpAfterSuccessfulHits;
                goon.Behaviour.CombatBehaviour.DefaultSearchTime = DefaultSearchTime;

                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                goon.Behaviour.ScheduleManager.EnableSchedule();

                if (goon.Health.IsDead)
                    goon.Health.Revive();
                if (goon.IsGoonSpawned)
                    goon.Despawn();
                if (goon.Behaviour.CombatBehaviour.Active)
                    goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            }
            manorGoons.Clear();
            manorGoonGuids.Clear();
            if (spawnedSafe != null)
                UnityEngine.Object.Destroy(spawnedSafe);
            spawnedSafe = null;

            yield return null;
        }

        private IEnumerator LerpDoorRotation(Transform tr, Action cb)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, 55f, 0f);

            Quaternion startRotation = tr.localRotation;

            float duration = 2.5f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                tr.localRotation = Quaternion.Lerp(startRotation, targetRotation, progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            tr.localRotation = targetRotation;

            if (cb != null)
                cb();

            yield return null;
        }

        private void SpawnManorGoons()
        {
            Log("Roompos Keys to list");
            List<Vector3> roomPositionsList = roomsPositions.Keys.ToList();

            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 4)
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
                } while (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 4 || currentIter >= maxIter);
            }

            for (int i = 0; i < NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count; i++)
            {
                if (i > 3) break;
                Log("SpawnGoon");
                CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(roomPositionsList[i]);
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

                Log("Search time default: " + goon.Behaviour.CombatBehaviour.DefaultSearchTime);
                SetupGoonWeapon(goon);
                goon.Inventory.AddCash(Mathf.Round(UnityEngine.Random.Range(40f * questDifficultyScalar, 130f * questDifficultyScalar)));
                Log("Set HP and movespeed");
                goon.Health.MaxHealth = Mathf.Round(Mathf.Lerp(150f, 300f, questDifficultyScalar - 1f));
                goon.Health.Health = Mathf.Round(Mathf.Lerp(150f, 300f, questDifficultyScalar - 1f));
                goon.Movement.MoveSpeedMultiplier = Mathf.Lerp(UnityEngine.Random.Range(1.3f, 1.5f), 1.75f, questDifficultyScalar - 1f);

                void onCombatBehEnd()
                {
                    // Because sometimes it seems that they just end prematurely for no reason, check if is alive and not knocked out
                    if (!goon.Health.IsKnockedOut && !goon.Health.IsDead && Player.Local.Health.CurrentHealth > 0f)
                    {
                        goon.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
                        goon.Behaviour.CombatBehaviour.Enable_Networked(null);
                        if (goon.Behaviour.CombatBehaviour.currentWeapon == null) // does it retain prev weps?? this can cause issue
                        {
                            SetupGoonWeapon(goon);
                        }
                    }
                    else
                    {
                        goon.Behaviour.CombatBehaviour.onEnd.RemoveListener((UnityEngine.Events.UnityAction)onCombatBehEnd);
                    }
                }
                goon.Behaviour.CombatBehaviour.onEnd.AddListener((UnityEngine.Events.UnityAction)onCombatBehEnd);

                void onGoonDie()
                {
                    manorGoonsAlive--;
                    goon.Health.onDieOrKnockedOut.RemoveListener((UnityEngine.Events.UnityAction)onGoonDie);
                }
                goon.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)onGoonDie);

                if (GiveUpRange == 0f)
                {
                    GiveUpRange = goon.Behaviour.CombatBehaviour.GiveUpRange;
                    GiveUpAfterSuccessfulHits = goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                    DefaultSearchTime = goon.Behaviour.CombatBehaviour.DefaultSearchTime;
                }

                goon.Behaviour.CombatBehaviour.GiveUpRange = 60f;
                goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 60;
                goon.Behaviour.CombatBehaviour.DefaultSearchTime = 120f;

                goon.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject); // should swap to player get nearest?
                goon.Behaviour.CombatBehaviour.Enable_Networked(null);
                manorGoons.Add(goon);
                manorGoonGuids.Add(goon.GUID.ToString());
            }

            manorGoonsAlive = manorGoons.Count();

            return;
        }

        private void SetupGoonWeapon(CartelGoon goon)
        {
            goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");
            if (goon.Behaviour.CombatBehaviour.currentWeapon != null)
            {
#if MONO
                if (goon.Behaviour.CombatBehaviour.currentWeapon is AvatarMeleeWeapon wep)
                {
                    float min = 1.35f;
                    float max = 1.75f;
                    float dmgMin = 18f;
                    float dmgMax = 30f;
                    float t = questDifficultyScalar - 1f;

                    wep.AttackRadius = wep.AttackRadius * Mathf.Lerp(min, max, t);
                    wep.AttackRange = wep.AttackRange * Mathf.Lerp(min, max, t);
                    wep.MaxUseRange = 2.7f;
                    wep.MinUseRange = 0.01f;
                    wep.CooldownDuration = UnityEngine.Random.Range(0.7f, 1.5f);
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));
                }
#else
                AvatarMeleeWeapon wep = null;
                try
                {
                    Log("CastWep");
                    wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarMeleeWeapon>();
                } 
                catch (InvalidCastException ex)
                {
                    MelonLogger.Warning("Failed to Cast Manor Goon Weapon Instance: " + ex);
                }

                if (wep != null)
                {
                    float min = 1.35f;
                    float max = 1.75f;
                    float dmgMin = 18f;
                    float dmgMax = 30f;
                    float t = questDifficultyScalar - 1f;

                    wep.AttackRadius = wep.AttackRadius * Mathf.Lerp(min, max, t);
                    wep.AttackRange = wep.AttackRange * Mathf.Lerp(min, max, t);
                    wep.MaxUseRange = 2.7f;
                    wep.MinUseRange = 0.01f;
                    wep.CooldownDuration = UnityEngine.Random.Range(0.7f, 1.5f);
                    wep.Damage = Mathf.Round(Mathf.Lerp(dmgMin, dmgMax, t));
                }
#endif

                if (goon.Behaviour.CombatBehaviour.currentWeapon != null && goon.Behaviour.CombatBehaviour.DefaultWeapon == null)
                    goon.Behaviour.CombatBehaviour.DefaultWeapon = goon.Behaviour.CombatBehaviour.currentWeapon;
            }
        }

        public override void MinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || manorCompleted || this.State != EQuestState.Active) return;
#if MONO
            base.MinPass();

#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_InvestigateWoods != null && QuestEntry_InvestigateWoods.State == EQuestState.Active)
            {
                QuestEntry_InvestigateWoods.SetEntryTitle($"• Investigate the hillside forest near Manor ({forestPosSearched}/4)");

                if (forestSearchLocs == null) return;

                if (forestPosSearched == 4)
                {
                    QuestEntry_InvestigateWoods.SetEntryTitle($"• Investigate the hillside forest near Manor (4/4)");
                    QuestEntry_InvestigateWoods.Complete();
                    return;
                }

                if (forestPosSearched < forestSearchLocs.Count && Vector3.Distance(Player.Local.CenterPointTransform.position, forestSearchLocs[forestPosSearched]) < 5f)
                {
                    forestPosSearched++;
                    QuestEntry_InvestigateWoods.SetEntryTitle($"• Investigate the hillside forest near Manor ({forestPosSearched}/4)");

                    if (forestPosSearched < forestSearchLocs.Count)
                    {
                        QuestEntry_InvestigateWoods.SetPoILocation(forestSearchLocs[forestPosSearched]);
                    }
                    else
                    {
                        QuestEntry_InvestigateWoods.SetEntryTitle($"• Investigate the hillside forest near Manor (4/4)");
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
            }
            else if (QuestEntry_SearchResidence != null && QuestEntry_SearchResidence.State == EQuestState.Active)
            {
                try
                {
                    QuestEntry_SearchResidence.SetEntryTitle($"• Investigate the upstairs rooms ({roomsVisited}/4)");
                }
                catch (NullReferenceException) { }

                bool allRoomsVisited = true;
                foreach (var roomEntry in roomsPositions)
                {
                    Vector3 roomPosition = roomEntry.Key;
                    bool hasBeenVisited = roomEntry.Value;

                    if (!hasBeenVisited)
                    {
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, roomPosition) < 0.85f)
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


        private QuestEntry QuestEntry_InvestigateWoods;
        private QuestEntry QuestEntry_ReturnToRay;
        private QuestEntry QuestEntry_WaitForNight;
        private QuestEntry QuestEntry_BreakIn;
        private QuestEntry QuestEntry_DefeatManorGoons;
        private QuestEntry QuestEntry_SearchResidence;
        private QuestEntry QuestEntry_EscapeManor;

    }

}
