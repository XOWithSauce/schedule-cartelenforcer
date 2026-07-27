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
    public class Quest_CarMeetup : ModQuestBase
    {
        protected CarMeetupHelper _helper;
#if MONO
        public Quest_CarMeetup() 
        {
            _helper = new CarMeetupHelper(this);
        }
#else
        public Quest_CarMeetup(IntPtr ptr) : base(ptr) 
        {
            _helper = new CarMeetupHelper(this);
        }
        public Quest_CarMeetup() : base(ClassInjector.DerivedConstructorPointer<Quest_CarMeetup>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        public float questDifficultyScalar = 1f;

        public readonly List<Vector3> pileBasePos = new()
        {
            new Vector3(-14.6f, -4.77f, 168.2f),
            new Vector3(-14.6f, -4.77f, 168.6f),
            new Vector3(-14.4f, -4.77f, 168.2f),
            new Vector3(-14.4f, -4.77f, 168.6f),
            new Vector3(-14.8f, -4.77f, 168.7f),
            new Vector3(-15.3f, -4.77f, 168.6f),
        };

        public GameObject brickBase = null;
        public List<GameObject> spawnedDecor = new();

        public Dictionary<string, CartelGoon> spawnedGoons = new();
        public List<string> spawnedGoonsGuids = new();

        public StorageEntity rewardStorage = null;

        public bool combatBegun = false;
        public bool playerSightedActive = false;
        public bool investigationActive = false;

        // store the combat variables
        public bool hasSavedCombatVariables = false;
        public float GiveUpRange = 0f;
        public int GiveUpAfterSuccessfulHits = 0;
        public float DefaultSearchTime = 0f;

        // Store the callbacks for goon combat logic
        public UnityAction combatStartedAction = null;
        public UnityAction goonDiedAction = null;
        public UnityAction combatEndCrouchAction = null;

        public QuestEntry QuestEntry_TalkToJeremy;
        private UnityAction _talkToJeremyAction;

        public QuestEntry QuestEntry_StopCarMeetup;
        private UnityAction _stopCarMeetupAction;

        public QuestEntry QuestEntry_EscapeNorthWaterfront;
        private UnityAction _escapeAction;

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
            Log("Quest_CarMeetup: End method called.");
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

            _helper.InitializeQuest("Four Wheels", xp: Mathf.RoundToInt(300f * questDifficultyScalar));

            _talkToJeremyAction = (UnityAction)OnTalkToJeremyComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_TalkToJeremy,
                name: "TalkToJeremy",
                title: "Ask Jeremy about the cars at their home after curfew",
                new PoIConfig(true, false, false, poiPosition: new Vector3(69.00f, 5.93f, -119.09f)),
                _talkToJeremyAction);

            _stopCarMeetupAction = (UnityAction)OnStopCarMeetupComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_StopCarMeetup,
                name: "StopCarMeetup",
                title: "• Stop the Benzies from transporting cocaine\n• Avoid police attention",
                new PoIConfig(true, false, false, poiPosition: new Vector3(-32.68f, -2.54f, 168.73f)),
                _stopCarMeetupAction);

            _helper.InitializeQuestEntry(ref QuestEntry_StopCarMeetup,
                name: "Escape",
                title: "• Steal Cocaine from the SUV\n• Escape before police arrive",
                new PoIConfig(false, false, false));

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;

            var action = (Action)OnMinPass;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
            instance.onMinutePass.Add(action);
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            _helper.StartQuestFromEntry(QuestEntry_TalkToJeremy);
        }


        public override void OnMinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || carMeetupCompleted || this.State != EQuestState.Active) return;

