using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;

using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.AlliedExtension;

#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Interaction;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Storage;
using ScheduleOne.Cartel;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Quests;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using ScheduleOne.Persistence;
using ScheduleOne.GameTime;
using ScheduleOne.UI;
using ScheduleOne.UI.Handover;
using ScheduleOne.NPCs;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet;
using TMPro;
#else
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Handover;
using Il2CppScheduleOne.NPCs;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet;
using Il2CppTMPro;
#endif

namespace CartelEnforcer
{
    public static class SuppliesModule
    {
        public static List<SupplyLocation> supplyLocations;

        public static readonly List<string> barrelLootIds = new()
        {
            "acid", "phosphorus", "gasoline"
        };
        public static List<ItemInstance> barrelLoot = new();

        public static readonly List<string> carLootIds = new()
        {
            "fullspectrumgrowlight", "dryingrack", "airpot"
        };
        public static List<ItemInstance> carLoot = new();

        public static readonly List<string> thomasMessageTemplates = new()
        {
            "We need to expand production in Hyland Point. Help us unload supplies from",
            "New shipment just arrived. I need you to secure contents of",
            "Product demand is spiking. Go and recover the supplies from",
            "We're scaling the operation. Move quickly and get the contents of",
            "Run up some logistics for me. Pick up the contents of",
        };

        public static void InitSuppliesModule()
        {
            Log("[ALLIEDEXT] Init supplies module");
            #region Init Car locations
            SupplyLocation manorCar = new SupplyLocation();
            manorCar.CarPosition = new Vector3(167.3347f, 10.5078f, -68.9665f);
            manorCar.CarRotation = new Vector3(0f, 15.2327f, 0f);
            manorCar.Description = "the Uptown manor parking lot";
            manorCar.Name = "Manor Supplies";
            manorCar.ID = "SUPPLY_MANOR";
            manorCar.Type = ESupplyType.Van;
            manorCar.GuardPosition = new Vector3(162.3889f, 10.99f, -73.1324f);
            manorCar.GuardRotation = new Vector3(0f, 92f, 0f);

            SupplyLocation supermarketCar = new SupplyLocation();
            supermarketCar.CarPosition = new Vector3(18.3931f, 0.5828f, 71.3268f);
            supermarketCar.CarRotation = new Vector3(0f, 271.5327f, 0f);
            supermarketCar.Description = "the Downtown supermarket";
            supermarketCar.Name = "Supermarket Supplies";
            supermarketCar.ID = "SUPPLY_SUPERMARKET";
            supermarketCar.Type = ESupplyType.Van;
            supermarketCar.GuardPosition = new Vector3(24.7739f, 1.065f, 68.6945f);
            supermarketCar.GuardRotation = Vector3.zero;

            SupplyLocation constructionSiteCar = new SupplyLocation();
            constructionSiteCar.CarPosition = new Vector3(-129.0546f, -3.5171f, 96.506f);
            constructionSiteCar.CarRotation = new Vector3(0f, 27.2817f, 0f);
            constructionSiteCar.Description = "the Westville construction site";
            constructionSiteCar.Name = "Construction Site Supplies";
            constructionSiteCar.ID = "SUPPLY_CONSTRUCTION_SITE";
            constructionSiteCar.Type = ESupplyType.Van;
            constructionSiteCar.GuardPosition = new Vector3(-134.0546f, -3.5171f, 96.506f);
            constructionSiteCar.GuardRotation = new Vector3(0f, 158f, 0f);
            #endregion

            #region Init barrel locations
            string transformPath = "";
            string searchName = "";

            SupplyLocation waterfrontBarrel = new SupplyLocation();
            waterfrontBarrel.Description = "the Northtown waterfront";
            waterfrontBarrel.Name = "Northtown Waterfront Supplies";
            waterfrontBarrel.ID = "SUPPLY_WATERFRONT";
            waterfrontBarrel.Type = ESupplyType.Barrel;
            waterfrontBarrel.GuardPosition = new Vector3(-63.3022f, -4.035f, 163.7165f);
            waterfrontBarrel.GuardRotation = new Vector3(0f, 94.6f, 0f);
            waterfrontBarrel.BarrelObjects = new();
            transformPath = "Hyland Point/Region_Northtown/Waterfront/Pallet Rack/";
            searchName = "Pallet_LiquidDrum";
            PopulateBarrels(transformPath, searchName, waterfrontBarrel.BarrelObjects);

            SupplyLocation thompsonsBarrel = new SupplyLocation();
            thompsonsBarrel.Description = "the Northtown Thompsons' construction yard";
            thompsonsBarrel.Name = "Northtown Thompsons' Supplies";
            thompsonsBarrel.ID = "SUPPLY_THOMPSONS";
            thompsonsBarrel.Type = ESupplyType.Barrel;
            thompsonsBarrel.GuardPosition = new Vector3(-31.4224f, 1.0639f, 97.6543f);
            thompsonsBarrel.GuardRotation = Vector3.zero;
            thompsonsBarrel.BarrelObjects = new();
            transformPath = "Hyland Point/Region_Northtown/Construction yard/Fence/";
            searchName = "Liquid Drum";
            PopulateBarrels(transformPath, searchName, thompsonsBarrel.BarrelObjects);

            SupplyLocation docksBarrel = new SupplyLocation();
            docksBarrel.Description = "the Docks";
            docksBarrel.Name = "Docks Supplies";
            docksBarrel.ID = "SUPPLY_DOCKS";
            docksBarrel.Type = ESupplyType.Barrel;
            docksBarrel.GuardPosition = new Vector3(-71.1649f, -1.535f, -33.5302f);
            docksBarrel.GuardRotation = new Vector3(0f, 240f, 0f);
            docksBarrel.BarrelObjects = new();
            transformPath = "Hyland Point/Region_Docks/";
            searchName = "Liquid Drum";
            PopulateBarrels(transformPath, searchName, docksBarrel.BarrelObjects);
            #endregion

            // Add locations
            supplyLocations = new()
            {
                manorCar, supermarketCar, constructionSiteCar, waterfrontBarrel, thompsonsBarrel, docksBarrel
            };

            #region Fill loot tables
            Func<string, ItemDefinition> GetItem;
#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif

            // acid, phosphorus, gasoline 5 qty itemInstance
            foreach (string id in barrelLootIds)
            {
                ItemDefinition def = GetItem(id);
                barrelLoot.Add(def.GetDefaultInstance(5));
            }

            // growing gear 7 qty inst
            foreach (string id in carLootIds)
            {
                ItemDefinition def = GetItem(id);
                carLoot.Add(def.GetDefaultInstance(7));
            }
            #endregion

        }

