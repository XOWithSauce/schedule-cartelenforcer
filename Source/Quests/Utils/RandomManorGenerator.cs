using MelonLoader;
using System.Collections;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne;
using ScheduleOne.Audio;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Interaction;
using ScheduleOne.ItemFramework;
using ScheduleOne.Misc;
using ScheduleOne.EntityFramework;
using ScheduleOne.AvatarFramework.Animation;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.ObjectScripts;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Property;
using ScheduleOne.Quests;
using ScheduleOne.Storage;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
#else
using Il2CppScheduleOne;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Misc;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.AvatarFramework.Animation;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Storage;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
#endif

namespace CartelEnforcer 
{

    public static class RandomManorGenerator
    {
        // Room centers adjusted to ground level
        public static readonly List<Vector3> upstairsRoomCenters = new()
        {
            new Vector3(170.7373f, 14.25f, -53.7164f),
            new Vector3(170.7373f, 14.25f, -60.0944f),
            new Vector3(156.7373f, 14.25f, -60.0944f),
            new Vector3(156.7373f, 14.25f, -53.7164f)
        };

        public static readonly List<Vector3> downstairsRoomCenters = new()
        {
            new Vector3(157.1746f, 10.5f, -56.9532f),
            new Vector3(169.755f, 10.5f, -56.9532f)
        };

        // Additional safe spawn spots (not in room decor)
        public static readonly List<SafeSpawnSpot> lootSafeSpawns = new()
        {
            new SafeSpawnSpot(new Vector3(153.63f, 14.24f, -56.13f), new Vector3(0f, 0f, 0f)),
            new SafeSpawnSpot(new Vector3(159.43f, 14.24f, -57.73f), new Vector3(0f, 90f, 0f)),
            new SafeSpawnSpot(new Vector3(173.43f, 14.24f, -57.89f), new Vector3(0f, 180f, 0f)),
            new SafeSpawnSpot(new Vector3(166.56f, 14.24f, -51.41f), new Vector3(0f, 75f, 0f)),
            new SafeSpawnSpot(new Vector3(153.46f, 15.28f, -54.77f), new Vector3(0f, 335f, 0f)),
        };

        // Check spawnable item name contains this
        public static Dictionary<string, NetworkObject> buildablesMap = new()
        {
            { "CoffeeTable", null },
            { "DisplayCabinet", null },
            { "FloorLamp", null },
            { "GrandfatherClock", null },
            { "Jukebox_Built", null },
            { "MetalSquareTable", null },
            { "MixingStation_Built", null },
            { "Painting_Rapscallion", null },
            { "SingleBed", null },
            { "Small Safe", null },
            { "TV_Built", null },
            { "WoodSquareTable", null },
            { "Metal Bars Sewer Door", null },
            { "Thomas", null },
            { "GoldenToilet", null }

        };
        // Except for sofa thats in the hierarchy inside the RE office which will be manual
        private static GameObject sofaObj = null;

        // During quest needs to track safe
        public static Safe questSafe = null;
        public static Action onSafeBuilt; 

        // During quest needs to track to enable song
        public static Jukebox activeJukebox = null;

        // And track all generated objects to destroy them later
        public static List<RoomDesignBase> roomsGenerated = new();
        public static List<GameObject> nonRoomObjects = new();

        // For door lerp animation
        private static Quaternion doorOrigRot;

