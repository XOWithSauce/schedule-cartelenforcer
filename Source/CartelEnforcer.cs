using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Reflection;

using static CartelEnforcer.AmbushOverrides;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.ConfigLoader;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.FrequencyOverrides;
using static CartelEnforcer.InfluenceOverrides;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.MiniQuest;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.DealerActivity;
using static CartelEnforcer.CartelGathering;
using static CartelEnforcer.SabotageEvent;
using static CartelEnforcer.StealBackCustomer;
using static CartelEnforcer.AlliedExtension;
using static CartelEnforcer.AlliedCartelDialogue;
using static CartelEnforcer.CartelInfluenceChangePopup_Show_Patch;
using static CartelEnforcer.SuppliesModule;
using static CartelEnforcer.RandomManorGenerator;

#if MONO
using ScheduleOne.Property;
using ScheduleOne.Cartel;
using ScheduleOne.NPCs;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.UI;
using ScheduleOne.Dialogue;
using FishNet.Managing.Object;
using FishNet.Managing;
using FishNet.Object;
#else
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Dialogue;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Object;
#endif


[assembly: MelonInfo(typeof(CartelEnforcer.CartelEnforcer), CartelEnforcer.BuildInfo.Name, CartelEnforcer.BuildInfo.Version, CartelEnforcer.BuildInfo.Author, CartelEnforcer.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

#if MONO
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonLoader.VerifyLoaderVersion("0.7.0", true)]
#else // Note this block cant exclude 0.7.1 IL2CPP and allow again 0.7.2 nightlys?
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: MelonLoader.VerifyLoaderVersion("0.7.0", true)]
#endif

namespace CartelEnforcer
{
    public static class BuildInfo
    {
        public const string Name = "Cartel Enforcer";
        public const string Description = "Cartel - Modded and configurable";
        public const string Author = "XOWithSauce";
        public const string Company = null;
        public const string Version = "1.8.4";
        public const string DownloadLink = null;
    }

    public class CartelEnforcer : MelonMod
    {
        public static ModPrefsHandler Prefs { get; private set; } 
        public static CartelEnforcer Instance { get; private set; }
        public static ModConfig currentConfig;
        public static InfluenceConfig influenceConfig;
        public static CurrentEventCooldowns eventCooldowns;
        public static List<object> coros = new();
        public static bool registered = false;
        private bool firstTimeLoad = false;
        public static bool isSaving = false;

        #region await
        public static WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
        public static WaitForSeconds Wait025 = new WaitForSeconds(0.25f);
        public static WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
        public static WaitForSeconds Wait1 = new WaitForSeconds(1f);
        public static WaitForSeconds Wait2 = new WaitForSeconds(2f);
        public static WaitForSeconds Wait5 = new WaitForSeconds(5f);
        public static WaitForSeconds Wait10 = new WaitForSeconds(10f);
        public static WaitForSeconds Wait30 = new WaitForSeconds(30f);
        public static WaitForSeconds Wait60 = new WaitForSeconds(60f);
        #endregion

        #region Melon Prefs
        // On init sync .json config based on melon preferences if they differ from default
        public static void SyncConfig()
        {
            bool hasChanged = false;
            FieldInfo[] modConfigFields = currentConfig.GetType().GetFields();
            foreach (FieldInfo field in modConfigFields)
            {
                if (field.Name.Contains("endGameQuestMonologueSpeed")) continue;

                var entry = Prefs.modConfigCategory.GetEntry(field.Name);
                if (entry == null) continue;

                if ((bool)field.GetValue(currentConfig) == (bool)entry.BoxedValue)
                {
                    //MelonLogger.Msg("No changed value for :" + field.Name);
                    continue; // not changed
                }
                else
                {
                    hasChanged = true;
                    //MelonLogger.Msg("Update config value for :" + field.Name);
                    field.SetValue(currentConfig, entry.BoxedValue);
                }
            }

            // Because allied extension depends on the end game quest -> enable on incorrect state
            // regardless if its changed
            if (currentConfig.alliedExtensions && !currentConfig.endGameQuest)
            {
                hasChanged = true;
                currentConfig.endGameQuest = true;
            }
            if (hasChanged)
            {
                ConfigLoader.Save(currentConfig);
            }
        }
        #endregion

        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();