        public static void PopulateBarrels(string transformPath, string searchName, List<GameObject> populateList)
        {
            Transform target = Singleton<Map>.Instance.transform.Find(transformPath);
            if (target != null)
            {
                Transform targetBarrel = null;
                for (int i = 0; i < target.childCount; i++)
                {
                    targetBarrel = target.GetChild(i);
                    if (targetBarrel != null && targetBarrel.name.Contains(searchName))
                        populateList.Add(targetBarrel.gameObject);
                }
            }
            else
            {
                Log($"[ALLIEDEXT] Failed to find transform: {transformPath}");
            }
            return;
        }

        public static IEnumerator SpawnSupply(SupplyLocation location)
        {
            // MSG
            string supplyObj = location.Type == ESupplyType.Barrel ? "the blue barrels" : "a white van";
            string msg = $"{thomasMessageTemplates[UnityEngine.Random.Range(0, thomasMessageTemplates.Count)]} {supplyObj} at {location.Description}.";
            Thomas thomas = UnityEngine.Object.FindObjectOfType<Thomas>(true);
            if (thomas == null)
            {
                Log("[ALLIEDEXT] Failed to find thomas obj");
                yield break;
            }

            thomas.MSGConversation.SendMessage(
                new Message(msg,
                Message.ESenderType.Other,
                true),
                notify: true,
                network: true);

            if (location.Type == ESupplyType.Barrel)
            {
                Log("[ALLIEDEXT] Spawn Supply Barrel");
                foreach (GameObject go in location.BarrelObjects)
                {
                    SetBarrelInteractable(
                        target: go,
                        item: barrelLoot[UnityEngine.Random.Range(0, barrelLoot.Count)],
                        capacity: 20
                        );
                }
            }
            else // ESupplyType.Van
            {
                Log("[ALLIEDEXT] Spawn Supply Van");

                // if manor open gates
                if (location.ID == "SUPPLY_MANOR")
                {
                    ManorGate[] gate = UnityEngine.Object.FindObjectsOfType<ManorGate>(true);
                    if (gate != null && gate.Length > 1)
                    {
                        // Log("Gates count:" + gate.Length); // there are 2
                        gate[1].SetEnterable(true);
                    }
                }

                coros.Add(MelonCoroutines.Start(SpawnSupplyVan(new Tuple<Vector3, Vector3>(location.CarPosition, location.CarRotation))));
            }

            // spawn guard
            if (activeAlliedSupplies != null)
            {
                Log("[ALLIEDEXT] Goon Spawned");
                SpawnGuardGoon(location.GuardPosition, location.GuardRotation);
            }
            yield return null;
        }