        public static IEnumerator SetupManor()
        {
            Log("SETUP MANOR");
            Manor manor = UnityEngine.Object.FindObjectOfType<Manor>(true);
            if (manor == null)
            {
                Log("Manor is null");
                yield break;
            }

            // Disable the dupe doors for now, remember to re-enable later
            // simultaneously get the door sound
            AudioClip open = null;
            AudioClip close = null;

            Transform doorsTr = manor.transform.Find("Doors");
            if (doorsTr != null)
            {
                // Doors first transsform child
                // Find tr child named "Audio Source" "Closet_Heavy_Open" OR "door_close"
                // if name contains that then from that object get component audio source

                if (doorsTr.childCount > 0)
                {
                    Transform doorExample = doorsTr.GetChild(0);
                    for (int i = 0; i < doorExample.childCount; i++)
                    {
                        if (doorExample.GetChild(i).name.ToLower().Contains("audio source"))
                        {
                            if (doorExample.GetChild(i).name.ToLower().Contains("closet_heavy_open"))
                            {
                                open = doorExample.GetChild(i).gameObject.GetComponent<AudioSource>()?.clip;
                                continue;
                            }
                            if (doorExample.GetChild(i).name.ToLower().Contains("door_close"))
                            {
                                close = doorExample.GetChild(i).gameObject.GetComponent<AudioSource>()?.clip;
                                continue;
                            }
                        }
                    }
                }

                // Then deactivate because custom door
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
            // Before setting up interactables for custom door (1) disable the front door interactables
            Transform doorFront = manor.OriginalContainer.transform.Find("MansionDoor");
            if (doorFront == null)
            {
                Log("Manor DOOR in orig container is NULL");
                yield break;
            }
            Transform doorFrontContainer = doorFront.Find("Container"); // Lerp rotation on this one because it works with hinges
            if (doorFrontContainer == null)
            {
                Log("Manor DOORCONTAINER in container is NULL");
                yield break;
            }
            doorFrontContainer.GetChild(0).GetComponent<InteractableObject>().enabled = false;
            doorFrontContainer.GetChild(1).GetComponent<InteractableObject>().enabled = false;


            doorOrigRot = doorContainer.transform.localRotation;
            InteractableObject exteriorIntObj = doorContainer.GetChild(0).GetComponent<InteractableObject>();
            InteractableObject interiorIntObj = doorContainer.GetChild(1).GetComponent<InteractableObject>();

            UnityEngine.Events.UnityAction doorInteractedAction = null;
            void onDoorInteracted()
            {
                if (activeManorQuest.QuestEntry_BreakIn.State == EQuestState.Active)
                {
                    coros.Add(MelonCoroutines.Start(LerpDoorRotation(doorContainer, activeManorQuest.QuestEntry_BreakIn.Complete, open, close)));
                    coros.Add(MelonCoroutines.Start(DoorEnterAnimation(enterBuilding: true)));
                    coros.Add(MelonCoroutines.Start(OnDoorFirstEntered()));

                    if (doorInteractedAction != null)
                    {
                        exteriorIntObj.onInteractStart.RemoveListener(doorInteractedAction);
                        doorInteractedAction = null;
                    }
                }
            }
            exteriorIntObj.message = "Open Door";
            doorInteractedAction = (UnityEngine.Events.UnityAction)onDoorInteracted;
            exteriorIntObj.onInteractStart.AddListener(doorInteractedAction);
            exteriorIntObj.MaxInteractionRange = 2f;

            UnityEngine.Events.UnityAction interiorDoorInteractedAction = null;
            void onInteriorDoorInteracted()
            {
                if (activeManorQuest.QuestEntry_EscapeManor.State == EQuestState.Active || activeManorQuest.QuestEntry_SearchResidence.State == EQuestState.Active || activeManorQuest.QuestEntry_DefeatManorGoons.State == EQuestState.Active)
                {
                    coros.Add(MelonCoroutines.Start(LerpDoorRotation(doorContainer, null, open, close)));
                    coros.Add(MelonCoroutines.Start(DoorEnterAnimation(enterBuilding: false)));

                    if (interiorDoorInteractedAction != null)
                    {
                        interiorIntObj.onInteractStart.RemoveListener(interiorDoorInteractedAction);
                        interiorDoorInteractedAction = null;
                    }
                }
            }
            interiorDoorInteractedAction = (UnityEngine.Events.UnityAction)onInteriorDoorInteracted;
            interiorIntObj.onInteractStart.AddListener(interiorDoorInteractedAction);
            interiorIntObj.MaxInteractionRange = 3f;
            interiorIntObj.message = "Leave Manor";

            Log("Configured int objects");

            // then setup lights
            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };

            foreach (Transform room in rooms)
            {
                room.gameObject.SetActive(true);
                ToggleableLight[] lights = room.GetComponentsInChildren<ToggleableLight>();
                if (lights.Length == 0)
                {
                    Log("No lights found");
                    continue;
                }
                yield return Wait05;
                if (!registered) yield break;

                foreach (ToggleableLight light in lights)
                {
                    yield return Wait05;
                    if (!registered) yield break;
                    light.isOn = true;
                }
            }

            onSafeBuilt = new Action(() => { coros.Add(MelonCoroutines.Start(PrepareSafe())); });

            Log("Base Setup rooms done, generating content");
            coros.Add(MelonCoroutines.Start(GenerateRooms()));

            yield break;
        }

        public static IEnumerator InitManorItemRef()
        {
            // parse needed nobs and assign them
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            List<string> keys = new List<string>(buildablesMap.Keys);

            bool sofaAssigned = false;

            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                yield return Wait01;
                if (!registered) yield break;
                if (keys.Count == 0) break; // Objects fulfilled, break out

                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                string name = prefab?.gameObject.name;
                // First check for sofa root
                if (!sofaAssigned && name.Contains("RE Office"))
                {
                    //For Sofa: Search Spawnable Prefabs for "RE Office"
                    //That has transform child object: "Interior" and that has child object "Double Sofa" Instantiate new from that
                    Transform interior = prefab.transform.Find("Interior");
                    if (interior != null)
                    {
                        sofaAssigned = true;
                        sofaObj = interior.Find("Double Sofa").gameObject;
                    }
                }
                else
                {
                    // If the prefab name contains the wanted string assign it in the dictionary
                    string wasAssigned = string.Empty;
                    foreach (string key in keys)
                    {
                        if (buildablesMap[key] == null && name.Contains(key))
                        {
                            if (key == "Thomas" && name.Contains("BoxSUV")) continue;
                            buildablesMap[key] = prefab;
                            wasAssigned = key;
                            break; // break out
                        }
                    }
                    // was assigned remove from keys to save up on iterations
                    if (wasAssigned != string.Empty)
                        keys.Remove(wasAssigned);
                }
            }

            Log("Finished initializing manor items");
            yield break;
        }

        public static void ResetManorItemRef()
        {
            foreach (string key in buildablesMap.Keys)
            {
                buildablesMap[key] = null;
            }
            sofaObj = null;
            questSafe = null;
            activeJukebox = null;
            onSafeBuilt = null;
            roomsGenerated.Clear();
            nonRoomObjects.Clear();
            manorGoons.Clear();
            manorGoonGuids.Clear();
        }

