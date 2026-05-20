

using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.PlayerScripts;
using ScheduleOne.Combat;
using ScheduleOne.Interaction;
using ScheduleOne.NPCs;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using ScheduleOne.Police;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.Packaging;
using ScheduleOne.Storage;
using ScheduleOne.Vehicles;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Lighting;
using ScheduleOne.Vehicles.AI;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
#else
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Packaging;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Lighting;
using Il2CppScheduleOne.Vehicles.AI;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppFishNet;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_TrueBrothers : Quest
    {
#if IL2CPP
        public Quest_TrueBrothers(IntPtr ptr) : base(ptr) { }

        public Quest_TrueBrothers() : base(ClassInjector.DerivedConstructorPointer<Quest_TrueBrothers>())
            => ClassInjector.DerivedConstructorBody(this);
#endif
        private float questDifficultyScalar = 1f;

        private readonly List<Vector3> pileBasePos = new()
        {
            new Vector3(-14.6f, -4.77f, 168.2f),
            new Vector3(-14.6f, -4.77f, 168.6f),
            new Vector3(-14.4f, -4.77f, 168.2f),
            new Vector3(-14.4f, -4.77f, 168.6f),
            new Vector3(-14.8f, -4.77f, 168.7f),
            new Vector3(-15.3f, -4.77f, 168.6f),
        };

        private readonly List<Vector3> ambushCopSpawnPositions = new()
        {
            new Vector3(-48.54f, -3.64f, 166.59f),
            new Vector3(-34.67f, -2.54f, 155.85f),
            new Vector3(-42.38f, -2.54f, 152.52f),
        };

        public static readonly Vector3 followDestination = new Vector3(-32.68f, -2.54f, 168.73f);

        public CartelGoon startGoon; // must be spawned before quest setup and assigned
        public static readonly float startGoonMoveSpeed = 0.40f;
        public List<CartelGoon> alliedGoons = new(); // 3 - 4

        public List<PoliceOfficer> ambushCops = new(); // waves of cops

        public GameObject brickBase = null;
        public List<GameObject> spawnedDecor = new();

        public StorageEntity destinationStorage = null;

        public LandVehicle spawnedVehicle = null;

        // Each interactable press retracts from top of piles
        // Track the piles instantiated objects in order
        public List<List<GameObject>> spawnedBrickPiles = new();
        private int totalBricksCount;

        private int bricksInPallet = 0;
        private int bricksInSUV = 0;

        private int currentAmbushWave = 1;
        private int currentDeadCopsCount = 0;
        public bool ambushDefeated = false;
        public bool preCompleteQueued = false;

        private float defaultM1911Dmg;
        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only
        public override void Complete(bool network = true)
        {
            Log("Quest_TrueBrothers: Complete method called.");
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

                Log("Quest_TrueBrothers: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrueBrothers: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_TrueBrothers: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_TrueBrothers: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrueBrothers: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            Log("Quest_TrueBrothers: End method called.");
            trueBrothersCompleted = true;
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
                coros.Add(MelonCoroutines.Start(CleanupTrueBrothersQuest()));
                Log("Quest_TrueBrothers: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_TrueBrothers: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }

        #endregion

        public void SetupSelf()
        {
            Log("QuestInit");
            this.name = "Quest_TrueBrothers";
            Expires = false;
            title = "True Brothers";
            CompletionXP = 1500;
            Description = "Help the cartel transport cocaine at the northern waterfront";
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
            GameObject followGoonObject = new GameObject("QuestEntry_FollowGoon");
            followGoonObject.transform.SetParent(this.transform);

            GameObject moveCocaineObject = new GameObject("QuestEntry_MoveCocaine");
            moveCocaineObject.transform.SetParent(this.transform);

            GameObject policeAmbushObject = new GameObject("QuestEntry_PoliceAmbush");
            policeAmbushObject.transform.SetParent(this.transform);

            QuestEntry followGoon = followGoonObject.AddComponent<QuestEntry>();
            QuestEntry moveCocaine = moveCocaineObject.AddComponent<QuestEntry>();
            QuestEntry policeAmbush = policeAmbushObject.AddComponent<QuestEntry>();

            this.QuestEntry_FollowGoon = followGoon;
            this.QuestEntry_MoveCocaine = moveCocaine;
            this.QuestEntry_PoliceAmbush = policeAmbush;

            this.Entries = new();
            this.Entries.Add(followGoon);
            this.Entries.Add(moveCocaine);
            this.Entries.Add(policeAmbush);

            Log("Config Entries");

            followGoon.SetEntryTitle("Follow the Cartel Goon to Northern Waterfront");
            followGoon.ParentQuest = this;
            followGoon.CompleteParentQuest = false;
            followGoon.PoILocation = new GameObject("FollowGoonEntry_POI").transform;
            followGoon.PoILocation.transform.SetParent(followGoon.transform);
            followGoon.PoILocation.transform.position = Vector3.zero; // TODO -> Mark as the spawned goon that guides & Update pos
            followGoon.AutoUpdatePoILocation = true;
            followGoon.SetState(EQuestState.Active, false);

            UnityEngine.Events.UnityAction followGoonAction = null;
            void OnFollowGoonComplete()
            {
                if (followGoon != null && followGoon.State == EQuestState.Failed) return;
                if (moveCocaine == null) return;

                moveCocaine.Begin();
                if (moveCocaine != null)
                {
                    if (moveCocaine.compassElement != null)
                        moveCocaine.compassElement.Visible = false;
                    else
                    {
                        moveCocaine.CreateCompassElement();
                        moveCocaine.compassElement.Visible = false;
                    }
                }

                if (followGoonAction != null)
                {
                    followGoon.onComplete.RemoveListener(followGoonAction);
                    followGoonAction = null;
                }
            }
            followGoonAction = (UnityEngine.Events.UnityAction)OnFollowGoonComplete;
            followGoon.onComplete.AddListener(followGoonAction);

            moveCocaine.SetEntryTitle("Move cocaine from the pallets to the SUV");
            moveCocaine.ParentQuest = this;
            moveCocaine.CompleteParentQuest = false;
            moveCocaine.PoILocation = new GameObject("MoveCocaineEntry_POI").transform;
            moveCocaine.PoILocation.SetParent(moveCocaine.transform);
            moveCocaine.AutoCreatePoI = false;
            moveCocaine.SetState(EQuestState.Inactive, false);

            UnityEngine.Events.UnityAction moveCocaineAction = null;
            void OnMoveCocaineComplete()
            {
                if (moveCocaine != null && moveCocaine.State == EQuestState.Failed) return;
                if (policeAmbush == null) return;

                policeAmbush.Begin();
                if (policeAmbush != null)
                {
                    if (policeAmbush.compassElement != null)
                        policeAmbush.compassElement.Visible = false;
                    else
                    {
                        policeAmbush.CreateCompassElement();
                        policeAmbush.compassElement.Visible = false;
                    }
                }

                if (moveCocaine != null)
                {
                    moveCocaine.onComplete.RemoveListener(moveCocaineAction);
                    moveCocaineAction = null;
                }

                coros.Add(MelonCoroutines.Start(SpawnPoliceAmbush()));
            }
            moveCocaineAction = (UnityEngine.Events.UnityAction)OnMoveCocaineComplete;
            moveCocaine.onComplete.AddListener(moveCocaineAction);

            policeAmbush.SetEntryTitle("Defeat the police ambush");
            policeAmbush.ParentQuest = this;
            policeAmbush.CompleteParentQuest = false;
            policeAmbush.PoILocation = new GameObject("PoliceAmbushEntry_POI").transform;
            policeAmbush.PoILocation.SetParent(policeAmbush.transform);
            policeAmbush.AutoCreatePoI = false;
            policeAmbush.SetState(EQuestState.Inactive, false);

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
                    this.hudUI.MainLabel.text = "True Brothers";
                this.hudUI.gameObject.SetActive(true);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            UpdateQuestMapLogo(QuestEntry_FollowGoon);

            // Player must not get arrested during quest
            UnityEngine.Events.UnityAction playerArrested = null;
            void OnPlayerArrested()
            {
                if (this.State != EQuestState.Active) return;

                this.Fail();

                if (playerArrested != null)
                {
                    Player.Local.onArrested.RemoveListener(playerArrested);
                    playerArrested = null;
                }
            }
            playerArrested = (UnityEngine.Events.UnityAction)OnPlayerArrested;
            Player.Local.onArrested.AddListener(playerArrested);

            // Start spawning immediate
            coros.Add(MelonCoroutines.Start(SpawnCarMeetup()));

            return;
        }

        private IEnumerator SpawnCarMeetup()
        {
            alliedGoons.Add(startGoon);

            // parse needed nobs
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject nobSuv = null;
            NetworkObject nobPallet = null;

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
            }

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
                product.Quality = EQuality.Premium;

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
                product.Quality = EQuality.Premium;

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

            spawnedDecor.Add(brickBase);

            Log("Spawn veh1");
            // spawn SUV1 reward veh
            NetworkObject boxSuv1 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            spawnedDecor.Add(boxSuv1.gameObject);
            netManager.ServerManager.Spawn(boxSuv1);
            yield return Wait01;
            if (!registered || this.State != EQuestState.Active) yield break;

            boxSuv1.transform.parent = Map.Instance.transform;
            boxSuv1.gameObject.SetActive(true);
            boxSuv1.transform.SetPositionAndRotation(new Vector3(-19.0712f, -4.1883f, 174.4196f), Quaternion.Euler(0.0009f, 0.4645f, 0f));
            destinationStorage = boxSuv1.GetComponent<StorageEntity>();
            destinationStorage.AccessSettings = StorageEntity.EAccessSettings.Full;
            yield return Wait05;
            if (!registered || this.State != EQuestState.Active) yield break;

            boxSuv1.GetComponent<Rigidbody>().isKinematic = true;

            Log("Spawn veh2");
            // spawn SUV2 lights on vehicle
            NetworkObject boxSuv2 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            spawnedDecor.Add(boxSuv2.gameObject);
            netManager.ServerManager.Spawn(boxSuv2);
            yield return Wait01;
            if (!registered || this.State != EQuestState.Active) yield break;

            boxSuv2.transform.parent = Map.Instance.transform;
            boxSuv2.gameObject.SetActive(true);
            boxSuv2.transform.SetPositionAndRotation(new Vector3(-21.7171f, -4.1801f, 167.101f), Quaternion.Euler(0.2846f, 78.2566f, 359.4543f));
            yield return Wait05;
            if (!registered || this.State != EQuestState.Active) yield break;

            boxSuv2.GetComponent<Rigidbody>().isKinematic = true;
            boxSuv2.GetComponent<VehicleLights>().HeadlightsOn = true;
            

            // Prepare the item instance for cocaine bricks
#if IL2CPP
            ProductItemInstance productItemInstance;
#endif

            Log("Spawn pallet");
            // Spawn pallet
            NetworkObject pallet = UnityEngine.Object.Instantiate<NetworkObject>(nobPallet);
            netManager.ServerManager.Spawn(pallet);
            spawnedDecor.Add(pallet.gameObject);
            yield return Wait01;
            if (!registered || this.State != EQuestState.Active) yield break;

            pallet.transform.parent = Map.Instance.transform;
            pallet.gameObject.SetActive(true);
            pallet.transform.position = new Vector3(-14.93f, -5f, 168.7f);
            Rigidbody palletRb = pallet.GetComponent<Rigidbody>();
            if (palletRb != null)
                palletRb.isKinematic = true;
            

            // Box collider to make the pallet interactable work
            GameObject colliderObj = new("InteractableCollider");
            colliderObj.transform.parent = pallet.transform;
            colliderObj.transform.localPosition = new Vector3(0.4f, 0f, -0.5f);
            BoxCollider bc = colliderObj.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.5f, 2f, 0.5f);

            InteractableObject palletInteractable = pallet.gameObject.AddComponent<InteractableObject>();

            palletInteractable.message = "Pick Up";
            palletInteractable.displayLocationCollider = bc;

            void OnPalletInteracted()
            {
                // Check inventory has space
                if (!PlayerSingleton<PlayerInventory>.Instance.CanItemFitInInventory(cokeInst, 1))
                    return;

                // When interacted get highest count list from spawned piles
                // destroy the last index gameobject of the selected pile
                // And give 1 to inventory
                if (spawnedBrickPiles.Count > 0)
                {
                    int highestCountIndex = -1;
                    int highestBrickCountInPile = 0;
                    for (int i = 0; i < spawnedBrickPiles.Count; i++)
                    {
                        if (spawnedBrickPiles[i].Count > highestBrickCountInPile)
                        {
                            highestBrickCountInPile = spawnedBrickPiles[i].Count;
                            highestCountIndex = i;
                        }
                    }
                    if (highestCountIndex != -1)
                    {
                        int lastBrickIndex = spawnedBrickPiles[highestCountIndex].Count - 1;
                        GameObject go = spawnedBrickPiles[highestCountIndex][lastBrickIndex];
                        spawnedBrickPiles[highestCountIndex].RemoveAt(lastBrickIndex);
                        UnityEngine.Object.Destroy(go);

                        // If empty pile remove
                        if (spawnedBrickPiles[highestCountIndex].Count == 0)
                            spawnedBrickPiles.RemoveAt(highestCountIndex);

                        // Append into inventory +1
                        PlayerSingleton<PlayerInventory>.Instance.AddItemToInventory(cokeInst);
                        bricksInPallet--;
                    }
                }

                // If Spawned brick piles is count 0 nothing is left and previous interact was last one
                // remove interactable
                if (spawnedBrickPiles.Count == 0)
                {
                    UnityEngine.Object.Destroy(palletInteractable);
                }
            }
            palletInteractable.onInteractStart.AddListener((UnityEngine.Events.UnityAction)OnPalletInteracted);

            // spawn bricks into tr pallet
            for (int i = 0; i < pileBasePos.Count; i++)
            {
                List<GameObject> newPile = new();
                Vector3 basePos = pileBasePos[i];
                for (int j = 0; j < UnityEngine.Random.Range(6, 11); j++)
                {
                    yield return Wait01;
                    if (!registered || this.State != EQuestState.Active) yield break;

                    GameObject newBrick = UnityEngine.Object.Instantiate(brickBase, pallet.transform);
                    newBrick.transform.position = new Vector3(basePos.x, basePos.y + 0.06f * j, basePos.z);
                    newBrick.transform.rotation = Quaternion.Euler(0f, Mathf.Round(UnityEngine.Random.Range(85f, 95f)), 0f);
                    newBrick.gameObject.SetActive(true);
                    spawnedDecor.Add(newBrick);
                    newPile.Add(newBrick);
                    totalBricksCount++;
                }

                spawnedBrickPiles.Add(newPile);
            }
            bricksInPallet = totalBricksCount;

            Log("Summon");

            // Summon and configure enemies
            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 2)
            {
                foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
                {
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count >= 2) break;
                    if (!NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(goon)) continue;

                    if (goon.IsGoonSpawned && (goon.Health.IsDead || goon.Health.IsKnockedOut))
                    {
                        goon.Health.Revive();
                        goon.Despawn();
                    }
                }
            }

            // Near the entrance to the alley
            CartelGoon goonGuard = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-32.68f, -2.54f, 168.73f));
            goonGuard.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            alliedGoons.Add(goonGuard);

            CartelGoon goonGuard2 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-31f, -2.54f, 168.73f));
            goonGuard2.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard2.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard2.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            alliedGoons.Add(goonGuard2);

            Log("Setup weapons");
            foreach (CartelGoon goon in alliedGoons)
            {
                // Setup weapon soo if player wants to steal cocaine, they will die, simple
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
                        defaultM1911Dmg = wep.Damage;
                        wep.Damage = 100f;
                    }

                }
                    //TODO:
                    // Setup Combat behaviour callback to trigger multiple goons fighting the player
            }

            Log("Make OnClosed");
            // Configure the destination storage onClosed to check that all the bricks have arrived to it
