using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;
using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.NPCInitHelper;

#if MONO
using ScheduleOne.Core.Avatar;
using ScheduleOne.Core.Avatar.Properties;
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
using ScheduleOne.Lighting;
using ScheduleOne.Vehicles.AI;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using ScheduleOne.AvatarFramework.Equipping;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
#else
using Il2CppScheduleOne.Core.Avatar;
using Il2CppScheduleOne.Core.Avatar.Properties;
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
    public class Quest_TrueBrothers : ModQuestBase
    {
        protected readonly TrueBrothersHelper _helper;
#if MONO
        public Quest_TrueBrothers()
        {
            _helper = new TrueBrothersHelper(this);
        }
#else
        public Quest_TrueBrothers(IntPtr ptr) : base(ptr) 
        { 
            _helper = new TrueBrothersHelper(this);
        }
        public Quest_TrueBrothers() : base(ClassInjector.DerivedConstructorPointer<Quest_TrueBrothers>())
            => ClassInjector.DerivedConstructorBody(this);

        public new string title;
        public new string Subtitle;
        public new Il2CppSystem.Collections.Generic.List<QuestEntry> Entries;
#endif
        public readonly List<Vector3> pileBasePos = new()
        {
            new Vector3(-14.6f, -4.77f, 168.2f),
            new Vector3(-14.6f, -4.77f, 168.6f),
            new Vector3(-14.4f, -4.77f, 168.2f),
            new Vector3(-14.4f, -4.77f, 168.6f),
            new Vector3(-14.8f, -4.77f, 168.7f),
            new Vector3(-15.3f, -4.77f, 168.6f),
        };
        public readonly List<Vector3> ambushCopSpawnPositions = new()
        {
            new Vector3(-48.54f, -3.64f, 166.59f),
            new Vector3(-34.67f, -2.54f, 155.85f),
            new Vector3(-42.38f, -2.54f, 152.52f),
        };
        public static readonly Vector3 followDestination = new Vector3(-32.68f, -2.54f, 168.73f);

        public CartelGoon startGoon; // must be spawned before quest setup and assigned
        public static readonly float startGoonMoveSpeed = 0.75f;
        public List<CartelGoon> alliedGoons = new(); // 3 - 4

        public List<PoliceOfficer> ambushCops = new(); // waves of cops

        public GameObject brickBase = null;
        public List<GameObject> spawnedDecor = new();
        public StorageEntity destinationStorage = null;
        public LandVehicle spawnedVehicle = null;

        public InteractableObject palletInteractable = null;
        // Each interactable press retracts from top of piles
        // Track the piles instantiated objects in order
        public List<List<GameObject>> spawnedBrickPiles = new();
        public int totalBricksCount;

        public int bricksInPallet = 0;
        public int bricksInSUV = 0;

        public int currentAmbushWave = 1;
        public int currentDeadCopsCount = 0;
        public bool ambushDefeated = false;
        public bool preCompleteQueued = false;

        public float defaultM1911Dmg;

        public QuestEntry QuestEntry_FollowGoon;
        private UnityAction _followGoonAction;

        public QuestEntry QuestEntry_MoveCocaine;
        private UnityAction _moveCocaineAction;

        public QuestEntry QuestEntry_PoliceAmbush;

        private Action _playerArrestedAction;

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


                if (instance == null) return;
#if MONO
                instance.onMinutePass -= this.OnMinPass;
#else
                Action action = (Action)OnMinPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)action);
#endif
                ModCoroutines.Start(CleanupTrueBrothersQuest());
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
            _helper.InitializeQuest("True Brothers", xp: 1500);

            _followGoonAction = (UnityAction)OnFollowGoonComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_FollowGoon,
                name: "FollowGoon",
                title: "Follow the Cartel Goon to Northern Waterfront",
                new PoIConfig(true, true, true, startGoon.transform, Vector3.zero),
                _followGoonAction);

            _moveCocaineAction = (UnityAction)OnMoveCocaineComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_MoveCocaine,
                name: "MoveCocaine",
                title: "Move cocaine from the pallets to the SUV",
                new PoIConfig(false, false, false),
                _moveCocaineAction);

            _helper.InitializeQuestEntry(ref QuestEntry_PoliceAmbush,
                name: "PoliceAmbush",
                title: "Defeat the police ambush",
                new PoIConfig(false, false, false));

#if IL2CPP
            _helper.SyncEntriesField(ref this.Entries);
#endif

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;

#if MONO
            instance.onMinutePass += this.OnMinPass;
