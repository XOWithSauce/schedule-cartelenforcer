

using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.ItemFramework;
using ScheduleOne.Storage;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using ScheduleOne.Packaging;
using ScheduleOne.Product;
using ScheduleOne.Product.Packaging;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Vehicles;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Managing;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.Packaging;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet.Managing;
using Il2CppScheduleOne.Storage;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_CarMeetup : Quest
    {
#if IL2CPP
        public Quest_CarMeetup(IntPtr ptr) : base(ptr) { }

        public Quest_CarMeetup() : base(ClassInjector.DerivedConstructorPointer<Quest_CarMeetup>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;

        private float questDifficultyScalar = 1f;

        private GameObject brickBase = null;
        private List<GameObject> spawnedDecor = new();

        private Dictionary<string, CartelGoon> spawnedGoons = new();
        public List<string> spawnedGoonsGuids = new();

        private StorageEntity rewardStorage = null;

        private bool combatBegun = false;
        private bool playerSightedActive = false;
        private bool investigationActive = false;


        // store the combat variables
        private float GiveUpRange = 0f;
        private int GiveUpAfterSuccessfulHits = 0;
        private float DefaultSearchTime = 0f;

        // Store the callbacks
        UnityEngine.Events.UnityAction combatStartedAction = null;
        UnityEngine.Events.UnityAction goonDiedAction = null;
        UnityEngine.Events.UnityAction combatEndCrouchAction = null;
        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only
        public override void Complete(bool network = true) 
        {
            Log("Quest_CarMeetup: Complete method called.");
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

                Log("Quest_CarMeetup: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CarMeetup: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_CarMeetup: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_CarMeetup: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CarMeetup: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            MelonLogger.Msg("Quest_CarMeetup: End method called.");
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
                investigationActive = false;
                playerSightedActive = false;
                combatBegun = false;

                Log("Quest_CarMeetup: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CarMeetup: An error occurred in base.End: {ex.Message}");
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
            this.name = "Quest_CarMeetup";
            Expires = false;
            title = "Four Wheels";
            CompletionXP = Mathf.RoundToInt(300f * questDifficultyScalar);
            Description = "Stop the Cartel from transporting Cocaine";
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
            GameObject talkTojeremyObject = new GameObject("QuestEntry_TalkToJeremy");
            talkTojeremyObject.transform.SetParent(this.transform);

            GameObject stopMeetupObject = new GameObject("QuestEntry_StopCarMeetup");
            stopMeetupObject.transform.SetParent(this.transform);

            GameObject escapeWaterfrontObject = new GameObject("QuestEntry_EscapeNorthWaterfront");
            escapeWaterfrontObject.transform.SetParent(this.transform);

            QuestEntry talkToJeremy = talkTojeremyObject.AddComponent<QuestEntry>();
            QuestEntry stopMeetup = stopMeetupObject.AddComponent<QuestEntry>();
            QuestEntry escape = escapeWaterfrontObject.AddComponent<QuestEntry>();


            Log("Setting Entries");
            this.QuestEntry_TalkToJeremy = talkToJeremy;
            this.QuestEntry_StopCarMeetup = stopMeetup;
            this.QuestEntry_EscapeNorthWaterfront = escape;
#if MONO
            this.Entries = new()
                {
                    talkToJeremy, stopMeetup, escape
                };
#else
            this.Entries = new();
            this.Entries.Add(talkToJeremy);
            this.Entries.Add(stopMeetup);
            this.Entries.Add(escape);
#endif
            Log("Config Entries");

            talkToJeremy.SetEntryTitle("• Ask Jeremy about the cars at their home after curfew");
            talkToJeremy.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            talkToJeremy.PoILocation.name = "TalkToJeremyEntry_POI";
            talkToJeremy.PoILocation.transform.SetParent(talkToJeremy.transform);
            talkToJeremy.PoILocation.transform.position = new Vector3(69.00f, 5.93f, -119.09f);
            //investigate.CreatePoI();
            talkToJeremy.AutoUpdatePoILocation = true;
            talkToJeremy.SetState(EQuestState.Active, true);
            talkToJeremy.ParentQuest = this;
            talkToJeremy.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction talkToJeremyAction = null;
            void OnTalkToJeremyComplete()
            {
                if (talkToJeremy != null && talkToJeremy.State == EQuestState.Failed) return;
                if (stopMeetup == null) return;

                stopMeetup.Begin();
                if (stopMeetup.PoI != null && stopMeetup.PoI.gameObject != null)
                    stopMeetup.PoI.gameObject.SetActive(true);

                if (stopMeetup.compassElement == null)
                    stopMeetup.CreateCompassElement();
                stopMeetup.compassElement.Visible = true;

                coros.Add(MelonCoroutines.Start(SpawnCarMeetup()));

                if (talkToJeremyAction != null)
                {
                    talkToJeremy.onComplete.RemoveListener(talkToJeremyAction);
                    talkToJeremyAction = null;
                }
            }
            talkToJeremyAction = (UnityEngine.Events.UnityAction)OnTalkToJeremyComplete;
            talkToJeremy.onComplete.AddListener(talkToJeremyAction);

            stopMeetup.SetEntryTitle("• Stop the Benzies from transporting cocaine\n• Avoid police attention");
            stopMeetup.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            stopMeetup.PoILocation.name = "StopCarMeetupEntry_POI";
            stopMeetup.PoILocation.transform.SetParent(stopMeetup.transform);
            stopMeetup.PoILocation.transform.position = new Vector3(-32.68f, -2.54f, 168.73f);
            stopMeetup.AutoUpdatePoILocation = true;
            stopMeetup.SetState(EQuestState.Inactive, false);
            stopMeetup.ParentQuest = this;
            stopMeetup.CompleteParentQuest = false;
            UnityEngine.Events.UnityAction stopMeetupAction = null;
            void OnStopMeetupComplete()
            {
                if (stopMeetup != null && stopMeetup.State == EQuestState.Failed) return;
                if (escape == null) return;
                coros.Add(MelonCoroutines.Start(StartLateInvestigation()));
                rewardStorage.AccessSettings = StorageEntity.EAccessSettings.Full;
                escape.Begin();
                if (escape.PoI != null)
                    escape.PoI.gameObject.SetActive(false);
                if (escape.compassElement != null)
                    escape.compassElement.Visible = false;

                if (stopMeetupAction != null)
                {
                    stopMeetup.onComplete.RemoveListener(stopMeetupAction);
                    stopMeetupAction = null;
                }
            }
            stopMeetupAction = (UnityEngine.Events.UnityAction)OnStopMeetupComplete;
            stopMeetup.onComplete.AddListener(stopMeetupAction);

            escape.SetEntryTitle("• Steal Cocaine from the SUV\n• Escape before police arrive");
            escape.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            escape.PoILocation.name = "EscapeEntry_POI";
            escape.PoILocation.transform.SetParent(escape.transform);
            escape.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            escape.AutoUpdatePoILocation = true;
            escape.SetState(EQuestState.Inactive, false);
            escape.ParentQuest = this;
            escape.CompleteParentQuest = false;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
#endif
            instance.onMinutePass.Add(new Action(this.MinPass));
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
                    this.hudUI.MainLabel.text = "Four Wheels";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_TalkToJeremy != null)
            {
                QuestEntry_TalkToJeremy.CreateCompassElement();
                if (QuestEntry_TalkToJeremy.compassElement != null)
                    QuestEntry_TalkToJeremy.compassElement.Visible = true;

                if (QuestEntry_TalkToJeremy.PoI != null)
                    QuestEntry_TalkToJeremy.PoI.gameObject.SetActive(true);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            return;
        }

        void CombatStarted()
        {
            if (combatBegun) return;
            combatBegun = true;
            foreach (KeyValuePair<string, CartelGoon> kvp in spawnedGoons)
            {
                if (combatStartedAction != null)
                    kvp.Value.Behaviour.CombatBehaviour.onBegin.RemoveListener(combatStartedAction);
                if (goonDiedAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(goonDiedAction);
            }
            Player p = Player.GetClosestPlayer(spawnedGoons["guard"].CenterPointTransform.position, out float _);
            spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
            spawnedGoons["extra1"].AttackEntity(p.GetComponent<ICombatTargetable>());

            combatStartedAction = null;
            goonDiedAction = null;
        }

        void OnCarMeetupGoonDie()
        {
            if (combatBegun) return;
            combatBegun = true;
            foreach (KeyValuePair<string, CartelGoon> kvp in spawnedGoons)
            {
                if (combatStartedAction != null)
                    kvp.Value.Behaviour.CombatBehaviour.onBegin.RemoveListener(combatStartedAction);
                if (goonDiedAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(goonDiedAction);
            }

            CartelGoon guard = null;
            if (spawnedGoons.Keys.Contains("guard"))
                guard = spawnedGoons["guard"];

            Player p = null;
            if (guard != null)
            {
                p = Player.GetClosestPlayer(spawnedGoons["guard"].CenterPointTransform.position, out float _);
                spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
            }

            if (p != null && spawnedGoons.Keys.Contains("extra1"))
                spawnedGoons["extra1"].AttackEntity(p.GetComponent<ICombatTargetable>());

            combatStartedAction = null;
            goonDiedAction = null;
        }

        private IEnumerator SpawnCarMeetup()
        {
            // parse needed nobs
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject nobSuv = null;
            NetworkObject nobPallet = null;
            NetworkObject nobBrickPress = null;

            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "BoxSUV")
                {
                    nobSuv = prefab;
                }
                else if (prefab?.gameObject?.name == "Pallet")
                {
                    nobPallet = prefab;
                }
                else if (prefab?.gameObject?.name == "BrickPress")
                {
                    nobBrickPress = prefab;
                }
            }

            BrickPress brickPressComp = nobBrickPress.GetComponent<BrickPress>();
            PackagingDefinition brickDef = null;

            if (brickPressComp != null && brickPressComp.BrickPackaging != null)
            {
                Log("Assigned brick packaging");
                brickDef = brickPressComp.BrickPackaging;
            }

            // bricks
            ItemInstance item;
            int qty;
            Func<string, ItemDefinition> GetItem;

#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
            ItemInstance cokeInst = null;
            ItemDefinition def = GetItem("cocaine");
            cokeInst = def.GetDefaultInstance();



#if MONO
            if (cokeInst is ProductItemInstance product)
            {
                if (brickDef != null)
                    product.SetPackaging(brickDef);
                else
                    Log("Brick definition is null");


                if (product.StoredItem != null && product.StoredItem is FilledPackaging_StoredItem storedItemInst)
                {
                    Log("Parsing Brick");
                    GameObject brickOriginal = storedItemInst.Visuals.CocaineVisuals.VisualsContainer.gameObject;
                    if (brickOriginal != null) 
                    { 
                        brickBase = UnityEngine.Object.Instantiate(brickOriginal);
                        brickBase.name = "CokeBrickDecor";
                        brickBase.transform.SetParent(this.transform);
                    }
                    else
                    {
                        Log("Brick original obj is null");
                    }
                }
            }
#else
            ProductItemInstance product = cokeInst.TryCast<ProductItemInstance>();
            if (product != null)
            {
                if (brickDef != null)
                    product.SetPackaging(brickDef);
                else
                    Log("Brick definition is null");

                if (product.StoredItem != null)
                {
                    FilledPackaging_StoredItem storedItemInst = product.StoredItem.TryCast<FilledPackaging_StoredItem>();
                    if (storedItemInst != null)
                    {
                        Log("Parsing Brick");
                        GameObject brickOriginal = storedItemInst.Visuals.CocaineVisuals.VisualsContainer.gameObject;
                        if (brickOriginal != null) 
                        { 
                            brickBase = UnityEngine.Object.Instantiate(brickOriginal);
                            brickBase.name = "CokeBrickDecor";
                            brickBase.transform.SetParent(this.transform);
                        }
                        else
                        {
                            Log("Brick original obj is null");
                        }
                    } 
                }
            }
#endif

            Log("Spawn veh1");
            // spawn SUV1 reward veh
            NetworkObject boxSuv1 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            netManager.ServerManager.Spawn(boxSuv1);
            yield return Wait01;
            if (!registered) yield break;

            boxSuv1.transform.parent = Map.Instance.transform;
            boxSuv1.gameObject.SetActive(true);
            boxSuv1.transform.SetPositionAndRotation(new Vector3(-19.0712f, -4.1883f, 174.4196f), Quaternion.Euler(0.0009f, 0.4645f, 0f));
            rewardStorage = boxSuv1.GetComponent<StorageEntity>();
            //storage.AccessSettings = StorageEntity.EAccessSettings.Full;
            yield return Wait05;
            if (!registered) yield break;

            boxSuv1.GetComponent<Rigidbody>().isKinematic = true;
            spawnedDecor.Add(boxSuv1.gameObject);
            // set storage content
            int maxSlotsToFill = Mathf.RoundToInt(Mathf.Lerp(2f, 4f, questDifficultyScalar - 1f));
            int slotsToFill = UnityEngine.Random.Range(1, maxSlotsToFill);

#if IL2CPP
            ProductItemInstance productReward;
#endif
            for (int i = 0; i < slotsToFill; i++)
            {
                int maxSlotQty = Mathf.RoundToInt(Mathf.Lerp(5f, 10f, questDifficultyScalar - 1f));
                int slotQty = UnityEngine.Random.Range(3, maxSlotQty);

                cokeInst = def.GetDefaultInstance(slotQty);

#if MONO
                if (cokeInst is ProductItemInstance productReward)
                {
                    if (brickDef != null)
                        productReward.SetPackaging(brickDef);
                    else
                        Log("Brick definition is null");
                }
#else
                productReward = cokeInst.TryCast<ProductItemInstance>();
                if (productReward != null)
                {
                    if (brickDef != null)
                        productReward.SetPackaging(brickDef);
                    else
                        Log("Brick definition is null");
                }
#endif

                if (i < rewardStorage.ItemSlots.Count)
                    rewardStorage.ItemSlots[i].InsertItem(cokeInst);
            }

            Log("Spawn veh2");

            // spawn SUV2 lights on vehicle
            NetworkObject boxSuv2 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            netManager.ServerManager.Spawn(boxSuv2);
            yield return Wait01;
            if (!registered) yield break;

            boxSuv2.transform.parent = Map.Instance.transform;
            boxSuv2.gameObject.SetActive(true);
            boxSuv2.transform.SetPositionAndRotation(new Vector3(-21.7171f, -4.1801f, 167.101f), Quaternion.Euler(0.2846f, 78.2566f, 359.4543f));
            yield return Wait05;
            if (!registered) yield break;

            boxSuv2.GetComponent<Rigidbody>().isKinematic = true;
            boxSuv2.GetComponent<VehicleLights>().headLightsOn = true;
            spawnedDecor.Add(boxSuv2.gameObject);

            Log("Spawn pallet");
            // Spawn pallet
            NetworkObject pallet = UnityEngine.Object.Instantiate<NetworkObject>(nobPallet);
            netManager.ServerManager.Spawn(pallet);
            yield return Wait01;
            if (!registered) yield break;

            pallet.transform.parent = Map.Instance.transform;
            pallet.gameObject.SetActive(true);
            pallet.transform.position = new Vector3(-14.93f, -5f, 168.7f);
            Rigidbody palletRb = pallet.GetComponent<Rigidbody>();
            if (palletRb != null)
                palletRb.isKinematic = true;
            spawnedDecor.Add(pallet.gameObject);

            // spawn bricks into tr pallet

            List<Vector3> pileBasePos = new()
            {
                new Vector3(-14.6f, -4.77f, 168.2f),
                new Vector3(-14.6f, -4.77f, 168.6f),
                new Vector3(-14.4f, -4.77f, 168.2f),
                new Vector3(-14.4f, -4.77f, 168.6f),
                new Vector3(-14.8f, -4.77f, 168.7f),
                new Vector3(-15.3f, -4.77f, 168.6f),
            };

            foreach (Vector3 basePos in pileBasePos)
            {
                for (int i = 0; i < UnityEngine.Random.Range(4, 9); i++)
                {
                    yield return Wait01;
                    if (!registered) yield break;

                    GameObject newBrick = UnityEngine.Object.Instantiate(brickBase, pallet.transform);
                    newBrick.transform.position = new Vector3(basePos.x, basePos.y + 0.06f * i, basePos.z);
                    newBrick.transform.rotation = Quaternion.Euler(0f, Mathf.Round(UnityEngine.Random.Range(85f, 95f)), 0f);
                    newBrick.gameObject.SetActive(true);
                    spawnedDecor.Add(newBrick);
                }
            }
            Log("Summon");

            // Summon and configure enemies
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
                } while (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 4 || currentIter == maxIter);
            }

            // first handler guy crouched next to bricks
            CartelGoon goonHandler = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-16.11f, -3.64f, 169.57f));
            goonHandler.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonHandler.Behaviour.ScheduleManager.DisableSchedule();
            goonHandler.transform.rotation = Quaternion.Euler(0f, 116f, 0f);
            goonHandler.Avatar.Animation.SetCrouched(true);
            spawnedGoons.Add("handler", goonHandler);

            // Near the entrance to the alley, should have sight trigger and custom defender beh 
            CartelGoon goonGuard = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-32.68f, -2.54f, 168.73f));
            goonGuard.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            spawnedGoons.Add("guard", goonGuard);

            // Looks at extra 1 faces towards the city
            CartelGoon goonExtra1 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-16.2101f, -3.64f, 173.96f));
            goonExtra1.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonExtra1.Behaviour.ScheduleManager.DisableSchedule();
            goonExtra1.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            spawnedGoons.Add("extra1", goonExtra1);

            // extra 2 behind containers
            CartelGoon goonExtra2 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-29.09f, -2.64f, 158.73f));
            goonExtra2.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonExtra2.Behaviour.ScheduleManager.DisableSchedule();
            goonExtra2.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            spawnedGoons.Add("extra2", goonExtra2);

            goonGuard.AddGoonMate(goonExtra2);
            goonGuard.AddGoonMate(goonExtra1);
            goonGuard.AddGoonMate(goonHandler);
            goonExtra1.AddGoonMate(goonHandler);

            combatStartedAction = (UnityEngine.Events.UnityAction)CombatStarted;
            goonDiedAction = (UnityEngine.Events.UnityAction)OnCarMeetupGoonDie;

            foreach (KeyValuePair<string, CartelGoon> kvp in spawnedGoons)
            {
                if (kvp.Value.Health.IsDead || kvp.Value.Health.IsKnockedOut)
                    kvp.Value.Health.Revive();

                float randMaxHP = Mathf.Round(UnityEngine.Random.Range(160f, 230f) / 10f) * 10f;
                kvp.Value.Health.MaxHealth = Mathf.Round(Mathf.Lerp(100f, randMaxHP, questDifficultyScalar - 1f));
                kvp.Value.Health.Health = Mathf.Round(Mathf.Lerp(100f, randMaxHP, questDifficultyScalar - 1f));
                kvp.Value.Movement.MoveSpeedMultiplier = Mathf.Lerp(UnityEngine.Random.Range(1.1f, 1.3f), 1.5f, questDifficultyScalar - 1f);

                kvp.Value.Behaviour.CombatBehaviour.onBegin.AddListener(combatStartedAction);
                kvp.Value.Health.onDieOrKnockedOut.AddListener(goonDiedAction);

                if (GiveUpRange == 0f)
                {
                    GiveUpRange = kvp.Value.Behaviour.CombatBehaviour.GiveUpRange;
                    GiveUpAfterSuccessfulHits = kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                    DefaultSearchTime = kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime;
                }

                kvp.Value.Behaviour.CombatBehaviour.GiveUpRange = 120f;
                kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 60;
                kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime = 300f;

                spawnedGoonsGuids.Add(kvp.Value.GUID.ToString());
            }

            // Instantiate default weapons and assign to summoned and then configure stats if they are not beefed enough

            // extras got m1911