            Instance = this;
            currentConfig = ConfigLoader.Load();
            influenceConfig = ConfigLoader.LoadInfluenceConfig();

            Prefs = new ModPrefsHandler();
            Prefs.SetupMelonPreferences();
            SyncConfig();

            MelonLogger.Msg("Cartel Enforcer Mod Loaded");
            return;
        }

        #region Unity Methods
        public override void OnUpdate()
        {
            if (!registered || currentConfig == null)
                return;
            if (currentConfig.debugMode)
            {
                if (_playerTransform != null && _positionText != null)
                {
                    Vector3 playerPos = _playerTransform.position;
                    string formattedPosition = $"X: {playerPos.x:F2}\nY: {playerPos.y:F2}\nZ: {playerPos.z:F2}";
                    _positionText.text = formattedPosition;
                }

                if (Input.GetKey(KeyCode.LeftControl))
                {
                    // SEE Debug #region in code for InputFunctions

                    // Left CTRL + R to Start Rob Dealer Function to nearest
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputStartRob());
                        }
                    }
                    // Left CTRL + G to Start Drive By Instant 
                    else if (Input.GetKeyDown(KeyCode.G))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputStartDriveBy());
                        }
                    }
                    // Left CTRL + H to Give Mini Quest Instantly to one of the NPCs 
                    else if (Input.GetKeyDown(KeyCode.H))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputGiveMiniQuest());
                        }
                    }
                    // Left CTRL + L to Log Big Blop of info
                    else if (Input.GetKeyDown(KeyCode.L))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputInternalLog());
                        }
                    }

                    // Left CTRL + T Intercept random deal
                    else if (Input.GetKeyDown(KeyCode.T))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputInterceptContract());
                        }
                    }

                    // Left CTRL + Y Gen End quest
                    else if (Input.GetKeyDown(KeyCode.Y))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputGenerateEndQuest());
                        }
                    }

                    // Left CTRL + U Gen Manor quest
                    else if (Input.GetKeyDown(KeyCode.U))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputGenerateManorQuest());
                        }
                    }

                    // Left CTRL + P Gathering Spawn
                    else if (Input.GetKeyDown(KeyCode.P))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            hoursUntilNextGathering = 1;
                            MelonCoroutines.Start(TryStartGathering());
                            debounce = false;
                        }
                    }

                    // Left CTRL + N Start sabotage event
                    else if (Input.GetKeyDown(KeyCode.N))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            coros.Add(MelonCoroutines.Start(OnInputStartSabotage()));
                        }
                    }

                    // Left CTRL + O Steal back customer
                    else if (Input.GetKeyDown(KeyCode.O))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            coros.Add(MelonCoroutines.Start(OnInputStealNearestCustomer()));
                        }
                    }

                    // Left CTRL + I Start the Allied Intro Quest
                    else if (Input.GetKeyDown(KeyCode.I))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            coros.Add(MelonCoroutines.Start(OnInputGenerateAlliedIntroQuest()));
                        }
                    }

                    // Left CTRL + K Start the Allied Supplies Quest
                    else if (Input.GetKeyDown(KeyCode.K))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            coros.Add(MelonCoroutines.Start(OnInputGenerateAlliedSupplyQuest()));
                        }
                    }

                }
            }

            return;
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 1)
            {
                if (LoadManager.Instance != null && !registered && !firstTimeLoad)
                {
                    firstTimeLoad = true;
#if MONO
                    LoadManager.Instance.onLoadComplete.AddListener(OnLoadCompleteCb);
#else
                    LoadManager.Instance.onLoadComplete.AddListener((UnityEngine.Events.UnityAction)OnLoadCompleteCb);
#endif
                }
            }
            if (buildIndex != 1)
            {
                if (registered)
                {
                    ExitPreTask();
                }
            }

            return;
        }
        #endregion

        #region Mod Initialization and Coroutine load order

        private void OnLoadCompleteCb()
        {
            if (registered) return;
            registered = true;
            coros.Add(MelonCoroutines.Start(Setup()));
            return;
        }

        public static IEnumerator Setup()
        {
#if MONO
            yield return new WaitUntil(() => LoadManager.Instance.IsGameLoaded);
#else
            yield return new WaitUntil((Il2CppSystem.Func<bool>)(() => LoadManager.Instance.IsGameLoaded));
#endif
            currentConfig = ConfigLoader.Load();
            influenceConfig = ConfigLoader.LoadInfluenceConfig();
            cartelStolenItems = ConfigLoader.LoadStolenItems();
            frequencyConfig = ConfigLoader.LoadEventFrequencyConfig();
            eventCooldowns = ConfigLoader.LoadPersistentCooldowns();
            dealerConfig = ConfigLoader.LoadDealerConfig();

#if MONO
            NetworkSingleton<TimeManager>.Instance.onDayPass += OnDayPassChangePassive;
#else
            NetworkSingleton<TimeManager>.Instance.onDayPass += (Il2CppSystem.Action)OnDayPassChangePassive;
#endif
            PreparePackagingRefs();
            PopulateBombLocations();
            PrepareBombFXObjects();

            coros.Add(MelonCoroutines.Start(FetchUIElementsInit()));

            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateDriveBy()));

            coros.Add(MelonCoroutines.Start(InitializeAmbush()));

            InitFrequencyOverrides();

