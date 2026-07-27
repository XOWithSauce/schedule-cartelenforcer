using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Reflection;

using static CartelEnforcer.AmbushOverrides;
using static CartelEnforcer.CartelInventory;
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
using static CartelEnforcer.NPCInitHelper;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.NPCs;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.UI;
using ScheduleOne.NPCs.Framework;
using ScheduleOne.Dialogue;
using FishNet.Managing;
using FishNet.Object;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.NPCs.Framework;
using Il2CppScheduleOne.Dialogue;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Object;
using Il2Cpp;
#endif

[assembly: MelonInfo(typeof(CartelEnforcer.CartelEnforcer), CartelEnforcer.BuildInfo.Name, CartelEnforcer.BuildInfo.Version, CartelEnforcer.BuildInfo.Author, CartelEnforcer.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

#if MONO
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonLoader.VerifyLoaderVersion("0.7.3", true)]
#else 
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: MelonLoader.VerifyLoaderVersion("0.7.3", true)]
#endif

namespace CartelEnforcer
{
    public static class BuildInfo
    {
        public const string Name = "Cartel Enforcer";
        public const string Description = "Cartel - Modded and configurable";
        public const string Author = "XOWithSauce";
        public const string Company = null;
        public const string Version = "2.0.0";
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
        public static WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();
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
                if (playerTransform != null && positionText != null)
                {
                    Vector3 playerPos = playerTransform.position;
                    string formattedPosition = $"X: {playerPos.x:F2}\nY: {playerPos.y:F2}\nZ: {playerPos.z:F2}";
                    positionText.text = formattedPosition;
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
            stolenNPCs = ConfigLoader.LoadStolenCustomers();

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

            // needed for true brothers quest only
            if (currentConfig.endGameQuest && currentConfig.alliedExtensions)
                coros.Add(MelonCoroutines.Start(ReplicateCopNPC()));

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

        public static IEnumerator ExtendGoonPool()
        {
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();
            CartelGoon goon = UnityEngine.Object.FindObjectOfType<CartelGoon>(true);

            if (goon == null)
            {
                Log("No goon found");
                yield break;
            }
            GameObject obj = goon.gameObject;
            obj.SetActive(false);
            yield return Wait01;
            yield return frameEnd;

            NPCData templateData = null;
            if (goon.NPCData == null)
            {
                Log("Original goon is missing NPC Template data!");
            }
            else
            {
                templateData = goon.NPCData.GetDeepCopy();
                if (templateData == null || templateData.WeatherBehaviour == null)
                    Log("Failed to get template NPCData");
                else
                    templateData.WeatherBehaviour.UseUmbrellaChance = 0f;
            }
            
            List<CartelGoon> clones = new();
            for (int i = 5; i <= 9; i++)
            {
                yield return Wait01;
                yield return frameEnd;

                GameObject clone = UnityEngine.Object.Instantiate(obj);
                NetworkObject newNob = clone.GetComponent<NetworkObject>();
                NPC npc = clone.GetComponent<NPC>();
                if (npc == null)
                    Log("Failed to find NPC Component from instantiated Goon!");
                if (npc.Actions == null)
                    Log("NPC does not have initialized actions!");
                else
                    npc.Actions._canUseUmbrella = false;

                npc.GUID = GUIDManager.GenerateUniqueGUID();
                if (GUIDManager.IsGUIDAlreadyRegistered(npc.GUID))
                {
                    Log("Failed to generate registreable GUID");
                    continue;
                }
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;

                clone.name = $"CartelGoon ({i})";
                yield return MelonCoroutines.Start(InitiateClone(newNob, netManager, templateData));

                Log("Spawn");
                netManager.ServerManager.Spawn(newNob);

                CartelGoon newGoonComp = clone.GetComponent<CartelGoon>();
                if (newGoonComp.DialogueHandler == null)
                {
                    Log("New goon does not have Dialogue Handler initiated!");
                }
                else
                {
                    DialogueController controller = newGoonComp.DialogueHandler.GetComponent<DialogueController>();
                    if (controller == null)
                    {
                        Log("Failed to find dialogue controller!");
                    }
                    else
                        controller.Choices.Clear();
                }
                    

                clones.Add(newGoonComp);

                Log($"  Done: {i} -------------\n");
            }

            obj.SetActive(true);

            Log("Swapping array");
            try
            {
                GoonPool goonPool = NetworkSingleton<Cartel>.Instance.GoonPool;
                CartelGoon[] originalGoons = goonPool.goons;

                int originalCount = originalGoons.Length;

                int extra = clones.Count;
                int newCount = originalCount + extra;

                CartelGoon[] newGoons = new CartelGoon[newCount];

                Log("Copying array");
                System.Array.Copy(originalGoons, newGoons, originalCount);

                if (goonPool.unspawnedGoons == null)
                {
                    Log("GoonPool unspawned list is uninitialized");
                }

                int clonesIdx = 0;
                for (int i = originalCount; i < newCount; i++)
                {
                    if (clonesIdx >= clones.Count) break;
                    CartelGoon newGoon = clones[clonesIdx];
                    newGoons[i] = newGoon;
                    goonPool.unspawnedGoons.Add(newGoon);
                    clonesIdx++;
                }
                Log("Replace array ");
                goonPool.goons = newGoons;

                Log("Array swapped now count: " + NetworkSingleton<Cartel>.Instance.GoonPool.goons.Length);
            } catch (Exception ex)
            {
                Log($"Failed to extend goon pool array:{ex}");
            }
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
            stolenNPCs.Clear();
            supplyLocations.Clear();
            barrelLoot.Clear();

            // clear inner lists for car loot (kinda redundant since they get repopulated on save load)
            foreach (string key in carLoot.Keys.ToList())
                carLoot[key].Clear();

            // allied extension states and objects reset also
            foreach (string key in alliedDialogueKeys)
                persuasionChances[key] = 0f;

            allCartelDealers = null;

            influenceConfig = null;
            eventCooldowns = null;

            isSaving = false;

            // Now the created states and any boolean flags for events
            // QUests
            activeDefeatEnforcerQuest = null;
            defeatEnforcerCompleted = false;
            activeManorQuest = null;
            manorCompleted = false;
            activeCarMeetupQuest = null;
            carMeetupCompleted = false;

            hasGeneratedDefeatEnforcerQuest = false;
            hasGeneratedManorQuest = false;
            hasGeneratedCarQuest = false;

            RandomManorGenerator.ResetManorItemRef();

            // allied quests
            activeTruceIntro = null;
            activeAlliedSupplies = null;
            alliedSuppliesActive = false;
            alliedGuard = null;
            alliedVanObject = null;
            guardChoiceIndex = -1;

            activeTrueBrothersQuest = null;
            trueBrothersCompleted = false;
            encounterActive = false;

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
            sabotager = null;
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
                    ConfigLoader.Save(cartelStolenItems);
                    if (currentConfig.alliedExtensions)
                        ConfigLoader.Save(alliedQuests);

                    if (currentConfig.stealBackCustomers)
                    {
                        SerializedStolenNPCs data = new();
                        data.stolenCustomers = new();
                        foreach(StolenNPC stolenNPC in stolenNPCs)
                        {
                            StolenNPCSerialized serialized = new();
                            serialized.npcID = stolenNPC.npc.ID;
                            serialized.sampleChancesProcessed = stolenNPC.sampleChancesProcessed;
                            data.stolenCustomers.Add(serialized);
                        }

                        ConfigLoader.Save(data);
                    }

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
                    Log("Saved");
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
        Sometimes during cartel goon despawn (in mod added events non-daypass despawn)
        The cartel goon is "despawned" but stays active, invisible but aware.

        Despawn postfix checks each possible bugged state and then reverts it.
        Spawn postfix handles the logic for ensuring that later spawns after potential bug
        will not keep the cartel goon invisible while spawned.

        Then because the StayInside behaviour forces the cartel goons to navigate to 
        the nearest valid building but the the cartel goon starts attempting to go inside
        constantly invoking despawn.
         */
         
        [HarmonyPatch(typeof(CartelGoon), "Spawn")]
        public static class CartelGoon_Spawn_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CartelGoon __instance)
            {
                coros.Add(MelonCoroutines.Start(AfterSpawnEvaluate(__instance)));
                return;
            }

            public static IEnumerator AfterSpawnEvaluate(CartelGoon __instance)
            {
                yield return Wait01;
                if (!registered) yield break;

                if (!__instance.IsGoonSpawned)
                    __instance.IsGoonSpawned = true;

                if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Remove(__instance);

                if (!NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Add(__instance);

                if (__instance.Movement.IsPaused)
                    __instance.Movement.ResumeMovement();

                if (!__instance.Movement.Agent.enabled)
                    __instance.Movement.Agent.enabled = true;

                if (!__instance.isVisible)
                    __instance.SetVisible(true, false);

                if (!__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(true);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                if (!__instance.gameObject.activeSelf)
                    __instance.gameObject.SetActive(true);

                yield break;
            }
        }

        [HarmonyPatch(typeof(CartelGoon), "Despawn")]
        public static class CartelGoon_Despawn_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CartelGoon __instance)
            {
                coros.Add(MelonCoroutines.Start(AfterDespawnEvaluate(__instance)));
                return;
            }

            public static IEnumerator AfterDespawnEvaluate(CartelGoon __instance)
            {
                yield return Wait01;
                if (!registered) yield break;

                if (__instance.IsGoonSpawned)
                    __instance.IsGoonSpawned = false;

                if (NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Remove(__instance);

                if (!NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Contains(__instance))
                    NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Add(__instance);

                if (!__instance.Movement.IsPaused)
                    __instance.Movement.PauseMovement();

                if (__instance.Movement.Agent.enabled)
                    __instance.Movement.Agent.enabled = false;

                if (__instance.isVisible)
                    __instance.SetVisible(false, false);

                if (__instance.Awareness.enabled)
                    __instance.Awareness.SetAwarenessActive(false);

                if (__instance.goonMates.Count > 0)
                    __instance.goonMates.Clear();

                yield break;
            }
        }
        #endregion



    }
}