#else
            Action action = (Action)OnMinPass;
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            _helper.StartQuestFromEntry(QuestEntry_FollowGoon);

            // Player must not get arrested during quest
            Player.Local.onArrested += (Action)OnPlayerArrested;

            // Start spawning immediate
            ModCoroutines.Start(_helper.SpawnCarMeetup());
        }
        public override void OnMinPass()
        {
            Log("Minpass");
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

            Log("Pass precheck");

            // When time passes 4am OR when player sleeps while quest is active fail
            if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 359 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 701)
            {
                Log("Fail timeout quest");
                Fail();
            }

            // Update follow poi location pos and check proximity to target
            if (QuestEntry_FollowGoon != null && QuestEntry_FollowGoon.State == EQuestState.Active)
            {
                Log("Follow goon entry");
                if (startGoon == null)
                {
                    Log("Start goon is null return");
                    return;
                }
                // If the goon dies or goes into combat, fail quest
                if (startGoon.Health.IsDead || startGoon.Health.IsKnockedOut || (startGoon.Behaviour.activeBehaviour != null && startGoon.Behaviour.activeBehaviour == startGoon.Behaviour.CombatBehaviour))
                {
                    Fail();
                }

                Log("Set poi");
                QuestEntry_FollowGoon.PoILocation.transform.position = startGoon.transform.position;
                Log($"{Vector3.Distance(Player.Local.CenterPointTransform.position, followDestination)}");
                if (Vector3.Distance(Player.Local.CenterPointTransform.position, followDestination) < 7f)
                {
                    QuestEntry_FollowGoon.Complete();
                    return;
                }

                if (!startGoon.Movement._hasDestination)
                    startGoon.Movement.SetDestination(followDestination);
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
                            goon.Movement.SpeedController.AddSpeedControl("combat", 0.75f, 5);
                            goon.AttackEntity(combatTargetable);
                        }
                    }
                }
            }

            if (QuestEntry_PoliceAmbush != null && QuestEntry_PoliceAmbush.State == EQuestState.Active)
            {
                Log("Ambush minpass");
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

                        if (offc.Behaviour.activeBehaviour != offc.Behaviour.CombatBehaviour)
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

                                // To keep target on goon
                                if (offc.Awareness.enabled)
                                    offc.Awareness.SetAwarenessActive(false);

                                offc.Behaviour.CombatBehaviour.SetTarget(goon.GetComponent<ICombatTargetable>().NetworkObject);
                                break;
                            }
                        }
                        else
                        {
                            // To allow player target
                            if (!offc.Awareness.enabled)
                                offc.Awareness.SetAwarenessActive(true);

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
                Log("Eval pursuit state");
                if (Player.Local.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None && ambushDefeated)
                {
                    Log("Ambush defeat checked");
                    if (!preCompleteQueued)
                    {
                        Log("Queue Pre Complete");
                        preCompleteQueued = true;
                        ModCoroutines.Start(_helper.PreComplete());
                        return;
                    }
                }
                else if (Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.Lethal && !ambushDefeated)
                {
                    // To keep it active while the entry is going on because it can in long combat be removed
                    Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Lethal);
                }

            }
        }
        public void OnFollowGoonComplete()
        {
            if (QuestEntry_FollowGoon != null && QuestEntry_FollowGoon.State == EQuestState.Failed) return;
            if (QuestEntry_MoveCocaine == null) return;

            palletInteractable.SetInteractableState(InteractableObject.EInteractableState.Default);
            QuestEntry_MoveCocaine.Begin();
            if (QuestEntry_MoveCocaine.compassElement != null)
                QuestEntry_MoveCocaine.compassElement.Visible = false;
            else
            {
                QuestEntry_MoveCocaine.CreateCompassElement();
                QuestEntry_MoveCocaine.compassElement.Visible = false;
            }

            if (_followGoonAction != null)
            {
                QuestEntry_FollowGoon.onComplete.RemoveListener(_followGoonAction);
                _followGoonAction = null;
            }
        }
        public void OnMoveCocaineComplete()
        {
            if (QuestEntry_MoveCocaine != null && QuestEntry_MoveCocaine.State == EQuestState.Failed) return;
            if (QuestEntry_PoliceAmbush == null) return;

            QuestEntry_PoliceAmbush.Begin();
            if (QuestEntry_PoliceAmbush.compassElement != null)
                QuestEntry_PoliceAmbush.compassElement.Visible = false;
            else
            {
                QuestEntry_PoliceAmbush.CreateCompassElement();
                QuestEntry_PoliceAmbush.compassElement.Visible = false;
            }

            if (QuestEntry_MoveCocaine != null)
            {
                QuestEntry_MoveCocaine.onComplete.RemoveListener(_moveCocaineAction);
                _moveCocaineAction = null;
            }

            ModCoroutines.Start(_helper.SpawnPoliceAmbush());
        }
        public void OnPlayerArrested()
        {
            if (State != EQuestState.Active) return;

            Fail();

            if (_playerArrestedAction != null)
            {
                Player.Local.onArrested -= _playerArrestedAction;
                _playerArrestedAction = null;
            }
        }

        #region Quest Police avatar randomize
        public static List<Color> skinColors = new()
        {
            new Color(0.729412f, 0.596078f, 0.541176f),
            new Color(0.7768f, 0.5931f, 0.4442f),
            new Color(0.364705f, 0.298039f, 0.270588f),
            new Color(0.454902f, 0.372549f, 0.337255f),
            new Color(0.7768f, 0.5931f, 0.4442f)
        };

        public static List<string> randomFaceLayers = new()
        {
            "smugpout",
            "slightsmile",
            "neutralface",
            "openmouthsmile",
            "slightfrown",
        };

        public static List<string> randomMaleHairLayers = new()
        {
            "balding",
            "buzzcut",
        };

        public static List<string> randomFacialHairLayers = new()
        {
            "beardbushy",
            "beardfull",
            "goatee",
            "swirlymustache",
        };
        public static List<string> randomFemaleHairLayers = new()
        {
            "midfringe",
            "lowbun",
            "doubletopknot"
        };

        public static List<Color> randomHairColors = new()
        {
            new Color(0.141176f, 0.109803f, 0.066666f),
            new Color(0.666666f, 0.533333f, 0.4f),
            new Color(0.501960f, 0.501965f, 0.5019607f),
            new Color(0.294117f, 0.196078f, 0.121568f),
            new Color(0.6071f, 0.3886f, 0.087f),
            new Color(0.0804f, 0.0699f, 0.0624f),
            new Color(0.1278f, 0.1278f, 0.1278f)
        };

        public void SetRandomOfficerAvatar(PoliceOfficer offc)
        {
            Log("Start random");

            NakedAppearance nakedAppearance = offc.Avatar.Appearance.AppliedNakedAppearance.Clone();
            SetRandomLook(ref nakedAppearance);
            Log("Load settings");
            offc.Avatar.Appearance.ApplyNakedAppearance(nakedAppearance);
        }

        /*
         Does cause following error??
        NullReferenceException: Object reference not set to an instance of an object
        at ScheduleOne.Core.Avatar.AttachmentColorApplicator.Start () 0x11
         */
        public static void SetRandomLook(ref NakedAppearance original)
        {
            Log("Set random look");
            List<SerializedAvatarObject> createdObjects = new();

            SerializedColorProperty[] face0Properties = new SerializedColorProperty[1];
            face0Properties[0] = new SerializedColorProperty("MainColor", new Color(0f, 0f, 0f, 1f));
            SerializedAvatarObject face0 = new(randomFaceLayers[UnityEngine.Random.Range(0, randomFaceLayers.Count)], face0Properties);
            createdObjects.Add(face0);

            original.Gender = UnityEngine.Random.Range(0f, 1f);

            if (UnityEngine.Random.Range(0f, 1f) > 0.6f && original.Gender < 0.5f)
            {
                SerializedColorProperty[] face1Properties = new SerializedColorProperty[1];
                face1Properties[0] = new SerializedColorProperty("MainColor", new Color(0f, 0f, 0f, 1f));
                SerializedAvatarObject face1 = new(randomFacialHairLayers[UnityEngine.Random.Range(0, randomFacialHairLayers.Count)], face1Properties);
                createdObjects.Add(face1);
            }

            SerializedColorProperty[] face2Properties = new SerializedColorProperty[1];
            face2Properties[0] = new SerializedColorProperty("MainColor", new Color(0f, 0f, 0f, 0.8223f));
            SerializedAvatarObject face2 = new("eyeshadow", face2Properties);
            createdObjects.Add(face2);

            if (UnityEngine.Random.Range(0f, 1f) > 0.3f)
            {
                string faceLayerID = "";
                Color faceLayerColor;
                if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
                {
                    faceLayerID = "oldpersonwrinkles";
                    faceLayerColor = new(0f, 0f, 0f, 1f);
                }
                else
                {
                    faceLayerID = "freckles";
                    faceLayerColor = new(1f, 1f, 1f, 1f);
                }
                SerializedColorProperty[] face3Properties = new SerializedColorProperty[1];
                face3Properties[0] = new SerializedColorProperty("MainColor", faceLayerColor);
                SerializedAvatarObject face3 = new(faceLayerID, face3Properties);
                createdObjects.Add(face3);
            }

            original.HairColor = randomHairColors[UnityEngine.Random.Range(0, randomHairColors.Count)];

            float scale = UnityEngine.Random.Range(1f, 1.1f);
            float thickness = UnityEngine.Random.Range(1f, 1.4f);
            float restHeight = UnityEngine.Random.Range(-1f, -1.40f);
            EyebrowSettings eyebrowSetting = new(scale, thickness, restHeight, 0f, original.HairColor);

            string hairID = "";
            if (original.Gender < 0.5f)
                hairID = randomMaleHairLayers[UnityEngine.Random.Range(0, randomMaleHairLayers.Count)];
            else
                hairID = randomFemaleHairLayers[UnityEngine.Random.Range(0, randomFemaleHairLayers.Count)];
            SerializedColorProperty[] hairProperties = new SerializedColorProperty[1];
            hairProperties[0] = new SerializedColorProperty("MainColor", original.HairColor);
            SerializedAvatarObject hair = new(hairID, hairProperties);
            createdObjects.Add(hair);

            original.Height = UnityEngine.Random.Range(1.6f, 1.8f);
            original.Weight = UnityEngine.Random.Range(0.3f, 0.7f);

            original.RightEyebrowSettings = eyebrowSetting;
            original.LeftEyebrowSettings = eyebrowSetting;
            original.RightEyelidSettings = EyelidSettings.Default;
            original.LeftEyelidSettings = EyelidSettings.Default;
            original.RightEyeSettings = EyeSettings.Default;
            original.LeftEyeSettings = EyeSettings.Default;

            original.SkinColor = skinColors[UnityEngine.Random.Range(0, skinColors.Count)];
            original.AvatarObjects = createdObjects.ToArray();  

            Log("Ret face settings");
            return;
        }
        #endregion
    }

    // Quest coroutine wrapper
    public class TrueBrothersHelper : QuestHelperBase<Quest_TrueBrothers>
    {
        public TrueBrothersHelper(Quest_TrueBrothers quest) : base(quest) { }

        public IEnumerator SpawnCarMeetup()
        {
            _quest.alliedGoons.Add(_quest.startGoon);

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
                product.Quality = EQuality.Premium;

                if (CartelInventory.brickPackaging != null)
                    product.SetPackaging(CartelInventory.brickPackaging);
                else
                    Log("Brick definition is null");

                if (product.StoredItemPrefab != null && product.StoredItemPrefab is FilledPackaging_StoredItem storedItemInst)
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
                product.Quality = EQuality.Premium;

                if (CartelInventory.brickPackaging != null)
                    product.SetPackaging(CartelInventory.brickPackaging);
                else
                    Log("Brick definition is null");

                if (product.StoredItemPrefab != null)
                {
                    FilledPackaging_StoredItem storedItemInst = product.StoredItemPrefab.TryCast<FilledPackaging_StoredItem>();
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

            _quest.spawnedDecor.Add(_quest.brickBase);

            Log("Spawn veh1");
            // spawn SUV1 reward veh
            NetworkObject boxSuv1 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            _quest.spawnedDecor.Add(boxSuv1.gameObject);
            netManager.ServerManager.Spawn(boxSuv1);
            yield return Wait01;
            if (!registered || _quest.State != EQuestState.Active) yield break;

            boxSuv1.transform.parent = Map.Instance.transform;
            boxSuv1.gameObject.SetActive(true);
            boxSuv1.transform.SetPositionAndRotation(new Vector3(-19.0712f, -4.1883f, 174.4196f), Quaternion.Euler(0.0009f, 0.4645f, 0f));
            _quest.destinationStorage = boxSuv1.GetComponent<StorageEntity>();
            _quest.destinationStorage.AccessSettings = StorageEntity.EAccessSettings.Full;
            yield return Wait05;
            if (!registered || _quest.State != EQuestState.Active) yield break;

            Log("Spawn veh2");
            // spawn SUV2 lights on vehicle
            NetworkObject boxSuv2 = UnityEngine.Object.Instantiate<NetworkObject>(nobSuv);
            _quest.spawnedDecor.Add(boxSuv2.gameObject);
            // For some reason the second vehicle consistently clips through map if not set pos+rot before active
            boxSuv2.transform.SetPositionAndRotation(new Vector3(-20.2015f, -3.9164f, 167.2993f), Quaternion.Euler(2.5122f, 79.6851f, 359.3438f));
            netManager.ServerManager.Spawn(boxSuv2);
            yield return Wait01;
            if (!registered || _quest.State != EQuestState.Active) yield break;

            boxSuv2.transform.parent = Map.Instance.transform;
            boxSuv2.gameObject.SetActive(true);
            yield return Wait05;
            if (!registered || _quest.State != EQuestState.Active) yield break;

            boxSuv2.GetComponent<VehicleLights>().HeadlightsOn = true;

            Log("Spawn pallet");
            // Spawn pallet
            GameObject pallet = UnityEngine.Object.Instantiate<GameObject>(objPallet);
            _quest.spawnedDecor.Add(pallet);
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

            _quest.palletInteractable = pallet.AddComponent<InteractableObject>();

            _quest.palletInteractable.message = "Pick Up";
            _quest.palletInteractable.displayLocationCollider = bc;
            _quest.palletInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            void OnPalletInteracted()
            {
                // Check inventory has space
                if (!PlayerSingleton<PlayerInventory>.Instance.CanItemFitInInventory(cokeInst, 1))
                    return;

                // When interacted get highest count list from spawned piles
                // destroy the last index gameobject of the selected pile
                // And give 1 to inventory
                if (_quest.spawnedBrickPiles.Count > 0)
                {
                    int highestCountIndex = -1;
                    int highestBrickCountInPile = 0;
                    for (int i = 0; i < _quest.spawnedBrickPiles.Count; i++)
                    {
                        if (_quest.spawnedBrickPiles[i].Count > highestBrickCountInPile)
                        {
                            highestBrickCountInPile = _quest.spawnedBrickPiles[i].Count;
                            highestCountIndex = i;
                        }
                    }
                    if (highestCountIndex != -1)
                    {
                        int lastBrickIndex = _quest.spawnedBrickPiles[highestCountIndex].Count - 1;
                        GameObject go = _quest.spawnedBrickPiles[highestCountIndex][lastBrickIndex];
                        _quest.spawnedBrickPiles[highestCountIndex].RemoveAt(lastBrickIndex);
                        UnityEngine.Object.Destroy(go);

                        // If empty pile remove
                        if (_quest.spawnedBrickPiles[highestCountIndex].Count == 0)
                            _quest.spawnedBrickPiles.RemoveAt(highestCountIndex);

                        // Append into inventory +1
                        PlayerSingleton<PlayerInventory>.Instance.AddItemToInventory(cokeInst);
                        _quest.bricksInPallet--;
                    }
                }

                // If Spawned brick piles is count 0 nothing is left and previous interact was last one
                // remove interactable
                if (_quest.spawnedBrickPiles.Count == 0)
                {
                    UnityEngine.Object.Destroy(_quest.palletInteractable);
                    _quest.palletInteractable = null;
                }
            }
            _quest.palletInteractable.onInteractStart.AddListener((UnityEngine.Events.UnityAction)OnPalletInteracted);

            // spawn bricks into tr pallet
            for (int i = 0; i < _quest.pileBasePos.Count; i++)
            {
                List<GameObject> newPile = new();
                Vector3 basePos = _quest.pileBasePos[i];
                for (int j = 0; j < UnityEngine.Random.Range(6, 11); j++)
                {
                    yield return Wait01;
                    if (!registered || _quest.State != EQuestState.Active) yield break;

                    GameObject newBrick = UnityEngine.Object.Instantiate(_quest.brickBase, pallet.transform);
                    newBrick.transform.position = new Vector3(basePos.x, basePos.y + 0.06f * j, basePos.z);
                    newBrick.transform.rotation = Quaternion.Euler(0f, Mathf.Round(UnityEngine.Random.Range(85f, 95f)), 0f);
                    newBrick.gameObject.SetActive(true);
                    _quest.spawnedDecor.Add(newBrick);
                    newPile.Add(newBrick);
                    _quest.totalBricksCount++;
                }

                _quest.spawnedBrickPiles.Add(newPile);
            }
            _quest.bricksInPallet = _quest.totalBricksCount;

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
                        goon.Despawn();
                    }
                }
            }

            // Near the entrance to the alley
            CartelGoon goonGuard = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-32.68f, -2.54f, 168.73f));
            goonGuard.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            _quest.alliedGoons.Add(goonGuard);

            CartelGoon goonGuard2 = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(new Vector3(-31f, -2.54f, 168.73f));
            goonGuard2.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goonGuard2.Behaviour.ScheduleManager.DisableSchedule();
            goonGuard2.transform.rotation = Quaternion.Euler(0f, 226f, 0f);
            _quest.alliedGoons.Add(goonGuard2);

            Log("Setup weapons");
            foreach (CartelGoon goon in _quest.alliedGoons)
            {
                goon.HasUmbrella = false;
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
                        _quest.defaultM1911Dmg = wep.Damage;
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
                if (_quest.QuestEntry_MoveCocaine == null) return;
                if (_quest.QuestEntry_MoveCocaine.State != EQuestState.Active) return;

                int validCocaineBricksCount = 0;

                // Check foreach slot in storage that its a cocaine brick that is premium
                foreach (ItemSlot slot in _quest.destinationStorage.ItemSlots)
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

                _quest.bricksInSUV = validCocaineBricksCount;
                Log($"Found {_quest.bricksInSUV}/{_quest.totalBricksCount} Bricks in SUV");
                if (validCocaineBricksCount >= _quest.totalBricksCount)
                {
                    // All cocaine has been moved, complete entry move next
                    _quest.destinationStorage.AccessSettings = StorageEntity.EAccessSettings.Closed;
                    _quest.QuestEntry_MoveCocaine.Complete();
                }
            }