#if MONO
            GameObject m1911Go = Resources.Load("Avatar/Equippables/M1911") as GameObject;
#else
            UnityEngine.Object m1911Obj = Resources.Load("Avatar/Equippables/M1911");
            GameObject m1911Go = m1911Obj.TryCast<GameObject>();
#endif

            AvatarEquippable m1911Equippable = UnityEngine.Object.Instantiate<GameObject>(m1911Go, new Vector3(0f, -5f, 0f), Quaternion.identity, this.transform).GetComponent<AvatarEquippable>();
#if MONO
            if (m1911Equippable is AvatarWeapon weaponm1911)
            {
                goonExtra1.Behaviour.CombatBehaviour.DefaultWeapon = weaponm1911;
                goonExtra2.Behaviour.CombatBehaviour.DefaultWeapon = weaponm1911;
            }
#else
            AvatarWeapon weaponm1911 = m1911Equippable.TryCast<AvatarWeapon>();
            if (weaponm1911 != null) 
            {
                goonExtra1.Behaviour.CombatBehaviour.DefaultWeapon = weaponm1911;
                goonExtra2.Behaviour.CombatBehaviour.DefaultWeapon = weaponm1911;
            }
#endif


            // guard has shotgun
#if MONO
            GameObject shotgunGo = Resources.Load("Avatar/Equippables/PumpShotgun") as GameObject;