#if MONO
            base.OnMinPass();
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
                    coros.Add(MelonCoroutines.Start(_helper.CleanupCarMeetup()));
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
                    coros.Add(MelonCoroutines.Start(_helper.CleanupCarMeetup()));
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
                    Player nearest = PlayerManager.GetClosestPlayer(QuestEntry_StopCarMeetup.PoI.transform.position, out _);
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
                            coros.Add(MelonCoroutines.Start(_helper.PlayerSightedByGuard(nearest)));
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
                        coros.Add(MelonCoroutines.Start(_helper.CleanupCarMeetup()));
                        return;
                    }

                    if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None && Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.Investigating)
                    {
                        carMeetupCompleted = true;
                        Fail();
                        coros.Add(MelonCoroutines.Start(_helper.CleanupCarMeetup()));
                        return;
                    }
                }
            }
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

        void OnDialogComplete()
        {
            QuestEntry_TalkToJeremy.Complete();
        }
        public void CombatStarted()
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
            Player p = PlayerManager.GetClosestPlayer(spawnedGoons["guard"].CenterPointTransform.position, out float _);
            spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
            spawnedGoons["extra1"].AttackEntity(p.GetComponent<ICombatTargetable>());

            combatStartedAction = null;
            goonDiedAction = null;
        }
        public void OnCarMeetupGoonDie()
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
                p = PlayerManager.GetClosestPlayer(spawnedGoons["guard"].CenterPointTransform.position, out float _);
                spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
            }

            if (p != null && spawnedGoons.Keys.Contains("extra1"))
                spawnedGoons["extra1"].AttackEntity(p.GetComponent<ICombatTargetable>());

            combatStartedAction = null;
            goonDiedAction = null;
        }

        private void OnTalkToJeremyComplete()
        {
            if (QuestEntry_TalkToJeremy != null && QuestEntry_TalkToJeremy.State == EQuestState.Failed) return;
            if (QuestEntry_StopCarMeetup == null) return;

            QuestEntry_StopCarMeetup.Begin();
            UpdateQuestMapLogo(QuestEntry_StopCarMeetup);

            coros.Add(MelonCoroutines.Start(_helper.SpawnCarMeetup()));

            if (_talkToJeremyAction != null)
            {
                QuestEntry_TalkToJeremy.onComplete.RemoveListener(_talkToJeremyAction);
                _talkToJeremyAction = null;
            }
            return;
        }
        private void OnStopCarMeetupComplete()
        {
            if (QuestEntry_StopCarMeetup != null && QuestEntry_StopCarMeetup.State == EQuestState.Failed) return;
            if (QuestEntry_EscapeNorthWaterfront == null) return;
            coros.Add(MelonCoroutines.Start(_helper.StartLateInvestigation()));
            rewardStorage.AccessSettings = StorageEntity.EAccessSettings.Full;

            QuestEntry_EscapeNorthWaterfront.Begin();
            if (QuestEntry_EscapeNorthWaterfront.compassElement != null)
                QuestEntry_EscapeNorthWaterfront.compassElement.Visible = false;

            if (_stopCarMeetupAction != null)
            {
                QuestEntry_StopCarMeetup.onComplete.RemoveListener(_stopCarMeetupAction);
                _stopCarMeetupAction = null;
            }
            return;
        }
    }

    public class CarMeetupHelper : QuestHelperBase<Quest_CarMeetup>
    {
        public CarMeetupHelper(Quest_CarMeetup quest) : base(quest) { }

        public IEnumerator SpawnCarMeetup()
        {
            // parse needed nobs
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject nobSuv = null;
            GameObject objPallet = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "BoxSUV")
                {
                    nobSuv = prefab;
                }
            }

            GameObject palletContainer = GameObject.Find("Map/Hyland Point/Region_Northtown/Hardware Store/Loading bay/PalletStand (2)/Container");
            int n = 0;
            for (int i = 0; i < palletContainer.transform.childCount; i++)
            {
                Transform currentObject = palletContainer.transform.GetChild(i);
                if (currentObject.name == "Pallet")
                {
                    n++;
                    if (n == 2)
                    {
                        objPallet = currentObject.gameObject;
                        break;
                    }
                }
            }

            if (objPallet == null)
                Log("Failed to find Pallet obj for quest props");

            // bricks
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
                if (CartelInventory.brickPackaging != null)
                    product.SetPackaging(CartelInventory.brickPackaging);
                else
                    Log("Brick definition is null");

                if (product.StoredItem != null && product.StoredItem is FilledPackaging_StoredItem storedItemInst)
                {
                    Log("Parsing Brick");
                    GameObject brickOriginal = storedItemInst.Visuals.CocaineVisuals.VisualsContainer.gameObject;
                    if (brickOriginal != null) 
                    { 
                        _quest.brickBase = UnityEngine.Object.Instantiate(brickOriginal);
                        _quest.brickBase.name = "CokeBrickDecor";
                        _quest.brickBase.transform.SetParent(_quest.transform);
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
                if (CartelInventory.brickPackaging != null)
                    product.SetPackaging(CartelInventory.brickPackaging);
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
                            _quest.brickBase = UnityEngine.Object.Instantiate(brickOriginal);
                            _quest.brickBase.name = "CokeBrickDecor";
                            _quest.brickBase.transform.SetParent(_quest.transform);
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
            _quest.rewardStorage = boxSuv1.GetComponent<StorageEntity>();
            //storage.AccessSettings = StorageEntity.EAccessSettings.Full;
            yield return Wait05;
            if (!registered) yield break;

            boxSuv1.GetComponent<Rigidbody>().isKinematic = true;
            _quest.spawnedDecor.Add(boxSuv1.gameObject);
            // set storage content
            int maxSlotsToFill = Mathf.RoundToInt(Mathf.Lerp(2f, 4f, _quest.questDifficultyScalar - 1f));
            int slotsToFill = UnityEngine.Random.Range(1, maxSlotsToFill);

#if IL2CPP
            ProductItemInstance productReward;
#endif
            for (int i = 0; i < slotsToFill; i++)
            {
                int maxSlotQty = Mathf.RoundToInt(Mathf.Lerp(5f, 10f, _quest.questDifficultyScalar - 1f));
                int slotQty = UnityEngine.Random.Range(3, maxSlotQty);

                cokeInst = def.GetDefaultInstance(slotQty);

#if MONO
                if (cokeInst is ProductItemInstance productReward)
                {
                    if (CartelInventory.brickPackaging != null)
                        productReward.SetPackaging(CartelInventory.brickPackaging);
                    else
                        Log("Brick definition is null");
                }
#else
                productReward = cokeInst.TryCast<ProductItemInstance>();
                if (productReward != null)
                {
                    if (CartelInventory.brickPackaging != null)
                        productReward.SetPackaging(CartelInventory.brickPackaging);
                    else
                        Log("Brick definition is null");
                }
#endif

                if (i < _quest.rewardStorage.ItemSlots.Count)
                    _quest.rewardStorage.ItemSlots[i].InsertItem(cokeInst);
            }

            Log("Spawn veh2");

            // spawn SUV2 lights on vehicle
            NetworkObject boxSuv2 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            // For some reason the second vehicle consistently clips through map if not set pos+rot before active
            boxSuv2.transform.SetPositionAndRotation(new Vector3(-20.2015f, -3.9164f, 167.2993f), Quaternion.Euler(2.5122f, 79.6851f, 359.3438f));
            netManager.ServerManager.Spawn(boxSuv2);
            yield return Wait01;
            if (!registered) yield break;

            boxSuv2.transform.parent = Map.Instance.transform;
            boxSuv2.gameObject.SetActive(true);
            yield return Wait05;
            if (!registered) yield break;

            boxSuv2.GetComponent<Rigidbody>().isKinematic = true;
            boxSuv2.GetComponent<VehicleLights>().HeadlightsOn = true;
            _quest.spawnedDecor.Add(boxSuv2.gameObject);

            Log("Spawn pallet");
            GameObject pallet = UnityEngine.Object.Instantiate<GameObject>(objPallet);
            pallet.transform.parent = Map.Instance.transform;
            pallet.gameObject.SetActive(true);
            pallet.transform.position = new Vector3(-14.93f, -5f, 168.7f);
            Rigidbody palletRb = pallet.GetComponent<Rigidbody>();
            if (palletRb != null)
                palletRb.isKinematic = true;
            _quest.spawnedDecor.Add(pallet.gameObject);


            // spawn bricks into tr pallet
            foreach (Vector3 basePos in _quest.pileBasePos)
            {
                for (int i = 0; i < UnityEngine.Random.Range(4, 9); i++)
                {
                    yield return Wait01;
                    if (!registered) yield break;

                    GameObject newBrick = UnityEngine.Object.Instantiate(_quest.brickBase, pallet.transform);
                    newBrick.transform.position = new Vector3(basePos.x, basePos.y + 0.06f * i, basePos.z);
                    newBrick.transform.rotation = Quaternion.Euler(0f, Mathf.Round(UnityEngine.Random.Range(85f, 95f)), 0f);
                    newBrick.gameObject.SetActive(true);
                    _quest.spawnedDecor.Add(newBrick);
                }
            }
            Log("Summon");

            // Summon and configure enemies
            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 4)
            {
                foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
                {
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count >= 4) break;
                    if (!NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(goon)) continue;

                    if (goon.IsGoonSpawned && (goon.Health.IsDead || goon.Health.IsKnockedOut))
                    {
                        goon.Despawn();
                    }
                }
            }

            // first handler guy crouched next to bricks
            CartelGoon goonHandler = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-16.11f, -3.64f, 169.57f));
            goonHandler.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonHandler.Behaviour.ScheduleManager.DisableSchedule();
            goonHandler.transform.rotation = Quaternion.Euler(0f, 116f, 0f);
            goonHandler.Avatar.Animation.SetCrouched(true);
            _quest.spawnedGoons.Add("handler", goonHandler);

            // Near the entrance to the alley, should have sight trigger and custom defender beh 
            CartelGoon goonGuard = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-32.68f, -2.54f, 168.73f));
            goonGuard.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            _quest.spawnedGoons.Add("guard", goonGuard);

            // Looks at extra 1 faces towards the city
            CartelGoon goonExtra1 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-16.2101f, -3.64f, 173.96f));
            goonExtra1.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonExtra1.Behaviour.ScheduleManager.DisableSchedule();
            goonExtra1.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _quest.spawnedGoons.Add("extra1", goonExtra1);

            // extra 2 behind containers
            CartelGoon goonExtra2 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-29.09f, -2.64f, 158.73f));
            goonExtra2.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonExtra2.Behaviour.ScheduleManager.DisableSchedule();
            goonExtra2.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            _quest.spawnedGoons.Add("extra2", goonExtra2);

            goonGuard.AddGoonMate(goonExtra2);
            goonGuard.AddGoonMate(goonExtra1);
            goonGuard.AddGoonMate(goonHandler);
            goonExtra1.AddGoonMate(goonHandler);

            _quest.combatStartedAction = (UnityEngine.Events.UnityAction)_quest.CombatStarted;
            _quest.goonDiedAction = (UnityEngine.Events.UnityAction)_quest.OnCarMeetupGoonDie;

            foreach (KeyValuePair<string, CartelGoon> kvp in _quest.spawnedGoons)
            {
                if (kvp.Value.Health.IsDead || kvp.Value.Health.IsKnockedOut)
                    kvp.Value.Health.Revive();

                float randMaxHP = Mathf.Round(UnityEngine.Random.Range(160f, 230f) / 10f) * 10f;
                kvp.Value.NPCData.Health.MaxHealth = Mathf.Round(Mathf.Lerp(100f, randMaxHP, _quest.questDifficultyScalar - 1f));
                kvp.Value.Health.Health = Mathf.Round(Mathf.Lerp(100f, randMaxHP, _quest.questDifficultyScalar - 1f));
                kvp.Value.Movement.MoveSpeedMultiplier = Mathf.Lerp(UnityEngine.Random.Range(1.1f, 1.3f), 1.5f, _quest.questDifficultyScalar - 1f);

                kvp.Value.Behaviour.CombatBehaviour.onBegin.AddListener(_quest.combatStartedAction);
                kvp.Value.Health.onDieOrKnockedOut.AddListener(_quest.goonDiedAction);

                if (!_quest.hasSavedCombatVariables)
                {
                    _quest.GiveUpRange = kvp.Value.Behaviour.CombatBehaviour.GiveUpRange;
                    _quest.GiveUpAfterSuccessfulHits = kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                    _quest.DefaultSearchTime = kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime;
                }

                kvp.Value.Behaviour.CombatBehaviour.GiveUpRange = 120f;
                kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 60;
                kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime = 300f;

                _quest.spawnedGoonsGuids.Add(kvp.Value.GUID.ToString());
            }

            // Instantiate default weapons and assign to summoned and then configure stats if they are not beefed enough

            // extras got m1911