        public static IEnumerator SpawnSupplyVan(Tuple<Vector3, Vector3> target)
        {
            // parse needed nobs
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject nobVan = null;
            StorageEntity rewardStorage = null;

            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "Van")
                {
                    nobVan = prefab;
                }
            }

            // spawn white van reward veh
            NetworkObject veeperVeh = UnityEngine.Object.Instantiate<NetworkObject>(nobVan);
            netManager.ServerManager.Spawn(veeperVeh);
            alliedVanObject = veeperVeh;
            yield return Wait01;
            if (!registered) yield break;
            veeperVeh.transform.parent = Map.Instance.transform;
            veeperVeh.gameObject.SetActive(true);
            veeperVeh.transform.SetPositionAndRotation(target.Item1, Quaternion.Euler(target.Item2));
            rewardStorage = veeperVeh.GetComponent<StorageEntity>();
            rewardStorage.AccessSettings = StorageEntity.EAccessSettings.Full;
            yield return Wait05; // wait physics
            if (!registered) yield break;

            veeperVeh.GetComponent<Rigidbody>().isKinematic = true;

            int rewardedSlots = UnityEngine.Random.Range(2, 8);
            for (int i = 0; i < rewardStorage.ItemSlots.Count; i++)
            {
                if (i >= rewardedSlots) break;
                ItemInstance randomItem = carLoot[UnityEngine.Random.Range(0, carLoot.Count)].GetCopy();
                // half the time reduce 1 quantity to make it more natural not just flat amount stacks
                // so it generates 6 to 7 items each slot
                if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
                    randomItem.Quantity -= 1;
                rewardStorage.ItemSlots[i].InsertItem(randomItem);
            }

            // Reward storage on closed checks if its empty and
            // completes quest entry, it doesnt always work on first try?
#if MONO
            System.Action onClosedAction = null;
#else
            Il2CppSystem.Action onClosedAction = null;
#endif
            void CloseTrigger()
            {
                Log("[ALLIEDEXT] Close trigger");
                if (activeAlliedSupplies == null) return;
                if (activeAlliedSupplies.State != EQuestState.Active) return;
                if (activeAlliedSupplies.QuestEntry_GatherSupplies != null &&
                    activeAlliedSupplies.QuestEntry_GatherSupplies.State == EQuestState.Active)
                {
                    Log("[ALLIEDEXT] storage count:" + rewardStorage.ItemCount);
                    if (rewardStorage.ItemCount == 0)
                    {
                        activeAlliedSupplies.Complete(false);
                    }
                }
            }
#if MONO
            onClosedAction = (System.Action)CloseTrigger;
#else
            onClosedAction = (Il2CppSystem.Action)CloseTrigger;
#endif

            rewardStorage.Close();
            rewardStorage.onClosed += onClosedAction;
            rewardStorage.StorageEntitySubtitle = "Cartel Supply Delivery";