#else
            UnityEngine.Object shotgunObj = Resources.Load("Avatar/Equippables/PumpShotgun");
            GameObject shotgunGo = shotgunObj.TryCast<GameObject>();
#endif

            AvatarEquippable shotgunEquippable = UnityEngine.Object.Instantiate<GameObject>(shotgunGo, new Vector3(0f, -5f, 0f), Quaternion.identity, this.transform).GetComponent<AvatarEquippable>();
#if MONO
            if (shotgunEquippable is AvatarWeapon weaponShotgun)
            {
                goonGuard.Behaviour.CombatBehaviour.DefaultWeapon = weaponShotgun;
            }
#else
            AvatarWeapon weaponShotgun = shotgunEquippable.TryCast<AvatarWeapon>();
            if (weaponShotgun != null) 
            {
                goonGuard.Behaviour.CombatBehaviour.DefaultWeapon = weaponShotgun;
            }
#endif
            goonGuard.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/PumpShotgun");


            // handler has knife
#if MONO
            GameObject knifeGo = Resources.Load("Avatar/Equippables/Knife") as GameObject;
#else
            UnityEngine.Object knifeObj = Resources.Load("Avatar/Equippables/Knife");
            GameObject knifeGo = knifeObj.TryCast<GameObject>();
#endif

            AvatarEquippable knifeEquippable = UnityEngine.Object.Instantiate<GameObject>(knifeGo, new Vector3(0f, -5f, 0f), Quaternion.identity, this.transform).GetComponent<AvatarEquippable>();