#if MONO
            GameObject m1911Go = Resources.Load("Avatar/Equippables/M1911") as GameObject;
#else
            UnityEngine.Object m1911Obj = Resources.Load("Avatar/Equippables/M1911");
            GameObject m1911Go = m1911Obj.TryCast<GameObject>();
#endif

            AvatarEquippable m1911Equippable = UnityEngine.Object.Instantiate<GameObject>(m1911Go, new Vector3(0f, -5f, 0f), Quaternion.identity, _quest.transform).GetComponent<AvatarEquippable>();
            AvatarWeapon weaponm1911 = null;
#if MONO
            if (m1911Equippable is AvatarWeapon)
                weaponm1911 = m1911Equippable as AvatarWeapon;
#else
            weaponm1911 = m1911Equippable.TryCast<AvatarWeapon>();
#endif
            if (weaponm1911 != null)
            {
                goonExtra1.Behaviour.CombatBehaviour.SetDefaultWeapon(weaponm1911);
                goonExtra2.Behaviour.CombatBehaviour.SetDefaultWeapon(weaponm1911);
            }


            // guard has shotgun
#if MONO
            GameObject shotgunGo = Resources.Load("Avatar/Equippables/PumpShotgun") as GameObject;
#else
            UnityEngine.Object shotgunObj = Resources.Load("Avatar/Equippables/PumpShotgun");
            GameObject shotgunGo = shotgunObj.TryCast<GameObject>();