            yield return null;
        }

        public static void SetBarrelInteractable(GameObject target, ItemInstance item, int capacity)
        {
            GameObject interactable = new GameObject("CE_SUPPLY");
            interactable.transform.SetParent(target.transform);
            interactable.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            InteractableObject intObj = target.AddComponent<InteractableObject>();
            intObj.message = $"Take 5 x {item.Name}";
            intObj.SetInteractableState(InteractableObject.EInteractableState.Default);
            // How to get rid of the interactable object if not cconsumed
            int remainingCapacity = capacity;
            UnityEngine.Events.UnityAction barrelInteracted = null;
            void OnBarrelInteracted()
            {
                if (!PlayerSingleton<PlayerInventory>.InstanceExists)
                {
                    Log("[ALLIEDEXT] PlayerInventory instance does not exist");
                    return;
                }

                if (PlayerSingleton<PlayerInventory>.Instance.CanItemFitInInventory(item, 5))
                {
                    PlayerSingleton<PlayerInventory>.Instance.AddItemToInventory(item);
                    remainingCapacity -= 5;
                }

                if (remainingCapacity <= 0)
                {
                    if (interactable != null)
                        GameObject.Destroy(interactable);

                    if (intObj != null)
                        UnityEngine.Object.Destroy(intObj);
                }
            }
            barrelInteracted = (UnityEngine.Events.UnityAction)OnBarrelInteracted;
            intObj.onInteractStart.AddListener(barrelInteracted);

            return;
        }

        public static void SpawnGuardGoon(Vector3 pos, Vector3 rot)
        {
            // spawn 1 overpowered guard with shotgun if possible
            CartelGoon goonGuard = null;
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count == 0) return;

            goonGuard = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(pos);
            goonGuard.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false); // set stayinside disable
            goonGuard.Behaviour.ScheduleManager.DisableSchedule();
            if (goonGuard.isInBuilding) 
            {
                Log("Exit Guard Building");
                goonGuard.ExitBuilding(goonGuard.CurrentBuilding);
                goonGuard.Movement.Warp(pos);
            }
            goonGuard.transform.rotation = Quaternion.Euler(rot);

            // guard has shotgun
#if MONO
            GameObject shotgunGo = Resources.Load("Avatar/Equippables/PumpShotgun") as GameObject;
#else
            UnityEngine.Object shotgunObj = Resources.Load("Avatar/Equippables/PumpShotgun");
            GameObject shotgunGo = shotgunObj.TryCast<GameObject>();
#endif

            AvatarEquippable shotgunEquippable = UnityEngine.Object.Instantiate<GameObject>(shotgunGo, new Vector3(0f, -5f, 0f), Quaternion.identity).GetComponent<AvatarEquippable>();

#if MONO
            AvatarWeapon weaponShotgun = shotgunEquippable as AvatarWeapon;
            AvatarRangedWeapon weaponRangedShotgun = shotgunEquippable as AvatarRangedWeapon;
#else
            // If while truced the player wants to mess with this cartel guard they will die
            AvatarWeapon weaponShotgun = shotgunEquippable.TryCast<AvatarWeapon>();
            AvatarRangedWeapon weaponRangedShotgun = shotgunEquippable.TryCast<AvatarRangedWeapon>();