#if MONO
            if (knifeEquippable is AvatarWeapon weaponKnife)
            {
                goonHandler.Behaviour.CombatBehaviour.DefaultWeapon = weaponKnife;
            }
#else
            AvatarWeapon weaponKnife = knifeEquippable.TryCast<AvatarWeapon>();
            if (weaponKnife != null) 
            {
                goonHandler.Behaviour.CombatBehaviour.DefaultWeapon = weaponKnife;
            }
#endif
            void EndCrouchOnCombat()
            {
                goonHandler.Avatar.Animation.SetCrouched(false);
                if (combatEndCrouchAction != null)
                {
                    goonHandler.Behaviour.CombatBehaviour.onBegin.RemoveListener(combatEndCrouchAction);
                    combatEndCrouchAction = null;
                }
            }
            combatEndCrouchAction = (UnityEngine.Events.UnityAction)EndCrouchOnCombat;
            goonHandler.Behaviour.CombatBehaviour.onBegin.AddListener(combatEndCrouchAction);

            yield return null;
        }

        private IEnumerator PlayerSightedByGuard(Player p)
        {
            if (!spawnedGoons.Keys.Contains("guard"))
            {
                Log("Spawned goons does not contain key guard");
                yield break;
            }

            DialogueController controller = spawnedGoons["guard"].DialogueHandler.gameObject.GetComponent<DialogueController>();
            int timesWarned = 0;
            Vector3 closestPoint = Vector3.zero;
            while (registered && !combatBegun)
            {
                if (!spawnedGoons.Keys.Contains("guard"))
                {
                    Log("Spawned goons does not contain key guard");
                    yield break;
                }

                if (spawnedGoons["guard"].Health.IsDead || spawnedGoons["guard"].Health.IsKnockedOut) break;

                spawnedGoons["guard"].Movement.GetClosestReachablePoint(p.CenterPointTransform.position, out closestPoint);

                if (closestPoint != Vector3.zero)
                    spawnedGoons["guard"].Movement.SetDestination(closestPoint);

                yield return Wait2;
                if (!registered || combatBegun) break;
                spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);

                if (timesWarned < 2)
                {
                    switch (UnityEngine.Random.Range(0, 4))
                    {
                        case 0:
                            controller.handler.WorldspaceRend.ShowText($"Get out of here you punk!", 3f);
                            break;
                        case 1:
                            controller.handler.WorldspaceRend.ShowText($"This ain't your business!", 3f);
                            break;
                        case 2:
                            controller.handler.WorldspaceRend.ShowText($"Screw off mate!", 3f);
                            break;
                        case 3:
                            controller.handler.WorldspaceRend.ShowText($"You're not invited here, get lost!", 3f);
                            break;
                    }
                    timesWarned++;
                    spawnedGoons["guard"].PlayVO(EVOLineType.Command);
                }
                else if (timesWarned == 2)
                {
                    switch (UnityEngine.Random.Range(0, 3))
                    {
                        case 0:
                            controller.handler.WorldspaceRend.ShowText($"Last warning buddy!", 3f);
                            break;
                        case 1:
                            controller.handler.WorldspaceRend.ShowText($"Walk away or you get shot.", 3f);
                            break;
                        case 2:
                            controller.handler.WorldspaceRend.ShowText($"One of us is going to leave in a casket...", 5f);
                            break;
                    }
                    spawnedGoons["guard"].PlayVO(EVOLineType.Angry);
                    spawnedGoons["guard"].Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 10f, 1);
                    spawnedGoons["guard"].Behaviour.CombatBehaviour.SetWeaponRaised(true);
                    yield return Wait05;
                    if (!registered || combatBegun) break;

                    spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);
                    timesWarned++;
                }
                else if (timesWarned == 3 && !combatBegun)
                {
                    spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
                    break;
                }
                
                yield return Wait2;
                if (!registered || combatBegun) break;
                spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);
            }
            yield return null;
        }

        public override void MinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || carMeetupCompleted || this.State != EQuestState.Active) return;

