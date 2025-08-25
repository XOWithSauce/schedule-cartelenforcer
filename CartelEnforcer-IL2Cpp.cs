using System.Collections;
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.AI;
using Il2CppScheduleOne.VoiceOver;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

[assembly: MelonInfo(typeof(CartelEnforcer_IL2Cpp.CartelEnforcer_IL2Cpp), CartelEnforcer_IL2Cpp.BuildInfo.Name, CartelEnforcer_IL2Cpp.BuildInfo.Version, CartelEnforcer_IL2Cpp.BuildInfo.Author, CartelEnforcer_IL2Cpp.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CartelEnforcer_IL2Cpp
{
    public static class BuildInfo
    {
        public const string Name = "Cartel Enforcer";
        public const string Description = "Cartel - Modded and configurable";
        public const string Author = "XOWithSauce";
        public const string Company = null;
        public const string Version = "1.2.0";
        public const string DownloadLink = null;
    }

    #region Persistent JSON Files and Serialization
    public class ModConfig
    {
        public bool debugMode = false; // While in debug mode, spawn visuals for Cartel Ambushes, Enable Debug Log Messages, etc.

        public float activityFrequency = 0.0f; // From -1.0 to 0.0 to 1.0,
                                               // -1.0= Activity is 10 times less frequent
                                               // 0.0 = Activity is at game default frequency 
                                               // 1.0 = Activity is 10 times more frequent

        public float activityInfluenceMin = 0.0f; // From -1.0 to 0.0 to 1.0,
                                                  // -1.0 = Activity influence requirement is 100% Less (Means cartel influence requirement is at 0 and activities happen always)
                                                  // 0.0  = Activity influence requirement is at Game Default
                                                  // 1.0  = Activity influence requirement is 100% More (Means cartel influence will only happen if at maximum regional cartel influence)

        public bool driveByEnabled = true;

        public bool realRobberyEnabled = true;

        public bool miniQuestsEnabled = true;
    }

    // Serializer for base CartelAmbushLocation
    public class NewAmbushConfig
    {
        public int mapRegion = 0; // Maps out to 0 = Northtown, 5 = Uptown
        public Vector3 ambushPosition = Vector3.zero; // Needed for detection radius check, instantiate new monobeh base at this location
        public List<Vector3> spawnPoints = new(); // note min 4 spawn points, instantiate as child obj new empty transform objects to fill base class AmbushPoints variable
        public float detectionRadius = 10f; // How far player can be at maximum from ambushPosition variable, default 10
    }

    // Serialize this class to json file for configure
    public class ListNewAmbush
    {
        public List<NewAmbushConfig> addedAmbushes = new List<NewAmbushConfig>();
    }

    public static class ConfigLoader
    {
        private static string pathModConfig = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "config.json");
        private static string pathAmbushes = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Ambush", "ambush.json");
        private static string pathDefAmbushes = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Ambush", "default.json");

        #region Mod Config 
        public static ModConfig Load()
        {
            ModConfig config;
            if (File.Exists(pathModConfig))
            {
                try
                {
                    string json = File.ReadAllText(pathModConfig);
                    config = JsonConvert.DeserializeObject<ModConfig>(json);
                    config.activityFrequency = Mathf.Clamp(config.activityFrequency, -1.0f, 1.0f); // Ensure limits
                    config.activityInfluenceMin = Mathf.Clamp(config.activityInfluenceMin, -1.0f, 1.0f); // Ensure limits
                }
                catch (Exception ex)
                {
                    config = new ModConfig();
                    MelonLogger.Warning("Failed to read CartelEnforcer config: " + ex);
                }
            }
            else
            {
                config = new ModConfig();
                Save(config);
            }
            return config;
        }
        public static void Save(ModConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(pathModConfig));
                File.WriteAllText(pathModConfig, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer config: " + ex);
            }
        }
        #endregion

        #region User Generated Ambush config
        public static ListNewAmbush LoadAmbushConfig()
        {
            ListNewAmbush config;
            if (File.Exists(pathAmbushes))
            {
                try
                {
                    string json = File.ReadAllText(pathAmbushes);
                    config = JsonConvert.DeserializeObject<ListNewAmbush>(json);
                }
                catch (Exception ex)
                {
                    config = new ListNewAmbush();
                    config.addedAmbushes = new();
                    MelonLogger.Warning("Failed to read CartelEnforcer config: " + ex);
                }
            }
            else
            {
                config = new ListNewAmbush();
                config.addedAmbushes = new();
                Save(config);
            }
            return config;
        }

        public static void Save(ListNewAmbush config)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new UnityContractResolver()
                };
                string json = JsonConvert.SerializeObject(config, Formatting.Indented, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(pathAmbushes));
                File.WriteAllText(pathAmbushes, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer config: " + ex);
            }
        }
        #endregion

        #region Default Ambush data
        // Read the default ambush config, if there are changes to the default.json data we apply changes
        public static ListNewAmbush LoadDefaultAmbushConfig()
        {
            ListNewAmbush config;
            if (File.Exists(pathDefAmbushes))
            {
                try
                {
                    string json = File.ReadAllText(pathDefAmbushes);
                    config = JsonConvert.DeserializeObject<ListNewAmbush>(json);
                }
                catch (Exception ex)
                {
                    config = new ListNewAmbush();
                    MelonLogger.Warning("Failed to read default.json config: " + ex);
                }
            }
            else
            {
                config = GenerateAmbushState();
            }
            return config;
        }
        // Only generate (initial default.json write) + read (for user modding the default params)
        public static ListNewAmbush GenerateAmbushState()
        {
            ListNewAmbush currentState = new();
            currentState.addedAmbushes = new List<NewAmbushConfig>(); // Init empty if not already
            try
            {
                CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
                foreach (CartelRegionActivities act in regAct)
                {
                    foreach (CartelAmbushLocation loc in act.AmbushLocations)
                    {
                        NewAmbushConfig config = new NewAmbushConfig();
                        config.mapRegion = (int)act.Region;
                        config.ambushPosition = loc.transform.position;
                        config.spawnPoints = loc.AmbushPoints.Select(tr => tr.position).ToList();
                        config.detectionRadius = loc.DetectionRadius;
                        
                        bool isDuplicate = currentState.addedAmbushes.Any(existingConfig =>
                            existingConfig.mapRegion == config.mapRegion &&
                            existingConfig.ambushPosition == config.ambushPosition);

                        if (!isDuplicate)
                            currentState.addedAmbushes.Add(config);
                    }
                }

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new UnityContractResolver()
                };
                string json = JsonConvert.SerializeObject(currentState, Formatting.Indented, settings);

                Directory.CreateDirectory(Path.GetDirectoryName(pathDefAmbushes));
                File.WriteAllText(pathDefAmbushes, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save default.json config: " + ex);
            }
            return currentState;
        }
        #endregion
    }

    // Because vector3 isnt just xyz for serialization, we remove everything except xyz from the base object properties
    public class UnityContractResolver : DefaultContractResolver
    {
        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            JsonObjectContract contract = base.CreateObjectContract(objectType);

            if (objectType == typeof(Vector3))
            {
                for (int i = contract.Properties.Count - 1; i >= 0; i--)
                {
                    var property = contract.Properties[i];
                    if (property.PropertyName == "normalized" || property.PropertyName == "magnitude" || property.PropertyName == "sqrMagnitude")
                    {
                        contract.Properties.RemoveAt(i);
                    }
                }
            }
            return contract;
        }
    }
    #endregion
    public class CartelEnforcer_IL2Cpp : MelonMod
    {
        public static CartelEnforcer_IL2Cpp Instance;

        public static ModConfig currentConfig;
        public static ListNewAmbush ambushConfig;
        public static ListNewAmbush gameDefaultAmbush;

        public static List<HrPassParameterMap> actFreqMapping = new();

        public static List<DriveByTrigger> driveByLocations = new();

        public static List<object> coros = new();

        static bool registered = false;
        private bool firstTimeLoad = false;
        static bool debounce = false; // Keyboard Input

        // Drive By logic
        static LandVehicle driveByVeh;
        static VehicleAgent driveByAgent;
        static VehicleTeleporter driveByTp;
        static bool driveByActive = false;
        static Thomas thomasInstance;
        static ParkData driveByParking;
        static int hoursUntilDriveBy = 5;

        // Coordinate ui elements for debug
        private static TextMeshProUGUI _positionText;
        private static Transform _playerTransform;

        // Mini Quest Dead Drops
        public static List<string> rareDrops = new()
        {
            "silverwatch",
            "goldwatch",
            "silverchain",
            "goldchain",
            "oldmanjimmys",
            "brutdugloop",
        };
        public static List<string> commonDrops = new()
        {
            "cocaine",
            "meth",
            "greencrackseed",
            "ogkushseed",
        };
        public static Dictionary<NPC, NpcQuestStatus> targetNPCs = new Dictionary<NPC, NpcQuestStatus>();
        public class NpcQuestStatus
        {
            public bool HasAskedQuestToday { get; set; }
            public bool HasActiveQuest { get; set; }
        }


        #region Basic Mod Utils / Unity Method
        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();
            Instance = this;
            currentConfig = ConfigLoader.Load();
            MelonLogger.Msg("Cartel Enforcer Mod Loaded");
        }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 1)
            {
                if (LoadManager.Instance != null && !registered && !firstTimeLoad)
                {
                    firstTimeLoad = true;
                    LoadManager.Instance.onLoadComplete.AddListener((UnityEngine.Events.UnityAction)OnLoadCompleteCb);
                }
            }
            if (buildIndex != 1)
            {
                if (registered)
                {
                    registered = false;
                    foreach (object coro in coros)
                    {
                        if (coro != null)
                            MelonCoroutines.Stop(coro);
                    }
                    coros.Clear();
                    driveByActive = false;
                    driveByLocations.Clear();
                    actFreqMapping.Clear();
                    targetNPCs.Clear();
                }
            }
        }
        private void OnLoadCompleteCb()
        {
            if (registered) return;
            registered = true;

            currentConfig = ConfigLoader.Load();

            if (currentConfig.driveByEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateDriveBy()));

            coros.Add(MelonCoroutines.Start(InitializeAmbush()));

            if (currentConfig.miniQuestsEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateMiniQuest()));

            if (currentConfig.activityFrequency != 0.0f)
                coros.Add(MelonCoroutines.Start(TickOverrideHourPass()));

            if (currentConfig.debugMode)
                MelonCoroutines.Start(MakeUI());
        }

        public static IEnumerator InitializeAndEvaluateDriveBy()
        {
            yield return MelonCoroutines.Start(InitializeDriveByData());
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnDriveByAreaVisual());

            coros.Add(MelonCoroutines.Start(EvaluateDriveBy()));
        }

        public static IEnumerator InitializeAmbush() 
        {
            yield return MelonCoroutines.Start(ApplyGameDefaultAmbush());
            yield return MelonCoroutines.Start(AddUserModdedAmbush());
            yield return MelonCoroutines.Start(AfterAmbushInitComplete());
        }

        public static IEnumerator AfterAmbushInitComplete()
        {
            yield return MelonCoroutines.Start(PopulateParameterMap());
            yield return MelonCoroutines.Start(ApplyInfluenceConfig());
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnAmbushAreaVisual());

            coros.Add(MelonCoroutines.Start(TickOverrideHourPass()));
        }

        public static IEnumerator InitializeAndEvaluateMiniQuest()
        {
            yield return InitMiniQuest();
            NetworkSingleton<TimeManager>.Instance.onDayPass += (Il2CppSystem.Action)OnDayPassNewDiag;
            coros.Add(MelonCoroutines.Start(EvaluateMiniQuestCreation()));
            yield return null;
        }

        public override void OnUpdate()
        {
            if (!registered || currentConfig == null)
                return;

            if (currentConfig.debugMode && _playerTransform != null && _positionText != null)
            {
                Vector3 playerPos = _playerTransform.position;
                string formattedPosition = $"X: {playerPos.x:F2}\nY: {playerPos.y:F2}\nZ: {playerPos.z:F2}";
                _positionText.text = formattedPosition;
            }
            // SEE Debug #region in code for InputFunctions
            // Left CTRL + R to Start Rob Dealer Function to nearest dealer
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.R))
            {
                if (!debounce)
                {
                    debounce = true;
                    MelonCoroutines.Start(OnInputStartRob());
                }
            }

            // Left CTRL + G to Start Drive By Instant 
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.G))
            {
                if (!debounce)
                {
                    debounce = true;
                    MelonCoroutines.Start(OnInputStartDriveBy());
                }
            }

            // Left CTRL + H to Give Mini Quest Instantly to one of the NPCs 
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.H))
            {
                if (!debounce)
                {
                    debounce = true;
                    MelonCoroutines.Start(OnInputGiveMiniQuest());
                }
            }
            

        }
        #endregion

        #region Harmony Patches for exiting coros
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
            driveByActive = false;
            driveByLocations.Clear();
            actFreqMapping.Clear();
            targetNPCs.Clear();
        }

        [HarmonyPatch(typeof(LoadManager), "ExitToMenu")]
        public static class LoadManager_ExitToMenu_Patch
        {
            public static bool Prefix(SaveInfo autoLoadSave = null, Il2CppScheduleOne.UI.MainMenu.MainMenuPopup.Data mainMenuPopup = null, bool preventLeaveLobby = false)
            {
                //MelonLogger.Msg("Exit Menu");
                ExitPreTask();
                return true;
            }
        }

        [HarmonyPatch(typeof(DeathScreen), "LoadSaveClicked")]
        public static class DeathScreen_LoadSaveClicked_Patch
        {
            public static bool Prefix(DeathScreen __instance)
            {
                //MelonLogger.Msg("LoadLastSave");
                ExitPreTask();
                return true;
            }
        }
        #endregion

        #region Load and Apply Serialized Ambush Data
        public static IEnumerator ApplyGameDefaultAmbush()
        {
            yield return new WaitForSeconds(5f);
            ambushConfig = ConfigLoader.LoadAmbushConfig();
            gameDefaultAmbush = ConfigLoader.LoadDefaultAmbushConfig();
            Log("Loaded Ambush Config Data");
            yield return new WaitForSeconds(1f);

            CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
            Log("Applying Game Defaults Cartel Ambushes");
            int i = 0;
            foreach (CartelRegionActivities act in regAct)
            {
                foreach (CartelAmbushLocation loc in act.AmbushLocations)
                {
                    List<Vector3> defaultData = loc.AmbushPoints.Select(tr => tr.position).ToList();
                    NewAmbushConfig loadedConfig = gameDefaultAmbush.addedAmbushes.ElementAt(i);
                    Log($"Checking Default Ambush {i}");
                    i++;
                    if (loadedConfig.spawnPoints.Count != defaultData.Count)
                    {
                        Log("    - SpawnPoints count diff Skipping");
                        continue;
                    }

                    if (loc.transform.position != loadedConfig.ambushPosition)
                    {
                        Log("    - Changing Default ambush position");
                        loc.transform.position = loadedConfig.ambushPosition;
                    }

                    if (loc.DetectionRadius != loadedConfig.detectionRadius)
                    {
                        Log("    - Override default detection radius");
                        loc.DetectionRadius = loadedConfig.detectionRadius;
                    }

                    for (int j = 0; j < loadedConfig.spawnPoints.Count; j++)
                    {
                        if (loadedConfig.spawnPoints[j] != defaultData[j])
                        {
                            Log("    - Override default ambush spawn points");
                            loc.AmbushPoints[j].position = loadedConfig.spawnPoints[j];
                        }
                    }

                }
            }
            Log("Done Applying Game Default Ambushes");
            yield return null;
        }

        public static IEnumerator AddUserModdedAmbush()
        {
            yield return new WaitForSeconds(2f);
            Log("Adding User Modded Ambushes to existing ones");
            CartelRegionActivities[] regAct = NetworkSingleton<Cartel>.Instance.Activities.RegionalActivities;
            int i = 1;
            if (ambushConfig.addedAmbushes != null && ambushConfig.addedAmbushes.Count > 0)
            {
                foreach (NewAmbushConfig config in ambushConfig.addedAmbushes)
                {
                    CartelRegionActivities regActivity = regAct.FirstOrDefault(act => (int)act.Region == config.mapRegion);

                    Log($"Generating Ambush object {i} in region: {regActivity.Region}");
                    Transform nextParent = regActivity.transform.Find("Ambush locations");
                    GameObject newAmbushObj = new($"AmbushLocation ({nextParent.childCount})");
                    CartelAmbushLocation baseComp = newAmbushObj.AddComponent<CartelAmbushLocation>();
                    newAmbushObj.transform.position = config.ambushPosition;
                    baseComp.DetectionRadius = config.detectionRadius;

                    GameObject spawnPointsTr = new("SpawnPoints");
                    spawnPointsTr.transform.parent = newAmbushObj.transform;
                    baseComp.AmbushPoints = new Transform[config.spawnPoints.Count];
                    int j = 0;
                    foreach (Vector3 spawnPoint in config.spawnPoints)
                    {
                        Log($"    - Making Spawn point {j}: {spawnPoint}");
                        string name = $"SP{((j == 0) ? "" : " (" + j.ToString() + ")")}";
                        GameObject newSpawnPoint = new(name);
                        newSpawnPoint.transform.position = spawnPoint;
                        newSpawnPoint.transform.parent = spawnPointsTr.transform;
                        baseComp.AmbushPoints[j] = newSpawnPoint.transform;
                        baseComp.enabled = true;
                        j++;
                    }
                    i++;
                    CartelAmbushLocation[] originalLocations = regActivity.AmbushLocations;
                    CartelAmbushLocation[] newLocations = new CartelAmbushLocation[originalLocations.Length + 1];

                    Array.Copy(originalLocations, newLocations, originalLocations.Length);
                    newLocations[newLocations.Length - 1] = baseComp;
                    regActivity.AmbushLocations = newLocations;

                    // Important to add this at the end - otherwise the networked object refuses to swap out the array for locations
                    newAmbushObj.transform.parent = nextParent;
                }

                Log("Done adding User Modded Ambushes");
            }
            else
            {
                Log("No User Added Ambushes found");
            }
        }


        #endregion

        #region Activity Frequency modification

        public class HrPassParameterMap
        {
            public int itemIndex { get; set; }
            public Func<int> Getter { get; set; }
            public Action<int> Setter { get; set; }
            public Action HourPassAction { get; set; }
            public int modTicksPassed { get; set; }
            public int currentModHours { get; set; }
            public Func<bool> CanPassHour { get; set; }
        }

        public static IEnumerator PopulateParameterMap()
        {
            yield return new WaitForSeconds(2f);
            if (!registered) yield break;
            Log("Populating Activity Frequency Parameters");

            int indexCurrent = 0;

            CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemIndex = indexCurrent,
                Getter = () => instanceActivities.HoursUntilNextGlobalActivity,
                Setter = (value) => instanceActivities.HoursUntilNextGlobalActivity = value,
                HourPassAction = () => instanceActivities.HourPass(),
                modTicksPassed = 0,
                currentModHours = instanceActivities.HoursUntilNextGlobalActivity,
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
            });
            indexCurrent++;

            CartelRegionActivities[] regInstanceActivies = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
            foreach (CartelRegionActivities act in regInstanceActivies)
            {
                actFreqMapping.Add(new HrPassParameterMap
                {
                    itemIndex = indexCurrent,
                    Getter = () => act.HoursUntilNextActivity,
                    Setter = (value) => act.HoursUntilNextActivity = value,
                    HourPassAction = () => act.HourPass(),
                    modTicksPassed = 0,
                    currentModHours = act.HoursUntilNextActivity,
                    CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
                });
                indexCurrent++;
            }

            CartelDealManager instanceDealMgr = NetworkSingleton<Cartel>.Instance.DealManager;
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemIndex = indexCurrent,
                Getter = () => instanceDealMgr.HoursUntilNextDealRequest,
                Setter = (value) => instanceDealMgr.HoursUntilNextDealRequest = value,
                HourPassAction = () => instanceDealMgr.HourPass(),
                modTicksPassed = 0,
                currentModHours = instanceDealMgr.HoursUntilNextDealRequest,
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced
            });
            indexCurrent++;

            actFreqMapping.Add(new HrPassParameterMap
            {
                itemIndex = indexCurrent,
                Getter = () => hoursUntilDriveBy,
                Setter = (value) => hoursUntilDriveBy = value,
                HourPassAction = () => hoursUntilDriveBy = Mathf.Clamp(hoursUntilDriveBy - 1, 0, 48),
                modTicksPassed = 0,
                currentModHours = hoursUntilDriveBy,
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
            });

            Log("Finished populating Activity Frequency Parameters");
            yield return null;
        }

        public static IEnumerator TickOverrideHourPass()
        {
            yield return new WaitForSeconds(5f);
            if (!registered) yield break;
            if (currentConfig.activityFrequency == 0.0f) yield break;

            float defaultRate = 60f; // By default hour pass is 60sec
            float tickRate = 60f;
            if (currentConfig.activityFrequency > 0.0f) // If number is higher than 0, set tick rate to be rougly 10 times faster at 1.0
                tickRate = Mathf.Lerp(defaultRate, defaultRate / 10, currentConfig.activityFrequency);
            // Else condition here is that Activity Frequency is at minimum -1.0, where tick rate should be 10 times slower
            // But this doesnt work for tickrate because HourPass functions in classes add automatically
            // Therefore we must have an "hour" pass in 600 seconds
            Log("Starting HourPass Override, Tick once every " + tickRate + " seconds");

            while (registered)
            {
                yield return new WaitForSeconds(tickRate);
                if (!registered) yield break;
                if (actFreqMapping.Count == 0) continue;
                foreach (HrPassParameterMap item in actFreqMapping)
                {
                    yield return new WaitForSeconds(0.2f);
                    if (!registered) yield break;
                    MelonCoroutines.Start(HelperSet(item));
                }
            }
            yield return null;
        }

        public static IEnumerator HelperSet(HrPassParameterMap hpmap)
        {
            yield return new WaitForSeconds(0.1f);
            // based on source code these guards needed
            if (!hpmap.CanPassHour())
                yield break;
            if (!InstanceFinder.IsServer)
                yield break;
            // Our own guards for coroutine safety
            if (!registered)
                yield break;

            if (currentConfig.activityFrequency > 0.0f)
            {
                hpmap.HourPassAction();
            }
            else
            {
                // Because we are decreasing / resetting the hours value, we must avoid changing the value while its at 1 to avoid repeating the same
                // state change from 1->0 by normal hourpass function logic
                if (hpmap.Getter() < 2) yield break;

                float ticksReqForPass = Mathf.Lerp(1, 10, -currentConfig.activityFrequency);

                if (hpmap.Getter() < hpmap.currentModHours)
                    hpmap.Setter(hpmap.currentModHours);
                else
                {
                    // Now because we avoid setting the hourpass to 0, we let higher values reset current "hour" ticks and reassign the randomised next value
                    hpmap.modTicksPassed = 0;
                    hpmap.currentModHours = hpmap.Getter();
                }

                hpmap.modTicksPassed++;

                if (hpmap.modTicksPassed >= ticksReqForPass)
                {
                    hpmap.HourPassAction();
                    hpmap.modTicksPassed = 0;
                    hpmap.currentModHours = hpmap.currentModHours - 1; // Update value now since we ticked
                }
            }

            yield return null;
        }

        #endregion

        #region Activity Influence modification
        public static IEnumerator ApplyInfluenceConfig()
        {
            yield return new WaitForSeconds(2f);
            if (!registered) yield break;
            Log($"Config Activity Influence: {currentConfig.activityInfluenceMin}");
            if (currentConfig.activityInfluenceMin != 0.0f)
            {
                Log("Changing Activity Influence Requirements");
                // Change Activity Influence requirements
                CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;

                foreach (CartelActivity act in instanceActivities.GlobalActivities)
                {
                    float result = 0f;
                    if (currentConfig.activityInfluenceMin > 0.0f && act.InfluenceRequirement > 0.0f)
                        result = Mathf.Lerp(act.InfluenceRequirement, 1.0f, currentConfig.activityInfluenceMin);
                    else
                        result = Mathf.Lerp(act.InfluenceRequirement, 0.0f, -currentConfig.activityInfluenceMin);

                    Log($"Changing Global Activity Influence from {act.InfluenceRequirement} to {result}");
                    act.InfluenceRequirement = result;
                }

                CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
                foreach (CartelRegionActivities act in regAct)
                {
                    foreach(CartelActivity activity in act.Activities)
                    {
                        float result = 0f;
                        if (currentConfig.activityInfluenceMin > 0.0f && activity.InfluenceRequirement > 0.0f)
                            result = Mathf.Lerp(activity.InfluenceRequirement, 1.0f, currentConfig.activityInfluenceMin);
                        else
                            result = Mathf.Lerp(activity.InfluenceRequirement, 0.0f, -currentConfig.activityInfluenceMin);

                        Log($"Changing Regional Activity Influence from {activity.InfluenceRequirement} to {result}");
                        activity.InfluenceRequirement = result;
                    } 
                }
            }
            Log("Finished changing Activity Influence Requirements");
            yield return null;
        }
        #endregion

        #region Robbing Dealers spawns CartelGoons and actually moves items + cash to inventory

        [HarmonyPatch(typeof(Dealer), "TryRobDealer")] // This is only reached by FishNet Instance IsServer
        public static class DealerRobberyPatch
        {
            private static bool IsPlayerNearby(Dealer dealer, float maxDistance = 60f)
            {
                return Vector3.Distance(dealer.transform.position, Player.Local.transform.position) < maxDistance;
            }

            [HarmonyPrefix]
            public static bool Prefix(Dealer __instance)
            {
                if (!currentConfig.realRobberyEnabled) return true;

                if (!IsPlayerNearby(__instance))
                {
                    Log("Player not nearby, allowing original TryRobDealer logic to proceed.");
                    return true;
                }

                Log("Player is nearby! Initiating combat robbery.");

                __instance.MSGConversation.SendMessage(new Message(
                    "HELP BOSS!! Benzies are trying to ROB ME!!",
                    Message.ESenderType.Other, false, -1), true, true);

                coros.Add(MelonCoroutines.Start(RobberyCombatCoroutine(__instance)));

                return false;
            }

            private static IEnumerator RobberyCombatCoroutine(Dealer dealer)
            {
                yield return new WaitForSeconds(1f);
                if (!registered) yield break;
                EMapRegion region = EMapRegion.Northtown;
                for (int i = 0; i < Singleton<Map>.Instance.Regions.Length; i++)
                {
                    if (Singleton<Map>.Instance.Regions[i].RegionBounds.IsPointInsidePolygon(dealer.CenterPointTransform.position))
                    {
                        region = Singleton<Map>.Instance.Regions[i].Region;
                    }
                }

                Vector3 spawnPos = Vector3.zero;
                do
                {
                    yield return new WaitForSeconds(0.3f);
                    if (!registered) yield break;

                    Log("Finding Spawn Robber Position");
                    Vector3 randomDirection = UnityEngine.Random.onUnitSphere;
                    randomDirection.y = 0;
                    randomDirection.Normalize();
                    float randomRadius = UnityEngine.Random.Range(8f, 16f);
                    Vector3 randomPoint = dealer.transform.position + (randomDirection * randomRadius);
                    dealer.Movement.GetClosestReachablePoint(targetPosition: randomPoint, out spawnPos);
                } while (spawnPos == Vector3.zero); // Because GetClosestReachablePoint can return V3.Zero as default (unreachable)

                CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);

                goon.Movement.Warp(spawnPos);
                yield return new WaitForSeconds(0.5f);
                if (!registered) yield break;

                goon.Behaviour.CombatBehaviour.DefaultWeapon = null;

                dealer.Behaviour.CombatBehaviour.SetTarget(null, dealer.NetworkObject);
                dealer.Behaviour.CombatBehaviour.Enable_Networked(null);
                ICombatTargetable targetable = dealer.NetworkObject.GetComponent<ICombatTargetable>();
                if (targetable != null)
                {
                    coros.Add(MelonCoroutines.Start(StateRobberyCombat(dealer, goon, region)));
                    goon.AttackEntity(targetable);
                }
                else
                    MelonLogger.Warning("ICombatTargetable Not Found");

            }
        }

        public static IEnumerator StateRobberyCombat(Dealer dealer, CartelGoon goon, EMapRegion region)
        {
            // While Both dealer and spawned goon are alive and conscious evaluate every sec, max timeout is 1 minute
            int maxWaitSec = 60;
            int elapsed = 0;
            while (!dealer.Health.IsDead && !dealer.Health.IsKnockedOut &&
                !goon.Health.IsDead && !goon.Health.IsKnockedOut &&
                Vector3.Distance(Player.Local.CenterPointTransform.position, goon.CenterPointTransform.position) <= 90f && 
                elapsed < maxWaitSec)
            {
                yield return new WaitForSeconds(1f);
                if (!registered) yield break;

                elapsed++;
                //Log($"In Combat:\n    Dealer:{dealer.Health.Health}\n    Goon:{goon.Health.Health}");
            }

            if (dealer.Health.IsDead || !dealer.IsConscious || dealer.Health.IsKnockedOut)
            {

                // Dealer is dead Partial rob
                Log("Dealer was defeated! Initiating partial robbery.");

                int availableSlots = 0;
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                    {
                        availableSlots++;
                    }
                }

                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_partially_defended"), Message.ESenderType.Other, false, -1), true, true);

                // Rob default items first from dealer
                List<ItemInstance> list = new List<ItemInstance>();
                int takenSlots = 0;
                for (int i = 0; i < dealer.Inventory.ItemSlots.Count; i++)
                {
                    if (takenSlots >= availableSlots) break; // No more space in goon inventory

                    if (dealer.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        takenSlots++;
                        int qtyRobbed = Mathf.Max(1, Mathf.RoundToInt((float)dealer.Inventory.ItemSlots[i].ItemInstance.Quantity * 0.6f));
                        list.Add(dealer.Inventory.ItemSlots[i].ItemInstance.GetCopy(qtyRobbed));
                        dealer.Inventory.ItemSlots[i].ChangeQuantity(-qtyRobbed, false);
                    }
                }

                // Based on source code this should be done
                dealer.TryMoveOverflowItems();

                // Does this need Inventory.OnContentsChange invoke for networking after the change??

                // Move items to goon inventory
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (list.Count == 0) break;

                    if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                    {
                        Log($"Inserting {list.FirstOrDefault().Name} to Slot {i}");
                        goon.Inventory.ItemSlots[i].InsertItem(list.FirstOrDefault());
                        list.Remove(list.FirstOrDefault());
                    }
                }
                
                if (takenSlots < availableSlots)
                {
                    // Also take cash if there is still available space
                    float qtyCashLoss = dealer.Cash * 0.6f;

                    float clamp = Mathf.Clamp(qtyCashLoss, 1f, 2000f);// For cash stack size max

                    dealer.ChangeCash(-clamp);

                    CashInstance cashInstance = NetworkSingleton<MoneyManager>.Instance.GetCashInstance(clamp);
                    // Now insert cash stack
                    for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                    {
                        if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                        {
                            Log($"Inserting Cash to Slot {i}");
                            goon.Inventory.ItemSlots[i].InsertItem(cashInstance);
                            break;
                        }
                    }
                }
                // Does this need Inventory.OnContentsChange invoke for networking after the change??

                // Now here we need to start new coro for escaping goon running to nearest Cartel Dealer
                coros.Add(MelonCoroutines.Start(NavigateGoonEsacpe(goon, region)));
            }
            else if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                // Goon is dead or knocked out,defended robbery
                Log("Goon was defeated! Robbery attempt defended.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.080f);
                }
            }
            else if (Vector3.Distance(Player.Local.CenterPointTransform.position, goon.CenterPointTransform.position) > 90f)
            {
                // Player is out of range

                // For now just make a hacky way to prevent robbery
                // if outside range, then full defend
                Log("Player outside of range. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.020f);
                }
            }
            else if (elapsed >= 60)
            {
                Log("State Timed Out. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
            }
        }
        public static IEnumerator DespawnSoon(CartelGoon goon)
        {
            yield return new WaitForSeconds(30f);
            if (!registered) yield break;

            if (goon.IsGoonSpawned)
                goon.Despawn();
            yield return null;
        }

        public static IEnumerator NavigateGoonEsacpe(CartelGoon goon, EMapRegion region)
        {
            yield return new WaitForSeconds(0.5f);
            if (!registered) yield break;

            // After succesful robbery, navigate goon towards nearest CartelDealer apartment door
            CartelDealer[] cartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            float distance = 150f;
            Vector3 destination = Vector3.zero;
            foreach (CartelDealer d in cartelDealers)
            {
                yield return new WaitForSeconds(0.1f);
                if (!registered) yield break;

                if (d.isInBuilding && d.CurrentBuilding != null)
                {
                    NPCEnterableBuilding building = d.CurrentBuilding;
                    Il2CppScheduleOne.Doors.StaticDoor door = building.GetClosestDoor(goon.CenterPointTransform.position, false);
                    float distToDoor = Vector3.Distance(door.transform.position, d.CenterPointTransform.position);
                    if (distToDoor < distance)
                    {
                        destination = door.transform.position;
                        distance = distToDoor;
                    }
                }
            }

            Log($"Escaping to: {destination}");
            Log($"Distance: {distance}");

            goon.Movement.GetClosestReachablePoint(destination, out Vector3 closest);
            coros.Add(MelonCoroutines.Start(ApplyAdrenalineRush(goon)));

            if (destination == Vector3.zero || !goon.Movement.CanGetTo(closest)) // If the destination look up fails or cant traverse to
            {
                Log("No Destination or can not traverse to it");
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                goon.Behaviour.FleeBehaviour.SetEntityToFlee(Player.GetClosestPlayer(goon.CenterPointTransform.position, out float _).NetworkObject);
                goon.Behaviour.FleeBehaviour.Begin_Networked(null);
                yield break;
            }
            
            goon.Movement.SetDestination(closest);
            Vector3 appliedDest = goon.Movement.CurrentDestination;

            // While not dead or escape has elapsed under 60 seconds
            int elapsedNav = 0;
            while (elapsedNav < 60 &&
                goon.IsConscious &&
                !goon.Health.IsDead &&
                !goon.Health.IsKnockedOut &&
                !goon.isInBuilding)
            {
                yield return new WaitForSeconds(0.4f);
                if (!registered) yield break;

                elapsedNav++;
                
                if (goon.Movement.CurrentDestination != appliedDest)
                    goon.Movement.SetDestination(appliedDest);

                if (goon.Behaviour.activeBehaviour.ToString().Contains("Follow Schedule"))
                    continue;

                if (goon.Behaviour.activeBehaviour != null && goon.Behaviour.activeBehaviour is CombatBehaviour)
                {
                    goon.Behaviour.activeBehaviour.SendEnd();
                    yield return new WaitForSeconds(0.2f);
                    if (!registered) yield break;

                    goon.Movement.SetDestination(closest);
                }
            }

            if (goon.isInBuilding)
            {
                // The goon successfully escaped.
                Log("Goon Escaped to Cartel Dealer!");
                NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.050f);
                goon.Despawn();
            }
            else if (elapsedNav >= 60)
            {
                // The escape attempt timed out.
                Log("Despawned escaping goon due to timeout");
                goon.Despawn();
            }
            else
            {
                // The goon was defeated (dead or knocked out).
                Log("Goon Escape Dead or Knocked out!");
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
            }

            yield return null;
        }

        // After combat goon gets adrenaline rush, getting little health regen instantly and increasing speed for 15sec ...
        public static IEnumerator ApplyAdrenalineRush(CartelGoon goon)
        {
            float origWalk = goon.Movement.WalkSpeed;
            float origRun = goon.Movement.RunSpeed;
            goon.Movement.WalkSpeed = goon.Movement.WalkSpeed * 2.2f;
            goon.Movement.RunSpeed = goon.Movement.RunSpeed * 1.6f;
            goon.Health.Health = Mathf.Round(Mathf.Lerp(goon.Health.Health, 100f, 0.4f));

            Log($"Adrenaline applied:\n    Speed:{(goon.Movement.WalkSpeed)}\n    Health:{goon.Health.Health}");

            yield return new WaitForSeconds(5f);
            if (!registered) yield break;

            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);

            yield return new WaitForSeconds(5f);
            if (!registered) yield break;

            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);

            yield return new WaitForSeconds(5f);
            if (!registered) yield break;

            goon.Movement.WalkSpeed = origWalk;
            goon.Movement.RunSpeed = origRun;
        }
        #endregion

        #region Thomas Drive By Shooting Logic
        public class DriveByTrigger
        {
            public Vector3 triggerPosition;
            public float radius;
            public Vector3 spawnEulerAngles;
            public Vector3 startPosition;
            public Vector3 endPosition;
        }

        public static IEnumerator InitializeDriveByData()
        {
            yield return new WaitForSeconds(5f);
            if (!registered) yield break;

            Log("Configuring Drive By Triggers");
            // 1. Uptown Bus Stop
            DriveByTrigger uptownTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(110.39f, 5.36f, -111.69f),
                radius = 2f,
                startPosition = new Vector3(144.68f, 5.6f, -103.69f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(17.40f, 1.37f, -103.53f)
            };
            driveByLocations.Add(uptownTrigger);

            // 2. Uptown Park Area (same event as the bus stop but trigger diff)
            DriveByTrigger uptownParkTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(84.99f, 5.36f, -122.38f),
                radius = 5f,
                startPosition = new Vector3(144.68f, 5.6f, -103.69f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(17.40f, 1.37f, -103.53f)
            };
            driveByLocations.Add(uptownParkTrigger);

            // 3. Barn path towards road
            DriveByTrigger barnTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(163.29f, 1.18f, -9.95f),
                radius = 6f,
                startPosition = new Vector3(155.20f, 1.37f, 22.79f),
                spawnEulerAngles = new Vector3(0f, 240f, 0f),
                endPosition = new Vector3(89.85f, 5.37f, -81.81f)
            };
            driveByLocations.Add(barnTrigger);

            // 4. Mollys house
            DriveByTrigger mollysTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-168.44f, -2.54f, 88.32f),
                radius = 15f,
                startPosition = new Vector3(-146.40f, -2.63f, 39.92f),
                spawnEulerAngles = new Vector3(0f, 310f, 0f),
                endPosition = new Vector3(-111.28f, -2.64f, 123.40f)
            };
            driveByLocations.Add(mollysTrigger);

            // 5. Car Wash Computer
            DriveByTrigger carWashTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-6.05f, 1.21f, -17.83f),
                radius = 5f,
                startPosition = new Vector3(-19.96f, 1.37f, 20.44f),
                spawnEulerAngles = new Vector3(0f, 180f, 0f),
                endPosition = new Vector3(-10.61f, 1.37f, -102.43f)
            };
            driveByLocations.Add(carWashTrigger);

            // 6. Laundromat computer
            DriveByTrigger laundromatTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-29.19f, 1.96f, 26.13f),
                radius = 4f,
                startPosition = new Vector3(-17.17f, 1.37f, -22.86f),
                spawnEulerAngles = new Vector3(0f, 0f, 0f),
                endPosition = new Vector3(-16.98f, -2.51f, 123.42f)
            };
            driveByLocations.Add(laundromatTrigger);

            // 7. Grocery Market
            DriveByTrigger groceryMarketTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(17.62f, 1.61f, -3.31f),
                radius = 2f,
                startPosition = new Vector3(-11.64f, 1.15f, -43.54f),
                spawnEulerAngles = new Vector3(0f, 60f, 0f),
                endPosition = new Vector3(33.02f, 1.15f, 65.28f)
            };
            driveByLocations.Add(groceryMarketTrigger);

            // 8. Jane's RV
            DriveByTrigger janeRVTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-36.74f, 0.14f, -77.14f),
                radius = 11f,
                startPosition = new Vector3(3.98f, 1.37f, -103.35f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(-16.90f, 1.37f, 52.00f)
            };
            driveByLocations.Add(janeRVTrigger);

            // 9. In front of Town Hall stairs
            DriveByTrigger townHallTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(51.28f, 1.48f, 31.03f),
                radius = 2f,
                startPosition = new Vector3(29.97f, 1.37f, 81.63f),
                spawnEulerAngles = new Vector3(0f, 180f, 0f),
                endPosition = new Vector3(-12.39f, 1.37f, -40.43f)
            };
            driveByLocations.Add(townHallTrigger);

            // 10. Alley between Ham Legal and Hyland Tower
            DriveByTrigger alleyTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(84.01f, 1.46f, 63.21f),
                radius = 2f,
                startPosition = new Vector3(61.99f, 1.37f, 49.59f),
                spawnEulerAngles = new Vector3(0f, 90f, 0f),
                endPosition = new Vector3(90.05f, 5.37f, -79.52f)
            };
            driveByLocations.Add(alleyTrigger);

            // 11. Road in front of Ray's Estate and Blueball Boutique
            DriveByTrigger raysEstateTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(74.65f, 1.37f, -3.24f),
                radius = 2f,
                startPosition = new Vector3(93.06f, 2.30f, -37.39f),
                spawnEulerAngles = new Vector3(6f, 0f, 0f),
                endPosition = new Vector3(30.11f, 1.37f, 72.90f)
            };
            driveByLocations.Add(raysEstateTrigger);
            Log("Succesfully configured Drive By Triggers");
            yield return new WaitForSeconds(2f);
            if (!registered) yield break;

            Log("Configuring Drive By Vehicle and Character");
            // Object references to speed up DriveBy generation and preset values for vehicle
            Transform thomasCar = NetworkSingleton<Cartel>.Instance.transform.Find("ThomasBoxSUV");
            if (thomasCar != null)
            {
                if (!thomasCar.gameObject.activeSelf)
                    thomasCar.gameObject.SetActive(true);

                driveByVeh = thomasCar.GetComponent<LandVehicle>();
                driveByAgent = thomasCar.GetComponent<VehicleAgent>();
                driveByTp = thomasCar.GetComponent<VehicleTeleporter>();
                thomasInstance = UnityEngine.Object.FindObjectOfType<Thomas>();

                // Now configure the vehicle and agent based on testings..
                if (driveByVeh != null)
                {
                    driveByVeh.TopSpeed = 100f;
                    ParkData data = new();
                    data.spotIndex = -1; // set visible false
                    ParkingLot lot = UnityEngine.Object.FindObjectOfType<ParkingLot>(); // find any parking lot
                    data.lotGUID = new Il2CppSystem.Guid(lot.BakedGUID);
                    driveByParking = data; // Now we can use that parking lot to network the visibility + set static
                }
                else
                    MelonLogger.Warning("Drive By Vehicle is null!");

                if (driveByAgent != null)
                {
                    driveByAgent.Flags.OverriddenSpeed = 75f;
                    driveByAgent.Flags.OverrideSpeed = true;
                    driveByAgent.Flags.IgnoreTrafficLights = true;
                    driveByAgent.Flags.ObstacleMode = DriveFlags.EObstacleMode.IgnoreAll;

                    driveByAgent.turnSpeedReductionDivisor = 150f;
                    driveByAgent.turnSpeedReductionMaxRange = 20f;
                    driveByAgent.turnSpeedReductionMinRange = 6f;
                }
                else
                    MelonLogger.Warning("Drive By Vehicle Agent is null!");

                // Then configure Thomas and weapon
                if (thomasInstance != null)
                {
                    thomasInstance.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
                    yield return new WaitForSeconds(3f);
                    AvatarRangedWeapon wep = null;
                    try
                    {
                        wep = thomasInstance.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
                    } catch (InvalidCastException ex)
                    {
                        MelonLogger.Warning("Failed to Cast Thomas Gun Weapon Instance: " + ex);
                    }

                    if (wep != null)
                    {
                        wep.MaxUseRange = 45f;
                        wep.MinUseRange = 8f;
                        wep.HitChance_MaxRange = 0.4f;
                        wep.HitChance_MinRange = 0.9f;
                        wep.MaxFireRate = 0.1f;
                        wep.CooldownDuration = 0.1f;
                        wep.Damage = 33f;
                    }
                    else
                        MelonLogger.Warning("Failed to configure Thomas Gun Instance for Drive By events");
                }
                else
                    MelonLogger.Warning("Failed to configure Thomas Instance for Drive By events");

                // Lastly if the Game Object under it is inactive
                Transform boxCar = thomasCar.Find("Box SUV");
                if (!boxCar.gameObject.activeSelf)
                    boxCar.gameObject.SetActive(true);


                Log("Finished Configuring Drive By Vehicle and Character");
            }
            else
                MelonLogger.Warning("Failed to find Thomas Car Instance");

        }

        public static IEnumerator EvaluateDriveBy()
        {
            yield return new WaitForSeconds(5f);
            if (!registered) yield break;
            if (!InstanceFinder.NetworkManager.IsServer)
            {
                Log("Not Server instance, returning from Drive By Evaluation");
                yield break;
            }

            Log("Starting Drive By Evaluation");
            float elapsedSec = 0f;
            while (registered)
            {
                yield return new WaitForSeconds(1f);
                elapsedSec += 1f;
                if (!registered) yield break;
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile || driveByActive)
                {
                    yield return new WaitForSeconds(60f);
                    continue;
                }

                if (elapsedSec >= 60f)
                {
                    hoursUntilDriveBy = hoursUntilDriveBy - 1;
                    elapsedSec = elapsedSec - 60f;
                }

                // Only at 22:30 until 05:00
                if ((TimeManager.Instance.CurrentTime >= 2230 || TimeManager.Instance.CurrentTime <= 500) && hoursUntilDriveBy <= 0)
                {
                    foreach (DriveByTrigger trig in driveByLocations)
                    {
                        yield return new WaitForSeconds(1f);
                        elapsedSec += 1f;
                        if (!registered) yield break;

                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition) <= trig.radius)
                        {
                            coros.Add(MelonCoroutines.Start(BeginDriveBy(trig)));
                        }
                    }
                }
                else
                {
                    yield return new WaitForSeconds(30f);
                    elapsedSec += 30f;
                }
            }

            yield return null;
        }

        public static IEnumerator BeginDriveBy(DriveByTrigger trig)
        {
            if (driveByActive || !registered) yield break;
            driveByActive = true;
            Log("Beginning Drive By Event");
            Player player = Player.GetClosestPlayer(trig.triggerPosition, out _);

            driveByVeh.ExitPark_Networked(null, false);
            yield return new WaitForSeconds(0.1f);
            if (!registered) yield break;

            driveByVeh.SetTransform_Server(trig.startPosition, Quaternion.Euler(trig.spawnEulerAngles));
            yield return new WaitForSeconds(0.1f);
            if (!registered) yield break;

            driveByTp.MoveToRoadNetwork(false);
            yield return new WaitForSeconds(0.1f);
            if (!registered) yield break;

            driveByAgent.Navigate(trig.endPosition, null, (Il2CppScheduleOne.Vehicles.AI.VehicleAgent.NavigationCallback)DriveByNavComplete);
            driveByAgent.AutoDriving = true;

            coros.Add(MelonCoroutines.Start(DriveByShooting(player)));
            yield return null;
        }

        public static IEnumerator DriveByShooting(Player player)
        {
            float distToPlayer;
            int maxBulletsShot = UnityEngine.Random.Range(4, 9);
            int bulletsShot = 0;
            thomasInstance.Behaviour.CombatBehaviour.SetTarget(null, player.NetworkObject);
            thomasInstance.Behaviour.CombatBehaviour.SetWeaponRaised(true);
            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayerMask = LayerMask.GetMask("Terrain", "Default", "Vehicle");

            while (driveByActive)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.10f, 0.20f));
                if (!registered) yield break;
                if (bulletsShot >= maxBulletsShot) break;

                distToPlayer = Vector3.Distance(thomasInstance.transform.position, player.CenterPointTransform.position);
                Transform thomasTransform = thomasInstance.transform;
                Vector3 offsetPosition = thomasTransform.position + thomasTransform.up * 1.7f - thomasTransform.right * 0.8f;
                Vector3 toPlayer = player.CenterPointTransform.position - offsetPosition;
                float angleToPlayer = Vector3.SignedAngle(thomasInstance.transform.forward, toPlayer, Vector3.up);

                bool wepHits = false;
                RaycastHit[] hits = Physics.RaycastAll(offsetPosition, toPlayer, 50f);
                Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                foreach (RaycastHit hit in hits)
                {
                    if ((obstacleLayerMask & (1 << hit.collider.gameObject.layer)) != 0)
                    {
                        wepHits = false;
                        break;
                    }
                    else if (hit.collider.gameObject.layer == playerLayer)
                    {
                        wepHits = true;
                        break;
                    }
                }

                Log($"Angle: {angleToPlayer} - Dist: {distToPlayer} -  WeaponHits: {wepHits}");
                if (!wepHits) continue;

                if (angleToPlayer < -10f && angleToPlayer > -80f)
                {
                    if (distToPlayer < 15f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.1f || (angleToPlayer < -20f && angleToPlayer > -80f))
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 25f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.2f || ( angleToPlayer < -25f && angleToPlayer > -70f ))
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 35f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.4f || (angleToPlayer < -30f && angleToPlayer > -60f))
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 45f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.7f || (angleToPlayer < -40f && angleToPlayer > -80f) )
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (wepHits && angleToPlayer < -30f && angleToPlayer > -80f && UnityEngine.Random.Range(0f, 1f) > 0.9f)
                    {
                        thomasInstance.Behaviour.CombatBehaviour.Shoot();
                        bulletsShot++;
                    }
                }
            }
            Log($"Drive By Bullets shot: {bulletsShot}/{maxBulletsShot}");
            yield return null;
        }

        public static void DriveByNavComplete(VehicleAgent.ENavigationResult result)
        {
            if (!registered) return;
            driveByAgent.storedNavigationCallback = null;
            driveByAgent.StopNavigating();
            driveByVeh.Park_Networked(null, driveByParking);
            driveByActive = false;
            Log("Drive By Complete");
            hoursUntilDriveBy = UnityEngine.Random.Range(16, 48);
        }

        #endregion

        #region Dialogue Mini Quest for DeadDrops Logic

        public static IEnumerator InitMiniQuest()
        {
            Anna anna = UnityEngine.Object.FindObjectOfType<Anna>();
            if (anna != null)
                targetNPCs.Add(anna, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Fiona fiona = UnityEngine.Object.FindObjectOfType<Fiona>();
            if (fiona != null)
                targetNPCs.Add(fiona, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Dean dean = UnityEngine.Object.FindObjectOfType<Dean>();
            if (dean != null)
                targetNPCs.Add(dean, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Mick mick = UnityEngine.Object.FindObjectOfType<Mick>();
            if (mick != null)
                targetNPCs.Add(mick, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Jeff jeff = UnityEngine.Object.FindObjectOfType<Jeff>();
            if (jeff != null)
                targetNPCs.Add(jeff, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });
            Log("Finished Initializing MiniQuest NPCs");
            yield return null;
        }

        public static void OnDayPassNewDiag()
        {
            foreach (NPC npc in targetNPCs.Keys.ToList())
            {
                targetNPCs[npc].HasAskedQuestToday = false;
            }
        }

        public static IEnumerator EvaluateMiniQuestCreation()
        {
            Log("Starting Mini Quest Dialogue Random Generation");
            while (registered)
            {
                List<NPC> listOf = targetNPCs.Keys.ToList();
                NPC random = listOf[UnityEngine.Random.Range(0, listOf.Count)];
                if (targetNPCs.ContainsKey(random))
                {
                    if (!targetNPCs[random].HasActiveQuest && !targetNPCs[random].HasAskedQuestToday)
                    {
                        targetNPCs[random].HasActiveQuest = true;
                        InitMiniQuestDialogue(random);
                    }
                }
                yield return new WaitForSeconds(UnityEngine.Random.Range(480f, 960f));
            }
        }

        public static void InitMiniQuestDialogue(NPC npc)
        {
            DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "";
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    text = "Have you heard anything new about the Benzies?";
                    break;

                case 1:
                    text = "Any rumours about the Cartel you could share?";
                    break;

                case 2:
                    text = "What's the word around town? I need info on the Benzies.";
                    break;
            }


            choice.ChoiceText = $"{text} <color=#FF3008>(Bribe -$100)</color>";
            choice.Enabled = true;
            void OnMiniQuestChosenWrapped()
            {
                OnMiniQuestChosen(choice, npc, controller);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnMiniQuestChosenWrapped);
            int index = controller.AddDialogueChoice(choice);
            Log("Created Mini Quest Dialogue for: " + npc.FirstName);
            return;
        }

        public static void OnMiniQuestChosen(DialogueController.DialogueChoice choice, NPC npc, DialogueController controller)
        {

            Log("Option Chosen");
            if (UnityEngine.Random.Range(0f, 1f) > 0.30f || NetworkSingleton<MoneyManager>.Instance.cashBalance < 101f)
            {
                Log("RefuseQuestGive");
                npc.PlayVO(EVOLineType.Annoyed, false);
                npc.Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 10f, 1);
                Log("SubmitContinue");
                controller.handler.ContinueSubmitted();
                Log("ShowText");
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        controller.handler.WorldspaceRend.ShowText($"I've heard nothing...", 15f);
                        break;

                    case 1:
                        controller.handler.WorldspaceRend.ShowText($"No! Leave me alone!", 15f);
                        break;

                    case 2:
                        controller.handler.WorldspaceRend.ShowText($"I'm afraid to talk about it...", 15f);
                        break;
                }

            }
            else // Start mini quest
            {
                Log("Start Quest");
                List<DeadDrop> drops = new();
                for (int i = 0; i < DeadDrop.DeadDrops.Count; i++)
                {
                    if (DeadDrop.DeadDrops[i].Storage.ItemCount == 0)
                        drops.Add(DeadDrop.DeadDrops[i]);
                }

                DeadDrop random = drops[UnityEngine.Random.Range(0, drops.Count)];
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-100f, true, false);

                string location = "";
                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                    location = random.Region.ToString() + " region";
                else
                    location = random.DeadDropName;

                ItemInstance item;
                int qty;
                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                {
                    ItemDefinition def = Registry.GetItem(commonDrops[UnityEngine.Random.Range(0, commonDrops.Count)]);
                    qty = UnityEngine.Random.Range(3, 11);
                    item = def.GetDefaultInstance(qty);
                }
                else
                {
                    ItemDefinition def = Registry.GetItem(rareDrops[UnityEngine.Random.Range(0, rareDrops.Count)]);
                    qty = 1;
                    item = def.GetDefaultInstance(qty);
                }

                coros.Add(MelonCoroutines.Start(CreateDropContent(random, item, npc)));
                controller.handler.ContinueSubmitted();
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 0:
                        controller.handler.WorldspaceRend.ShowText($"I heard them talk about some drop around {location}...", 15f);
                        break;

                    case 1:
                        controller.handler.WorldspaceRend.ShowText($"There are rumours about suspicious actions near {location}!", 15f);
                        break;

                    case 2:
                        controller.handler.WorldspaceRend.ShowText($"Yes! I heard them stash something near {location}! You didn't hear this from me, okay?", 15f);
                        break;

                    case 3:
                        controller.handler.WorldspaceRend.ShowText($"I saw one of them hide something in a dead drop around {location}.", 15f);
                        break;

                    case 4:
                        controller.handler.WorldspaceRend.ShowText($"Yes and don't come asking anymore! They have been dealing at {location}.", 15f);
                        break;
                }
            }
            targetNPCs[npc].HasActiveQuest = false;
            targetNPCs[npc].HasAskedQuestToday = true;
            coros.Add(MelonCoroutines.Start(DisposeChoice(controller, npc)));
            return;
        }

        public static IEnumerator DisposeChoice(DialogueController controller, NPC npc)
        {
            yield return new WaitForSeconds(0.4f);
            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(oldChoices.Count - 1);
            controller.Choices = oldChoices;
            Log("Disposed Choice");
            yield return null;
        }

        public static IEnumerator CreateDropContent(DeadDrop entity, ItemInstance filledItem, NPC npc)
        {
            yield return new WaitForSeconds(5f);
            entity.Storage.InsertItem(filledItem, true);

            Log($"MiniQuest Drop at: {entity.DeadDropName}");
            Log($"MiniQuest Reward: {filledItem.Name} x {filledItem.Quantity}");

            bool opened = false;
            UnityEngine.Events.UnityAction onOpenedAction = null;
            void WrapOnOpenCallback()
            {
                Log("Quest Complete");
                NetworkSingleton<LevelManager>.Instance.AddXP(100);
                opened = true;
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(entity.Region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, -0.025f);
                }
                entity.Storage.onOpened.RemoveListener(onOpenedAction);
            }
            onOpenedAction = (UnityEngine.Events.UnityAction)WrapOnOpenCallback;
            entity.Storage.onOpened.AddListener(onOpenedAction);

            float duration = UnityEngine.Random.Range(30f, 120f);
            yield return new WaitForSeconds(duration);
            if (entity.Storage.ItemSlots[0].ItemInstance == filledItem)
                entity.Storage.ItemSlots[0].ClearStoredInstance();

            if (!opened)
            {
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(entity.Region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, 0.050f);
                }
            }

            entity.Storage.onOpened.RemoveListener(onOpenedAction);
            Log($"Removed MiniQuest Reward. Quest Duration: {duration}");
            yield return null;
        }

        #endregion

        #region Debug Mode Content
        public static void Log(string msg)
        {
            if (currentConfig.debugMode)
                MelonLogger.Msg(msg);
        }

        // Debug tool starts instant driveby on nearest and logs info
        public static IEnumerator OnInputStartDriveBy()
        {
            Log("Starting Instant Drive By");
            Player.Local.Health.RecoverHealth(100f);
            float nearest = 150f;
            DriveByTrigger trig = null;
            foreach (DriveByTrigger trigItem in driveByLocations)
            {
                float distanceTo = Vector3.Distance(Player.Local.CenterPointTransform.position, trigItem.triggerPosition);
                if (distanceTo <= nearest) {
                    trig = trigItem;
                    nearest = distanceTo;
                }
            }

            Log("Nearest Drive By Trigger");
            Log($"Distance: {Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition)}");
            Log($"In Radius: {Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition) <= trig.radius}");
            coros.Add(MelonCoroutines.Start(BeginDriveBy(trig)));
            yield return new WaitForSeconds(1f);
            debounce = false;
            yield break;
        }
        // Debug mode try to rob nearest dealer to test functionality
        public static IEnumerator OnInputStartRob()
        {
            Log("TestTryRob");

            Transform playerLocal = Player.Local.transform;
            Dealer[] allDealers = UnityEngine.Object.FindObjectsOfType<Dealer>(true);
            List<Dealer> regularDealers = new List<Dealer>();
            foreach (Dealer d in allDealers)
            {
                yield return new WaitForSeconds(0.1f);
                if (d.GetType() == typeof(Dealer))
                {
                    regularDealers.Add(d);
                }
            }

            Dealer nearest = null;
            float distanceToP = 100f;
            foreach (Dealer d in regularDealers)
            {
                yield return new WaitForSeconds(0.1f);
                float dist = Vector3.Distance(d.transform.position, playerLocal.position);
                if (dist < distanceToP)
                {
                    distanceToP = dist;
                    nearest = d;
                }
            }

            nearest.TryRobDealer();
            yield return new WaitForSeconds(1f);
            debounce = false;

            yield return null;
        }
        public static IEnumerator OnInputGiveMiniQuest()
        {
            List<NPC> listOf = targetNPCs.Keys.ToList();
            NPC random = listOf[UnityEngine.Random.Range(0, listOf.Count)];
            if (targetNPCs.ContainsKey(random))
            {
                if (!targetNPCs[random].HasActiveQuest && !targetNPCs[random].HasAskedQuestToday)
                {
                    targetNPCs[random].HasActiveQuest = true;
                    InitMiniQuestDialogue(random);
                }
            }
            yield return new WaitForSeconds(1f);
            debounce = false;
            yield return null;
        }

        public static IEnumerator MakeUI()
        {
            _playerTransform = Player.Local.CenterPointTransform;
            HUD hud = Singleton<HUD>.Instance;
            _positionText = new GameObject("PlayerPositionText").AddComponent<TextMeshProUGUI>();
            _positionText.transform.SetParent(hud.canvas.transform, false);
            _positionText.alignment = TextAlignmentOptions.TopLeft;
            _positionText.fontSize = 16;
            _positionText.color = Color.red;
            _positionText.rectTransform.anchorMin = new Vector2(0, 1);
            _positionText.rectTransform.anchorMax = new Vector2(0, 1);
            _positionText.rectTransform.pivot = new Vector2(0, 1);
            _positionText.rectTransform.anchoredPosition = new Vector2(40, -40);
            yield return null;
        }
        public static IEnumerator SpawnAmbushAreaVisual()
        {
            Log("Spawning Debug visuals for Ambush Areas");
            Shader standardShader = Shader.Find("Unlit/Color");
            if (standardShader == null)
            {
                standardShader = Shader.Find("Standard");
            }

            // Create materials once
            Dictionary<EMapRegion, Material> regionMaterials = new Dictionary<EMapRegion, Material>();
            foreach (EMapRegion region in Enum.GetValues(typeof(EMapRegion)))
            {
                Material mat = new Material(standardShader);
                mat.color = GetColorCorrespondance(region);
                regionMaterials[region] = mat;
            }

            Material capsuleMaterial = new Material(standardShader);
            capsuleMaterial.color = Color.cyan;

            CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
            foreach (CartelRegionActivities act in regAct)
            {
                foreach (CartelAmbushLocation loc in act.AmbushLocations)
                {
                    float rad = loc.DetectionRadius;

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    MeshRenderer mr = cube.GetComponent<MeshRenderer>();

                    if (regionMaterials.TryGetValue(act.Region, out Material cubeMaterial))
                    {
                        mr.material = cubeMaterial;
                    }
                    else
                    {
                        mr.material = new Material(standardShader);
                        mr.material.color = Color.white;
                    }

                    mr.receiveShadows = false;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    cube.transform.parent = Map.Instance.transform;
                    cube.transform.localScale = new Vector3(rad, rad, rad);
                    cube.transform.position = loc.transform.position + new Vector3(0, 25f+rad, 0);
                    cube.SetActive(true);

                    foreach (Transform tr in loc.AmbushPoints)
                    {
                        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        MeshRenderer mrc = capsule.GetComponent<MeshRenderer>();
                        mrc.material = capsuleMaterial;
                        mrc.receiveShadows = false;
                        mrc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        capsule.transform.position = tr.transform.position + new Vector3(0, 20f, 0);
                        capsule.transform.parent = cube.transform;

                        Vector3 desiredCapsuleWorldScale = new Vector3(0.2f, 12f, 0.2f);
                        Vector3 cubeWorldScale = cube.transform.lossyScale;

                        capsule.transform.localScale = new Vector3(
                            desiredCapsuleWorldScale.x / cubeWorldScale.x,
                            desiredCapsuleWorldScale.y / cubeWorldScale.y,
                            desiredCapsuleWorldScale.z / cubeWorldScale.z
                        );
                        
                        capsule.SetActive(true);
                    }
                }
            }
            yield return null;
        }
        public static IEnumerator SpawnDriveByAreaVisual()
        {
            Log("Spawning Debug visuals for Drive By Triggers");
            // Shader select order
            Shader standardShader = Shader.Find("Unlit/Color");
            if (standardShader == null)
                standardShader = Shader.Find("Standard");

            Material sphereMaterial = new Material(standardShader);
            sphereMaterial.color = new Color(255f / 255f, 145f / 255f, 0f / 255f);

            foreach (DriveByTrigger trig in driveByLocations)
            {
                float rad = trig.radius;
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                MeshRenderer mr = sphere.GetComponent<MeshRenderer>();

                mr.material = sphereMaterial;
                mr.receiveShadows = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                sphere.transform.parent = Map.Instance.transform;
                sphere.transform.localScale = new Vector3(rad * 2, rad * 2, rad * 2);
                sphere.transform.position = trig.triggerPosition + new Vector3(0, 20f+rad*2, 0);
                sphere.SetActive(true);
            }
            yield break;
        }
        static Color GetColorCorrespondance(EMapRegion reg)
        {
            switch (reg)
            {
                case EMapRegion.Northtown:
                    return Color.yellow;

                case EMapRegion.Westville:
                    return Color.blue;

                case EMapRegion.Downtown:
                    return Color.red;

                case EMapRegion.Docks:
                    return Color.green;

                case EMapRegion.Suburbia:
                    return Color.magenta;

                case EMapRegion.Uptown:
                    return Color.black;

                default:
                    return Color.white;
            }
        }
        #endregion

    }
}