#endif

            AvatarEquippable shotgunEquippable = UnityEngine.Object.Instantiate<GameObject>(shotgunGo, new Vector3(0f, -5f, 0f), Quaternion.identity, _quest.transform).GetComponent<AvatarEquippable>();
            AvatarWeapon weaponShotgun = null;
#if MONO
            if (shotgunEquippable is AvatarWeapon)
                weaponShotgun = shotgunEquippable as AvatarWeapon;
#else
            weaponShotgun = shotgunEquippable.TryCast<AvatarWeapon>();
#endif
            if (weaponShotgun != null)
                goonGuard.Behaviour.CombatBehaviour.SetDefaultWeapon(weaponShotgun);


            // handler has knife
#if MONO
            GameObject knifeGo = Resources.Load("Avatar/Equippables/Knife") as GameObject;
#else
            UnityEngine.Object knifeObj = Resources.Load("Avatar/Equippables/Knife");
            GameObject knifeGo = knifeObj.TryCast<GameObject>();
#endif

            AvatarEquippable knifeEquippable = UnityEngine.Object.Instantiate<GameObject>(knifeGo, new Vector3(0f, -5f, 0f), Quaternion.identity, _quest.transform).GetComponent<AvatarEquippable>();
            AvatarWeapon weaponKnife = null;