#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += OnHourPassReduceCartelRegActHours;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)OnHourPassReduceCartelRegActHours;
#endif

            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateMiniQuest()));

            coros.Add(MelonCoroutines.Start(EvaluateCartelIntercepts()));
#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += HourPassInterceptCooldown;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)HourPassInterceptCooldown;
#endif

            coros.Add(MelonCoroutines.Start(InitializeEndGameQuest()));

            coros.Add(MelonCoroutines.Start(EvaluateDealerState()));

            coros.Add(MelonCoroutines.Start(SetupAlliedExtension()));

#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += OnHourPassTryGather;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)OnHourPassTryGather;
#endif
            coros.Add(MelonCoroutines.Start(InitializeAndEvaluateSabotage()));

#if MONO
            NetworkSingleton<TimeManager>.Instance.onSleepEnd += OnDayPassTrySteal;
#else
            NetworkSingleton<TimeManager>.Instance.onSleepEnd += (Il2CppSystem.Action)OnDayPassTrySteal;
#endif

            if (currentConfig.debugMode)
            {
                coros.Add(MelonCoroutines.Start(GodMode()));
                MelonCoroutines.Start(MakeUI());
            }

            coros.Add(MelonCoroutines.Start(ExtendGoonPool()));

            yield break;
        }

        public static void ReduceDriveByHours()
        {
            if (isSaving) return;
            if (!currentConfig.driveByEnabled) return;
#if MONO
            bool isHostile = NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile;
#else
            bool isHostile = NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile;
#endif
            if (!isHostile) return;

            hoursUntilDriveBy = Mathf.Clamp(hoursUntilDriveBy - 1, 0, int.MaxValue);
        }

        public static IEnumerator InitializeAndEvaluateDriveBy()
        {
            yield return MelonCoroutines.Start(InitializeDriveByData());
#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += ReduceDriveByHours;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)ReduceDriveByHours;
#endif

            coros.Add(MelonCoroutines.Start(EvaluateDriveBy()));
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnDriveByAreaVisual());
            yield break;
        }

        public static IEnumerator InitializeAmbush()
        {
            yield return MelonCoroutines.Start(ApplyGameDefaultAmbush());
            yield return MelonCoroutines.Start(AddUserModdedAmbush());
            yield return MelonCoroutines.Start(SetAmbushGeneralSettings());
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnAmbushAreaVisual());
            yield break;
        }

        public static IEnumerator InitializeAndEvaluateSabotage()
        {
            yield return Wait2;
            if (!registered) yield break;

            Log("Starting Sabotage Event evaluation");
#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += SabotageEvent.ReduceSabotageHours;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)SabotageEvent.ReduceSabotageHours;
#endif
            coros.Add(MelonCoroutines.Start(EvaluateBombEvent()));

            yield break;
        }

        public static IEnumerator InitializeAndEvaluateMiniQuest()
        {
            yield return Wait10;
            if (!registered) yield break;

            yield return InitMiniQuest();
#if MONO
            NetworkSingleton<TimeManager>.Instance.onDayPass += OnDayPassNewDiag;
#else
            NetworkSingleton<TimeManager>.Instance.onDayPass += (Il2CppSystem.Action)OnDayPassNewDiag;
#endif
            coros.Add(MelonCoroutines.Start(EvaluateMiniQuestCreation()));

            yield break;
        }

        public static IEnumerator InitializeEndGameQuest()
        {
            yield return Wait10;
            if (!registered) yield break;

            coros.Add(MelonCoroutines.Start(InitManorItemRef()));

            Log("Evaluating End Game Quest Creation");
            bool hasGeneratedQuest = false;
            bool hasGeneratedManorQuest = false;
            bool hasGeneratedCarQuest = false;
            DialogueController frankController;
            while (registered)
            {
                yield return Wait30;
                if (!registered) yield break;
                if (!currentConfig.endGameQuest) continue;

                if (PreRequirementsMet() && !completed && !hasGeneratedQuest && activeQuest == null)
                {
                    hasGeneratedQuest = true;
                    coros.Add(MelonCoroutines.Start(GenDialogOption()));
                }
                if (PreRequirementsMet() && !manorCompleted && !hasGeneratedManorQuest && activeManorQuest == null)
                {
                    hasGeneratedManorQuest = true;
                    coros.Add(MelonCoroutines.Start(GenManorDialogOption()));
                }

                bool inTimeWindowForCarQuest = (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 1559 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 1801);
                if (CarQuestPreRequirementsMet() && !carMeetupCompleted && !hasGeneratedCarQuest && frankDiagIndex == -1 && inTimeWindowForCarQuest && activeCarMeetupQuest == null)
                {
                    // Gen quest opt in time window
                    Log("Car Quest opt generated");
                    hasGeneratedCarQuest = true;
                    coros.Add(MelonCoroutines.Start(GenFrankOption()));
                }
                else if (hasGeneratedCarQuest && !carMeetupCompleted && frankDiagIndex != -1 && !inTimeWindowForCarQuest && crankyFrank != null && activeCarMeetupQuest == null)
                {
                    Log("Car Quest opt removed");
                    hasGeneratedCarQuest = false;
                    frankController = crankyFrank.DialogueHandler.gameObject.GetComponent<DialogueController>();
                    // Del quest opt out of time window when it exists and quest not generated
                    coros.Add(MelonCoroutines.Start(DisposeFrankChoice(frankController)));
                }
            }

            yield break;
        }

        public static IEnumerator ExtendGoonPool()
        {
            GoonPool goonPool = NetworkSingleton<Cartel>.Instance.GoonPool;
            CartelGoon[] originalGoons = goonPool.goons;

            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;
            NetworkObject nob = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "CartelGoon")
                {
                    nob = prefab;
                    break;
                }
            }

            Log("Swapping array count: " + NetworkSingleton<Cartel>.Instance.GoonPool.goons.Length);

            int originalCount = originalGoons.Length;

            int extra = 5;
            int newCount = originalCount + extra;

            CartelGoon[] newGoons = new CartelGoon[newCount];

            System.Array.Copy(originalGoons, newGoons, originalCount);

            CartelGoon goonPrefab = originalGoons.FirstOrDefault();

            if (goonPrefab != null)
            {
                for (int i = originalCount; i < newCount; i++)
                {
                    NetworkObject nobNew = UnityEngine.Object.Instantiate<NetworkObject>(nob);
                    CartelGoon newGoon = nobNew.GetComponent<CartelGoon>();
                    newGoon.name = newGoon.name + i;
                    newGoon.transform.parent = NPCManager.Instance.NPCContainer;
                    NPCManager.NPCRegistry.Add(newGoon);
                    yield return Wait05;
                    if (!registered) yield break;

                    netManager.ServerManager.Spawn(nobNew);
                    yield return Wait05;
                    if (!registered) yield break;

                    newGoon.gameObject.SetActive(true);
                    yield return Wait01;
                    if (!registered) yield break;

                    newGoon.Movement.enabled = true;
                    newGoon.gameObject.SetActive(true);
                    newGoons[i] = newGoon;
                    goonPool.unspawnedGoons.Add(newGoon);
                }
                goonPool.goons = newGoons;
            }

            foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
            {
                if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                    goon.Health.Revive();
                yield return Wait01;
                if (!registered) yield break;

                if (goon.Behaviour.ScheduleManager.ActionList.Count > 0)
                {
                    goon.Behaviour.ScheduleManager.ActionList[0].Resume();
                }
                goon.IsGoonSpawned = true;
                yield return Wait05;
                if (!registered) yield break;
                goon.Despawn_Client(null);
            }
            Log("Array swapped now count: " + NetworkSingleton<Cartel>.Instance.GoonPool.goons.Length);
            yield break;
        }