#if MONO
            base.MinPass(); // Is this necessary in mono or does cause recursion??
#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }

            if (QuestEntry_StopCarMeetup != null && QuestEntry_StopCarMeetup.State == EQuestState.Active)
            {
                if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None)
                {
                    carMeetupCompleted = true;
                    QuestEntry_StopCarMeetup.SetState(EQuestState.Failed);
                    Fail();
                    coros.Add(MelonCoroutines.Start(CleanupCarMeetup()));
                    return;
                }

                if (TimeManager.Instance.CurrentTime >= 2100 || TimeManager.Instance.CurrentTime <= 359)
                {
                    // in time window do nothing
                }
                else
                {
                    carMeetupCompleted = true;
                    QuestEntry_StopCarMeetup.SetState(EQuestState.Failed);
                    Fail();
                    coros.Add(MelonCoroutines.Start(CleanupCarMeetup()));
                    return;
                }

                if (combatBegun)
                {
                    int goonsDead = 0;
                    foreach (KeyValuePair<string, CartelGoon> kvp in spawnedGoons)
                    {
                        if (kvp.Value.Health.IsDead || kvp.Value.Health.IsKnockedOut)
                            goonsDead++;
                    }
                    if (goonsDead == spawnedGoons.Count)
                    {
                        QuestEntry_StopCarMeetup.Complete();
                        return;
                    }
                    return;
                }
                else if (!playerSightedActive && spawnedGoons.Keys.Contains("guard"))
                {
                    Player nearest = Player.GetClosestPlayer(QuestEntry_StopCarMeetup.PoI.transform.position, out _);
                    // Check player distance if nearby 30 units evaluate vision of guard
                    if (Vector3.Distance(nearest.CenterPointTransform.position, QuestEntry_StopCarMeetup.PoI.transform.position) < 30f)
                    {
                        // if guard is guaranteed to be visible to player then higher chance for it to face player
                        if (nearest.IsPointVisibleToPlayer(spawnedGoons["guard"].CenterPointTransform.position))
                        {
                            if (UnityEngine.Random.Range(0f, 1f) > 0.80f)
                            {
                                spawnedGoons["guard"].Movement.FacePoint(nearest.CenterPointTransform.position, lerpTime: 1f);
                            }
                        }
                        // sometimes rotate towards if player is acting sneaky
                        else if (UnityEngine.Random.Range(0f, 1f) > 0.90f && Vector3.Distance(nearest.CenterPointTransform.position, QuestEntry_StopCarMeetup.PoI.transform.position) < 15f)
                        {
                            spawnedGoons["guard"].Movement.FacePoint(nearest.CenterPointTransform.position, lerpTime: 1f);
                        }

                        if (spawnedGoons["guard"].Awareness.VisionCone.IsPlayerVisible(nearest))
                        {
                            playerSightedActive = true;
                            coros.Add(MelonCoroutines.Start(PlayerSightedByGuard(nearest)));
                        }
                    }
                }
            }

            if (QuestEntry_EscapeNorthWaterfront != null && QuestEntry_EscapeNorthWaterfront.State == EQuestState.Active)
            {
                if (investigationActive)
                {
                    if (Player.Local.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None)
                    {
                        carMeetupCompleted = true;
                        coros.Add(MelonCoroutines.Start(QuestCarMeetupReward()));
                        Complete();
                        coros.Add(MelonCoroutines.Start(CleanupCarMeetup()));
                        return;
                    }

                    if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None && Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.Investigating)
                    {
                        carMeetupCompleted = true;
                        Fail();
                        coros.Add(MelonCoroutines.Start(CleanupCarMeetup()));
                        return;
                    }
                }
            }
        }


        private IEnumerator StartLateInvestigation()
        {
            yield return Wait10;

            Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Investigating);

            // this is not guaranteed dispatch, limited by offc qty in station + car limit
            // works without it too but officers should traverse to player?
            // todo fix