#if MONO
            if (knifeEquippable is AvatarWeapon)
                weaponKnife = knifeEquippable as AvatarWeapon;
#else
            weaponKnife = knifeEquippable.TryCast<AvatarWeapon>();
#endif
            if (weaponKnife != null)
                goonHandler.Behaviour.CombatBehaviour.SetDefaultWeapon(weaponKnife);

            void EndCrouchOnCombat()
            {
                goonHandler.Avatar.Animation.SetCrouched(false);
                if (_quest.combatEndCrouchAction != null)
                {
                    goonHandler.Behaviour.CombatBehaviour.onBegin.RemoveListener(_quest.combatEndCrouchAction);
                    _quest.combatEndCrouchAction = null;
                }
            }
            _quest.combatEndCrouchAction = (UnityEngine.Events.UnityAction)EndCrouchOnCombat;
            goonHandler.Behaviour.CombatBehaviour.onBegin.AddListener(_quest.combatEndCrouchAction);

            yield break;
        }
        public IEnumerator PlayerSightedByGuard(Player p)
        {
            if (!_quest.spawnedGoons.Keys.Contains("guard"))
            {
                Log("Spawned goons does not contain key guard");
                yield break;
            }

            DialogueController controller = _quest.spawnedGoons["guard"].DialogueHandler.gameObject.GetComponent<DialogueController>();
            int timesWarned = 0;
            Vector3 closestPoint = Vector3.zero;
            while (registered && !_quest.combatBegun)
            {
                if (!_quest.spawnedGoons.Keys.Contains("guard"))
                {
                    Log("Spawned goons does not contain key guard");
                    yield break;
                }

                if (_quest.spawnedGoons["guard"].Health.IsDead || _quest.spawnedGoons["guard"].Health.IsKnockedOut) break;

                _quest.spawnedGoons["guard"].Movement.GetClosestReachablePoint(p.CenterPointTransform.position, out closestPoint);

                if (closestPoint != Vector3.zero)
                    _quest.spawnedGoons["guard"].Movement.SetDestination(closestPoint);

                yield return Wait2;
                if (!registered || _quest.combatBegun) break;
                _quest.spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);

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
                    _quest.spawnedGoons["guard"].PlayVO(EVOLineType.Command);
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
                    _quest.spawnedGoons["guard"].PlayVO(EVOLineType.Angry);
                    _quest.spawnedGoons["guard"].Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 10f, 1);
                    _quest.spawnedGoons["guard"].Behaviour.CombatBehaviour.SetWeaponRaised(true);
                    yield return Wait05;
                    if (!registered || _quest.combatBegun) break;

                    _quest.spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);
                    timesWarned++;
                }
                else if (timesWarned == 3 && !_quest.combatBegun)
                {
                    _quest.spawnedGoons["guard"].AttackEntity(p.GetComponent<ICombatTargetable>());
                    break;
                }

                yield return Wait2;
                if (!registered || _quest.combatBegun) break;
                _quest.spawnedGoons["guard"].Movement.FacePoint(p.CenterPointTransform.position);
            }
            yield break;
        }
        public IEnumerator StartLateInvestigation()
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
            _quest.investigationActive = true;

            yield break;
        }
        public IEnumerator CleanupCarMeetup()
        {

            yield return Wait10;
            if (!registered) yield break;

            foreach (GameObject go in _quest.spawnedDecor)
            {
                yield return Wait05;
                if (!registered) yield break;

                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
            _quest.spawnedDecor.Clear();

            foreach (KeyValuePair<string, CartelGoon> kvp in _quest.spawnedGoons)
            {
                yield return Wait05;
                if (!registered) yield break;

                if (_quest.combatStartedAction != null)
                    kvp.Value.Behaviour.CombatBehaviour.onBegin.RemoveListener(_quest.combatStartedAction);

                if (_quest.goonDiedAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(_quest.goonDiedAction);

                if (_quest.combatEndCrouchAction != null)
                    kvp.Value.Health.onDieOrKnockedOut.RemoveListener(_quest.combatEndCrouchAction);

                if (kvp.Value.Health.IsDead)
                    kvp.Value.Health.Revive();

                kvp.Value.Behaviour.CombatBehaviour.Disable_Networked(null);

                kvp.Value.Movement.MoveSpeedMultiplier = 1f;
                kvp.Value.NPCData.Health.MaxHealth = 100f;
                kvp.Value.Health.Health = 100f;

                kvp.Value.Behaviour.CombatBehaviour.GiveUpRange = _quest.GiveUpRange;
                kvp.Value.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = _quest.GiveUpAfterSuccessfulHits;
                kvp.Value.Behaviour.CombatBehaviour.DefaultSearchTime = _quest.DefaultSearchTime;

                kvp.Value.Behaviour.ScheduleManager.EnableSchedule();
                kvp.Value.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);

                if (kvp.Value.IsGoonSpawned)
                {
                    kvp.Value.Despawn();
                }
            }
            _quest.spawnedGoons.Clear();
            _quest.spawnedGoonsGuids.Clear();

            _quest.combatStartedAction = null;
            _quest.goonDiedAction = null;
            _quest.combatEndCrouchAction = null;

            yield break;
        }

    }
}
