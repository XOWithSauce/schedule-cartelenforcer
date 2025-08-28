using System.Collections;
using HarmonyLib;
using Il2Cpp;
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
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.AI;
using Il2CppScheduleOne.VoiceOver;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.UI;

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
        public const string Version = "1.3.1";
        public const string DownloadLink = null;
    }

    #region Persistent JSON Files and Serialization
    public class ModConfig
    {
        public bool debugMode = false; // While in debug mode, spawn visuals for Cartel Ambushes, Enable Debug Log Messages, etc.

        // From -1.0 to 0.0 to 1.0,
        // -1.0= Activity is 10 times less frequent
        // 0.0 = Activity is at game default frequency 
        // 1.0 = Activity is 10 times more frequent
        public float activityFrequency = 0.0f; 

        // From -1.0 to 0.0 to 1.0,
        // -1.0 = Activity influence requirement is 100% Less (Means cartel influence requirement is at 0 and activities happen always)
        // 0.0  = Activity influence requirement is at Game Default
        // 1.0  = Activity influence requirement is 100% More (Means cartel influence will only happen if at maximum regional cartel influence)
        public float activityInfluenceMin = 0.0f;

        // All four follow same pattern:
        // Basically different from the Activity Frequency parameter, because this specifically states how often at maximum something can happen
        // Whereas the ActivityFrequency simulates hours passing at a faster or slower pace. This Patches the "Start" method to block if condition is not met!
        // from -1.0 to 0.0 to 1.0
        // -1.0 = Activity for this specific type can happen only once every 4 days
        // 0.0 = Activity for this specific type can happen only once every 2 days
        // 1.0 = Activity for this specific type can happen every hour
        public float ambushFrequency = 1.0f;
        public float deadDropStealFrequency = 1.0f;
        public float cartelCustomerDealFrequency = 1.0f; // NOTE: NOT the "Truced" deals, but the one where CartelDealer goes to customers on the map
        public float cartelRobberyFrequency = 1.0f;

        public bool driveByEnabled = true;

        public bool realRobberyEnabled = true;

        public bool miniQuestsEnabled = true;

        public bool interceptDeals = true;
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
        private static string pathCartelStolen = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "CartelItems"); // Filename {organization}.json
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

        #region Persistence for Cartel Stolen Items
        public static string SanitizeAndFormatName(string orgName)
        {
            string stolenItemsFileName = orgName;

            if (stolenItemsFileName != null)
            {
                stolenItemsFileName = stolenItemsFileName.Replace(" ", "_").ToLower();
                stolenItemsFileName = stolenItemsFileName.Replace(",", "");
                stolenItemsFileName = stolenItemsFileName.Replace(".", "");
                stolenItemsFileName = stolenItemsFileName.Replace("<", "");
                stolenItemsFileName = stolenItemsFileName.Replace(">", "");
                stolenItemsFileName = stolenItemsFileName.Replace(":", "");
                stolenItemsFileName = stolenItemsFileName.Replace("\"", "");
                stolenItemsFileName = stolenItemsFileName.Replace("/", "");
                stolenItemsFileName = stolenItemsFileName.Replace("\\", "");
                stolenItemsFileName = stolenItemsFileName.Replace("|", "");
                stolenItemsFileName = stolenItemsFileName.Replace("?", "");
                stolenItemsFileName = stolenItemsFileName.Replace("*", "");
            }
            stolenItemsFileName = stolenItemsFileName + ".json";
            return stolenItemsFileName;
        }
        public class SerializeStolenItems
        {
            public string ID;
            public int Quality;
            public int Quantity;
        }
        public class StolenItemsList
        {
            public List<SerializeStolenItems> items;
        }
        public static List<QualityItemInstance> LoadStolenItems()
        {
            StolenItemsList stolenItems;
            string orgName = LoadManager.Instance.ActiveSaveInfo?.OrganisationName;
            string fileName = SanitizeAndFormatName(orgName);
            if (File.Exists(Path.Combine(pathCartelStolen, fileName)))
            {
                try
                {
                    string json = File.ReadAllText(Path.Combine(pathCartelStolen, fileName));
                    stolenItems = JsonConvert.DeserializeObject<StolenItemsList>(json);
                }
                catch (Exception ex)
                {
                    stolenItems = new();
                    MelonLogger.Warning("Failed to read Cartel Stolen Items data: " + ex);
                }
            }
            else
            {
                stolenItems = new();
            }
            List<QualityItemInstance> newQualityItemList = new List<QualityItemInstance>();
            if (stolenItems.items != null && stolenItems.items.Count > 0)
            {
                foreach (SerializeStolenItems seri in stolenItems.items)
                {
                    ItemDefinition def = Registry.GetItem(seri.ID);
                    ItemInstance item = def.GetDefaultInstance(seri.Quantity);
                    switch (seri.Quality)
                    {
                        case 0:
                            (item as QualityItemInstance).Quality = EQuality.Trash;
                            break;
                        case 1:
                            (item as QualityItemInstance).Quality = EQuality.Poor;
                            break;
                        case 2:
                            (item as QualityItemInstance).Quality = EQuality.Standard;
                            break;
                        case 3:
                            (item as QualityItemInstance).Quality = EQuality.Premium;
                            break;
                        case 4:
                            (item as QualityItemInstance).Quality = EQuality.Heavenly;
                            break;

                    }
                    newQualityItemList.Add((item as QualityItemInstance));
                }
            }
            return newQualityItemList;
        }

        public static void Save(List<QualityItemInstance> stolenItems)
        {
            StolenItemsList itemsList = new();
            itemsList.items = new();

            foreach (QualityItemInstance item in stolenItems)
            {
                SerializeStolenItems newItem = new() { ID = item.ID, Quality = (int)item.Quality, Quantity = item.Quantity };
                itemsList.items.Add(newItem);
            }
            try
            {
                string orgName = LoadManager.Instance.ActiveSaveInfo?.OrganisationName;
                string fileName = SanitizeAndFormatName(orgName);
                string saveDestination = Path.Combine(pathCartelStolen, fileName);
                string json = JsonConvert.SerializeObject(itemsList, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(saveDestination));
                File.WriteAllText(saveDestination, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save Cartel Stolen Items data: " + ex);
            }
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
        public static List<QualityItemInstance> cartelStolenItems;
        private static readonly object cartelItemLock = new object(); // for above list

        public static List<HrPassParameterMap> actFreqMapping = new();
        public static List<CartelRegActivityHours> regActivityHours = new();

        public static List<DriveByTrigger> driveByLocations = new();

        public static List<object> coros = new();

        static bool registered = false;
        private bool firstTimeLoad = false;
        static bool debounce = false; // Keyboard Input
        static bool interceptingDeal = false;

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

        // UI Elements to save Sprites and Colors for changing Quest icon when intercepted
        public static Color questIconBack;
        public static Sprite handshake;
        public static Sprite benziesLogo;

        // Track current intercepted contract GUID
        public static List<string> contractGuids = new();

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
                    interceptingDeal = false;
                    driveByLocations.Clear();
                    regActivityHours.Clear();
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

            cartelStolenItems = ConfigLoader.LoadStolenItems();

            if (currentConfig.driveByEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateDriveBy()));

            coros.Add(MelonCoroutines.Start(InitializeAmbush()));

            if (currentConfig.miniQuestsEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateMiniQuest()));

            if (currentConfig.interceptDeals)
            {
                coros.Add(MelonCoroutines.Start(FetchUIElementsInit()));
                coros.Add(MelonCoroutines.Start(EvaluateCartelIntercepts()));
            }

            if (currentConfig.debugMode)
                MelonCoroutines.Start(MakeUI());
        }

        public static IEnumerator InitializeAndEvaluateDriveBy()
        {
            yield return MelonCoroutines.Start(InitializeDriveByData());


            coros.Add(MelonCoroutines.Start(EvaluateDriveBy()));
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnDriveByAreaVisual());
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

            coros.Add(MelonCoroutines.Start(TickOverrideHourPass()));
            Log("Adding HourPass Function to callbacks");
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)OnHourPassReduceCartelRegActHours;
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnAmbushAreaVisual());
        }

        public static IEnumerator InitializeAndEvaluateMiniQuest()
        {
            yield return InitMiniQuest();
            Log("Adding DayPass Function for Mini Quest");
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
            // Left CTRL + L to Log Big Blop of info
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.L))
            {
                if (!debounce)
                {
                    debounce = true;
                    MelonCoroutines.Start(OnInputInternalLog());
                }
            }

            // Left CTRL + I (INVENTORY) to Log Big Blop of info
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.I))
            {
                debounce = true;
                for (int i = 0; i < Player.Local.Inventory.Count(); i++)
                {
                    if (Player.Local.Inventory[i].ItemInstance != null)
                    {
                        Log($"{Player.Local.Inventory[i].ItemInstance.ID}");
                        Log($"Quality: 0 Low, 5 Highest - {(Player.Local.Inventory[i].ItemInstance as QualityItemInstance).Quality}");
                        Log($"Amount: {Player.Local.Inventory[i].ItemInstance.Quantity}");

                        if (Player.Local.Inventory[i].ItemInstance is ProductItemInstance inst)
                        {
                            if (inst != null && inst.ID != null)
                                Log($"AppliedPackaging ID: {inst.AppliedPackaging.ID}");
                        }
                    }
                }
                debounce = false;
            }

            // Left CTRL + T Intercept random deal
            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.T))
            {
                debounce = true;
                MelonCoroutines.Start(OnInputInterceptContract());
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
            interceptingDeal = false;
            actFreqMapping.Clear();
            regActivityHours.Clear();
            targetNPCs.Clear();
        }

        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { typeof(string) })]
        public static class SaveManager_Save_String_Patch
        {
            public static bool Prefix(SaveManager __instance, string saveFolderPath)
            {
                ConfigLoader.Save(cartelStolenItems);
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
            public static bool Prefix(LoadManager __instance, SaveInfo autoLoadSave = null, Il2CppScheduleOne.UI.MainMenu.MainMenuPopup.Data mainMenuPopup = null, bool preventLeaveLobby = false)
            {
                ConfigLoader.Save(cartelStolenItems);
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

        #region Load and Apply Serialized Ambush Data
        public static IEnumerator ApplyGameDefaultAmbush()
        {
            yield return new WaitForSeconds(5f);
            if (!registered) yield break;

            ambushConfig = ConfigLoader.LoadAmbushConfig();
            gameDefaultAmbush = ConfigLoader.LoadDefaultAmbushConfig();
            Log("Loaded Ambush Config Data");

            CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
            Log("Applying Game Defaults Cartel Ambushes");
            int i = 0;
            foreach (CartelRegionActivities act in regAct)
            {
                foreach (CartelAmbushLocation loc in act.AmbushLocations)
                {
                    List<Vector3> defaultData = loc.AmbushPoints.Select(tr => tr.position).ToList();
                    NewAmbushConfig loadedConfig = gameDefaultAmbush.addedAmbushes.ElementAt(i);
                    Log($"  Checking Default Ambush {i}");
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
            if (!registered) yield break;

            Log("Adding User Modded Ambushes to existing ones");
            CartelRegionActivities[] regAct = NetworkSingleton<Cartel>.Instance.Activities.RegionalActivities;
            int i = 1;
            if (ambushConfig.addedAmbushes != null && ambushConfig.addedAmbushes.Count > 0)
            {
                foreach (NewAmbushConfig config in ambushConfig.addedAmbushes)
                {
                    CartelRegionActivities regActivity = regAct.FirstOrDefault(act => (int)act.Region == config.mapRegion);

                    Log($"  Generating Ambush object {i} in region: {regActivity.Region}");
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
            public string itemDesc { get; set; }
            public Func<int> Getter { get; set; }
            public Action<int> Setter { get; set; }
            public Action HourPassAction { get; set; }
            public int modTicksPassed { get; set; }
            public int currentModHours { get; set; }
            public Func<bool> CanPassHour { get; set; }
        }

        // Because we want to be able to change the individual activity in region frequency we must cast types to check
        // Otherwise same randomization as in the source code originally
        // Just adds hours structure into the inner activity which can block...
        // Not just the RegionalActivity, Ambush Globals also added
        public class CartelRegActivityHours
        {
            public int region; // Identifier integer maps out to region, but -1 is global
            public int cartelActivityClass = 0; // This will hold the DeadDropSteal (0) class, CartelCustomerDeal (1) or RobDealer (2)
            public int hoursUntilEnable = 0; // ingame hours (60sec)

        }

        // Helper function for populating activityhours
        public static int GetActivityHours(float configValue)
        {
            int hours = 0;
            if (configValue > 0.0f) // 2 days at 0.0 -> every hour at 1.0
            {
                int startValue = 48;
                int endValue = 1;

                hours = Mathf.RoundToInt(Mathf.Lerp((float)startValue, (float)endValue, configValue));
            }
            else if (configValue < 0.0f) // 2 days at 0.0 -> every 4 days at -1.0
            {
                int startValue = 48;
                int endValue = 96;
                // we flip because its negative
                float t = -configValue;
                hours = Mathf.RoundToInt(Mathf.Lerp((float)startValue, (float)endValue, t));
            }
            else
            {
                hours = 48;
            }
            return hours;
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
                itemDesc = "Global Activities",
                Getter = () => instanceActivities.HoursUntilNextGlobalActivity,
                Setter = (value) => instanceActivities.HoursUntilNextGlobalActivity = value,
                HourPassAction = () => instanceActivities.HourPass(),
                modTicksPassed = 0,
                currentModHours = instanceActivities.HoursUntilNextGlobalActivity,
                CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
            });
            indexCurrent++;
            // Add above to the custom activity hours
            CartelRegActivityHours activityGlobalHrs = new();
            activityGlobalHrs.region = -1; // Global Activity Ambush has region index -1
            activityGlobalHrs.hoursUntilEnable = GetActivityHours(currentConfig.ambushFrequency);
            activityGlobalHrs.cartelActivityClass = -1; // -1 reserved for the global ambushes
            regActivityHours.Add(activityGlobalHrs); // Always first element

            CartelRegionActivities[] regInstanceActivies = NetworkSingleton<Cartel>.Instance.Activities.RegionalActivities;
            foreach (CartelRegionActivities act in regInstanceActivies)
            {
                actFreqMapping.Add(new HrPassParameterMap
                {
                    itemDesc = $"Cartel Regional Activities ({act.Region.ToString()})",
                    Getter = () => act.HoursUntilNextActivity,
                    Setter = (value) => act.HoursUntilNextActivity = value,
                    HourPassAction = () => act.HourPass(),
                    modTicksPassed = 0,
                    currentModHours = act.HoursUntilNextActivity,
                    CanPassHour = () => NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile
                });
                indexCurrent++;
                Log($"  {act.Region.ToString()} - Parsing Inner Activities");
                foreach (CartelActivity inRegAct in act.Activities)
                {
                    CartelRegActivityHours activityHrs = new();
                    activityHrs.region = (int)act.Region;

                    int hours = 0;
                    // Now determine class
                    if (inRegAct is StealDeadDrop)
                    {
                        activityHrs.cartelActivityClass = 0;
                        hours = GetActivityHours(currentConfig.deadDropStealFrequency);
                    }
                    else if (inRegAct is CartelCustomerDeal)
                    {
                        activityHrs.cartelActivityClass = 1;
                        hours = GetActivityHours(currentConfig.cartelCustomerDealFrequency);

                    }
                    else // else its RobDealer class
                    {
                        activityHrs.cartelActivityClass = 2;
                        hours = GetActivityHours(currentConfig.cartelRobberyFrequency);
                    }

                    activityHrs.hoursUntilEnable = hours;

                    regActivityHours.Add(activityHrs);
                    Log($"    {act.Region.ToString()} - {activityHrs.cartelActivityClass} class Added to regActivityHours");
                }
            }

            CartelDealManager instanceDealMgr = NetworkSingleton<Cartel>.Instance.DealManager;
            actFreqMapping.Add(new HrPassParameterMap
            {
                itemDesc = "Cartel Deal Manager (Truced only)",
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
                itemDesc = "Drive-By Events",
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

        // Tie the new class into basic hour pass
        public static void OnHourPassReduceCartelRegActHours()
        {
            if (regActivityHours.Count > 0)
            {
                foreach (CartelRegActivityHours regActHrs in regActivityHours)
                {
                    int resultHrs = Mathf.Clamp(regActHrs.hoursUntilEnable - 1, 0, 96);
                    regActHrs.hoursUntilEnable = resultHrs;
                }
            }
            else
            {
                //Log("Region Activity Hours are Not Mapped in HourPass");
            }
        }

        // Basically same as in original source code but patched to obey the global activity frequency cap of mod
        [HarmonyPatch(typeof(CartelActivities), "TryStartActivity")]
        public static class CartelActivities_TryStartActivityPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(CartelActivities __instance)
            {
                Log("[GLOBACT] TryStartGlobalActivity");
                __instance.HoursUntilNextGlobalActivity = CartelActivities.GetNewCooldown();
                if (!__instance.CanNewActivityBegin())
                {
                    Log("[GLOBACT]    NewActivity Cant Begin");
                    return false;
                }
                List<CartelActivity> activitiesReadyToStart = new(); // Normally just assing the activities ready to start but here in il2cpp world we need to actually have buttsex and run for each loop to assign each element to the list its so dumb
                foreach (CartelActivity actItem in __instance.GetActivitiesReadyToStart())
                {
                    activitiesReadyToStart.Add(actItem);
                }
                List<EMapRegion> validRegionsForActivity = new(); // Same thing here the types mismatch for iterable
                foreach (EMapRegion reg in __instance.GetValidRegionsForActivity())
                {
                    validRegionsForActivity.Add(reg);
                }

                if (activitiesReadyToStart.Count == 0 || validRegionsForActivity.Count == 0)
                {
                    Log("[GLOBACT]    No Activities or Regions ready to start");
                    return false;
                }
                Log($"[GLOBACT]    Total Activities ready to start: {activitiesReadyToStart.Count}");
                validRegionsForActivity.Sort((EMapRegion a, EMapRegion b) => NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(b).CompareTo(NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(a)));
                EMapRegion region = EMapRegion.Northtown;
                bool flag = false;
                foreach (EMapRegion emapRegion in validRegionsForActivity)
                {
                    float influence = NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(emapRegion);
                    // This part is modified to obey the influence mod
                    float mult = 0f;
                    float result = 0f;
                    if (currentConfig.activityInfluenceMin == 0.0f)
                    {
                        //per original source code
                        mult = 0.7f;
                        result = influence * mult; // this is actually division, only 70% of original influence
                                                   // And then if result is higher than 0..1 ranged rand
                                                   // Per original source code Random value 0..1 is smaller than result
                        if (UnityEngine.Random.Range(0f, 1f) < result)
                        {
                            region = emapRegion;
                            flag = true;
                            break;
                        }
                    }
                    else if (currentConfig.activityInfluenceMin > 0.0f)
                    {
                        result = Mathf.Lerp(influence * 0.7f, 1f, currentConfig.activityInfluenceMin);
                        if (UnityEngine.Random.Range(0f, 1f) > result)
                        {
                            region = emapRegion;
                            flag = true;
                            break;
                        }
                    }
                    else
                    {
                        // flip because negative
                        float t = -currentConfig.activityInfluenceMin;
                        // now if activityInfluenceMin was -1.0, it becomes t=1 so that multiplier is always 0f
                        // meaning that the random range check will always return true
                        mult = Mathf.Lerp(1f, 0f, currentConfig.activityInfluenceMin);
                        result = influence * mult;
                        if (UnityEngine.Random.Range(0f, 1f) > result)
                        {
                            region = emapRegion;
                            flag = true;
                            break;
                        }
                    }

                }
                if (!flag)
                {
                    Log("[GLOBACT]    Ambush Random Roll not triggered");
                    return false;
                }
                Log("[GLOBACT]    Check the Ambush hours");
                // Now we check that the activity activation obeys to the ambush in config
                // Element at 0 is always the timer for Ambush global activity
                if (regActivityHours[0].hoursUntilEnable > 0)
                {
                    Log("[GLOBACT]    Ambush not ready");
                    return false;
                }


                int readyCount = activitiesReadyToStart.Count;
                Log($"[GLOBACT]    Ambush Pos ReadyCount: {readyCount}");
                do
                {
                    readyCount = activitiesReadyToStart.Count;
                    if (readyCount == 0) break;

                    int activityIndex = UnityEngine.Random.Range(0, readyCount);
                    if (activitiesReadyToStart[activityIndex].IsRegionValidForActivity(region))
                    {
                        int indexInActivities = NetworkSingleton<Cartel>.Instance.Activities.GlobalActivities.IndexOf(activitiesReadyToStart[activityIndex]); // This causes deadlock when passed to global activity?? for now set to 0
                        if (indexInActivities != -1)
                        {
                            Log($"[GLOBACT]   idx: {indexInActivities} | {activitiesReadyToStart[activityIndex].name}");
                            Log("[GLOBACT]    Start Global Activity");
                            NetworkSingleton<Cartel>.Instance.Activities.StartGlobalActivity(null, region, 0);
                            regActivityHours[0].hoursUntilEnable = GetActivityHours(currentConfig.ambushFrequency);
                            break;
                        }
                    }
                    else
                    {
                        activitiesReadyToStart.Remove(activitiesReadyToStart[activityIndex]);
                    }

                } while (readyCount != 0);

                Log("[GLOBACT] TryStartGlobalActivity Finished");

                return false; // Always block since patch handles the original source code
            }
        }

        [HarmonyPatch(typeof(CartelRegionActivities), "TryStartActivity")]
        public static class CartelRegionActivities_TryStartActivityPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(CartelRegionActivities __instance)
            {
                __instance.HoursUntilNextActivity = CartelRegionActivities.GetNewCooldown(__instance.Region);
                Log("[REGACT] TryStartRegionalActivity");
                List<CartelActivity> list = new();
                foreach (CartelActivity activity in __instance.Activities)
                    list.Add(activity);

                // Maps out indexes in the reg act hours
                List<int> foundMatch = new();
                for (int i = 0; i < regActivityHours.Count; i++)
                {
                    if (regActivityHours[i].region == (int)__instance.Region)
                    {
                        foundMatch.Add(i);
                    }
                }
                Dictionary<CartelActivity, List<int>> enabledActivities = new(); // List Int first element = index at regActivityHours, second element is actInt
                // parse activity int
                foreach (CartelActivity inRegAct in list)
                {
                    int actInt = 0;
                    if (inRegAct is StealDeadDrop)
                        actInt = 0;
                    else if (inRegAct is CartelCustomerDeal)
                        actInt = 1;
                    else // else its RobDealer class
                        actInt = 2;

                    for (int i = 0; i < foundMatch.Count; i++)
                    {
                        if (regActivityHours[foundMatch[i]].cartelActivityClass == actInt)
                        {
                            if (regActivityHours[foundMatch[i]].hoursUntilEnable <= 0)
                            {
                                if (!enabledActivities.ContainsKey(inRegAct))
                                {
                                    List<int> indexAndActInt = new() { foundMatch[i], actInt };
                                    enabledActivities.Add(inRegAct, indexAndActInt);
                                }
                            }
                        }
                    }
                }

                if (enabledActivities.Count == 0)
                {
                    Log("[REGACT]    No Regional Activities can be enabled at this moment");
                    return false;
                }

                int enabledCount = enabledActivities.Count;
                Log("[REGACT]    Enabled Activities Count: " + enabledCount);
                do
                {
                    enabledCount = enabledActivities.Count;
                    if (enabledCount == 0) break;

                    KeyValuePair<CartelActivity, List<int>> selected = enabledActivities.ElementAtOrDefault(UnityEngine.Random.Range(0, enabledCount));
                    if (selected.Key.IsRegionValidForActivity(__instance.Region))
                    {
                        __instance.StartActivity(null, __instance.Activities.IndexOf(selected.Key));
                        Log("[REGACT]    Starting Activity!");
                        if (selected.Value[1] == 0)// StealDeadDrop
                        {
                            regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.deadDropStealFrequency);
                        }
                        else if (selected.Value[1] == 1)// CartelCustomerDeals
                        {
                            regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.cartelCustomerDealFrequency);
                        }
                        else if (selected.Value[1] == 2)// RobDealer
                        {

                            regActivityHours[selected.Value[0]].hoursUntilEnable = GetActivityHours(currentConfig.cartelRobberyFrequency);
                        }

                        // Finally break
                        break;
                    }
                    else
                    {
                        enabledActivities.Remove(selected.Key);
                    }
                } while (enabledCount != 0);

                Log("[REGACT] Finished TryStartRegionalActivity");

                return false; // Just block running the original function with shuffle as described in comments
            }
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
            Log("[HOURPASS] Starting HourPass Override, Tick once every " + tickRate + " seconds");

            while (registered)
            {
                if (!registered) yield break;
                if (actFreqMapping.Count == 0) continue;
                foreach (HrPassParameterMap item in actFreqMapping)
                {
                    yield return new WaitForSeconds(tickRate / actFreqMapping.Count); // So we arrive at the end of list around the full time length of tick rate, less cluttering big chunk changes more like overtime one by one
                    yield return new WaitForSeconds(0.05f); //min rate
                    if (!registered) yield break;
                    MelonCoroutines.Start(HelperSet(item));
                }
            }
            yield return null;
        }

        public static IEnumerator HelperSet(HrPassParameterMap hpmap)
        {
            yield return new WaitForSeconds(0.1f);
            if (!registered) yield break;

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

            private static void Original(Dealer __instance)
            {
                // This is original source code, difference is that we allow for stealing items and persist them.
                // Per original source code SummarizeLosses is defined inside the function.
                static void SummarizeLosses(Dealer __instance, List<ItemInstance> items, float cash)
                {
                    // Added this piece of code to the original source, everything else inside Original function is original source code or equivalent
                    if (items.Count > 0)
                        coros.Add(MelonCoroutines.Start(CartelStealsItems(items, () => { Log($"[CARTEL INV]    Succesfully stolen {items.Count} unique items"); })));

                    if (items.Count == 0 && cash <= 0f)
                    {
                        return;
                    }
                    List<string> list = new List<string>();
                    for (int i = 0; i < items.Count; i++)
                    {
                        string text = items[i].Quantity.ToString() + "x ";
                        if (items[i] is ProductItemInstance && (items[i] as ProductItemInstance).AppliedPackaging != null)
                        {
                            text = text + (items[i] as ProductItemInstance).AppliedPackaging.Name + " of ";
                        }
                        text += items[i].Definition.Name;
                        if (items[i] is QualityItemInstance)
                        {
                            text = text + " (" + (items[i] as QualityItemInstance).Quality.ToString() + " quality)";
                        }
                        list.Add(text);
                    }
                    if (cash > 0f)
                    {
                        list.Add(MoneyManager.FormatAmount(cash, false, false) + " cash");
                    }
                    string text2 = "This is what they got:\n" + string.Join("\n", list);
                    __instance.MSGConversation.SendMessage(new Message(text2, Message.ESenderType.Other, true, -1), false, true);
                }

                float num = 0f;
                foreach (ItemSlot itemSlot in __instance.Inventory.ItemSlots)
                {
                    if (itemSlot.ItemInstance != null)
                    {
                        num = Mathf.Max(num, (itemSlot.ItemInstance.Definition as StorableItemDefinition).CombatUtilityForNPCs);
                    }
                }
                float num2 = UnityEngine.Random.Range(0f, 1f);
                num2 = Mathf.Lerp(num2, 1f, num * 0.5f);
                if (num2 > 0.67f)
                {
                    __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                    return;
                }
                if (num2 > 0.25f)
                {
                    __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_partially_defended"), Message.ESenderType.Other, false, -1), true, true);
                    List<ItemInstance> list = new List<ItemInstance>();
                    float num3 = 1f - Mathf.InverseLerp(0.25f, 0.67f, num2);
                    for (int i = 0; i < __instance.Inventory.ItemSlots.Count; i++)
                    {
                        if (__instance.Inventory.ItemSlots[i].ItemInstance != null)
                        {
                            float num4 = num3 * 0.8f;
                            if (UnityEngine.Random.Range(0f, 1f) < num4)
                            {
                                int num5 = Mathf.RoundToInt((float)__instance.Inventory.ItemSlots[i].ItemInstance.Quantity * num3);
                                list.Add(__instance.Inventory.ItemSlots[i].ItemInstance.GetCopy(num5));
                                __instance.Inventory.ItemSlots[i].ChangeQuantity(-num5, false);
                            }
                        }
                    }
                    __instance.TryMoveOverflowItems();
                    float num6 = __instance.Cash * num3;
                    __instance.ChangeCash(-num6);
                    SummarizeLosses(__instance, list, num6);
                    return;
                }

                __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_loss"), Message.ESenderType.Other, false, -1), true, true);
                List<ItemInstance> list2 = new List<ItemInstance>();
                foreach (ItemSlot itemSlot2 in __instance.Inventory.ItemSlots)
                {
                    if (itemSlot2.ItemInstance != null)
                    {
                        list2.Add(itemSlot2.ItemInstance.GetCopy(itemSlot2.ItemInstance.Quantity));
                    }
                }
                __instance.Inventory.Clear();
                foreach (ItemSlot itemSlot3 in __instance.overflowSlots)
                {
                    if (itemSlot3.ItemInstance != null)
                    {
                        list2.Add(itemSlot3.ItemInstance.GetCopy(itemSlot3.ItemInstance.Quantity));
                        itemSlot3.ClearStoredInstance(false);
                    }
                }
                float cash = __instance.Cash;
                __instance.ChangeCash(-cash);
                SummarizeLosses(__instance, list2, cash);
            }

            [HarmonyPrefix]
            public static bool Prefix(Dealer __instance)
            {
                if (IsPlayerNearby(__instance) && currentConfig.realRobberyEnabled)
                {
                    Log("[TRY ROB]    Run Custom");
                    if (!__instance.isInBuilding)
                        coros.Add(MelonCoroutines.Start(RobberyCombatCoroutine(__instance)));
                }
                else
                {
                    Log("[TRY ROB]    Run original");
                    Original(__instance);
                }

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
                int maxAttempts = 6;
                int j = 0;
                do
                {
                    yield return new WaitForSeconds(0.3f);
                    if (!registered) yield break;

                    Log("[TRY ROB]    Finding Spawn Robber Position");
                    Vector3 randomDirection = UnityEngine.Random.onUnitSphere;
                    randomDirection.y = 0;
                    randomDirection.Normalize();
                    float randomRadius = UnityEngine.Random.Range(8f, 16f);
                    Vector3 randomPoint = dealer.transform.position + (randomDirection * randomRadius);
                    dealer.Movement.GetClosestReachablePoint(targetPosition: randomPoint, out spawnPos);
                } while (spawnPos == Vector3.zero && j <= maxAttempts); // Because GetClosestReachablePoint can return V3.Zero as default (unreachable)
                if (spawnPos == Vector3.zero) yield break;
                CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);

                string text = "";
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 0:
                        text = "HELP BOSS!! Benzies are trying to ROB ME!!";
                        break;
                    case 1:
                        text = "BOSS!! I'm getting robbed!";
                        break;
                    case 2:
                        text = "I'm being jumped, come back me up!";
                        break;
                    case 3:
                        text = "Benzies set me up!! Come help quick!";
                        break;
                    case 4:
                        text = "Help, boss! I'm getting ambushed!";
                        break;
                    default:
                        text = "HELP BOSS!! Benzies are trying to ROB ME!!";
                        break;
                }

                dealer.MSGConversation.SendMessage(new Message(text, Message.ESenderType.Other, false, -1), true, true);

                goon.Movement.Warp(spawnPos);
                yield return new WaitForSeconds(0.5f);
                if (!registered) yield break;

                goon.Behaviour.CombatBehaviour.DefaultWeapon = null;

                dealer.Behaviour.CombatBehaviour.SetTarget(null, dealer.NetworkObject);
                dealer.Behaviour.CombatBehaviour.Begin();
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
                Log("[TRY ROB]    Dealer was defeated! Initiating partial robbery.");

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

                // Move items to goon inventory
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (list.Count == 0) break;

                    if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                    {
                        Log($"[TRY ROB]    Inserting {list.FirstOrDefault().Name} to Slot {i}");
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
                            Log($"[TRY ROB]    Inserting Cash to Slot {i}");
                            goon.Inventory.ItemSlots[i].InsertItem(cashInstance);
                            break;
                        }
                    }
                }

                // Now here we need to start new coro for escaping goon running to nearest Cartel Dealer
                coros.Add(MelonCoroutines.Start(NavigateGoonEsacpe(goon, region)));
            }
            else if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                // Goon is dead or knocked out,defended robbery
                Log("[TRY ROB]    Goon was defeated! Robbery attempt defended.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                // Apparently dealer will not exit combat automatically, it bugs out so we disable manual if this happens
                if (dealer.Behaviour.activeBehaviour != null && dealer.Behaviour.activeBehaviour is CombatBehaviour)
                    dealer.Behaviour.CombatBehaviour.End();
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.080f);
                }
            }
            else if (Vector3.Distance(Player.Local.CenterPointTransform.position, goon.CenterPointTransform.position) > 90f)
            {
                Log("[TRY ROB]    Player outside of range. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);

                // Apparently dealer will not exit combat automatically, it bugs out so we disable manual if this happens
                if (dealer.Behaviour.activeBehaviour != null && dealer.Behaviour.activeBehaviour is CombatBehaviour)
                    dealer.Behaviour.CombatBehaviour.End();

                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                if (InstanceFinder.IsServer && Singleton<Map>.Instance.GetUnlockedRegions().Contains(region))
                {
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.020f);
                }
            }
            else if (elapsed >= 60)
            {
                Log("[TRY ROB]    State Timed Out. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);

                // Apparently dealer will not exit combat automatically, it bugs out so we disable manual if this happens
                if (dealer.Behaviour.activeBehaviour != null && dealer.Behaviour.activeBehaviour is CombatBehaviour)
                    dealer.Behaviour.CombatBehaviour.End();

                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
            }
        }
        public static IEnumerator DespawnSoon(CartelGoon goon)
        {
            yield return new WaitForSeconds(60f);
            if (!registered) yield break;

            if (!goon.Behaviour.CombatBehaviour.enabled)
                goon.Behaviour.CombatBehaviour.Enable_Networked(null);

            if (goon.IgnoreImpacts)
                goon.IgnoreImpacts = false;

            if (goon.IsGoonSpawned)
            {
                Log("[TRY ROB]    Despawned Goon");
                goon.Despawn();
            }
            yield return null;
        }

        public static IEnumerator NavigateGoonEsacpe(CartelGoon goon, EMapRegion region)
        {
            Log("[TRY ROB]    Start Escape");
            // After succesful robbery, navigate goon towards nearest CartelDealer apartment door
            CartelDealer[] cartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            float distance = 150f;
            NPCEnterableBuilding building = null;
            Il2CppScheduleOne.Doors.StaticDoor door = null;
            Vector3 destination = Vector3.zero;
            NPCEvent_CartelGoonExit stayInside = null;
            foreach (CartelDealer d in cartelDealers)
            {
                yield return new WaitForSeconds(0.1f);
                if (!registered) yield break;

                if (d.isInBuilding && d.CurrentBuilding != null)
                {
                    building = d.CurrentBuilding;
                    door = building.GetClosestDoor(goon.CenterPointTransform.position, false);
                    float distToDoor = Vector3.Distance(door.AccessPoint.position, goon.CenterPointTransform.position);
                    if (distToDoor < distance)
                    {
                        destination = door.AccessPoint.position;
                        distance = distToDoor;
                    }
                }
            }


            if (goon.Behaviour.ScheduleManager.ActionList != null)
            {
                for (int k = 0; k < goon.Behaviour.ScheduleManager.ActionList.Count; k++)
                {
                    NPCEvent_CartelGoonExit temp1 = goon.Behaviour.ScheduleManager.ActionList[k].TryCast<NPCEvent_CartelGoonExit>();
                    if (temp1 != null)
                        stayInside = temp1;
                }
            }
            if (stayInside != null)
            {
                stayInside.End();
                stayInside.gameObject.SetActive(false);
                stayInside.Door = door;
                stayInside.Building = building;
            }

            Log($"[TRY ROB]    Escaping to: {destination}");
            Log($"[TRY ROB]    Distance: {distance}");
            goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            goon.IgnoreImpacts = true;
            goon.Behaviour.GetBehaviour("Follow Schedule").Enable();

            goon.Movement.GetClosestReachablePoint(destination, out Vector3 closest);
            coros.Add(MelonCoroutines.Start(ApplyAdrenalineRush(goon)));

            if (destination == Vector3.zero || !goon.Movement.CanGetTo(closest)) // If the destination look up fails or cant traverse to
            {
                goon.Behaviour.FleeBehaviour.SetEntityToFlee(Player.GetClosestPlayer(goon.CenterPointTransform.position, out float _).NetworkObject);
                goon.Behaviour.FleeBehaviour.Begin_Networked(null);
            }
            else
            {
                goon.Movement.SetDestination(closest);
            }

            goon.Movement.SetDestination(closest);

            List<EMapRegion> mapReg = new();
            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                mapReg.Add(unlmapReg);
            bool changeInfluence = false;
            if (InstanceFinder.IsServer && mapReg.Contains(region))
                changeInfluence = true;

            // While not dead or escape has elapsed under 60 seconds
            int elapsedNav = 0;
            float remainingDist = 100f;
            while (elapsedNav < 60 &&
                goon.IsConscious &&
                goon.IsGoonSpawned &&
                !goon.Health.IsDead &&
                !goon.Health.IsKnockedOut &&
                !goon.isInBuilding &&
                Vector3.Distance(closest, goon.CenterPointTransform.position) > 3f)
            {
                if (!registered) yield break;
                float currDist = Vector3.Distance(closest, goon.CenterPointTransform.position);
                if (currDist < remainingDist)
                    remainingDist = currDist;
                yield return new WaitForSeconds(0.2f);
                elapsedNav++;
            }

            if (!goon.Health.IsDead && !goon.Health.IsKnockedOut && remainingDist < 5f)
            {
                Log("[TRY ROB]    Goon Escaped to Cartel Dealer!");

                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.050f);
                Log("[TRY ROB]    -ParsingItems!");

                List<ItemInstance> list = new List<ItemInstance>();
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null && goon.Inventory.ItemSlots[i].ItemInstance is CashInstance) continue;

                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        int qty = Mathf.Min(goon.Inventory.ItemSlots[i].ItemInstance.Quantity, 20);
                        list.Add(goon.Inventory.ItemSlots[i].ItemInstance.GetCopy(qty));
                    }
                }
                Log("[TRY ROB]    -Steal!");
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, () => { goon.Inventory.Clear(); })));

                if (goon.IsGoonSpawned)
                    goon.Despawn();

                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }

                if (!goon.Behaviour.CombatBehaviour.Enabled)
                    goon.Behaviour.CombatBehaviour.Enable_Networked(null);

                if (goon.IgnoreImpacts)
                    goon.IgnoreImpacts = false;
            }
            else if (goon.Health.IsDead || goon.Health.IsKnockedOut)
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.025f);
                Log("[TRY ROB]    Robber Defeated");
                // The goon was defeated (dead or knocked out).
                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }
                
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
            }
            else if (elapsedNav >= 60 && goon.IsGoonSpawned)
            {
                // The escape attempt timed out.
                Log("[TRY ROB]    Despawned escaping goon due to timeout");

                List<ItemInstance> list = new List<ItemInstance>();
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null && goon.Inventory.ItemSlots[i].ItemInstance is CashInstance) continue;

                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        int qty = Mathf.Min(goon.Inventory.ItemSlots[i].ItemInstance.Quantity, 20);
                        list.Add(goon.Inventory.ItemSlots[i].ItemInstance.GetCopy(qty));
                    }
                }
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, () => { goon.Inventory.Clear(); })));

                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }

                if (!goon.Behaviour.CombatBehaviour.Enabled)
                    goon.Behaviour.CombatBehaviour.Enable_Networked(null);

                if (goon.IgnoreImpacts)
                    goon.IgnoreImpacts = false;

                if (goon.IsGoonSpawned)
                    goon.Despawn();
                Log("[TRY ROB] End");
            }

            yield return null;
        }

        // After combat goon gets adrenaline rush, getting little health regen instantly and increasing speed for 15sec ...
        public static IEnumerator ApplyAdrenalineRush(CartelGoon goon)
        {
            float origWalk = goon.Movement.WalkSpeed;
            float origRun = goon.Movement.RunSpeed;
            goon.Movement.WalkSpeed = goon.Movement.WalkSpeed * 3.3f;
            goon.Movement.RunSpeed = goon.Movement.RunSpeed * 2.5f;
            goon.Movement.MoveSpeedMultiplier = 1.6f;

            goon.Health.Health = Mathf.Round(Mathf.Lerp(goon.Health.Health, 100f, 0.4f));

            for (int i = 0; i < 15; i++)
            {
                yield return new WaitForSeconds(1f);
                if (!registered) yield break;
                goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origWalk, 0.035f);
                goon.Movement.RunSpeed = Mathf.Lerp(goon.Movement.RunSpeed, origRun, 0.035f);
                goon.Movement.MoveSpeedMultiplier = Mathf.Lerp(goon.Movement.MoveSpeedMultiplier, 1f, 0.06f);
            }
            goon.Movement.WalkSpeed = origWalk;
            goon.Movement.RunSpeed = origRun;
            goon.Movement.MoveSpeedMultiplier = 1f;
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
                    if (!registered) yield break;
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
                yield return new WaitForSeconds(2f);
                elapsedSec += 2f;
                if (!registered) yield break;
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile || driveByActive)
                {
                    yield return new WaitForSeconds(60f);
                    continue;
                }
                if (driveByActive)
                {
                    yield return new WaitForSeconds(60f);
                    elapsedSec += 60f;
                    continue;
                }
                if (elapsedSec >= 60f)
                {
                    if (hoursUntilDriveBy != 0)
                        hoursUntilDriveBy = hoursUntilDriveBy - 1;
                    elapsedSec = elapsedSec - 60f;
                }

                // Only at 22:30 until 05:00
                if ((TimeManager.Instance.CurrentTime >= 2230 || TimeManager.Instance.CurrentTime <= 500) && hoursUntilDriveBy <= 0)
                {
                    foreach (DriveByTrigger trig in driveByLocations)
                    {
                        yield return new WaitForSeconds(0.5f);
                        elapsedSec += 0.5f;
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
            Log("[DRIVE BY] Beginning Drive By Event");
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

                Log($"[DRIVE BY]    Angle: {angleToPlayer} - Dist: {distToPlayer} -  WeaponHits: {wepHits}");
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
            Log($"[DRIVE BY]    Drive By Bullets shot: {bulletsShot}/{maxBulletsShot}");
            yield return null;
        }

        public static void DriveByNavComplete(VehicleAgent.ENavigationResult result)
        {
            if (!registered) return;
            driveByAgent.storedNavigationCallback = null;
            driveByAgent.StopNavigating();
            driveByVeh.Park_Networked(null, driveByParking);
            driveByActive = false;
            Log("[DRIVE BY] Drive By Complete");
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
            Log("[DAY PASS] Resetting Mini Quest Dialogue Flags");
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
                Log("[MINI QUEST] Try Generate");
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
            float paid = Mathf.Lerp(500f, 100f, npc.RelationData.NormalizedRelationDelta);
            paid = Mathf.Round(paid / 20f) * 20f;

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


            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-$100</color>)";
            choice.Enabled = true;
            void OnMiniQuestChosenWrapped()
            {
                OnMiniQuestChosen(choice, npc, controller, paid);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnMiniQuestChosenWrapped);
            int index = controller.AddDialogueChoice(choice);
            Log("[MINI QUEST]    Created Mini Quest Dialogue for: " + npc.FirstName);
            return;
        }

        public static void OnMiniQuestChosen(DialogueController.DialogueChoice choice, NPC npc, DialogueController controller, float paid)
        {

            Log("[MINI QUEST]    Option Chosen");
            float chance = Mathf.Lerp(0.30f, 0.60f, npc.RelationData.NormalizedRelationDelta); // At max rela only 40% chance to refuse
            if (UnityEngine.Random.Range(0f, 1f) < chance && NetworkSingleton<MoneyManager>.Instance.cashBalance >= 100f)
            {
                Log("[MINI QUEST]    Start Quest");
                List<DeadDrop> drops = new();
                for (int i = 0; i < DeadDrop.DeadDrops.Count; i++)
                {
                    if (DeadDrop.DeadDrops[i].Storage.ItemCount == 0)
                        drops.Add(DeadDrop.DeadDrops[i]);
                }

                DeadDrop random = drops[UnityEngine.Random.Range(0, drops.Count)];

                string location = "";
                if (UnityEngine.Random.Range(0f, 1f) > chance)
                    location = random.Region.ToString() + " region";
                else
                    location = random.DeadDropName;

                List<ItemInstance> listItems = new();
                ItemInstance item;
                int qty;
                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                {
                    ItemDefinition def = Registry.GetItem(commonDrops[UnityEngine.Random.Range(0, commonDrops.Count)]);
                    qty = UnityEngine.Random.Range(3, 11);
                    item = def.GetDefaultInstance(qty);
                    listItems.Add(item);
                }
                else
                {
                    ItemDefinition def = Registry.GetItem(rareDrops[UnityEngine.Random.Range(0, rareDrops.Count)]);
                    qty = 1;
                    item = def.GetDefaultInstance(qty);
                    listItems.Add(item);
                }
                // Then take from stolen items
                if (cartelStolenItems.Count > 0)
                {
                    List<ItemInstance> fromPool = GetFromPool(2);
                    if (fromPool.Count > 0)
                        listItems.AddRange(fromPool);
                }

                coros.Add(MelonCoroutines.Start(CreateDropContent(random, listItems, npc)));
                controller.handler.ContinueSubmitted();
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-paid, true, false);
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
            else
            {
                Log("[MINI QUEST] RefuseQuestGive");
                controller.handler.ContinueSubmitted();
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        controller.handler.WorldspaceRend.ShowText($"I've heard nothing...", 15f);
                        npc.PlayVO(EVOLineType.No, false);
                        break;

                    case 1:
                        controller.handler.WorldspaceRend.ShowText($"No! Leave me alone!", 15f);
                        npc.Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 6f, 1);
                        npc.PlayVO(EVOLineType.Annoyed, false);
                        break;

                    case 2:
                        controller.handler.WorldspaceRend.ShowText($"I'm afraid to talk about it...", 15f);
                        npc.PlayVO(EVOLineType.Concerned, false);
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
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(oldChoices.Count - 1);
            controller.Choices = oldChoices;
            Log("[MINI QUEST]    Disposed Choice");
            yield return null;
        }

        public static IEnumerator CreateDropContent(DeadDrop entity, List<ItemInstance> filledItems, NPC npc)
        {
            yield return new WaitForSeconds(5f);
            if (!registered) yield break;
            Log($"[MINI QUEST]    MiniQuest Drop at: {entity.DeadDropName}");
            for (int i = 0; i < filledItems.Count; i++)
            {
                entity.Storage.InsertItem(filledItems[i], true);
                Log($"[MINI QUEST]    MiniQuest Reward: {filledItems[i].Name} x {filledItems[i].Quantity}");
            }

            List<EMapRegion> mapReg = new();
            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                mapReg.Add(unlmapReg);
            bool opened = false;
            UnityEngine.Events.UnityAction onOpenedAction = null;
            void WrapOnOpenCallback()
            {
                Log("[MINI QUEST] Quest Complete");
                NetworkSingleton<LevelManager>.Instance.AddXP(100);
                opened = true;
                
                if (InstanceFinder.IsServer && mapReg.Contains(entity.Region))
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, -0.025f);

                entity.Storage.onOpened.RemoveListener(onOpenedAction);
            }
            onOpenedAction = (UnityEngine.Events.UnityAction)WrapOnOpenCallback;
            entity.Storage.onOpened.AddListener(onOpenedAction);

            float duration = UnityEngine.Random.Range(60f, 120f);
            yield return new WaitForSeconds(duration);
            if (!registered) yield break;

            if (!opened)
            {
                if (InstanceFinder.IsServer && mapReg.Contains(entity.Region))
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, 0.050f);
                entity.Storage.ClearContents();
            }

            entity.Storage.onOpened.RemoveListener(onOpenedAction);
            Log($"[MINI QUEST] Removed MiniQuest Reward. Quest Duration: {duration}");
            yield return null;
        }

        #endregion

        #region Helper Functions for Persistent Stolen Items
        // match with quality + id -> Else add it
        public static IEnumerator CartelStealsItems(List<ItemInstance> items, Action cb = null)
        {
            lock (cartelItemLock)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    int realQty = -1;

                    if (items[i] is QualityItemInstance inst)
                    {
                        // Search for existing 
                        int foundIdx = -1;
                        if (cartelStolenItems.Count > 0)
                        {
                            for (int j = 0; j < cartelStolenItems.Count; j++)
                            {
                                if (inst.ID == items[i].ID && cartelStolenItems[j].Quality == inst.Quality)
                                {
                                    foundIdx = j;
                                    break;
                                }
                            }
                        }
                        // Is packaging, jars + 5qty, brick +20
                        if (items[i] is ProductItemInstance packin)
                        {
                            if (packin != null && packin.ID != null)
                            {
                                switch (packin.ID)
                                {
                                    case "jar":
                                        realQty = 5;
                                        break;
                                    case "brick":
                                        realQty = 20;
                                        break;
                                    default:
                                        realQty = 1;
                                        break;
                                }
                            }
                        }

                        if (foundIdx >= 0) // Exists in already stolen items
                        {
                            if (realQty != -1)
                            {
                                cartelStolenItems[foundIdx].Quantity += items[i].Quantity * realQty;
                                Log($"[CARTEL INV] ADD: {items[i].Name}x{items[i].Quantity * realQty}");
                            }
                            else
                            {
                                cartelStolenItems[foundIdx].Quantity += items[i].Quantity;
                                Log($"[CARTEL INV] ADD: {items[i].Name}x{items[i].Quantity * realQty}");
                            }
                        }
                        else // not exist
                        {
                            if (realQty != -1)
                            {
                                cartelStolenItems.Add(inst);
                                // At end of list change quantity to be same as qty*packaging
                                cartelStolenItems[cartelStolenItems.Count].Quantity = items[i].Quantity * realQty;
                                Log($"[CARTEL INV] ADD: {items[i].Name}x{items[i].Quantity * realQty}");
                            }
                            else
                            {
                                cartelStolenItems.Add(inst); // Else Qty is already set nothing to do
                                Log($"[CARTEL INV] ADD: {inst.Quantity}");
                            }
                        }
                    }
                }
            }
            if (cb != null)
                cb();
            yield return null;
        }

        // From pool max 20 unpackaged items per slot, saves quality
        public static List<ItemInstance> GetFromPool(int maxEmptySlotsToFill)
        {
            List<ItemInstance> fromPool = new();
            lock (cartelItemLock)
            {
                int itemsToPick = Mathf.Min(maxEmptySlotsToFill, cartelStolenItems.Count);

                for (int i = 0; i < itemsToPick; i++)
                {
                    if (cartelStolenItems.Count == 0) break;
                    int randomIndex = UnityEngine.Random.Range(0, cartelStolenItems.Count);
                    QualityItemInstance randomSelected = cartelStolenItems[randomIndex];
                    int minQty = Mathf.Min(randomSelected.Quantity, 20);

                    ItemDefinition def = Registry.GetItem(randomSelected.ID);
                    ItemInstance item = def.GetDefaultInstance(minQty);
                    if (item is QualityItemInstance inst)
                        inst.Quality = cartelStolenItems[randomIndex].Quality;

                    fromPool.Add(item);

                    if (minQty >= randomSelected.Quantity)
                        cartelStolenItems.RemoveAt(randomIndex);
                    else
                        cartelStolenItems[randomIndex].Quantity -= minQty;
                }
            }
            return fromPool;
        }
        #endregion

        #region Intercept Deals 
        public static IEnumerator EvaluateCartelIntercepts()
        {
            yield return new WaitForSeconds(5f);
            Log("Starting Cartel Intercepts Evaluation");
            float frequency = 90f;
            if (currentConfig.activityFrequency >= 0.0f)
                frequency = Mathf.Lerp(frequency, 60f, currentConfig.activityFrequency);
            else
                frequency = Mathf.Lerp(frequency, 480f, -currentConfig.activityFrequency);


            while (registered)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(frequency, frequency * 2));
                if (!registered) yield break;

                // from 6pm to 4am only
                if (!(TimeManager.Instance.CurrentTime >= 1800 || TimeManager.Instance.CurrentTime <= 400))
                    continue;

                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    continue;

                if (!interceptingDeal)
                {
                    coros.Add(MelonCoroutines.Start(StartInterceptDeal()));
                }
            }
        }
        public static IEnumerator StartInterceptDeal()
        {
            Log("[INTERCEPT] Started Checking Intercept Deal Validity");
            List<string> occupied = new();
            CartelDealer[] allCartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            foreach (CartelDealer d in allCartelDealers)
            {
                foreach (Contract c in d.ActiveContracts)
                {
                    if (!occupied.Contains(c.GUID.ToString()))
                        occupied.Add(c.GUID.ToString());
                }
            }

            List<Contract> validContracts = new();

            int i = 0;
            do
            {
                if (i >= NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount)
                {
                    break; // Safe transform parse
                }
                Transform trContract = NetworkSingleton<QuestManager>.Instance.ContractContainer.GetChild(i);
                if (trContract != null)
                {
                    Contract contract = trContract.GetComponent<Contract>();
                    bool isValid = true;
                    if (contract.Dealer != null) isValid = false; // Not player
                    if (occupied.Contains(contract.GUID.ToString())) isValid = false; // Not cartel dealer
                    if (contract.GetMinsUntilExpiry() > 300) isValid = false; // Only take contracts with less than 5h left
                    if (contract.GetMinsUntilExpiry() < 90) isValid = false; // Only take contracts with More than 1h 30min left (30min) reserved for max wait sleep
                    if (contractGuids.Contains(contract.GUID.ToString())) isValid = false; // Only take contracts currently not intercepted

                    if (isValid)
                    {
                        if (!validContracts.Contains(contract))
                            validContracts.Add(contract);
                    }
                }
                i++;
            } while (i < NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount);
            // Check that the contract exists in UI since we need it later in functions
            QuestHUDUI[] current = UnityEngine.Object.FindObjectsOfType<QuestHUDUI>();
            List<Contract> ctrsToRemove = new();
            if (current.Length == 0)
            {
                Log($"[INTERCEPT]    No HUD Elements to parse");

            }
            foreach (Contract contract in validContracts)
            {
                bool exists = false;

                foreach (QuestHUDUI item in current)
                {
                    yield return new WaitForSeconds(0.1f);
                    if (!registered) yield break;
                    if (item.Quest == contract)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    ctrsToRemove.Add(contract);
            }
            // Remove the ones which dont exist in UI since that causes an error
            if (ctrsToRemove.Count > 0)
            {
                foreach (Contract ctr in ctrsToRemove)
                    validContracts.Remove(ctr);
            }
            if (validContracts.Count == 0)
            {
                Log("[INTERCEPT]    No Valid Contracts After Removing Non-UI supported");
                yield break; // No Valid Contracts this time
            }


            Contract randomContract = validContracts[UnityEngine.Random.Range(0, validContracts.Count)];
            Customer customer = randomContract.Customer.GetComponent<Customer>();
            CartelDealer selected = null;

            EMapRegion region = EMapRegion.Northtown;
            for (int j = 0; j < Singleton<Map>.Instance.Regions.Length; j++)
            {
                if (Singleton<Map>.Instance.Regions[j].RegionBounds.IsPointInsidePolygon(customer.NPC.CenterPointTransform.position))
                {
                    region = Singleton<Map>.Instance.Regions[j].Region;
                }
            }
            selected = NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities(region).CartelDealer;

            // Ensure Cartel Dealer is not dead or knocked out
            if (selected.Health.IsDead || selected.Health.IsKnockedOut) yield break;
            // Ensure NPC is not dead or knocked out
            if (customer.NPC.Health.IsDead || customer.NPC.Health.IsKnockedOut) yield break;
            // Ensure Player is not nearby
            float distanceToPlayer = Vector3.Distance(customer.NPC.CenterPointTransform.position, Player.Local.CenterPointTransform.position);
            if (distanceToPlayer < 40f) yield break;

            string cGuid = randomContract.GUID.ToString();
            contractGuids.Add(cGuid);

            NPCEvent_StayInBuilding event1 = null;
            NPCSignal_HandleDeal event2 = null;

            if (selected.Behaviour.ScheduleManager.ActionList != null)
            {
                for (int k = 0; k < selected.Behaviour.ScheduleManager.ActionList.Count; k++)
                {
                    NPCEvent_StayInBuilding temp1 = selected.Behaviour.ScheduleManager.ActionList[k].TryCast<NPCEvent_StayInBuilding>();
                    if (temp1 != null)
                        event1 = temp1;

                    NPCSignal_HandleDeal temp2 = selected.Behaviour.ScheduleManager.ActionList[k].TryCast<NPCSignal_HandleDeal>();
                    if (temp2 != null)
                        event2 = temp2;
                }
            }

            string text = "";
            switch (UnityEngine.Random.Range(0, 7))
            {
                case 0:
                    text = "What's taking so long? I will just find another dealer...";
                    break;
                case 1:
                    text = "Nevermind! I made a deal with someone else.";
                    break;
                case 2:
                    text = "Are you coming or not? I might just buy from someone else.";
                    break;
                case 3:
                    text = "Yo where are you?! I've been waiting at our spot. I'll message another dealer then...";
                    break;
                case 4:
                    text = "This isn't working out. I'm taking my business elsewhere.";
                    break;
                case 5:
                    text = "I'm not waiting around all day. Don't bother texting me back.";
                    break;
                case 6:
                    text = "You snooze, you lose. Found another dealer to sell me my shit.";
                    break;
                case 7:
                    text = "I'll hustle with someone else if you ghost me like this...";
                    break;
            }

            Log("[INTERCEPT]    Starting Intercept Deal");
            customer.NPC.MSGConversation.SendMessage(new Message(text, Message.ESenderType.Other, true, -1), true, true);
            interceptingDeal = true;

            coros.Add(MelonCoroutines.Start(QuestUIEffect(randomContract)));
            coros.Add(MelonCoroutines.Start(BeginIntercept(selected, randomContract, customer, region, event1, event2, cGuid)));
            yield return null;
        }

        public static IEnumerator BeginIntercept(CartelDealer dealer, Contract contract, Customer customer, EMapRegion region, NPCEvent_StayInBuilding ev1, NPCSignal_HandleDeal ev2, string cGuid)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(10f, 30f)); // Cartel dealer is kinda fast so have to wait a bit
            if (!registered) yield break;

            List<EMapRegion> mapReg = new();
            foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                mapReg.Add(unlmapReg);
            bool changeInfluence = false;
            if (InstanceFinder.IsServer && mapReg.Contains(region))
                changeInfluence = true;

            if (customer.CurrentContract == null) // If player managed to complete it within that timeframe
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.100f);
                contractGuids.Remove(cGuid);
                interceptingDeal = false;
                yield break;
            }

            contract.BopHUDUI();
            contract.CompletionXP = Mathf.RoundToInt((float)contract.CompletionXP * 0.5f);
            contract.completedContractsIncremented = false;

            for (int i = 0; i < dealer.Inventory.ItemSlots.Count; i++)
            {
                if (dealer.Inventory.ItemSlots[i].ItemInstance == null)
                {
                    List<ItemInstance> fromPool = GetFromPool(1);
                    if (fromPool.Count > 0)
                        dealer.Inventory.ItemSlots[i].ItemInstance = fromPool[0];
                }
            }

            void OnQuestEndEvaluateResult(EQuestState state)
            {
                Log("[INTERCEPT]    EVALUATE RESULT: " + state);
                float cartelDealerDist = Vector3.Distance(dealer.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                float playerDist = Vector3.Distance(Player.Local.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                
                if (cartelDealerDist < playerDist && cartelDealerDist < 5f)
                {
                    Log("[INTERCEPT]    Cartel Succesfully Intercepted Deal");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.100f);
                    customer.NPC.RelationData.ChangeRelationship(-1f, true);
                }
                else if (playerDist < 5f && (dealer.Health.IsDead || dealer.Health.IsKnockedOut) && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Player Stopped Cartel Intercept & killed dealer");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.100f);
                    customer.NPC.RelationData.ChangeRelationship(0.25f, true);

                }
                else if (playerDist < 5f && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Player Stopped Cartel Intercept");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.050f);
                    customer.NPC.RelationData.ChangeRelationship(0.25f, true);

                }
                if (contractGuids.Contains(cGuid))
                    contractGuids.Remove(cGuid);
                ev2.HasStarted = false;
                ev2.End();
                ev1.enabled = true;
                ev1.IsActive = true;
                ev1.HasStarted = true;
                ev1.Resume();

                interceptingDeal = false;

            }

            contract.onQuestEnd.AddListener(new Action<EQuestState>(OnQuestEndEvaluateResult));

            dealer.SetIsAcceptingDeals(true);
            dealer.AddContract(contract);

            if (ev1 != null)
            {
                ev1.StartTime = 0400;
                ev1.EndTime = 1800;
                ev1.End();
                ev1.HasStarted = false;
                ev1.enabled = false;
            }
            if (ev2 != null)
            {
                ev2.Started();
                ev2.HasStarted = true;
            }
            dealer.CheckAttendStart();
            // Set the dealer to null because player wont be able to complete the deal otherwise, locked because its reserved for "Rival Dealer"
            // The dealer will have the contract too and try to complete it, but this way player can do it too
            if (customer.CurrentContract != null)
                if (customer.CurrentContract.Dealer != null)
                    customer.CurrentContract.Dealer = null;

            yield return null;
        }

        public static IEnumerator FetchUIElementsInit()
        {
            yield return new WaitForSeconds(10f);
            if (!registered) yield break;

            RectTransform rt = PlayerSingleton<MessagesApp>.Instance.conversationEntryContainer;
            if (rt == null) 
            {
                Log("Conversation entry container is null");
                yield break;
            }

            // Now we build a mount everest here because otherwise il2cpp thinks we are dealing with system objects
            for (int i = 0; i < rt.childCount; i++) 
            {
                if (i > rt.childCount) break;
                Transform msgItem = rt.GetChild(i);
                if (msgItem != null)
                {
                    Transform nameTr = msgItem.Find("Name");
                    if (nameTr != null && nameTr.gameObject != null)
                    {
                        Text text = nameTr.gameObject.GetComponent<Text>();
                        if (text != null && text.text == "Thomas Benzies")
                        {
                            Transform iconMask = msgItem.Find("IconMask");
                            if (iconMask != null)
                            {
                                Transform icon = iconMask.Find("Icon");
                                if (icon != null && icon.gameObject != null)
                                {
                                    benziesLogo = icon.gameObject.GetComponent<Image>().sprite;
                                    Log("Benzies Logo Assigned");
                                }
                            }
                        }
                        else
                            continue;
                    }
                    else
                        Log("NameTr is null");
                }
                else
                    Log("Msg Item is Null");
            }
            // the below code is alternative to the mount everest code above
            //foreach (Transform tr in rt.childCount)
            //{
            //    if (tr.Find("Name").GetComponent<Text>().text != "Thomas Benzies")
            //        continue;
            //    benziesLogo = tr.Find("IconMask").Find("Icon").GetComponent<Image>().sprite;
            //}
            Log("Fetched Benzies Logo UI Element");
            yield return null;
        }

        public static IEnumerator QuestUIEffect(Contract contract)
        {
            QuestHUDUI[] current = UnityEngine.Object.FindObjectsOfType<QuestHUDUI>(true);
            QuestHUDUI found = null;
            foreach (QuestHUDUI item in current)
            {
                yield return new WaitForSeconds(0.1f);
                if (!registered) yield break;

                if (item == null) continue;
                if (item.Quest == contract)
                {
                    found = item;
                }
            }

            // Ugly code here but we need to actually check each transform for null

            if (found == null || found.MainLabel == null || found.MainLabel.transform == null)
            {
                Log("[INTERCEPT] foundItem got nulled, break");
                yield break;
            }

            Transform iconContainer = found.MainLabel.transform.Find("IconContainer");
            if (iconContainer == null)
            {
                Log("[INTERCEPT] iconContainer got nulled, break");
                yield break;
            }
            Transform contractIcon = iconContainer.Find("ContractIcon(Clone)");
            if (contractIcon == null)
            {
                Log("[INTERCEPT] contractIcon got nulled, break");
                yield break;
            }

            Transform backgroundTr = contractIcon.Find("Background");
            if (backgroundTr == null)
            {
                Log("[INTERCEPT] backgroundTr got nulled, break");
                yield break;
            }
            Image background = backgroundTr.GetComponent<Image>();
            questIconBack = background.color;

            coros.Add(MelonCoroutines.Start(LerpQuestColor(background)));

            Transform fillImgTr = contractIcon.Find("Fill");
            if (fillImgTr == null)
            {
                Log("[INTERCEPT] backgroundTr got nulled, break");
                yield break;
            }
            Image fillImg = fillImgTr.GetComponent<Image>();
            handshake = fillImg.sprite;

            found.BopIcon();
            fillImg.overrideSprite = benziesLogo;

            coros.Add(MelonCoroutines.Start(ResetQuestUIEffect(fillImg, background)));

            yield return null;
        }

        public static IEnumerator ResetQuestUIEffect(Image fillImg, Image background)
        {
            while (interceptingDeal && registered) { yield return new WaitForSeconds(10f); }
            background.color = questIconBack;
            fillImg.overrideSprite = handshake;
            Log("[INTERCEPT] Reset QuestUI");
            yield return null;
        }
        public static IEnumerator LerpQuestColor(Image background)
        {
            Color startColor = background.color;
            Color endColor = Color.white;
            float duration = 2.0f;
            float timer = 0f;

            while (timer < duration && registered)
            {
                float t = timer / duration;
                background.color = Color.Lerp(startColor, endColor, t);
                timer += Time.deltaTime;
                yield return new WaitForSeconds(0.1f);
                if (!registered) yield break;
            }
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
                if (d is not CartelDealer)
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
        // Log misc variables otherwise hidden
        public static IEnumerator OnInputInternalLog()
        {
            string Map(int classIndex)
            {
                switch (classIndex)
                {
                    case -1:
                        return "Ambush";
                    case 0:
                        return "StealDeadDrop";
                    case 1:
                        return "CartelCustomerDeal";
                    case 2:
                        return "RobDealer";
                    default:
                        return "Unknown";
                }
            }

            Log("\nActivity Hours Table Per Activity Type\n---------------");
            foreach (CartelRegActivityHours rghrs in regActivityHours)
            {
                Log($"\n  Class: {Map(rghrs.cartelActivityClass)}\n  HoursUntil Enable: {rghrs.hoursUntilEnable}\nRegion: {rghrs.region}\n******");
            }
            Log("---------------\n\n\n");
            yield return new WaitForSeconds(1f);

            Log("\nActivity Frequency Table\n---------------");
            foreach (HrPassParameterMap map in actFreqMapping)
            {
                Log($"\n{map.itemDesc}\n  Ticks Passed: {map.modTicksPassed}\n  Mod HoursUntilNext: {map.currentModHours}\n  Instance HoursUntilNext: {map.Getter()}\n******");
            }
            Log("---------------\n\n\n");
            yield return new WaitForSeconds(1f);

            Log("\nCartel Stolen Items\n---------------");
            foreach (QualityItemInstance itemInst in cartelStolenItems)
            {
                Log($"\n  Item: {itemInst.Name}\n  Quantity: {itemInst.Quantity}\n  Quality: {itemInst.Quality}\n******");
            }
            Log("---------------\n\n\n");

            yield return new WaitForSeconds(1f);

            Log("\nMini Quest NPC Status\n---------------");
            foreach (NPC npc in targetNPCs.Keys.ToList())
            {
                Log($"  Name: {npc.name}");
                Log($"    Has Active Quest: {targetNPCs[npc].HasActiveQuest}");
                Log($"    Has Asked Today: {targetNPCs[npc].HasAskedQuestToday}");
            }
            Log("---------------\n\n\n");

            yield return new WaitForSeconds(1f);
            debounce = false;
        }

        // Start Cartel Intercept Contract
        public static IEnumerator OnInputInterceptContract()
        {
            MelonCoroutines.Start(StartInterceptDeal());
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