#if MONO
            onClosedAction = (System.Action)CloseTrigger;
#else
            onClosedAction = (Il2CppSystem.Action)CloseTrigger;
#endif

            _quest.destinationStorage.onClosed += onClosedAction;
            _quest.destinationStorage.StorageEntitySubtitle = "Cocaine Transport";

            Log("Spawning done");
            yield break;
        }

        public IEnumerator InstantiateAmbushNPCs()
        {
            yield return Wait01;
            for (int i = 0; i < 3; i++)
            {
                Log($"Spawn {i}");
                NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);

                GameObject copNet = UnityEngine.Object.Instantiate<GameObject>(copBaseClone);
                NetworkObject newNob = copNet.GetComponent<NetworkObject>();
                PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();
                offc.AutoDeactivate = false; // Prevent from returning to station and from being added to officer pool

                copNet.name = $"RuntimeOfficer_{i}";
                Log("Init clone");
                yield return MelonCoroutines.Start(InitiateClone(newNob, netManager));

                NPC myNpc = copNet.gameObject.GetComponent<NPC>();
                myNpc.NPCData.BasicInfo.ID = $"RuntimeOfficer_{i}";
                myNpc.NPCData.BasicInfo.FirstName = "Officer";
                myNpc.NPCData.BasicInfo.LastName = "";
                myNpc.NPCData.Inventory.CanBePickpocketed = false;

                if (!NPCManager.NPCRegistry.Contains(myNpc))
                    NPCManager.NPCRegistry.Add(myNpc);
                else
                    Log("NPC already registered in NPCRegistry");

                netManager.ServerManager.Spawn(copNet);
                copNet.gameObject.SetActive(true);

                DialogueController controller = offc.DialogueHandler.GetComponent<DialogueController>();
                controller.Choices.Clear();

                myNpc.Avatar.SetVisible(true);
                myNpc.Avatar.gameObject.SetActive(true);
                copNet.GetComponent<NavMeshAgent>().enabled = true;

                Log("Reset avatar");
                _quest.SetRandomOfficerAvatar(offc);

                string beltId = string.Empty;
                if (offc.PoliceBelt != null)
                {
                    beltId = offc.PoliceBelt._id;
                    Log($"Baton assinged in belt: {offc.PoliceBelt.BatonObject != null}");
                    Log($"taser assinged in belt: {offc.PoliceBelt.TaserObject != null}");
                    Log($"gun assinged in belt: {offc.PoliceBelt.GunObject != null}");
                }
                else
                {
                    Log("Police belt is unassigned pre spawn!");
                }
                // Aftter cloning this way the avatar retains prev look in the objects container but doesnt track them in the field
                // so these need to be removed after clone manually
                // but avoid removing belt
                /*
                Does cause following error??
                NullReferenceException: Object reference not set to an instance of an object
                at ScheduleOne.Core.Avatar.AttachmentColorApplicator.Start ()
                */


                Log($"Removing unassigned avatar objects count: {offc.Avatar.Appearance._avatarObjectsContainer.transform.childCount}");
                Transform target = offc.Avatar.Appearance._avatarObjectsContainer.transform;
                List<AvatarObject> destroyable = new();
                for (int j = offc.Avatar.Appearance._avatarObjectsContainer.transform.childCount - 1; j >= 0; j--)
                {
                    Transform avatarObjectTr = target.GetChild(j);
                    if (avatarObjectTr)
                    {
                        AvatarObject comp = avatarObjectTr.gameObject.GetComponent<AvatarObject>();

                        if (beltId != string.Empty && comp._id == beltId)
                            continue; // to avoid null refs, probably causes a duped belt though

                        if (comp != null && !offc.Avatar.Appearance._appliedObjects.Contains(comp))
                            destroyable.Add(comp);
                    }
                }
                foreach (AvatarObject avObj in destroyable)
                {
                    avObj.Destroy();
                }

                Transform hipsTr = offc.Avatar._ragdollRoot;
                if (hipsTr.gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    Log("Removing ragdoll rigidbody component pre spawn");
                    // constant force class has it required so remove that first?
                    ConstantForce force = hipsTr.gameObject.GetComponent<ConstantForce>();
                    if (force != null)
                        UnityEngine.Object.Destroy(force);
                    UnityEngine.Object.Destroy(rb);
                }

                offc.HasUmbrella = false;
                offc.PursuitBehaviour.arrestingEnabled = false;
                offc.Behaviour.ScheduleManager.DisableSchedule();

                offc.Movement.Warp(_quest.ambushCopSpawnPositions[i]);
                offc.Awareness.VisionCone.WorldspaceIconsEnabled = false;
                offc.SetIgnorePlayers(true);
                _quest.ambushCops.Add(offc);

                yield return Wait01;
                if (offc.Avatar._impostor != null && offc.Avatar._impostor.isActiveAndEnabled)
                    offc.Avatar._impostor.Disable();

                if (!offc.Avatar.Animation.animationEnabled)
                    offc.Avatar.Animation.SetAnimationEnabled(true); // or play standup anim?

                yield return Wait01;
                offc.Avatar.Animation.PlayStandUpAnimation();

                if (offc.PoliceBelt != null)
                {
                    Log($"Baton assinged in belt: {offc.PoliceBelt.BatonObject != null}");
                    Log($"taser assinged in belt: {offc.PoliceBelt.TaserObject != null}");
                    Log($"gun assinged in belt: {offc.PoliceBelt.GunObject != null}");
                }
                else
                {
                    Log("Officer Belt is NULL after spawn!");
                }

                Log("Done spawning");
            }

            yield break;
        }

        public IEnumerator SpawnPoliceAmbush()
        {
            Log("Spawn 3 cops");
            yield return MelonCoroutines.Start(InstantiateAmbushNPCs());
            Log($"Move + combat spawned now listed: {_quest.ambushCops.Count} ");

            // 2 police attack 2 cartel
            _quest.ambushCops[0].Behaviour.CombatBehaviour.SetTarget(_quest.alliedGoons[0].GetComponent<ICombatTargetable>().NetworkObject);
            _quest.alliedGoons[0].Behaviour.CombatBehaviour.SetTarget(_quest.ambushCops[0].GetComponent<ICombatTargetable>().NetworkObject);

            _quest.ambushCops[1].Behaviour.CombatBehaviour.SetTarget(_quest.alliedGoons[1].GetComponent<ICombatTargetable>().NetworkObject);
            _quest.alliedGoons[1].Behaviour.CombatBehaviour.SetTarget(_quest.ambushCops[1].GetComponent<ICombatTargetable>().NetworkObject);

            // 1 copp shoots at player and allied goon shoots at that same cop
            _quest.ambushCops[2].Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
            _quest.ambushCops[2].SetIgnorePlayers(false);

            _quest.alliedGoons[2].Behaviour.CombatBehaviour.SetTarget(_quest.ambushCops[2].GetComponent<ICombatTargetable>().NetworkObject);

            // Health callbacks to trigger new wave after all dead
            foreach (PoliceOfficer offc in _quest.ambushCops)
            {
                UnityEngine.Events.UnityAction OnCopDiedAction = null;
                void OnCopDied()
                {
                    Log("Cop died");
                    _quest.currentDeadCopsCount++;
                    if (OnCopDiedAction != null)
                    {
                        offc.Health.onDieOrKnockedOut.RemoveListener(OnCopDiedAction);
                        OnCopDiedAction = null;
                    }
                    if (_quest.currentDeadCopsCount == _quest.ambushCops.Count)
                    {
                        // if the wave hasnt been changed
                        if (_quest.currentAmbushWave == 1)
                        {
                            _quest.currentAmbushWave++;
                            ModCoroutines.Start(SpawnSecondWave());
                        }
                    }
                }
                OnCopDiedAction = (UnityEngine.Events.UnityAction)OnCopDied;
                offc.Health.onDieOrKnockedOut.AddListener(OnCopDiedAction);

                offc.Movement.SetDestination(Quest_TrueBrothers.followDestination);

                offc.Behaviour.CombatBehaviour.Enable_Networked();
            }
            yield return Wait1; // Walk out of spawnpoint towards player and goons
            if (!registered) yield break;

            foreach (PoliceOfficer offc in _quest.ambushCops)
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

            foreach (CartelGoon goon in _quest.alliedGoons)
            {
                Log("Reset goon weps");
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
                        wep.Damage = _quest.defaultM1911Dmg;
                    }

                }
                // start combat
                goon.Behaviour.CombatBehaviour.Enable_Networked();
            }

            Log("Set Pursuit");
            yield return Wait1;
            if (!registered) yield break;
            Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Lethal);

            yield break;
        }

        public IEnumerator SpawnSecondWave()
        {
            Log("Next wave spawning");
            yield return Wait2;
            if (!registered) yield break;

            // Police car + the same 3 dead cops -> despwan+revive -> after car arrives -> spawn at the doors
            LandVehicle prefab = PoliceStation.PoliceStations[0].PoliceVehicles[1];

            Vector3 carSpawn = new(-17f, 0.975f, 94.75f);
            Quaternion carRotation = Quaternion.Euler(Vector3.zero);
            LandVehicle policeVehicle = NetworkSingleton<VehicleManager>.Instance.SpawnAndReturnVehicle(prefab.vehicleCode, Vector3.zero, Quaternion.identity, false);
            policeVehicle.gameObject.SetActive(true);

            _quest.spawnedVehicle = policeVehicle; // to despawn later

            policeVehicle.SetTransform_Server(carSpawn, carRotation);

            policeVehicle.GetComponent<VehicleTeleporter>().MoveToRoadNetwork(false);

            _quest.currentDeadCopsCount = 0;
            foreach (PoliceOfficer offc in _quest.ambushCops)
            {
                offc.EnterVehicle(null, policeVehicle);
                //offc.Movement.PauseMovement();
                offc.Health.Revive();
                offc.Behaviour.CombatBehaviour.Disable_Networked(null);

                offc.Health.onDieOrKnockedOut.RemoveAllListeners();

                UnityEngine.Events.UnityAction OnCopDiedAction = null;
                void OnCopDied()
                {
                    Log("Cop died 2nd wave");
                    _quest.currentDeadCopsCount++;
                    if (OnCopDiedAction != null)
                    {
                        offc.Health.onDieOrKnockedOut.RemoveListener(OnCopDiedAction);
                        OnCopDiedAction = null;
                    }
                    if (_quest.currentDeadCopsCount == _quest.ambushCops.Count)
                    {
                        // if the wave hasnt been changed
                        if (_quest.currentAmbushWave == 2)
                        {
                            Log("Ambush completed");
                            _quest.currentAmbushWave++;
                            _quest.ambushDefeated = true;
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

            policeVehicle.Agent.Flags.OverriddenSpeed = 40f;
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
            // OR Wait until nearby player
            int maxNavigationTime = 30;
            int currentNavigationTime = 0;
            float distFromPlayer = 100f;
            for (; ; )
            {
                yield return Wait1;
                if (!registered) yield break;

                currentNavigationTime++;
                distFromPlayer = Vector3.Distance(Player.Local.transform.position, policeVehicle.transform.position);
                if (currentNavigationTime >= maxNavigationTime || distFromPlayer < 8f)
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


            foreach (PoliceOfficer offc in _quest.ambushCops)
            {
                yield return Wait1;
                if (!registered) yield break;

                offc.ExitVehicle();
                // All cops can attack the player
                offc.SetIgnorePlayers(false);
                offc.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
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

            // Dont remove this as it seems they can sit in there sometimes very rarely even after calling it once?
            foreach (PoliceOfficer offc in _quest.ambushCops)
            {
                yield return Wait01;
                if (!registered) yield break;
                // If somehow with crazy magic they are not out of the vehicle
                if (offc.IsInVehicle)
                {
                    offc.ExitVehicle();
                }
            }

            // Goons attack cops
            for (int i = 0; i < _quest.alliedGoons.Count; i++)
            {
                if (!_quest.alliedGoons[i].Health.IsDead)
                {
                    // because they seem to drop weapons occasionally
                    _quest.alliedGoons[i].Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");

                    _quest.alliedGoons[i].Behaviour.CombatBehaviour.SetTarget(_quest.ambushCops[i].GetComponent<ICombatTargetable>().NetworkObject);

                    // because sometimes they just stand afk at first for a long time
                    if (_quest.alliedGoons[i].Behaviour.activeBehaviour != _quest.alliedGoons[i].Behaviour.CombatBehaviour)
                        _quest.alliedGoons[i].Behaviour.CombatBehaviour.Enable_Networked();
                }
            }

            Log("End of 2nd wave");
            yield break;
        }

        public IEnumerator PreComplete()
        {
            Log("Precoomplete start");
            // 2 guard goons continue stayinside beh to avoid npc trapping and cancelling movement
            foreach (CartelGoon goon in _quest.alliedGoons)
            {
                yield return Wait1;
                if (!registered) yield break;

                if (goon == _quest.startGoon) continue;

                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                goon.Behaviour.ScheduleManager.EnableSchedule();
            }

            bool playerConversated = false;
            // If the main goon is not dead after ambush, thank the player and complete mission on continue button
            if (!_quest.startGoon.Health.IsDead && !_quest.startGoon.Health.IsKnockedOut && _quest.startGoon.IsConscious)
            {
                _quest.startGoon.Movement.SpeedController.AddSpeedControl("combat", Quest_TrueBrothers.startGoonMoveSpeed, 5);

                // If the start goon is alive then it has now enabled schedule prevent that to traverse to player instead and after reset
                _quest.startGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
                _quest.startGoon.Behaviour.ScheduleManager.DisableSchedule();

                DialogueController controller = _quest.startGoon.DialogueHandler.gameObject.GetComponent<DialogueController>();

                DialogueController.GreetingOverride greeting = new();
                greeting.ShouldShow = true;
                string gender = Player.Local.Avatar.Appearance.Gender == EGender.Male ? "brother" : "sister";
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

                    if (_quest.State == EQuestState.Active)
                        _quest.Complete();

                    _quest.startGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                    _quest.startGoon.Behaviour.ScheduleManager.EnableSchedule();

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

                    if (_quest.startGoon == null || _quest.startGoon.Health.IsDead ||
                        _quest.startGoon.Health.IsKnockedOut || !_quest.startGoon.IsConscious ||
                        _quest.startGoon.Behaviour.activeBehaviour == _quest.startGoon.Behaviour.CombatBehaviour)
                        break;

                    if (!playerConversated && playerConversatable)
                    {
                        _quest.startGoon.Movement.EndSetDestination(NPCMovement.WalkResult.Success);
                        _quest.startGoon.Movement.FacePoint(Player.Local.CenterPointTransform.position);
                        _quest.startGoon.Movement.Stop();
                        controller.StartGenericDialogue(false);
                        playerConversated = true;
                        Log("Start Conversate");
                        break;
                    }

                    float distFromPlayer = Vector3.Distance(Player.Local.CenterPointTransform.position, _quest.startGoon.CenterPoint);
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
                        if (!_quest.startGoon.Movement._hasDestination)
                        {
                            _quest.startGoon.Movement.SetDestination(Player.Local.CenterPointTransform);
                        }
                    }
                }

                // out of break reset schedule so it goes back inside on its own
                _quest.startGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                _quest.startGoon.Behaviour.ScheduleManager.EnableSchedule();
            }

            // Edge cases from traverse fail etc, still auto complete mission
            if (_quest.State == EQuestState.Active && !playerConversated)
                _quest.Complete();

            yield break;
        }

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