        public static IEnumerator GenerateRooms()
        {
            // Safe spawn spot must be pre determined
            // 25% chance to use the original spawn positions
            // and 75% to use the ones assigned to upstair rooms
            bool useRoomSafeSpawn = false;
            bool useUpstairLivingSafe = false;
            if (UnityEngine.Random.Range(0f, 1f) < 0.75f)
            {
                useRoomSafeSpawn = true;
                // And decide which generated upstairs room has it 50/50
                if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
                    useUpstairLivingSafe = true;
            }

            // Generate Downstairs rooms
            DownstairsKitchenRoom kitchen = new();
            kitchen.roomCenter = downstairsRoomCenters[0];
            kitchen.roomRotation = Vector3.zero;
            kitchen.roomTransform = new GameObject("CE_ManorKitchenRoom").transform;
            kitchen.canGenerateSafe = false;
            kitchen.waitInstantiateEach = Wait05;
            roomsGenerated.Add(kitchen);

            DownstairsLivingRoom downstairLiving = new();
            downstairLiving.roomCenter = downstairsRoomCenters[1];
            downstairLiving.roomRotation = Vector3.zero;
            downstairLiving.roomTransform = new GameObject("CE_ManorLivingRoom").transform;
            downstairLiving.canGenerateSafe = false;
            downstairLiving.waitInstantiateEach = Wait05;
            roomsGenerated.Add(downstairLiving);

            int preReservedIndex = 1;
            // Check if exceedingly rare room gets generated
            if (UnityEngine.Random.Range(0, 1000) == 0 || currentConfig.debugMode)
            {
                ExceedinglyRareGoldenRoomWithThomasTakingAShitOnAGoldenToilet bigRoom = new();
                bigRoom.roomTransform = new GameObject("CE_ToiletRoom").transform;
                bigRoom.roomCenter = upstairsRoomCenters[preReservedIndex];
                bigRoom.roomRotation = Vector3.zero;
                bigRoom.canGenerateSafe = false;
                roomsGenerated.Add(bigRoom);
                activeManorQuest.CompletionXP += 1500;
            }
            else
                preReservedIndex = -1;

            // Pick 2 rooms from upstairs and generate
            UpstairsLivingRoom upstairLiving = new();
            int upstairsIndex = UnityEngine.Random.Range(0, upstairsRoomCenters.Count);
            if (upstairsIndex == preReservedIndex)
                upstairsIndex = (upstairsIndex + 1) % upstairsRoomCenters.Count;
            upstairLiving.roomCenter = upstairsRoomCenters[upstairsIndex];
            upstairLiving.roomRotation = Vector3.zero;
            upstairLiving.roomTransform = new GameObject("CE_ManorUpstairLiving").transform;
            upstairLiving.canGenerateSafe = useUpstairLivingSafe && useRoomSafeSpawn;
            upstairLiving.waitInstantiateEach = Wait05;
            roomsGenerated.Add(upstairLiving);

            UpstairsBedRoom upstairBed = new();
            int upstairsIndex2 = UnityEngine.Random.Range(0, upstairsRoomCenters.Count);
            while (upstairsIndex2 == upstairsIndex || upstairsIndex2 == preReservedIndex)
                upstairsIndex2 = (upstairsIndex2 + 1) % upstairsRoomCenters.Count;
            upstairBed.roomCenter = upstairsRoomCenters[upstairsIndex2];
            upstairBed.roomRotation = Vector3.zero;
            upstairBed.roomTransform = new GameObject("CE_ManorUpstairBed").transform;
            upstairBed.canGenerateSafe = !useUpstairLivingSafe && useRoomSafeSpawn;
            upstairBed.waitInstantiateEach = Wait05;
            roomsGenerated.Add(upstairBed);

            // Start instantiating
            foreach (RoomDesignBase room in roomsGenerated)
                coros.Add(MelonCoroutines.Start(room.SpawnDesign()));

            // Generate the downstair corridor which is always the same design, clock and a painting
            NetworkObject clock = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["GrandfatherClock"]);
            clock.transform.position = new Vector3(161.5f, 10.5f, -56f);
            clock.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            NetworkObject painting = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["Painting_Rapscallion"]);
            painting.transform.position = new Vector3(161.28f, 12.465f, -59f);
            painting.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            nonRoomObjects.Add(clock.gameObject);
            nonRoomObjects.Add(painting.gameObject);

            // Lastly if rooms dont spawn a safe its in one of the random indices
            if (!useRoomSafeSpawn)
            {
                // spawn random pos from the list of safespawnspots
                int index = UnityEngine.Random.Range(0, lootSafeSpawns.Count);
                NetworkObject nob = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["Small Safe"]);
                questSafe = nob.GetComponent<Safe>();
                nob.transform.position = lootSafeSpawns[index].spawnSpot;
                nob.transform.rotation = Quaternion.Euler(lootSafeSpawns[index].eulerAngles);
                nonRoomObjects.Add(nob.gameObject);
                yield return Wait05;
                nob.gameObject.SetActive(true);
                if (onSafeBuilt != null)
                    onSafeBuilt.Invoke();
            }
            yield return Wait2;

            foreach (GameObject go in nonRoomObjects)
                go.SetActive(true);

            // Enable the animation on grandfather clock
            Transform clockTr = clock.transform.Find("Grandfather Clock");
            if (clockTr != null && clockTr.TryGetComponent<Animation>(out Animation anim))
                anim.Play();

            yield break;
        }

        public static IEnumerator PrepareSafe()
        {
            Log("Prepare Safe");
            onSafeBuilt = null;

            StorageDoorAnimation doorAnim = questSafe.GetComponent<StorageDoorAnimation>();
            doorAnim.Open();
            yield return Wait05;
            if (!registered) yield break;

            doorAnim.Close();

#if MONO
            System.Action onOpenedAction = null;
#else
            Il2CppSystem.Action onOpenedAction = null;
#endif
            void SecondaryTrigger()
            {
                if (activeManorQuest.QuestEntry_SearchResidence.State == EQuestState.Active)
                    activeManorQuest.QuestEntry_SearchResidence.Complete();

                if (onOpenedAction != null)
                {
                    questSafe.onOpened -= onOpenedAction;
                    onOpenedAction = null;
                }
            }
#if MONO
            onOpenedAction = (System.Action)SecondaryTrigger;
#else
            onOpenedAction = (Il2CppSystem.Action)SecondaryTrigger;
#endif
            questSafe.onOpened += onOpenedAction;
            questSafe.StorageEntitySubtitle = "Thomas' Safe";

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
                questSafe.InsertItem(gunInst, true);
                currentSlots++;
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.90f)
            {
                ItemDefinition defCoke = GetItem("cocaine");
                ItemInstance cokeInst = defCoke.GetDefaultInstance(UnityEngine.Random.Range(12, 20));
                questSafe.InsertItem(cokeInst, true);
                currentSlots++;
            }

            ItemDefinition defMag = GetItem("m1911mag");
            ItemInstance magInst = defMag.GetDefaultInstance(1);
            questSafe.InsertItem(magInst, true);
            currentSlots++;

            bool goldBarInserted = false;
            if (UnityEngine.Random.Range(0f, 1f) > 0.30f && currentSlots <= maxSlotsToFill)
            {
                int goldBarQty = UnityEngine.Random.Range(3, 7);
                ItemDefinition goldBarDef = GetItem("goldbar");
                ItemInstance goldBar = goldBarDef.GetDefaultInstance(goldBarQty);
                questSafe.InsertItem(goldBar, true);
                goldBarInserted = true;
                currentSlots++;
            }
            if (currentSlots <= maxSlotsToFill && !goldBarInserted)
            {
                ItemDefinition defCash = GetItem("cash");
                ItemInstance cashInstance = defCash.GetDefaultInstance(1);
#if MONO
                if (cashInstance is CashInstance inst)
                {
                    inst.Balance = 1000f;
                }
#else
                CashInstance tempInst = cashInstance.TryCast<CashInstance>();
                if (tempInst != null)
                {
                    tempInst.Balance = 1000f;
                }
#endif
                questSafe.InsertItem(cashInstance, true);
                currentSlots++;
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.40f && currentSlots <= maxSlotsToFill)
            {
                ItemDefinition defWatch = GetItem("silverwatch");
                ItemInstance watchInst = defWatch.GetDefaultInstance(1);
                questSafe.InsertItem(watchInst, true);
                currentSlots++;
            }

            if (UnityEngine.Random.Range(0f, 1f) > 0.40f && currentSlots <= maxSlotsToFill)
            {
                ItemDefinition defChain = GetItem("silverchain");
                ItemInstance chainInst = defChain.GetDefaultInstance(1);
                questSafe.InsertItem(chainInst, true);
                currentSlots++;
            }

            yield break;
        }

        public static IEnumerator CleanupManor()
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
                    doorContainer.transform.localRotation = doorOrigRot;
                }
            }

            // Disable rooms lights
            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };

            foreach (Transform room in rooms)
            {
                room.gameObject.SetActive(false);
                ToggleableLight[] lights = room.GetComponentsInChildren<ToggleableLight>();
                if (lights.Length == 0)
                {
                    Log("No lights found");
                    continue;
                }
                yield return Wait05;
                if (!registered) yield break;

                foreach (ToggleableLight light in lights)
                {
                    yield return Wait05;
                    if (!registered) yield break;
                    light.isOn = false;
                }
            }

            // Despawn Goons
            foreach (CartelGoon goon in manorGoons)
            {
                yield return Wait05;
                if (!registered) yield break;

                goon.Health.MaxHealth = 100f;
                goon.Health.Health = 100f;

                goon.Behaviour.CombatBehaviour.GiveUpRange = activeManorQuest.GiveUpRange;
                goon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = activeManorQuest.GiveUpAfterSuccessfulHits;
                goon.Behaviour.CombatBehaviour.DefaultSearchTime = activeManorQuest.DefaultSearchTime;

                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                goon.Behaviour.ScheduleManager.EnableSchedule();

                if (goon.Health.IsDead)
                    goon.Health.Revive();
                if (goon.IsGoonSpawned)
                    goon.Despawn();
                if (goon.Behaviour.CombatBehaviour.Active)
                    goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            }

            // Destroy built
            foreach (RoomDesignBase room in roomsGenerated)
            {
                yield return Wait05;
                if (!registered) yield break;
                UnityEngine.Object.Destroy(room.roomTransform.gameObject);
            }

            foreach (GameObject go in nonRoomObjects)
            {
                yield return Wait05;
                if (!registered) yield break;
                UnityEngine.Object.Destroy(go);
            }

            ResetManorItemRef();

            yield break;
        }

        public static IEnumerator LerpDoorRotation(Transform tr, Action cb, AudioClip open, AudioClip close)
        {
            AudioSource source = tr.gameObject.AddComponent<AudioSource>();
            source.volume = 0.2f;
            source.clip = open;
            Quaternion targetRotation = Quaternion.Euler(0f, 75f, 0f);

            Quaternion startRotation = tr.localRotation;

            float duration = 1.5f;
            float elapsedTime = 0f;

            source.Play();
            while (elapsedTime < duration && registered)
            {
                float progress = elapsedTime / duration;
                tr.localRotation = Quaternion.Lerp(startRotation, targetRotation, progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            tr.localRotation = targetRotation;

            yield return new WaitForSeconds(3.8f); // Wait player anim but start 0.2s before it finishes turning around
            if (!registered) yield break;

            duration = 1f;
            elapsedTime = 0f;

            while (elapsedTime < duration && registered) 
            {
                float progress = elapsedTime / duration;
                tr.localRotation = Quaternion.Lerp(targetRotation, startRotation, progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            source.clip = close;
            source.Play();

            tr.localRotation = startRotation;

            if (cb != null)
                cb();

            UnityEngine.Object.Destroy(source);
            yield break;
        }

        public static IEnumerator DoorEnterAnimation(bool enterBuilding)
        {
            Singleton<GameInput>.Instance.ExitAll();
            PlayerSingleton<PlayerCamera>.Instance.SetCanLook(false);
            PlayerSingleton<PlayerMovement>.Instance.CanMove = false;

            Transform playerTr = PlayerSingleton<PlayerMovement>.Instance.transform.root;
            Vector3 sidePos = new Vector3(162.7173f, 11.1317f, -48.8222f);
            Quaternion sideRot = Quaternion.Euler(0f, enterBuilding ? 160f : 0f, 0f);

            Vector3 insidePos = new Vector3(163.4521f, 11.465f, -51.942f);
            Quaternion insideRot = Quaternion.Euler(0f, enterBuilding ? 185f : 10f, 0f);

            float elapsed = 0f;
            float dur = 1f;
            Vector3 startPos = playerTr.position;
            Quaternion startRot = playerTr.rotation;

            Vector3 startOrigin = enterBuilding ? sidePos : insidePos;
            Quaternion startOriginRot = enterBuilding ? sideRot : insideRot;

            while (elapsed < dur && registered)
            {
                float t = elapsed / dur;
                playerTr.position = Vector3.Lerp(startPos, startOrigin, t);
                playerTr.rotation = Quaternion.Slerp(startRot, startOriginRot, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return Wait05;
            if (!registered) yield break;

            elapsed = 0f;
            dur = 3f;
            startPos = playerTr.position;
            startRot = playerTr.rotation;

            Vector3 nextPos = enterBuilding ? insidePos : sidePos;
            Quaternion nextRot = enterBuilding ? insideRot : sideRot;

            while (elapsed < dur && registered)
            {
                float t = elapsed / dur;
                playerTr.position = Vector3.Lerp(startPos, nextPos, t);
                playerTr.rotation = Quaternion.Slerp(startRot, nextRot, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            playerTr.position = nextPos;
            playerTr.rotation = nextRot;

            yield return Wait05;
            if (!registered) yield break;

            elapsed = 0f;
            dur = 0.5f;
            startRot = playerTr.rotation;

            while (elapsed < dur && registered)
            {
                float t = elapsed / dur;
                playerTr.rotation = Quaternion.Slerp(startRot, Quaternion.Euler(0f, enterBuilding ? 0f : 165f, 0f), t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            PlayerSingleton<PlayerCamera>.Instance.SetCanLook(true);
            PlayerSingleton<PlayerMovement>.Instance.CanMove = true;
        }

        public static IEnumerator OnDoorFirstEntered()
        {
            if (Singleton<MusicPlayer>.Instance.IsPlaying)
                Singleton<MusicPlayer>.Instance.StopAndDisableTracks();

            Log("Starting jukebox");
            if (activeJukebox == null)
            {
                Log("Jukebox is unassigned!");
                yield break;
            }

            // Select track
            int trackIndex = 0;
            for (int i = 0; i < activeJukebox.TrackList.Length; i++)
            {
                if (activeJukebox.TrackList[i] != null && activeJukebox.TrackList[i].TrackName.ToLower().Contains("royalty in loyalty"))
                {
                    trackIndex = i;
                    break;
                }
            }

            activeJukebox.PlayTrack(trackIndex);
            activeManorQuest.isJukeboxPlaying = true;
            yield return Wait2;
            if (!registered) yield break;
#if MONO
            activeJukebox.onStateChanged = (Action)Delegate.Combine(activeJukebox.onStateChanged, (Action)OnJukeboxStateChange);
#else
            activeJukebox.onStateChanged += (Il2CppSystem.Action)OnJukeboxStateChange;
#endif
            yield break;
        }

        public static void OnJukeboxStateChange()
        {
            if (!activeManorQuest.isJukeboxPlaying) return;
            activeManorQuest.isJukeboxPlaying = false;

            Log("Jukebox Disable");
            if (activeJukebox == null)
            {
                Log("Jukebox is unassigned!");
                return;
            }
            Jukebox.JukeboxState newState = new();
            newState.CurrentTrackTime = 0f;
            newState.CurrentVolume = 0;
            newState.IsPlaying = false;
            newState.Sync = false;
            newState.RepeatMode = Jukebox.ERepeatMode.None;
            newState.Shuffle = false;
            activeJukebox.SetJukeboxState(newState, false);
            Log("Finished jukebox disable");
            return;
        }

        public class SafeSpawnSpot
        {

            public Vector3 spawnSpot;
            public Vector3 eulerAngles;
            public SafeSpawnSpot(Vector3 pos, Vector3 rot)
            {
                spawnSpot = pos;
                eulerAngles = rot;
            }
        }

        public abstract class RoomDesignBase
        {
            public WaitForSeconds waitInstantiateEach;
            public Vector3 roomCenter { get; set; }
            public Vector3 roomRotation { get; set; }
            public Transform roomTransform { get; set; }
            public bool canGenerateSafe { get; set; }
            public abstract IEnumerator SpawnDesign();

            public void AfterSpawnComplete()
            {
                for (int i = 0; i < roomTransform.childCount; i++)
                    roomTransform.GetChild(i).gameObject.SetActive(true);
            }
            public void SpawnItemOut(string itemKey, out NetworkObject nob)
            {
                nob = null;

                if (!buildablesMap.ContainsKey(itemKey))
                {
                    Log("Tried to instantiate obj with incorrect key");
                    return;
                }
                nob = this.SpawnItem(buildablesMap[itemKey]);
                return;
            }
            public void SpawnItemOut(string itemKey, out GameObject go)
            {
                go = null;
                if (itemKey == "Sofa")
                {
                    go = this.SpawnItem(sofaObj);
                    return;
                }
            }

            public void AdjustY(NetworkObject nob)
            {
                Transform boundsTr = nob.transform.Find("Bounds");
                if (boundsTr == null)
                {
                    Log("Cannot adjust Y, bounds is null");
                    return;
                }

                Transform buildPointTr = nob.transform.Find("BuildPoint");
                if (buildPointTr == null)
                {
                    Log("Cannot adjust Y, Build Point is null");
                    return;
                }
                float buildPointY = buildPointTr.localPosition.y;
                if (buildPointY < 0f)
                    buildPointY = -(buildPointY);
                float boundLocalY = boundsTr.localPosition.y;

                float y = buildPointY + boundLocalY;
                nob.transform.localPosition = new(nob.transform.localPosition.x, y, nob.transform.localPosition.z);
            }
            private GameObject SpawnItem(GameObject obj)
            {
                GameObject go = UnityEngine.Object.Instantiate<GameObject>(obj, roomTransform);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return go;
            }

            private NetworkObject SpawnItem(NetworkObject obj)
            {
                NetworkObject nob = UnityEngine.Object.Instantiate<NetworkObject>(obj, roomTransform);
                nob.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return nob;
            }

        }

        // Local positions from room center as its parented
        private class UpstairsLivingRoom : RoomDesignBase
        {

            private readonly Vector3 coffeeTablePos = new Vector3(-0.2873f, 0f, 1.2164f);
            private readonly Vector3 coffeeTableRot = Vector3.zero;

            private readonly Vector3 sofaPos = Vector3.zero;
            private readonly Vector3 sofaRot = Vector3.zero;

            private readonly Vector3 displayCabinetPos = new Vector3(0.9627f, 0.85f, -1.2836f);
            private readonly Vector3 displayCabinetRot = Vector3.zero;

            private readonly Vector3 safePos = new Vector3(0.7f, 2.37f, -1.23f);
            private readonly Vector3 safeRot = new Vector3(0f, 270f, 0f);

            public override IEnumerator SpawnDesign()
            {
                Log("Spawning upstairs living room");
                roomTransform.position = roomCenter;

                base.SpawnItemOut("CoffeeTable", out NetworkObject coffeeTable);
                coffeeTable.transform.localPosition = coffeeTablePos;
                coffeeTable.transform.localRotation = Quaternion.Euler(coffeeTableRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;

                base.SpawnItemOut("Sofa", out GameObject sofa);
                sofa.transform.localPosition = sofaPos;
                sofa.transform.localRotation = Quaternion.Euler(sofaRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;

                base.SpawnItemOut("DisplayCabinet", out NetworkObject cabinet);
                cabinet.transform.localPosition = displayCabinetPos;
                cabinet.transform.localRotation = Quaternion.Euler(displayCabinetRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;

                if (this.canGenerateSafe)
                {
                    base.SpawnItemOut("Small Safe", out NetworkObject safe);
                    questSafe = safe.GetComponent<Safe>();
                    safe.transform.localPosition = safePos;
                    safe.transform.localRotation = Quaternion.Euler(safeRot);
                    yield return waitInstantiateEach;
                    if (!registered) yield break;
                    safe.gameObject.SetActive(true);

                    if (onSafeBuilt != null)
                        onSafeBuilt.Invoke();
                }

                Log("Completed upstairs living room");
                AfterSpawnComplete();
                yield break;
            }
        }

        private class UpstairsBedRoom : RoomDesignBase
        {
            private readonly Vector3 floorLampPos = new Vector3(-1.0373f, 0f, 1.4664f);
            private readonly Vector3 floorLampRot = Vector3.zero;

            private readonly Vector3 bedPos = new Vector3(-0.0373f, 0f, 0.4664f);
            private readonly Vector3 bedRot = new Vector3(0f, 90f, 0f);

            private readonly Vector3 woodTablePos = new Vector3(-0.7872f, 0f, -0.7836f);
            private readonly Vector3 woodTableRot = Vector3.zero;

            private readonly Vector3 safePos = new Vector3(-0.74f, 0f, -0.76f);
            private readonly Vector3 safeRot = Vector3.zero;
            
            public override IEnumerator SpawnDesign()
            {
                Log("Spawning upstairs bed room");
                roomTransform.position = roomCenter;

                base.SpawnItemOut("FloorLamp", out NetworkObject lamp);
                lamp.transform.localPosition = floorLampPos;
                lamp.transform.localRotation = Quaternion.Euler(floorLampRot);
                base.AdjustY(lamp);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("SingleBed", out NetworkObject bed);
                bed.transform.localPosition = bedPos;
                bed.transform.localRotation = Quaternion.Euler(bedRot);
                base.AdjustY(bed);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("WoodSquareTable", out NetworkObject table);
                table.transform.localPosition = woodTablePos;
                table.transform.localRotation = Quaternion.Euler(woodTableRot);
                base.AdjustY(table);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                if (this.canGenerateSafe)
                {
                    base.SpawnItemOut("Small Safe", out NetworkObject safe);
                    questSafe = safe.GetComponent<Safe>();
                    safe.transform.localPosition = safePos;
                    safe.transform.localRotation = Quaternion.Euler(safeRot);
                    yield return waitInstantiateEach;
                    if (!registered) yield break;

                    safe.gameObject.SetActive(true);
                    if (onSafeBuilt != null)
                        onSafeBuilt.Invoke();
                }

                Log("Completed upstairs bed room");
                AfterSpawnComplete();
                yield break;
            }
        }

        private class DownstairsLivingRoom : RoomDesignBase
        {
            private readonly Vector3 sofaPos = new Vector3(0.25f, 0f, 0f);
            private readonly Vector3 sofaRot = Vector3.zero;

            private readonly Vector3 tvPos = new Vector3(0.295f, 0f, 2.4532f);
            private readonly Vector3 tvRot = new Vector3(0f, 180f, 0f);

            private readonly Vector3 displayCabinet1Pos = new Vector3(2.045f, 0.85f, 2.4532f);
            private readonly Vector3 displayCabinet1Rot = new Vector3(0f, 180f, 0f);

            private readonly Vector3 displayCabinet2Pos = new Vector3(-1.455f, 0.85f, 2.4532f);
            private readonly Vector3 displayCabinet2Rot = new Vector3(0f, 180f, 0f);

            private readonly Vector3 woodTablePos = new Vector3(-1.205f, 0f, -0.0468f);
            private readonly Vector3 woodTableRot = Vector3.zero;
            
            public override IEnumerator SpawnDesign()
            {
                Log("Spawning Downstair living room");
                roomTransform.position = roomCenter;


                base.SpawnItemOut("Sofa", out GameObject sofa);
                sofa.transform.localPosition = sofaPos;
                sofa.transform.localRotation = Quaternion.Euler(sofaRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("TV_Built", out NetworkObject tv);
                tv.transform.localPosition = tvPos;
                tv.transform.localRotation = Quaternion.Euler(tvRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("DisplayCabinet", out NetworkObject cabinet1);
                cabinet1.transform.localPosition = displayCabinet1Pos;
                cabinet1.transform.localRotation = Quaternion.Euler(displayCabinet1Rot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("DisplayCabinet", out NetworkObject cabinet2);
                cabinet2.transform.localPosition = displayCabinet2Pos;
                cabinet2.transform.localRotation = Quaternion.Euler(displayCabinet2Rot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("WoodSquareTable", out NetworkObject table);
                table.transform.localPosition = woodTablePos;
                table.transform.localRotation = Quaternion.Euler(woodTableRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                Log("Completed Downstair living room");
                AfterSpawnComplete();
                yield break;
            }
        }

        private class DownstairsKitchenRoom : RoomDesignBase
        {
            private readonly Vector3 jukeboxPos = new Vector3(1.795f, 0f, -1.5468f);
            private readonly Vector3 jukeboxRot = Vector3.zero;

            private readonly Vector3 mixingStationPos = new Vector3(0.295f, 0f, 2.4532f);
            private readonly Vector3 mixingStationRot = new Vector3(0f, 180f, 0f);

            private readonly Vector3 displayCabinet1Pos = new Vector3(-1.455f, 0.85f, -1.5468f);
            private readonly Vector3 displayCabinet1Rot = Vector3.zero;

            private readonly Vector3 displayCabinet2Pos = new Vector3(0.045f, 0.85f, -1.5468f);
            private readonly Vector3 displayCabinet2Rot = Vector3.zero;

            private readonly Vector3 metalTable1Pos = new Vector3(-1.205f, 0f, 2.4532f);
            private readonly Vector3 metalTable1Rot = Vector3.zero;

            private readonly Vector3 metalTable2Pos = new Vector3(1.795f, 0f, 2.4532f);
            private readonly Vector3 metalTable2Rot = Vector3.zero;

            public override IEnumerator SpawnDesign()
            {
                Log("Spawning Downstair Kitchen room");
                roomTransform.position = roomCenter;

                base.SpawnItemOut("Jukebox_Built", out NetworkObject jukebox);
                jukebox.transform.localPosition = jukeboxPos;
                jukebox.transform.localRotation = Quaternion.Euler(jukeboxRot);
                activeJukebox = jukebox.GetComponent<Jukebox>();
                base.AdjustY(jukebox);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("MixingStation_Built", out NetworkObject mixingStation);
                mixingStation.transform.localPosition = mixingStationPos;
                mixingStation.transform.localRotation = Quaternion.Euler(mixingStationRot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("DisplayCabinet", out NetworkObject cabinet1);
                cabinet1.transform.localPosition = displayCabinet1Pos;
                cabinet1.transform.localRotation = Quaternion.Euler(displayCabinet1Rot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("DisplayCabinet", out NetworkObject cabinet2);
                cabinet2.transform.localPosition = displayCabinet2Pos;
                cabinet2.transform.localRotation = Quaternion.Euler(displayCabinet2Rot);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("MetalSquareTable", out NetworkObject table1);
                table1.transform.localPosition = metalTable1Pos;
                table1.transform.localRotation = Quaternion.Euler(metalTable1Rot);
                base.AdjustY(table1);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                base.SpawnItemOut("MetalSquareTable", out NetworkObject table2);
                table2.transform.localPosition = metalTable2Pos;
                table2.transform.localRotation = Quaternion.Euler(metalTable2Rot);
                base.AdjustY(table2);
                yield return waitInstantiateEach;
                if (!registered) yield break;
                Log("Completed Downstair Kitchen room");

                AfterSpawnComplete();
                yield break;
            }

        }

        private class ExceedinglyRareGoldenRoomWithThomasTakingAShitOnAGoldenToilet : RoomDesignBase
        {
            // 1 / 1000
            public ExceedinglyRareGoldenRoomWithThomasTakingAShitOnAGoldenToilet()
            {
                // Occupies room center 170.7373f, 14.25f, -60.0944f (2nd index in upstairs centerpos list)
                // And uses absolute instead of localpos
                roomCenter = upstairsRoomCenters[1];
            }

            public override IEnumerator SpawnDesign()
            {

                // sewer door
                NetworkObject door = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["Metal Bars Sewer Door"], roomTransform);
                door.transform.SetPositionAndRotation(new Vector3(165.8605f, 14.2515f, -61f), Quaternion.Euler(0f, 270f, 0f));
                yield return Wait05;
                if (!registered) yield break;
                door.gameObject.SetActive(true);
                
                // Prepare toilet
                NetworkObject toilet = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["GoldenToilet"], roomTransform);
                toilet.transform.position = new Vector3(171.3982f, 14.215f, -60.5732f);

                GameObject sittingPoint = new("SittingPoint");
                sittingPoint.transform.parent = toilet.transform;
                sittingPoint.transform.localPosition = new Vector3(0f, 0.6f, 0.2f);
                AvatarSeat toiletSeat = toilet.gameObject.AddComponent<AvatarSeat>();
                toiletSeat.SittingPoint = sittingPoint.transform;

                Transform particleSystemTr = toilet.transform.Find("Golden Toilet/Toilet/Particle System");
                particleSystemTr.localPosition = new Vector3(0f, 0.6772f, 0.0364f);
                particleSystemTr.rotation = Quaternion.Euler(90f, 0f, 0f);
                ParticleSystem particleSystem = particleSystemTr.gameObject.GetComponent<ParticleSystem>();
                var main = particleSystem.main;
#if MONO
                main.loop = true;
#else
                particleSystem.loop = true;
#endif
                main.startColor = new Color(0.4129f, 0.2529f, 0.1529f, 1f);
                yield return Wait05;
                if (!registered) yield break;
                toilet.gameObject.SetActive(true);
                particleSystem.Play();

                // Prepare thomas
                NetworkObject thomas = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["Thomas"], roomTransform);
                Thomas thomasNpc = thomas.GetComponent<Thomas>();
                thomasNpc.Health.Invincible = true;
                yield return Wait05;
                if (!registered) yield break;

                thomas.gameObject.SetActive(true);
                thomasNpc.Movement.SetSeat(toiletSeat);
                thomasNpc.Avatar.LookController.AutoLookAtPlayer = true;

                // lamp
                NetworkObject floorLamp = UnityEngine.Object.Instantiate<NetworkObject>(buildablesMap["FloorLamp"], roomTransform);
                floorLamp.transform.position = new Vector3(172.3888f, 14.245f, -59.9603f);
                ToggleableItem lampToggle = floorLamp.GetComponent<ToggleableItem>();
                yield return Wait05;
                if (!registered) yield break;

                floorLamp.gameObject.SetActive(true);
                lampToggle.Toggle();

                // Prepare gold bar

                Func<string, ItemDefinition> GetItem;
#if MONO
                GetItem = ScheduleOne.Registry.GetItem;
#else
                GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
                ItemInstance barInst = null;
                ItemDefinition def = GetItem("goldbar");
                barInst = def.GetDefaultInstance();
                GameObject goldBarTemplate = null;
                StorableItemInstance storable;
#if MONO
                storable = barInst as StorableItemInstance;
#else
                storable = barInst.TryCast<StorableItemInstance>();
#endif
                if (storable != null)
                {
                    if (storable.StoredItem != null)
                    {
                        GameObject template = storable.StoredItem.transform.Find("Gold bar").gameObject;
                        if (template != null)
                            goldBarTemplate = UnityEngine.Object.Instantiate(template);
                    }
                }

                if (goldBarTemplate == null)
                {
                    Log("Failed to make gold bar template");
                    yield break;
                }
                BoxCollider bc = goldBarTemplate.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.05f, 0.05f, 0.2f);
                bc.center = new Vector3(0f, 0.025f, 0f);

                goldBarTemplate.transform.localScale = new Vector3(5f, 5f, 5f);
                // Spawn large gold bars
                var spawnData = new[] {
                    new { pos = new Vector3(168.3982f, 14.41f, -60.3f), rot = Quaternion.Euler(17, 0, 8) },
                    new { pos = new Vector3(168.3982f, 14.215f, -60.5732f), rot = Quaternion.Euler(0, 63, 0) },
                    new { pos = new Vector3(169.9482f, 14.215f, -60.5732f), rot = Quaternion.Euler(0, 4, 0) },
                    new { pos = new Vector3(170.9482f, 14.215f, -61.5732f), rot = Quaternion.Euler(0, 140, 0) },
                    new { pos = new Vector3(169.7982f, 14.215f, -61.5732f), rot = Quaternion.Euler(0, 98, 0) },
                    new { pos = new Vector3(170.2982f, 14.455f, -61.4732f), rot = Quaternion.Euler(0, 70, 0) },
                    new { pos = new Vector3(170.3982f, 14.215f, -60.5732f), rot = Quaternion.Euler(0, 0, 0) },
                    new { pos = new Vector3(170.1982f, 14.455f, -60.5732f), rot = Quaternion.Euler(0, 92, 0) },
                    new { pos = new Vector3(170.1982f, 14.455f, -60.1732f), rot = Quaternion.Euler(0, 85, 0) },
                    new { pos = new Vector3(170.1982f, 14.455f, -60.9732f), rot = Quaternion.Euler(0, 92, 0) },
                    new { pos = new Vector3(170.3982f, 14.695f, -60.5732f), rot = Quaternion.Euler(0, 0, 0) },
                    new { pos = new Vector3(170.1282f, 14.885f, -60.5732f), rot = Quaternion.Euler(0, 0, 103) }
                };

                foreach (var data in spawnData)
                {
                    GameObject bar = UnityEngine.Object.Instantiate(goldBarTemplate, roomTransform);
                    bar.transform.position = data.pos;
                    bar.transform.rotation = data.rot;
                    bar.SetActive(true);
                }

                // Now modify prefab to have collision with rb
                Rigidbody rb = goldBarTemplate.AddComponent<Rigidbody>();
                rb.angularDrag = 0.98f;
                rb.drag = 0.2f;
                rb.mass = 2.3f;
                rb.maxAngularVelocity = 3f;
                rb.maxLinearVelocity = 7f;

                PhysicMaterial physMat = new();
                physMat.bounceCombine = PhysicMaterialCombine.Maximum;
                physMat.bounciness = 0.03f;
                physMat.dynamicFriction = 0.096f;
                physMat.staticFriction = 0.03f;
                physMat.frictionCombine = PhysicMaterialCombine.Minimum;
                bc.material = physMat;

                goldBarTemplate.transform.localScale = new Vector3(3.6f, 3.6f, 3.6f);

                // Spawn smaller but interactable bars at 2 piles
                int physicsGoldBarCount = 9;
                for (int i = 0; i < physicsGoldBarCount; i++)
                {
                    yield return Wait05;
                    if (!registered) yield break;

                    GameObject bar = UnityEngine.Object.Instantiate(goldBarTemplate, roomTransform);
                    bar.transform.position = new Vector3(
                        169.7678f + UnityEngine.Random.Range(-0.6f, 0.6f),
                        15f + 0.1f*i,
                        -58.9568f + UnityEngine.Random.Range(-0.6f, 0.6f)
                    );
                    bar.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(0, 2) == 0 ? 0f : 30f, UnityEngine.Random.Range(0f, 359f), UnityEngine.Random.Range(0, 2) == 0 ? 0f : 120f);
                    bar.SetActive(true);
                    if (UnityEngine.Random.Range(0, 2) == 0)
                        bar.transform.localScale = goldBarTemplate.transform.localScale - new Vector3(0.8f, 0.8f, 0.8f);
                }

                for (int i = 0; i < physicsGoldBarCount; i++)
                {
                    yield return Wait05;
                    if (!registered) yield break;

                    GameObject bar = UnityEngine.Object.Instantiate(goldBarTemplate, roomTransform);
                    bar.transform.position = new Vector3(
                        172.4147f + UnityEngine.Random.Range(-0.6f, 0.6f),
                        15f + 0.1f * i,
                        -58.9158f + UnityEngine.Random.Range(-0.6f, 0.6f)
                    );
                    bar.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(0, 2) == 0 ? 0f : 30f, UnityEngine.Random.Range(0f, 359f), UnityEngine.Random.Range(0, 2) == 0 ? 0f : 120f);
                    bar.SetActive(true);
                    if (UnityEngine.Random.Range(0, 2) == 0)
                        bar.transform.localScale = goldBarTemplate.transform.localScale - new Vector3(0.8f, 0.8f, 0.8f);
                }

                // cleanup temp
                UnityEngine.Object.Destroy(goldBarTemplate);
                yield break;
            }
        }

    }
}