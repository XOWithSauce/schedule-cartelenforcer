
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.Map;
using ScheduleOne.Storage;
using ScheduleOne.Dragging;
using ScheduleOne.Interaction;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using ScheduleOne.Quests;
using ScheduleOne.Vehicles;
using ScheduleOne.Property;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using Pathfinding;
#else
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Dragging;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Property;
using Il2CppFishNet;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppPathfinding;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_CartelLogistics : ModQuestBase
    {
        protected readonly CartelLogisticsHelper _helper;
#if MONO
        public Quest_CartelLogistics()
        {
            _helper = new CartelLogisticsHelper(this);
        }
#else
        public Quest_CartelLogistics(IntPtr ptr) : base(ptr) 
        { 
            _helper = new CartelLogisticsHelper(this);
        }
        public Quest_CartelLogistics() : base(ClassInjector.DerivedConstructorPointer<Quest_CartelLogistics>())
            => ClassInjector.DerivedConstructorBody(this);

        public new string title;
        public new string Subtitle;
        public new Il2CppSystem.Collections.Generic.List<QuestEntry> Entries;
#endif
        public static readonly Vector3 vehicleSpawnPos = new(166.7f, 10.8f, -70.5f);
        public static readonly Vector3 unloadBoxPos = new(-19f, -4.2f, 174.5f);
        public static readonly Vector3 warehouseCenter = new(-26.5f, -4f, 173.56f); // max dist ~3.2
        public const string loadBoxesTitle = "Go to Manor and place the boxes on the flatbed";
        public const string dropOffTitleDrive = "Drive to the GPS location";
        public const string dropOffTitleUnload = "Pick up boxes from the flatbed rear end and move them inside the warehouse";
        public const string returnVehicleTitle = "Return the vehicle to the Manor parking lot";

        public GameObject woodenCrate;

        public LandVehicle questVehicle;
        public List<GameObject> questBoxes = new();
        public List<VehicleTrunkSlot> vehicleBoxSlots = new();
        public InteractableObject unloadBoxesIntObj;
        public ManorGate manorGate;
        public QuestEntry QuestEntry_LoadBoxes;
        private UnityAction _loadBoxesAction;

        public QuestEntry QuestEntry_DropOffBoxes;
        private UnityAction _dropOffBoxesAction;

        public QuestEntry QuestEntry_ReturnVehicle;

        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only
        public override void Complete(bool network = true)
        {
            Log("Quest_CartelDropOffs: Complete method called.");
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

                ModCoroutines.Start(RewardCartelLogisticsQuest());
                
                Log("Quest_CartelDropOffs: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CartelDropOffs: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_CartelDropOffs: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_CartelDropOffs: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CartelDropOffs: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            Log("Quest_CartelDropOffs: End method called.");
            try
            {
                ModCoroutines.Start(_helper.CleanupQuest());

                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;


                if (instance == null) return;
#if MONO
                instance.onMinutePass -= this.OnMinPass;
#else
                var action = (Action)this.OnMinPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)action);
#endif
                Log("Quest_CartelDropOffs: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_CartelDropOffs: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }

        #endregion

        public void SetupSelf()
        {
            Log("Setup quest");
            _helper.InitializeQuest("Cartel Logistics", xp: 500);

            _loadBoxesAction = (UnityAction)OnLoadBoxesComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_LoadBoxes,
                name: "LoadBoxes",
                title: loadBoxesTitle,
                new PoIConfig(true, false, false, poiPosition: vehicleSpawnPos),
                _loadBoxesAction);

            _dropOffBoxesAction = (UnityAction)OnDropOffBoxesComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_DropOffBoxes,
                name: "DropOffBoxes",
                title: dropOffTitleDrive,
                new PoIConfig(true, false, false, poiPosition: unloadBoxPos),
                _dropOffBoxesAction);

            _helper.InitializeQuestEntry(ref QuestEntry_ReturnVehicle,
                name: "ReturnVehicle",
                title: returnVehicleTitle,
                new PoIConfig(true, false, false, poiPosition: vehicleSpawnPos));

#if IL2CPP
            _helper.SyncEntriesField(ref this.Entries);
#endif

            ModCoroutines.Start(_helper.SpawnQuestObjects());

            // Open gate at manor
            ManorGate gate = null;
            for (int i = 0; i < Property.Properties.Count; i++)
            {
                if (Property.Properties[i].propertyCode == "manor")
                {
                    gate = Property.Properties[i].gameObject.GetComponentInChildren<ManorGate>();
                    if (gate == null)
                    {
                        // try again with transform child direct
                        Transform manorGateTr = Property.Properties[i].transform.Find("Manor Gate");
                        if (manorGateTr != null)
                        {
                            ManorGate gate2 = manorGateTr.gameObject.GetComponent<ManorGate>();
                            if (gate2 != null)
                                gate = gate2;
                        }
                    }
                    break;
                }
            }

            if (gate == null)
            {
                Log("Failed to find Manor Gate instance");
                return;
            }

            gate.SetEnterable(true);
            manorGate = gate;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            var action = OnMinPass;
#if MONO
            instance.onMinutePass += this.OnMinPass;
#else
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            _helper.StartQuestFromEntry(QuestEntry_LoadBoxes);
            Log("Start quest");
        }

        public override void OnMinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || this.State != EQuestState.Active) return;

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
            if (QuestEntry_LoadBoxes != null && QuestEntry_LoadBoxes.State == EQuestState.Active)
            {
                int completedSlotsCount = 0;
                foreach (VehicleTrunkSlot slot in vehicleBoxSlots)
                {
                    if (slot.isSlotOccupied)
                        completedSlotsCount++;
                }

                if (completedSlotsCount == 8)
                    QuestEntry_LoadBoxes.Complete();
                return;
            }

            // When nearby the storage enable unloading the boxes at the flatbed end part
            if (QuestEntry_DropOffBoxes != null && QuestEntry_DropOffBoxes.State == EQuestState.Active)
            {
                if (questVehicle != null && questVehicle.gameObject != null) 
                {
                    float distToWarehouse = Vector3.Distance(questVehicle.transform.position, unloadBoxPos);
                    if (distToWarehouse < 12f && !unloadBoxesIntObj.gameObject.activeSelf)
                    {
                        unloadBoxesIntObj.gameObject.SetActive(true);
                        Log("Enabled unload interactable");
                    }
                    else if (distToWarehouse >= 12f && unloadBoxesIntObj.gameObject.activeSelf)
                    {
                        unloadBoxesIntObj.gameObject.SetActive(false);
                        Log("Disabled unload interactable");
                    }
                }
                else
                {
                    // probably when driven off the map
                    Log("Quest vehicle is null quest failed");
                    Fail();
                }

                // have boxes been unloaded to warehouse and complete entry
                int boxesInPlace = 0;
                float dist = Vector3.Distance(questVehicle.transform.position, Quest_CartelLogistics.unloadBoxPos);
                if (dist < 12f)
                {
                    // update title
                    if (QuestEntry_DropOffBoxes.EntryTitle != dropOffTitleUnload)
                        QuestEntry_DropOffBoxes.SetEntryTitle(dropOffTitleUnload);

                    // Check all spawned within 4f of the center
                    foreach (GameObject box in questBoxes)
                    {
                        if (Vector3.Distance(box.transform.position, Quest_CartelLogistics.warehouseCenter) < 4f)
                            boxesInPlace++;
                    }
                    Log($"Boxes detected so far: {boxesInPlace}");
                }
                else
                {
                    if (QuestEntry_DropOffBoxes.EntryTitle != dropOffTitleDrive)
                        QuestEntry_DropOffBoxes.SetEntryTitle(dropOffTitleDrive);
                }

                // all in warehouse and none in vehicle
                if (boxesInPlace == questBoxes.Count && !vehicleBoxSlots.Any(x => x.isSlotOccupied))
                {
                    Log("Boxes in place met, complete entry");
                    QuestEntry_DropOffBoxes.Complete();
                    return;
                }
            }

            // Check vehicle back nearby in manor spawn spot and player has exited vehicle -> complete
            if (QuestEntry_ReturnVehicle != null && QuestEntry_ReturnVehicle.State == EQuestState.Active)
            {
                if (questVehicle != null && questVehicle.gameObject != null)
                {
                    float dist = Vector3.Distance(questVehicle.transform.position, vehicleSpawnPos);
                    if (dist < 5f && questVehicle.DriverPlayer == null)
                    {
                        this.Complete();
                    }
                }
                else
                {
                    // Probably when driven off the map?
                    Fail();
                }
            }
        }

        public void OnLoadBoxesComplete()
        {
            if (QuestEntry_LoadBoxes != null && QuestEntry_LoadBoxes.State == EQuestState.Failed) return;
            if (QuestEntry_DropOffBoxes == null) return;

            Log("Load boxes completed");
            questVehicle.SetIsPlayerOwned(null, true);
            questBoxes.Clear();

            ModCoroutines.Start(_helper.SetupSmallWarehouse());
            QuestEntry_DropOffBoxes.Begin();
            UpdateQuestMapLogo(QuestEntry_DropOffBoxes);
            if (_loadBoxesAction != null)
            {
                QuestEntry_LoadBoxes.onComplete.RemoveListener(_loadBoxesAction);
                _loadBoxesAction = null;
            }
        }

        public void OnDropOffBoxesComplete()
        {
            if (QuestEntry_DropOffBoxes != null && QuestEntry_DropOffBoxes.State == EQuestState.Failed) return;
            if (QuestEntry_ReturnVehicle == null) return;

            Log("Drop off complete");
            questBoxes.Clear();
            unloadBoxesIntObj.gameObject.SetActive(false);

            QuestEntry_ReturnVehicle.Begin();
            UpdateQuestMapLogo(QuestEntry_ReturnVehicle);
            if (_dropOffBoxesAction != null)
            {
                QuestEntry_DropOffBoxes.onComplete.RemoveListener(_dropOffBoxesAction);
                _dropOffBoxesAction = null;
            }
        }
    }
    public class VehicleTrunkSlot
    {
        public VehicleTrunkSlot(LandVehicle vehicle, Vector3 center, GameObject crate)
        {
            slotCenter = center;
            _vehicle = vehicle;

            // use only the model so instantiate the inner cube object
            loadedBox = UnityEngine.Object.Instantiate(crate.transform.GetChild(0).gameObject);
            // Then the object still has boxcollider that messes up camera and causes physics bugs
            BoxCollider bc = loadedBox.transform.Find("Cube").GetComponent<BoxCollider>();
            UnityEngine.Object.Destroy(bc);

            loadedBox.transform.parent = _vehicle.transform;
            loadedBox.transform.localPosition = center;
            loadedBox.transform.rotation = Quaternion.identity;
            loadedBox.SetActive(false);
        }
        private LandVehicle _vehicle;
        public Vector3 slotCenter;
        public bool isSlotOccupied = false;
        public GameObject loadedBox = null;
    }

    // Quest coroutine wrapper
    public class CartelLogisticsHelper : QuestHelperBase<Quest_CartelLogistics>
    {
        public CartelLogisticsHelper(Quest_CartelLogistics quest) : base(quest) { }

        public IEnumerator SpawnQuestObjects()
        {
            Log("Spawn start");
            yield return MelonCoroutines.Start(SetupQuestCrate());
            yield return MelonCoroutines.Start(SpawnQuestVehicle());
            yield return MelonCoroutines.Start(SpawnQuestBoxes());
            Log("Done spawnn");
            yield break;
        }

        public IEnumerator SetupQuestCrate()
        {
            // Find map tr: Map/Hyland Point/Region_Northtown/Yard/Wood Crate Prop
            Transform woodCrateObj = Singleton<Map>.Instance.transform.Find("Hyland Point/Region_Northtown/Yard/Wood Crate Prop");
            if (woodCrateObj == null)
            {
                Log("Failed to setup quest wooden crate, invalid transform path");
                yield break;
            }
            woodCrateObj.gameObject.SetActive(false);

            _quest.woodenCrate = UnityEngine.Object.Instantiate(woodCrateObj.gameObject);
            // Remove draggable and readd to remove the sync from prev obj
            SceneDraggable oldComp = _quest.woodenCrate.GetComponent<SceneDraggable>();
            UnityEngine.Component.Destroy(oldComp);
            _quest.woodenCrate.AddComponent<SceneDraggable>();

            // re enable original
            woodCrateObj.gameObject.SetActive(false);
            Log("Finished setup crate");
        }

        public IEnumerator SpawnQuestVehicle()
        {
            Log("Spawning veh");
            // parse needed nobs
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject prefabFlatbed = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "SmallFlatbed")
                {
                    prefabFlatbed = prefab;
                }
            }

            Log("Spawn veh");
            NetworkObject flatbedNob = UnityEngine.Object.Instantiate<NetworkObject>(prefabFlatbed);
            netManager.ServerManager.Spawn(flatbedNob);
            _quest.questVehicle = flatbedNob.GetComponent<LandVehicle>();

            Log("Fix navvmesh components");
            // Fix the unassigned fields in the spawned obj
            Transform obstacleTr = flatbedNob.transform.Find("Obstacle");
            NavMeshObstacle obstacleComponent = obstacleTr.GetComponent<NavMeshObstacle>();
            NavmeshCut nmcComponent = obstacleTr.gameObject.AddComponent<NavmeshCut>();

            _quest.questVehicle.transform.position = Quest_CartelLogistics.vehicleSpawnPos;
            _quest.questVehicle.transform.rotation = Quaternion.identity;

            _quest.questVehicle.NavMeshObstacle = obstacleComponent;
            _quest.questVehicle.NavmeshCut = nmcComponent;

            // HumanoidColliderContainer is null so create empty template and set the use to false 
            GameObject hmCol = new("HumanoidContainer");
            hmCol.transform.SetParent(NetworkSingleton<GameManager>.Instance.Temp);
            VehicleHumanoidCollider collComp = hmCol.AddComponent<VehicleHumanoidCollider>();
            collComp.Vehicle = _quest.questVehicle;
            _quest.questVehicle.HumanoidColliderContainer = collComp;
            _quest.questVehicle.UseHumanoidCollider = false;

            _quest.questVehicle.enabled = true;
            Log("Awake");
            _quest.questVehicle.Awake();
            Log("Start");
            _quest.questVehicle.Start();
            Log("Active");
            _quest.questVehicle.gameObject.SetActive(true);

            // in police checkpoints requires:
            // vehicle.Trunk (StorageDoorAnimation)
            // assert open
            // Then it calls later DoesVehicleContainIllicitItems
            // that requires vehicle.Storage to exist and Storage.ItemSlots to exist

            Log("Check storage");
            if (_quest.questVehicle.Storage != null)
                _quest.questVehicle.Storage.AccessSettings = StorageEntity.EAccessSettings.Closed;
            else
            {
                Log("Storage component is null!");
                _quest.questVehicle.Storage = new();
                _quest.questVehicle.Storage.AccessSettings = StorageEntity.EAccessSettings.Closed;
                if (_quest.questVehicle.Storage != null && _quest.questVehicle.Storage.ItemSlots == null)
                    _quest.questVehicle.Storage.ItemSlots = new();
            }

            Log("Check trunk");
            if (_quest.questVehicle.Trunk != null)
                _quest.questVehicle.Trunk.overriddeIsOpen = true;
            else
            {
                Log("Trunk component is null!");
                _quest.questVehicle.Trunk = new();
                _quest.questVehicle.Trunk.overriddeIsOpen = true;
            }

            Log("Setup int");
            if (_quest.questVehicle.intObj != null)
                _quest.questVehicle.intObj.onInteractStart.AddListener((UnityEngine.Events.UnityAction)_quest.questVehicle.EnterVehicle);
            else
                Log("Interactable component is null");

            Log("Fix colliders");
            // fix the vehicle front colliders
            Transform target = _quest.questVehicle.transform.Find("ObstructionDetector");
            // this has the sphere front collider and another which dunno where that is
            // sphere is too large for the frontal part, fix both
            Transform collider = target.Find("Collider");
            Transform sphere = target.Find("Sphere");
            collider.localScale = Vector3.one;
            sphere.localScale = new Vector3(3f, 1f, 2f);


            Log("Spawning done setup bed slots");
            // initiate the box objects in trunk bed
            float bedLocalPosX = -0.51f;
            float bedLocalPosZ = 1.45f;
            for (int i = 0; i < 4; i++) // rows count
            {
                float offsetRowZ = 1.005f * i;
                
                for (int j = 0; j < 2; j++) // columns / boxes per row
                {
                    // left right cols
                    float bedPosX = bedLocalPosX;
                    if (j == 0)
                        bedPosX = -bedLocalPosX;

                    Vector3 currentSlotLocation = new(bedPosX, 0.755f, bedLocalPosZ - offsetRowZ);
                    VehicleTrunkSlot newSlot = new(_quest.questVehicle, currentSlotLocation, _quest.woodenCrate);
                    _quest.vehicleBoxSlots.Add(newSlot);
                }
            }
            Log("Setup unload button int obj");
            // setup the unloading boxes interactable obj
            GameObject unloadBtnObj = new("UnloadIntObj");
            // at the end of the bed (Z) furthest from the cabin, centered
            unloadBtnObj.transform.parent = _quest.questVehicle.transform;
            unloadBtnObj.transform.localPosition = new(0f, 0.8f, -3f);
            BoxCollider bc = unloadBtnObj.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            unloadBtnObj.transform.localScale = new(2.3f, 1f, 1f);
            unloadBtnObj.SetActive(false);

            _quest.unloadBoxesIntObj = unloadBtnObj.AddComponent<InteractableObject>();
            _quest.unloadBoxesIntObj.displayLocationPoint = unloadBtnObj.transform;
            _quest.unloadBoxesIntObj.MaxInteractionRange = 4f;
            _quest.unloadBoxesIntObj.interactionState = InteractableObject.EInteractableState.Default;
            _quest.unloadBoxesIntObj.onInteractStart.AddListener((UnityEngine.Events.UnityAction)OnCratesUnloadedPress);
            _quest.unloadBoxesIntObj.message = "Unload boxes";
            Log("Spawned vehicle");
            yield break;
        }

        public IEnumerator SpawnQuestBoxes()
        {
            Log("Spawning boxes");
            for(int i = 0; i < 4; i++) // 4 piles
            {
                for (int j = 0; j < 2; j++) // 2 boxes for pile
                {
                    yield return Wait01;
                    GameObject newCrateObj = UnityEngine.Object.Instantiate(_quest.woodenCrate);
                    // Remove draggable to remove the sync from prev obj
                    SceneDraggable oldComp = newCrateObj.GetComponent<SceneDraggable>();
                    UnityEngine.Component.Destroy(oldComp);
                    SceneDraggable dragComp = newCrateObj.AddComponent<SceneDraggable>();
                    _quest.questBoxes.Add(newCrateObj);

                    // Position in piles/row
                    float yOffset = j == 0 ? 0f : 2f;
                    float zGap = 1.2f * i;
                    newCrateObj.transform.position = new Vector3(159f, 10.03f + yOffset, -72.5f + zGap);
                    newCrateObj.transform.rotation = Quaternion.identity;

                    // config the draggable + interactable
                    dragComp.DragOrigin = newCrateObj.transform.Find("Drag origin");
                    InteractableObject crateInteract = newCrateObj.GetComponent<InteractableObject>();
                    crateInteract.MaxInteractionRange = 1.8f;

                    void OnCrateInteractStart()
                    {
                        ModCoroutines.Start(WhileCrateInteracted(dragComp));
                    }

                    crateInteract.onInteractStart.AddListener((UnityEngine.Events.UnityAction)OnCrateInteractStart);
                    newCrateObj.SetActive(true);
                }
            }
            Log("Boxes spawned");
            yield break;
        }

        public IEnumerator WhileCrateInteracted(SceneDraggable dragComp)
        {
            if (dragComp == null) yield break;
            // Check distance from box to vehicle bed box slot and put it there while in the quest state
            if (_quest.QuestEntry_LoadBoxes != null && _quest.QuestEntry_LoadBoxes.State == EQuestState.Active)
            {
                while (_quest.QuestEntry_LoadBoxes != null && _quest.QuestEntry_LoadBoxes.State == EQuestState.Active)
                {
                    float distNearest = 100f;
                    VehicleTrunkSlot nearestSlot = null;
                    int occupiedSlotsCount = 0;
                    foreach (VehicleTrunkSlot slot in _quest.vehicleBoxSlots)
                    {
                        if (slot.isSlotOccupied)
                        {
                            occupiedSlotsCount++;
                            continue;
                        }

                        float dist = 0f;
                        if (dragComp.DragOrigin == null)
                            dist = Vector3.Distance(dragComp.transform.position, slot.loadedBox.transform.position);
                        else
                            dist = Vector3.Distance(dragComp.DragOrigin.position, slot.loadedBox.transform.position);

                        if (dist < distNearest)
                        {
                            distNearest = dist;
                            nearestSlot = slot;
                        }
                    }
                    if (distNearest < 0.85f)
                    {
                        // destroy the dragged box
                        dragComp.StopDragging();
                        if (_quest.questBoxes.Contains(dragComp.gameObject))
                            _quest.questBoxes.Remove(dragComp.gameObject);

                        GameObject.Destroy(dragComp.gameObject);
                        dragComp = null;
                        // enable the model in the flatbed
                        nearestSlot.loadedBox.SetActive(true);
                        nearestSlot.isSlotOccupied = true;
                        break;
                    }

                    yield return Wait05;
                    if (!registered || dragComp == null) yield break;
                }
            }
            Log("Drag end");
            yield break;
        }

        public IEnumerator SetupSmallWarehouse()
        {
            Transform warehouseTr = Singleton<Map>.Instance.transform.Find("Hyland Point/Region_Northtown/Small warehouse/Bodyshop (1)");
            // by default this place has the garage door closed, get rid of it
            warehouseTr.Find("bodyshop/GarageDoor (1)").gameObject.SetActive(false);
            // interior is disabled by default since its not in the game basically so enable that
            Transform interior = warehouseTr.Find("Interior");
            interior.gameObject.SetActive(true);
            // and the interior has the lights but they are too bright so dim them for mooooood
            Light[] lightComponents = interior.GetComponentsInChildren<Light>();
            foreach (Light light in lightComponents)
            {
                if (light.type == LightType.Spot)
                    light.range = 4.5f;
            }

            yield break;
        }

        public void OnCratesUnloadedPress()
        {
            if (_quest.QuestEntry_DropOffBoxes == null || _quest.QuestEntry_DropOffBoxes.State != EQuestState.Active)
                return;

            // get 2 boxes from bed end towards front
            List<VehicleTrunkSlot> occupiedSlots = _quest.vehicleBoxSlots.FindAll(x => x.isSlotOccupied == true);
            if (occupiedSlots.Count == 0)
            {
                _quest.unloadBoxesIntObj.interactionState = InteractableObject.EInteractableState.Disabled;
                return;
            }

            // check that the previous quest boxes arent in the spawn spot 
            foreach (GameObject go in _quest.questBoxes)
            {
                if (Vector3.Distance(go.transform.position, _quest.unloadBoxesIntObj.transform.position) < 2f)
                {
                    Log("Remove nearby box first");
                    return;
                }

            }

            // spawn
            for (int i = 0; i < 2; i++)
            {
                GameObject newCrateObj = UnityEngine.Object.Instantiate(_quest.woodenCrate);
                // Remove draggable to remove the sync from prev obj
                SceneDraggable oldComp = newCrateObj.GetComponent<SceneDraggable>();
                UnityEngine.Component.Destroy(oldComp);
                SceneDraggable dragComp = newCrateObj.AddComponent<SceneDraggable>();

                // translate the local pos to world for the spawned object
                float x = i == 0 ? -0.53f : 0.53f;
                GameObject temp = new("temp");
                temp.transform.parent = _quest.questVehicle.transform;
                temp.transform.localPosition = new(x, 0.8f, -3f);
                newCrateObj.transform.position = temp.transform.position;
                newCrateObj.transform.rotation = _quest.questVehicle.transform.rotation;
                GameObject.Destroy(temp);

                // config the draggable + interactable
                dragComp.DragOrigin = newCrateObj.transform.Find("Drag origin");
                InteractableObject crateInteract = newCrateObj.GetComponent<InteractableObject>();
                crateInteract.MaxInteractionRange = 1.8f;

                newCrateObj.SetActive(true);
                _quest.questBoxes.Add(newCrateObj);
                VehicleTrunkSlot currentSlot = occupiedSlots.Last();
                currentSlot.loadedBox.SetActive(false);
                currentSlot.isSlotOccupied = false;
                occupiedSlots.Remove(currentSlot);
            }
            Log("Spawned 2");

            return;
        }

        public IEnumerator CleanupQuest()
        {
            // disable veh
            _quest.questVehicle.IsPlayerOwned = false;

            // reset the warehouse
            Transform warehouseTr = Singleton<Map>.Instance.transform.Find("Hyland Point/Region_Northtown/Small warehouse/Bodyshop (1)");
            warehouseTr.Find("bodyshop/GarageDoor (1)").gameObject.SetActive(true);
            Transform interior = warehouseTr.Find("Interior");
            interior.gameObject.SetActive(false);

            // destroy the spawned crates
            foreach (GameObject go in _quest.questBoxes)
            {
                UnityEngine.Object.Destroy(go);
            }
            _quest.questBoxes.Clear();

            // destroy the woodenCrate fielded obj
            UnityEngine.Object.Destroy(_quest.woodenCrate);
            _quest.woodenCrate = null;

            // destroy the vehicle bed instantiated box models
            foreach (VehicleTrunkSlot slot in _quest.vehicleBoxSlots)
            {
                UnityEngine.Object.Destroy(slot.loadedBox);
            }
            _quest.vehicleBoxSlots.Clear();

            // destroy the vehicle with timeout
            yield return Wait30;
            if (!registered) yield break;

            if (_quest.questVehicle != null && _quest.questVehicle.gameObject != null)
            {
                // check that it isnt occupied
                if (_quest.questVehicle.IsOccupied)
                    _quest.questVehicle.ExitVehicle();

                UnityEngine.Object.Destroy(_quest.questVehicle);
            }

            _quest.manorGate.SetEnterable(false);
        }
    }

}