#endregion

        #region Harmony Patches for Saving and Coroutine safety
        static void ExitPreTask()
        {
            //MelonLogger.Msg("Pre-Exit Task");
            registered = false;
            foreach (object coro in coros)
            {
                if (coro != null)
                    MelonCoroutines.Stop(coro);
            }

            coros.Clear();
            // Now mostly just the different mod related lists that got populated in init, reset and clear to repopulate everything on new load
            driveByLocations.Clear();
            regActivityHours.Clear();
            targetNPCs.Clear();
            cartelStolenItems.Clear();
            emptyDrops.Clear();
            targetNPCsList.Clear();
            targetNPCs.Clear();
            manorGoons.Clear();
            manorGoonGuids.Clear();
            spawnedGatherGoons.Clear();
            burningPlayers.Clear();
            locations.Clear();
            playerDealerStolen.Clear();
            consumedGUIDs.Clear();
            stolenInDealerInv.Clear();
            stolenNPCs.Clear();
            supplyLocations.Clear();
            carLoot.Clear();
            barrelLoot.Clear();

            // allied extension states and objects reset also
            foreach (string key in alliedDialogueKeys)
            {
                persuasionChances[key] = 0f;
            }

            allCartelDealers = null;

            influenceConfig = null;
            eventCooldowns = null;

            isSaving = false;

            // Now the created states and any boolean flags for events
            // QUests
            activeQuest = null;
            completed = false;
            activeManorQuest = null;
            manorCompleted = false;
            activeCarMeetupQuest = null;
            carMeetupCompleted = false;

            RandomManorGenerator.ResetManorItemRef();

            // allied quests
            activeTruceIntro = null;
            activeAlliedSupplies = null;
            alliedSuppliesActive = false;
            alliedGuard = null;
            alliedVanObject = null;
            guardChoiceIndex = -1;

            // quest npcs
            fixer = null;
            ray = null;
            jeremy = null;
            crankyFrank = null;
            bossGoon = null;
            fixerDiagIndex = 0;
            rayChoiceIndex = 0;
            jeremyDiagIndex = -1;
            frankDiagIndex = -1;
            jeremyDialogueActive = false;
            inContactDialogue = false;

            // Mini quests and events
            lootGoblinIndex = -1;
            StageDeadDropsObserved = 0;
            StageGatheringsDefeated = 0;
            driveByActive = false;
            interceptingDeal = false;
            interceptor = null;
            startedCombat = false;
            areGoonsGathering = false;
            currentGatheringLocation = null;
            previousGatheringLocation = null;

            // sabotage related
            bombDefused = false;
            sabotageEventActive = false;
            interactionsUntilDefuse = 6;
            intBomb = null;
            reactiveFire = null;
            bombInteractable = null;
            bombLight = null;
            bombCubeMat = null;
            bombSound = null;
            fireHandler = null;
            fireLight = null;

            // cartel inv
            jarPackaging = null;
            brickPackaging = null;
            cartelCashAmount = 0f;

            // temp variable for showing the influence reduction while truced
            showEnqueued = false;

            hoursUntilNextGathering = 3;
            currentDealerActivity = 0f;
            previousDealerActivity = 0f;

        }
        
        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { typeof(string) })]
        public static class SaveManager_Save_String_Patch
        {
            public static bool Prefix(SaveManager __instance, string saveFolderPath)
            {
                if (!isSaving)
                {
                    isSaving = true;
                    lock (cartelItemLock)
                    {
                        ConfigLoader.Save(cartelStolenItems);
                    }
                    if (currentConfig.alliedExtensions)
                        ConfigLoader.Save(alliedQuests);

                    CurrentEventCooldowns currentCooldowns = new();

                    currentCooldowns.StealDeadDropCooldown = regActivityHours.First(x => x.cartelActivityClass == 0).hoursUntilEnable;
                    currentCooldowns.CartelCustomerDealCooldown = regActivityHours.First(x => x.cartelActivityClass == 1).hoursUntilEnable;
                    currentCooldowns.RobDealerCooldown = regActivityHours.First(x => x.cartelActivityClass == 2).hoursUntilEnable;
                    currentCooldowns.SprayGraffitiCooldown = regActivityHours.First(x => x.cartelActivityClass == 3).hoursUntilEnable;

                    currentCooldowns.DriveByCooldown = DriveByEvent.hoursUntilDriveBy;
                    currentCooldowns.GatheringCooldown = CartelGathering.hoursUntilNextGathering;
                    currentCooldowns.InterceptDealsCooldown = InterceptEvent.hoursUntilInterceptEvent;
                    
                    currentCooldowns.SabotageCooldowns = new();
                    SabotageEvent.locations.ForEach(x => currentCooldowns.SabotageCooldowns.Add(x.business.PropertyName, x.hoursUntilEnabled));

                    ConfigLoader.Save(currentCooldowns);
                }
                isSaving = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { })]
        public static class SaveManager_Save_Patch
        {
            public static bool Prefix(SaveManager __instance)
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(LoadManager), "ExitToMenu")]
        public static class LoadManager_ExitToMenu_Patch
        {
            public static bool Prefix(LoadManager __instance, SaveInfo autoLoadSave = null, MainMenuPopup.Data mainMenuPopup = null, bool preventLeaveLobby = false)
            {
                ExitPreTask();
                return true;
            }
        }

        [HarmonyPatch(typeof(DeathScreen), "LoadSaveClicked")]
        public static class DeathScreen_LoadSaveClicked_Patch
        {
            public static bool Prefix(DeathScreen __instance)
            {
                ExitPreTask();
                return true;
            }
        }
        #endregion


        #region Fix the Invisible Cartel Goon Bug
        /*
        Sometimes during cartel goon despawn the
        IsGoonSpawned state does not reset back to false (dunno why)
        causing the subsequent spawn attempts to not enable the avatar
        and still have the movement agent enabled
        upon spawn and then starts adding into the spawned goons list "unspawned" goons

        Despawn postfix checks each possible bugged state and then reverts it.
        Additionally theres 2 additions, one which disables/enables awareness
        And one which potentially clears out any remaining goon mates in the list
         */

        [HarmonyPatch(typeof(CartelGoon), "Spawn")]
        public static class CartelGoon_Spawn_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(CartelGoon __instance, GoonPool pool, Vector3 spawnPoint)
            {
                if (!__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(true);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                return true;
            }
        }

        [HarmonyPatch(typeof(CartelGoon), "Despawn")]
        public static class CartelGoon_Despawn_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CartelGoon __instance)
            {
                if (__instance.IsGoonSpawned)
                    __instance.IsGoonSpawned = false;

                if (NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Remove(__instance);

                if (!NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Add(__instance);

                if (__instance.Behaviour.activeBehaviour != null && __instance.Behaviour.activeBehaviour == __instance.Behaviour.CombatBehaviour)
                    __instance.Behaviour.CombatBehaviour.Disable();

                if (__instance.Movement.Agent.enabled)
                    __instance.Movement.Agent.enabled = false;

                if (__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(false);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                return;
            }

        }

        #endregion

    }
}