#endif
            if (weaponShotgun != null)
            {
                goonGuard.Behaviour.CombatBehaviour.DefaultWeapon = weaponShotgun;
            }
            if (weaponRangedShotgun != null)
            {
                weaponRangedShotgun.MaxFireRate = 0.6f;
                weaponRangedShotgun.RepositionAfterHit = true;
                weaponRangedShotgun.CanShootWhileMoving = true;
                weaponRangedShotgun.EquipTime = 0.1f;
                weaponRangedShotgun.HitChance_MinRange = 99f;
                weaponRangedShotgun.HitChance_MaxRange = 80f;
                weaponRangedShotgun.Damage = 98f;
                weaponRangedShotgun.CooldownDuration = 0.8f;
                weaponRangedShotgun.MaxUseRange = 36f;
                weaponRangedShotgun.MinUseRange = 0.1f;
            }
            goonGuard.Behaviour.CombatBehaviour.DefaultWeapon.Equip(goonGuard.Avatar);

            goonGuard.Health.MaxHealth = 500f;
            goonGuard.Health.Health = 500f;
            goonGuard.Movement.SpeedController.AddSpeedControl(new NPCSpeedController.SpeedControl("combat", 5, 0.38f));

            // create dialogue choice
            DialogueController controller = goonGuard.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "Relax buddy, the boss sent me.";
            choice.ChoiceText = $"{text}";
            choice.Enabled = true;

            void ReplyChosen()
            {
                activeAlliedSupplies.interrogatingPlayer = false;
                controller.npc.PlayVO(EVOLineType.Acknowledge);
                controller.handler.WorldspaceRend.ShowText("Go ahead and grab the supplies", 6f);
                controller.handler.ContinueSubmitted();

                if (guardChoiceIndex != -1)
                {
                    var oldChoices = controller.Choices;
                    oldChoices.RemoveAt(guardChoiceIndex);
                    controller.Choices = oldChoices;
                    guardChoiceIndex = -1;
                }
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)ReplyChosen);
            guardChoiceIndex = controller.AddDialogueChoice(choice);

            alliedGuard = goonGuard;

            coros.Add(MelonCoroutines.Start(HandleGuardGoon()));
        }

        public static IEnumerator HandleGuardGoon()
        {
            coros.Add(MelonCoroutines.Start(CheckInterrogate()));

            for (; ; )
            {
                yield return Wait1;
                if (!registered) yield break;

                if (activeAlliedSupplies == null || activeAlliedSupplies.State != EQuestState.Active)
                    break;

                if (alliedGuard == null || alliedGuard.Health.IsDead || alliedGuard.Health.IsKnockedOut || alliedGuard.Behaviour.activeBehaviour == alliedGuard.Behaviour.CombatBehaviour)
                    break;

                Log("Guard in building " + alliedGuard.isInBuilding);
                Log("Guard ragdolled " + alliedGuard.Avatar.Ragdolled);
                Log("Guard CanMove " + alliedGuard.Movement.CanMove());
                Log("Hasdest " + alliedGuard.Movement.HasDestination);
                Log("ISMoving " + alliedGuard.Movement.IsMoving);

                float distFromGuardPos = Vector3.Distance(alliedGuard.CenterPoint, activeAlliedSupplies.location.GuardPosition);
                if (distFromGuardPos > 30f || (activeAlliedSupplies.playerNoticed && activeAlliedSupplies.playerInterrogated && distFromGuardPos > 2f))
                {
                    if (alliedGuard.Movement.HasDestination && Vector3.Distance(alliedGuard.Movement.CurrentDestination, activeAlliedSupplies.location.GuardPosition) < 2f) continue;
                    else
                        alliedGuard.Movement.EndSetDestination(NPCMovement.WalkResult.Interrupted);

                    void OnGuardArrivedToPos(NPCMovement.WalkResult result)
                    {
                        if (result == NPCMovement.WalkResult.Success)
                        {
                            // Calculate forward position from guard pos facing guard rot and then face point
                            Vector3 fwdFromPos = activeAlliedSupplies.location.GuardPosition + (Quaternion.Euler(activeAlliedSupplies.location.GuardRotation) * Vector3.forward * 1.5f);
                            alliedGuard.Movement.FacePoint(fwdFromPos);
                            alliedGuard.Movement.PauseMovement();
                        }
                    }
#if MONO
                    Action<NPCMovement.WalkResult> walkCallback = (Action<NPCMovement.WalkResult>)OnGuardArrivedToPos;
#else
                    Il2CppSystem.Action<NPCMovement.WalkResult> walkCallback = (Il2CppSystem.Action<NPCMovement.WalkResult>)OnGuardArrivedToPos;
#endif
                    alliedGuard.Movement.SetDestination(activeAlliedSupplies.location.GuardPosition, walkCallback, interruptExistingCallback: true);
                    if (alliedGuard.Movement.IsPaused)
                        alliedGuard.Movement.ResumeMovement();
                    continue;
                }

                float distFromPlayer = Vector3.Distance(alliedGuard.CenterPoint, Player.Local.CenterPointTransform.position);
                if (distFromPlayer > 40f) continue;

                if (!activeAlliedSupplies.playerNoticed && !activeAlliedSupplies.playerInterrogated)
                {
                    Log("Look For Player");
                    if (Player.Local.IsPointVisibleToPlayer(alliedGuard.CenterPointTransform.position))
                    {
                        alliedGuard.Movement.FacePoint(Player.Local.CenterPointTransform.position, lerpTime: 0.9f);
                    }

                    if (alliedGuard.Awareness.VisionCone.IsPlayerVisible(Player.Local))
                    {
                        activeAlliedSupplies.playerNoticed = true;
                    }
                }

                if (activeAlliedSupplies.playerNoticed && !activeAlliedSupplies.playerInterrogated)
                {
                    // if not traversing to player
                    if (alliedGuard.Movement.HasDestination || alliedGuard.Movement.IsMoving) continue;
                    Log("Traverse");
                    alliedGuard.Movement.SetDestination(Player.Local.CenterPointTransform);
                    if (alliedGuard.Movement.IsPaused)
                        alliedGuard.Movement.ResumeMovement();
                }
            }
        }

        private static bool CanStartInterrogate()
        {
            return !Singleton<DialogueCanvas>.Instance.isActive && !Singleton<HandoverScreen>.Instance.IsOpen && PlayerSingleton<PlayerCamera>.Instance.activeUIElementCount <= 0 && !alliedGuard.DialogueHandler.IsDialogueInProgress;
        }

        public static IEnumerator CheckInterrogate()
        {
            for (; ; )
            {
                yield return Wait05;
                if (!registered) yield break;
                if (activeAlliedSupplies == null || activeAlliedSupplies.State != EQuestState.Active)
                    yield break;
                if (alliedGuard == null || alliedGuard.Health.IsDead || alliedGuard.Health.IsKnockedOut || alliedGuard.Behaviour.activeBehaviour == alliedGuard.Behaviour.CombatBehaviour)
                    yield break;
                if (activeAlliedSupplies.playerInterrogated || activeAlliedSupplies.interrogatingPlayer) yield break;
                if (Vector3.Distance(Player.Local.CenterPointTransform.position, alliedGuard.CenterPoint) > 3f) continue;

                Log("Interrogate");
                // start dialogue when possible
#if MONO
                yield return new WaitUntil(CanStartInterrogate);
#else
                yield return new WaitUntil((Il2CppSystem.Func<bool>)CanStartInterrogate);
#endif
                if (Vector3.Distance(Player.Local.CenterPointTransform.position, alliedGuard.CenterPoint) > 3f) continue;

                Log("Interrogate Start");
                activeAlliedSupplies.playerInterrogated = true;
                if (alliedGuard.DialogueHandler != null)
                {
                    activeAlliedSupplies.interrogatingPlayer = true;
                    alliedGuard.Movement.EndSetDestination(NPCMovement.WalkResult.Success);
                    DialogueController controller = alliedGuard.DialogueHandler.gameObject.GetComponent<DialogueController>();
                    if (controller != null)
                        controller.StartGenericDialogue(false);
                    Log("Player interrogated");
                }
                yield break;
            }
        }

        public static void OnHourPassEvaluateSupply()
        {
            if (!registered || SaveManager.Instance.IsSaving || isSaving) return;

#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced) return;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced) return;
#endif

            if (alliedSuppliesActive) return;

            if (alliedQuests.hoursUntilNextSupplies > 0)
            {
                alliedQuests.hoursUntilNextSupplies--;
                return;
            }

            int currentTime = NetworkSingleton<TimeManager>.Instance.CurrentTime;
            // If time is not 8:00 in the morning
            if (!(currentTime <= 801 && currentTime >= 759)) return;

            // if the quest has not been activated
            if (activeAlliedSupplies == null)
            {
                coros.Add(MelonCoroutines.Start(SetupTruceSuppliesQuest()));
            }
            // else quest already exists and can be reactivated
            else
            {
                activeAlliedSupplies.ResetSelf();
            }

            return;
        }