#if MONO
            System.Action onClosedAction = null;
#else
            Il2CppSystem.Action onClosedAction = null;
#endif
            void CloseTrigger()
            {
                Log("Trunk Closed");
                if (QuestEntry_MoveCocaine == null) return;
                if (QuestEntry_MoveCocaine.State != EQuestState.Active) return;

                int validCocaineBricksCount = 0;

                // Check foreach slot in storage that its a cocaine brick that is premium
                foreach(ItemSlot slot in destinationStorage.ItemSlots)
                {
                    if (slot.ItemInstance != null && slot.ItemInstance.Definition.ID == "cocaine")
                    {
                        ProductItemInstance temp = null;
#if MONO
                        temp = slot.ItemInstance as ProductItemInstance;
#else
                        temp = slot.ItemInstance.TryCast<ProductItemInstance>();
#endif
                        if (temp != null)
                        {
                            if (temp.Quality == EQuality.Premium && temp.AppliedPackaging == CartelInventory.brickPackaging)
                                validCocaineBricksCount += temp.Quantity;
                        }
                    }
                }

                bricksInSUV = validCocaineBricksCount;
                Log($"Found {bricksInSUV}/{totalBricksCount} Bricks in SUV");
                if (validCocaineBricksCount >= totalBricksCount)
                {
                    // All cocaine has been moved, complete entry move next
                    destinationStorage.AccessSettings = StorageEntity.EAccessSettings.Closed;
                    QuestEntry_MoveCocaine.Complete();
                }
            }