#if MONO
            PoliceStation.PoliceStations.FirstOrDefault().Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#else
            PoliceStation.PoliceStations[0].Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#endif
            investigationActive = true;

            yield return null;
        }
        void OnDialogComplete()
        {
            QuestEntry_TalkToJeremy.Complete();
        }
        private void HourPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || carMeetupCompleted || this == null || this.State != EQuestState.Active) return;

            Log("HourPass In Quest");
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_TalkToJeremy != null && QuestEntry_TalkToJeremy.State == EQuestState.Active)
            {
                Log("State Talk to Jeremy");
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2059 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 2102)
                {
                    // Add when time is 21:00
                    Action callback = new Action(OnDialogComplete);
                    coros.Add(MelonCoroutines.Start(GenJeremyOption(callback)));
                }
                else if (jeremyDiagIndex != -1 && NetworkSingleton<TimeManager>.Instance.CurrentTime < 2059 && !jeremyDialogueActive)
                {
                    // Remove if dialogue option not consumed yet and time is smaller than 20:59, e.g. option not consumed until midnight
                    DialogueController controller = jeremy.DialogueHandler.gameObject.GetComponent<DialogueController>();
                    coros.Add(MelonCoroutines.Start(DisposeJeremyChoice(controller)));
                    // Quest will still stay active and option appears next day basically
                }
            }
        }

        private IEnumerator CleanupCarMeetup()
        {

            yield return Wait10;
            if (!registered) yield break;

            foreach (GameObject go in spawnedDecor)
            {
                yield return Wait05;
                if (!registered) yield break;

                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
            spawnedDecor.Clear();

            foreach (KeyValuePair<string, CartelGoon> kvp in spawnedGoons)
            {
                yield return Wait05;
                if (!registered) yield break;

                if (combatStartedAction != null)
                    kvp.Value.Behaviour.CombatBehaviour.onBegin.RemoveListener(combatStartedAction);

                if (goonDiedAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(goonDiedAction);

                if (combatEndCrouchAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(combatEndCrouchAction);

                if (kvp.Value.Health.IsDead)
                    kvp.Value.Health.Revive();

                kvp.Value.Behaviour.CombatBehaviour.Disable_Networked(null);

                kvp.Value.Movement.MoveSpeedMultiplier = 1f;
                kvp.Value.Health.MaxHealth = 100f;
                kvp.Value.Health.Health = 100f;

                kvp.Value.Behaviour.CombatBehaviour.GiveUpRange = GiveUpRange;
                kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = GiveUpAfterSuccessfulHits;
                kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime = DefaultSearchTime;

                kvp.Value.Behaviour.ScheduleManager.EnableSchedule();
                kvp.Value.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);

                if (kvp.Value.IsGoonSpawned)
                {
                    kvp.Value.Despawn();
                }
            }
            spawnedGoons.Clear();
            spawnedGoonsGuids.Clear();

            combatStartedAction = null;
            goonDiedAction = null;
            combatEndCrouchAction = null;

            yield return null;
        }

        public QuestEntry QuestEntry_TalkToJeremy;

        public QuestEntry QuestEntry_StopCarMeetup;

        public QuestEntry QuestEntry_EscapeNorthWaterfront;
    }
}