#if IL2CPP
        public static void UpdateQuestHUD()
        {
            // because calling base.MinPass in il2cpp crashes instantly
            //activeAlliedSupplies.UpdateHUDUI();
            if (activeAlliedSupplies.hudUI != null && !activeAlliedSupplies.hudUI.WasCollected && activeAlliedSupplies.hudUI.Pointer != IntPtr.Zero)
            {
                TextMeshProUGUI textComp = activeAlliedSupplies.hudUI.MainLabel;
                if (textComp != null && !textComp.WasCollected && textComp.Pointer != IntPtr.Zero)
                {
                    textComp.text = activeAlliedSupplies.title + activeAlliedSupplies.Subtitle;

                    textComp.ForceMeshUpdate();
                }
                else
                    Log("Text component from Hud UI was garbage collected");


                VerticalLayoutGroup group = activeAlliedSupplies.hudUI.hudUILayout;
                if (group != null && !group.WasCollected && group.Pointer != IntPtr.Zero)
                {
                    group.CalculateLayoutInputVertical();
                    group.SetLayoutVertical();
                    if (activeAlliedSupplies.groupRt == null)
                        activeAlliedSupplies.groupRt = group.GetComponent<RectTransform>();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(activeAlliedSupplies.groupRt);
                    group.enabled = false;
                    group.enabled = true;
                }
                else
                    Log("Layout Group component from Hud UI was garbage collected");
            }
            else
            {
                Log("Hud UI was garbage collected");
            }
        }