#if MONO
            onClosedAction = (System.Action)CloseTrigger;
#else
            onClosedAction = (Il2CppSystem.Action)CloseTrigger;
#endif

            destinationStorage.onClosed += onClosedAction;
            destinationStorage.StorageEntitySubtitle = "Cocaine Transport";

            Log("Spawning done");
            yield break;
        }

        private IEnumerator SpawnPoliceAmbush()
        {
            // GEt the required nob

            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;
            NetworkObject nob = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "PoliceNPC")
                {
                    nob = prefab;
                    break;
                }
            }
            if (nob == null)
            {
                Log("No Police Base Found for spawn");
                yield break;
            }

            Log("Spawn 3 cops");

            for (int i = 0; i < 3; i++)
            {
                NetworkObject copNet = UnityEngine.Object.Instantiate<NetworkObject>(nob);
                PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();
                offc.AutoDeactivate = false; // Prevent from returning to station and from being added to officer pool
                offc.PursuitBehaviour.arrestingEnabled = false;

                NPC myNpc = copNet.gameObject.GetComponent<NPC>();
                myNpc.ID = $"RuntimeOfficer_{i}";
                myNpc.FirstName = "Officer";
                myNpc.LastName = "";
                myNpc.transform.parent = NPCManager.Instance.NPCContainer;

                NPCManager.NPCRegistry.Add(myNpc);
                netManager.ServerManager.Spawn(copNet);
                copNet.gameObject.SetActive(true);
                copNet.name = $"RuntimeOfficer_{i}";

                offc.Behaviour.ScheduleManager.DisableSchedule();
                offc.Movement.PauseMovement();

                offc.Movement.Warp(ambushCopSpawnPositions[i]);
                offc.Awareness.VisionCone.WorldspaceIconsEnabled = false;
                // This should allow them to target goons even while player is lethally wanted
                offc.Awareness.SetAwarenessActive(false);

                ambushCops.Add(offc);
            }
            Log("Move + combat spawned");

            // 2 police attack 2 cartel
            ambushCops[0].Behaviour.CombatBehaviour.SetTarget(alliedGoons[0].GetComponent<ICombatTargetable>().NetworkObject);
            alliedGoons[0].Behaviour.CombatBehaviour.SetTarget(ambushCops[0].GetComponent<ICombatTargetable>().NetworkObject);

            ambushCops[1].Behaviour.CombatBehaviour.SetTarget(alliedGoons[1].GetComponent<ICombatTargetable>().NetworkObject);
            alliedGoons[1].Behaviour.CombatBehaviour.SetTarget(ambushCops[1].GetComponent<ICombatTargetable>().NetworkObject);

            // 1 copp shoots at player and allied goon shoots at that same cop
            ambushCops[2].Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
            ambushCops[2].Awareness.SetAwarenessActive(true);

            alliedGoons[2].Behaviour.CombatBehaviour.SetTarget(ambushCops[2].GetComponent<ICombatTargetable>().NetworkObject);

            // Health callbacks to trigger new wave after all dead
            foreach (PoliceOfficer offc in ambushCops)
            {
                UnityEngine.Events.UnityAction OnCopDiedAction = null;
                void OnCopDied()
                {
                    Log("Cop died");
                    currentDeadCopsCount++;
                    if (OnCopDiedAction != null)
                    {
                        offc.Health.onDieOrKnockedOut.RemoveListener(OnCopDiedAction);
                        OnCopDiedAction = null;
                    }
                    if (currentDeadCopsCount == ambushCops.Count)
                    {
                        // if the wave hasnt been changed
                        if (currentAmbushWave == 1)
                        {
                            currentAmbushWave++;
                            coros.Add(MelonCoroutines.Start(SpawnSecondWave()));
                        }

                    }
                }
                OnCopDiedAction = (UnityEngine.Events.UnityAction)OnCopDied;
                offc.Health.onDieOrKnockedOut.AddListener(OnCopDiedAction);

                offc.Movement.ResumeMovement();
                offc.Movement.SetDestination(followDestination);
                

                offc.Behaviour.CombatBehaviour.Enable_Networked();
            }
            yield return Wait1; // Walk out of spawnpoint towards player and goons
            if (!registered) yield break;

            foreach (PoliceOfficer offc in ambushCops)
            {
                offc.Behaviour.CombatBehaviour.Enable_Networked();
                offc.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
                if (offc.Behaviour.CombatBehaviour.currentWeapon != null)
                {
                    AvatarRangedWeapon wep = null;
#if MONO
                    wep = offc.Behaviour.CombatBehaviour.currentWeapon as AvatarRangedWeapon;
#else
                    wep = offc.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
#endif
                    if (wep != null)
                    {
                        wep.CanShootWhileMoving = true;
                        wep.MaxMovingShotsBeforeReposition = 2;
                        wep.MaxStationaryShotsBeforeReposition = 1;
                    }
                }
            }
            Log("Police Combat started");
            
            foreach (CartelGoon goon in alliedGoons)
            {
                // Reset the weapon stats so they are not overpowered anymore
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
                        wep.Damage = defaultM1911Dmg;
                    }

                }
                // start combat
                goon.Movement.ResumeMovement();
                goon.Behaviour.CombatBehaviour.Enable_Networked();
            }

            yield return Wait2;
            if (!registered) yield break;
            Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Lethal);

            yield break;
        }

        private IEnumerator SpawnSecondWave() 
        {
            Log("Next wave spawning");
            yield return Wait2;
            if (!registered) yield break;

            // Police car + the same 3 dead cops -> despwan+revive -> after car arrives -> spawn at the doors
            LandVehicle prefab = PoliceStation.PoliceStations[0].PoliceVehiclePrefabs[1];

            Vector3 carSpawn = new(-17f, 0.975f, 94.75f);
            Quaternion carRotation = Quaternion.Euler(Vector3.zero);
            LandVehicle policeVehicle = NetworkSingleton<VehicleManager>.Instance.SpawnAndReturnVehicle(prefab.vehicleCode, Vector3.zero, Quaternion.identity, false);
            policeVehicle.gameObject.SetActive(true);

            spawnedVehicle = policeVehicle; // to despawn later

            policeVehicle.SetTransform_Server(carSpawn, carRotation);

            policeVehicle.GetComponent<VehicleTeleporter>().MoveToRoadNetwork(false);

            currentDeadCopsCount = 0;
            foreach (PoliceOfficer offc in ambushCops)
            {
                offc.EnterVehicle(null, policeVehicle);
                offc.Movement.PauseMovement();
                offc.Health.Revive();
                offc.Behaviour.CombatBehaviour.Disable_Networked(null);

                offc.Health.onDieOrKnockedOut.RemoveAllListeners();

                UnityEngine.Events.UnityAction OnCopDiedAction = null;
                void OnCopDied()
                {
                    Log("Cop died 2nd wave");
                    currentDeadCopsCount++;
                    if (OnCopDiedAction != null)
                    {
                        offc.Health.onDieOrKnockedOut.RemoveListener(OnCopDiedAction);
                        OnCopDiedAction = null;
                    }
                    if (currentDeadCopsCount == ambushCops.Count)
                    {
                        // if the wave hasnt been changed
                        if (currentAmbushWave == 2)
                        {
                            Log("Ambush completed");
                            currentAmbushWave++;
                            ambushDefeated = true;
                        }

                    }
                }
                OnCopDiedAction = (UnityEngine.Events.UnityAction)OnCopDied;
                offc.Health.onDieOrKnockedOut.AddListener(OnCopDiedAction);
            }

            // Enable Police Light + siren
            PoliceLight light = policeVehicle.GetComponentInChildren<PoliceLight>();
            light.IsOn = true;

            bool hasArrived = false;
            void CarNavComplete(VehicleAgent.ENavigationResult result)
            {
                if (!registered || hasArrived) return;
                hasArrived = true;
                policeVehicle.Agent.storedNavigationCallback = null;
                policeVehicle.Agent.StopNavigating();
                
                Log("Navigation complete");
            }

            policeVehicle.Agent.Flags.OverriddenSpeed = 80f;
            policeVehicle.Agent.Flags.OverrideSpeed = true;

            // Navigate from spawn position to target
            // Maybe needs VehicleAgent.NavigationCallback to unseat occupants and start combat (also destination?)
#if MONO
            policeVehicle.Agent.Navigate(new Vector3(-36.8969f, -3.025f, 145.251f), null, CarNavComplete);
#else
            policeVehicle.Agent.Navigate(new Vector3(-36.8969f, -3.025f, 145.251f), null, (Il2CppScheduleOne.Vehicles.AI.VehicleAgent.NavigationCallback)CarNavComplete);
#endif

            // Wait for arrived to position and exit cops
            // OR Wait until travel timeout (incase car navigation fails or gets blocked)
            int maxNavigationTime = 20;
            int currentNavigationTime = 0;
            for (; ; )
            {
                yield return Wait1;
                if (!registered) yield break;

                currentNavigationTime++;

                if (currentNavigationTime >= maxNavigationTime)
                {
                    if (policeVehicle.Agent.storedNavigationCallback != null)
                        policeVehicle.Agent.storedNavigationCallback = null;
                    policeVehicle.Agent.StopNavigating();
                    hasArrived = true;
                }

                if (hasArrived)
                    break;
            }

            if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.Lethal)
                Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Lethal);

            foreach (PoliceOfficer offc in ambushCops)
            {
                yield return Wait1;
                if (!registered) yield break;

                offc.ExitVehicle();
                // All cops attack the player
                offc.BeginFootPursuit(Player.Local.PlayerCode);
                offc.Behaviour.CombatBehaviour.Enable_Networked();
                offc.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
                if (offc.Behaviour.CombatBehaviour.currentWeapon != null)
                {
                    AvatarRangedWeapon wep = null;
#if MONO
                    wep = offc.Behaviour.CombatBehaviour.currentWeapon as AvatarRangedWeapon;
#else
                    wep = offc.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
#endif
                    if (wep != null)
                    {
                        wep.CanShootWhileMoving = true;
                        wep.MaxMovingShotsBeforeReposition = 2;
                        wep.MaxStationaryShotsBeforeReposition = 1;
                    }
                }
            }

            // Goons attack cops
            for (int i = 0; i < alliedGoons.Count; i++)
            {
                alliedGoons[i].Behaviour.CombatBehaviour.SetTarget(ambushCops[i].GetComponent<ICombatTargetable>().NetworkObject);
            }

            Log("End of 2nd wave");
            yield break;
        }

        private IEnumerator PreComplete()
        {
            Log("Precoomplete start");
            // 2 guard goons continue stayinside beh to avoid npc trapping and cancelling movement
            foreach(CartelGoon goon in alliedGoons)
            {
                yield return Wait1;
                if (!registered) yield break;

                if (goon == startGoon) continue;

                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                goon.Behaviour.ScheduleManager.EnableSchedule();
            }

            bool playerConversated = false;
            // If the main goon is not dead after ambush, thank the player and complete mission on continue button
            if (!startGoon.Health.IsDead && !startGoon.Health.IsKnockedOut && startGoon.IsConscious)
            {
                startGoon.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, Quest_TrueBrothers.startGoonMoveSpeed));

                DialogueController controller = startGoon.DialogueHandler.gameObject.GetComponent<DialogueController>();

                DialogueController.GreetingOverride greeting = new();
                greeting.ShouldShow = true;
                string gender = Player.Local.Avatar.IsMale() ? "brother" : "sister";
                greeting.Greeting = $"Thanks for your help! You're our true {gender}!";
                greeting.VOType = EVOLineType.None;
                greeting.PlayVO = true;
                controller.AddGreetingOverride(greeting);

                DialogueController.DialogueChoice choiceContinue = new();
                string acceptText = "Continue";
                choiceContinue.ChoiceText = $"{acceptText}";
                choiceContinue.Enabled = true;

                void ContinueChosen()
                {
                    controller.npc.PlayVO(EVOLineType.Thanks);
                    controller.handler.ContinueSubmitted();

                    if (this.State == EQuestState.Active)
                        this.Complete();

                    startGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
                    startGoon.Behaviour.ScheduleManager.DisableSchedule();

                    // Remove choices
                    var oldChoices = controller.Choices;
                    oldChoices.Clear();
                    controller.Choices = oldChoices;
                    controller.GreetingOverrides.Remove(greeting);
                }
                choiceContinue.onChoosen.AddListener((UnityEngine.Events.UnityAction)ContinueChosen);
                controller.AddDialogueChoice(choiceContinue);

                // Set destination to player
                int maxTraverseTime = 30;
                int traverseTime = 0;
                bool playerConversatable = false;
                for (; ; )
                {
                    // While traversing to player (check proximity and conversate OR interrupt -> cancel despawn)
                    yield return Wait1;
                    if (!registered) yield break;
                    if (traverseTime >= maxTraverseTime)
                    {
                        Log("Traverse time out");
                        break;
                    }

                    traverseTime++;

                    if (startGoon == null || startGoon.Health.IsDead || startGoon.Health.IsKnockedOut || !startGoon.IsConscious || startGoon.Behaviour.activeBehaviour == startGoon.Behaviour.CombatBehaviour)
                        break;

                    if (!playerConversated && playerConversatable)
                    {
                        startGoon.Movement.EndSetDestination(NPCMovement.WalkResult.Success);
                        startGoon.Movement.FacePoint(Player.Local.CenterPointTransform.position);
                        startGoon.Movement.PauseMovement();
                        controller.StartGenericDialogue(false);
                        playerConversated = true;
                        Log("Start Conversate");
                        break;
                    }

                    float distFromPlayer = Vector3.Distance(Player.Local.CenterPointTransform.position, startGoon.CenterPoint);
                    if (distFromPlayer < 3f)
                    {
                        // start dialogue when possible
                        Log("Wait conversate");
#if MONO
                        yield return new WaitUntil(CanStartConversate);
#else
                    yield return new WaitUntil((Il2CppSystem.Func<bool>)CanStartConversate);
#endif
                        Log("Can start conversate");
                        playerConversatable = true;
                        continue;
                    }
                    else
                    {
                        Log("Traverse to player");
                        if (!startGoon.Movement.HasDestination)
                        {
                            startGoon.Movement.SetDestination(Player.Local.CenterPointTransform);
                        }
                        if (startGoon.Movement.IsPaused)
                            startGoon.Movement.ResumeMovement();
                    }
                }

            }

            // Edge cases from traverse fail etc, still auto complete mission
            if (this.State == EQuestState.Active && !playerConversated)
                this.Complete();

            yield break;
        }

        public override void OnMinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || trueBrothersCompleted || this.State != EQuestState.Active) return;

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

            if (preCompleteQueued) return;

            // Update follow poi location pos and check proximity to target
            if (QuestEntry_FollowGoon != null && startGoon != null && QuestEntry_FollowGoon.State == EQuestState.Active)
            {
                // If the goon dies or goes into combat, fail quest
                if (startGoon.Health.IsDead || startGoon.Health.IsKnockedOut || (startGoon.Behaviour.activeBehaviour != null && startGoon.Behaviour.activeBehaviour == startGoon.Behaviour.CombatBehaviour))
                {
                    Fail();
                }

                QuestEntry_FollowGoon.PoILocation.transform.position = startGoon.transform.position;
                if (Vector3.Distance(Player.Local.CenterPointTransform.position, followDestination) < 5f)
                {
                    QuestEntry_FollowGoon.Complete();
                    return;
                }

                if (!startGoon.Movement.HasDestination)
                    startGoon.Movement.SetDestination(followDestination);
                if (startGoon.Movement.IsPaused)
                    startGoon.Movement.ResumeMovement();
            }


            if (QuestEntry_MoveCocaine != null && QuestEntry_MoveCocaine.State == EQuestState.Active)
            {
                // Track how many bricks are accounted for
                int bricksMissingFromSUV = totalBricksCount - bricksInSUV;
                int bricksMissingFromPallet = totalBricksCount - bricksInPallet;
                int bricksInPlayerInv = Mathf.Abs(totalBricksCount - (bricksMissingFromSUV + bricksMissingFromPallet));

                // During transport, player must not traverse too far while
                // having the cocaine
                Log($"BricksInInv: {bricksInPlayerInv}, missing pallet: {bricksMissingFromPallet}, missing SUV: {bricksMissingFromSUV}");
                if (bricksInPlayerInv > 0)
                {
                    float dist = Vector3.Distance(Player.Local.CenterPointTransform.position, destinationStorage.transform.position);
                    Log($"Distance: {dist}");
                    if (dist > 14f)
                    {
                        Fail();
                        ICombatTargetable combatTargetable = Player.Local.GetComponent<ICombatTargetable>();

                        startGoon.AttackEntity(combatTargetable);

                        foreach (CartelGoon goon in alliedGoons)
                        {
                            goon.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, 0.75f));
                            goon.AttackEntity(combatTargetable);
                        }
                    }
                }
            }

            if (QuestEntry_PoliceAmbush != null && QuestEntry_PoliceAmbush.State == EQuestState.Active)
            {

                // Ensure cops target cartel + player
                List<int> currentAliveIndices = new();

                if (currentDeadCopsCount != ambushCops.Count)
                {
                    for (int i = 0; i < ambushCops.Count; i++)
                    {
                        PoliceOfficer offc = ambushCops[i];
                        if (offc.Health.IsDead || offc.Health.IsKnockedOut)
                            continue;
                        if (offc.IsInVehicle) continue;

                        currentAliveIndices.Add(i);

                        if (offc.Behaviour.CombatBehaviour.currentWeapon == null)
                            offc.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");

                        if (offc.Behaviour.activeBehaviour == null || offc.Behaviour.activeBehaviour != offc.Behaviour.CombatBehaviour)
                            offc.Behaviour.CombatBehaviour.Enable_Networked();

                        // Does the combat behaviour retain target after target is killed?
                        if (offc.Behaviour.CombatBehaviour.Target != null)
                            continue;

                        // Select alive cartel members or player
                        if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
                        {
                            // Officer targets cartel goons
                            foreach (CartelGoon goon in alliedGoons)
                            {
                                if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                                    continue;

                                offc.Behaviour.CombatBehaviour.SetTarget(goon.GetComponent<ICombatTargetable>().NetworkObject);
                                break;
                            }
                        }
                        else
                        {
                            // Officer targets player
                            offc.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
                        }
                    }
                }

                // Ensure goons target police that are alive and not player
                foreach (CartelGoon goon in alliedGoons)
                {
                    if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                        continue;

                    if (goon.Behaviour.CombatBehaviour.currentWeapon == null)
                        goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");

                    if (currentAliveIndices.Count > 0 && goon.Behaviour.CombatBehaviour.Target == null)
                    {
                        Log("Goon changes combat target");
                        int randomAliveIndex = UnityEngine.Random.Range(0, currentAliveIndices.Count);
                        PoliceOfficer copTarget = ambushCops[currentAliveIndices[randomAliveIndex]];
                        goon.Behaviour.CombatBehaviour.SetTarget(copTarget.GetComponent<ICombatTargetable>().NetworkObject);
                        if (goon.Behaviour.activeBehaviour == null || goon.Behaviour.activeBehaviour != goon.Behaviour.CombatBehaviour)
                            goon.Behaviour.CombatBehaviour.Enable_Networked();
                    }
                }

                if (Player.Local.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None && ambushDefeated)
                {
                    if (!preCompleteQueued)
                    {
                        Log("Queue Pre Complete");
                        preCompleteQueued = true;
                        coros.Add(MelonCoroutines.Start(PreComplete()));
                    }
                }

            }
        }

        public QuestEntry QuestEntry_FollowGoon;
        public QuestEntry QuestEntry_MoveCocaine;
        public QuestEntry QuestEntry_PoliceAmbush;
    }


    // Patch a function from police beh
    // that ensures any cops during the quest, will also get tragetted
    // by the cartel IF the policetargets player

    [HarmonyPatch(typeof(CombatBehaviour), "SetTarget_Client")]
    public static class CombatBehaviour_SetTarget_Client_Patch
    {
        public static bool Prefix(CombatBehaviour __instance, NetworkObject target)
        {
            // if quest not active
            if (activeTrueBrothersQuest == null) return true;
            if (activeTrueBrothersQuest.State != EQuestState.Active) return true;
            if (activeTrueBrothersQuest.QuestEntry_PoliceAmbush.State != EQuestState.Active) return true;

            CartelGoon goon = null;
            PoliceOfficer offc = null;
#if MONO
            goon = __instance.Npc as CartelGoon;
            offc = __instance.Npc as PoliceOfficer;
#else
            goon = __instance.Npc.TryCast<CartelGoon>();
            offc = __instance.Npc.TryCast<PoliceOfficer>();
#endif
            if (goon == null && offc == null) 
            {
                Log("Not officer or goon, skip");
                return true;
            }

            if (goon != null)
            {
                if (target == Player.Local.NetworkObject)
                {
                    Log("Goon target player, prevent target", memberName: "CombatBehaviour_SetTarget_Patch");
                    return false;
                }
            }

            if (offc != null)
            {
                if (target == Player.Local.NetworkObject)
                {
                    for (int i = 0; i < activeTrueBrothersQuest.alliedGoons.Count; i++)
                    {
                        CartelGoon gooni = activeTrueBrothersQuest.alliedGoons[i];
                        if (gooni.Health.IsDead || gooni.Health.IsKnockedOut) continue;
                        if (Vector3.Distance(gooni.CenterPoint, __instance.Npc.CenterPoint) > 40f) continue;

                        gooni.Behaviour.CombatBehaviour.SetTarget(__instance.Npc.GetComponent<ICombatTargetable>().NetworkObject);
                        gooni.Behaviour.CombatBehaviour.Enable_Networked();
                        Log("Goon attacking normal cop from Set Target Trigger", memberName: "CombatBehaviour_SetTarget_Patch");
                        break;
                    }
                }
            }

            return true;
        }
    }

}