#endif

        public static void MinPassSupply()
        {
            if (!registered || Singleton<SaveManager>.Instance.IsSaving || activeAlliedSupplies == null || activeAlliedSupplies.State != EQuestState.Active) return;
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
                activeAlliedSupplies.Fail();
            }

            activeAlliedSupplies.Subtitle = $"\n<color=#757575>{activeAlliedSupplies.GetExpiryText()} until supplies vanish</color>";


#if MONO
            activeAlliedSupplies.OnMinPass();
#else
            UpdateQuestHUD();
            activeAlliedSupplies.CheckExpiry();
            if (activeAlliedSupplies.State != EQuestState.Active) return;
#endif

            if (activeAlliedSupplies.QuestEntry_LocateSupplies != null && activeAlliedSupplies.QuestEntry_LocateSupplies.State == EQuestState.Active)
            {
                if (Vector3.Distance(
                    a: Player.Local.CenterPointTransform.position,
                    b: activeAlliedSupplies.location.Type == ESupplyType.Van ? activeAlliedSupplies.location.CarPosition : activeAlliedSupplies.location.BarrelObjects[0].transform.position
                ) < 14f)
                {
                    activeAlliedSupplies.QuestEntry_LocateSupplies.SetState(EQuestState.Completed, false);
                    return;
                }
            }

            if (activeAlliedSupplies.QuestEntry_GatherSupplies != null && activeAlliedSupplies.QuestEntry_GatherSupplies.State == EQuestState.Active)
            {
                // If barrel update barrel poi and compass pos
                bool suppliesClaimed = false;
                // Check unclaimed barrels, update poi
                if (activeAlliedSupplies.location.Type == ESupplyType.Barrel)
                {
                    Vector3 nextBarrel = Vector3.zero;
                    Transform currentBarrel;
                    int consumedBarrels = 0;
                    foreach (GameObject go in activeAlliedSupplies.location.BarrelObjects)
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

                    if (consumedBarrels == activeAlliedSupplies.location.BarrelObjects.Count)
                    {
                        suppliesClaimed = true;
                    }
                    else if (nextBarrel != Vector3.zero)
                    {
                        if (activeAlliedSupplies.QuestEntry_GatherSupplies.PoI != null && activeAlliedSupplies.QuestEntry_GatherSupplies.PoI.gameObject != null)
                        {
                            activeAlliedSupplies.QuestEntry_GatherSupplies.SetPoILocation(nextBarrel);
                        }
                    }
                }

                if (suppliesClaimed)
                {
                    activeAlliedSupplies.Complete(false);
                    return;
                }
            }
            return;

        }
        
    }
    public enum ESupplyType
    {
        Barrel, Van
    }

    public class SupplyLocation
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Description { get; set; }
        public Vector3 CarPosition { get; set; } // Type==Car != null
        public Vector3 CarRotation { get; set; } // Type==Car != null
        public Vector3 GuardPosition { get; set; }
        public Vector3 GuardRotation { get; set; }
        public List<GameObject> BarrelObjects { get; set; } // Type==Barrel != null
        public ESupplyType Type { get; set; }

    }
}