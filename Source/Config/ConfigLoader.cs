using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.ModDataPaths;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.Levelling;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
#endif

namespace CartelEnforcer
{

    #region Persistent JSON Files and Serialization

    [Serializable]
    public class ModConfig
    {
        public bool debugMode = false; // While in debug mode, spawn visuals for Cartel Ambushes, etc.

        public bool driveByEnabled = true;

        public bool realRobberyEnabled = true;

        public bool defaultRobberyEnabled = true; // message robberies

        public bool miniQuestsEnabled = true;

        public bool interceptDeals = true;

        public bool enhancedDealers = true;
        
        public bool cartelGatherings = true;

        public bool businessSabotage = true;

        public bool stealBackCustomers = true;

        public bool alliedExtensions = true;

        public bool endGameQuest = true;
        public float endGameQuestMonologueSpeed = 1f; // clamped to 1 0 at 0 dialogue speed is signifigantly slower, at 1 normal
        // TODO for the end game quests should get rid of the monologue shit now that the allied extension thing got that dialogue init figured out
        // its a fuck ton of work

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

    public static class ConfigLoader
    {

        #region Mod Config 
        public static ModConfig Load()
        {
            ModConfig config;
            string filePath = GetPathTo(pathModConfig);
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<ModConfig>(json);
                    config.endGameQuestMonologueSpeed = Mathf.Clamp(config.endGameQuestMonologueSpeed, 0f, 1f);

                    if (config.alliedExtensions && !config.endGameQuest)
                    {
                        MelonLogger.Warning("Cartel Enforcer Allied Extensions depend on End Game Quests. Enabling End Game Quest config automatically.");
                        config.endGameQuest = true;
                    }
                }
                catch (JsonSerializationException ex) 
                {
                    config = new ModConfig();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new ModConfig();
                    MelonLogger.Warning("Failed to read CartelEnforcer config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer basic mod config, creating directory and template.");
                config = new ModConfig();
                Save(config);
            }
            return config;
        }
        public static void Save(ModConfig config)
        {
            try
            {
                string filePath = GetPathTo(pathModConfig);
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                DebugModule.Log($"CartelEnforcer basic mod config written to: {filePath}", "SaveModConfig");
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
            string filePath = GetPathTo(pathAmbushes);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<ListNewAmbush>(json);
                }
                catch (JsonSerializationException ex)
                {
                    config = new ListNewAmbush();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer User Added Ambush config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new ListNewAmbush();
                    config.addedAmbushes = new();
                    MelonLogger.Warning("Failed to read CartelEnforcer User Added Ambush config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer User Added Ambush config, creating directory and template.");
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
                string filePath = GetPathTo(pathAmbushes);
                string json = JsonConvert.SerializeObject(config, Formatting.Indented, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                MelonLogger.Warning($"    CartelEnforcer User Added Ambush config written to: {filePath}");

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
            string filePath = GetPathTo(pathDefAmbushes);
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<ListNewAmbush>(json);
                }
                catch (JsonSerializationException ex)
                {
                    config = new ListNewAmbush();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Default ambush config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new ListNewAmbush();
                    MelonLogger.Warning("Failed to read Cartel Enforcer Default ambush config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Default ambush config, creating directory and template.");
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
                string filePath = GetPathTo(pathDefAmbushes);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                MelonLogger.Warning($"    CartelEnforcer Default Ambush config written to: {filePath}");

            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save default.json config: " + ex);
            }
            return currentState;
        }
        #endregion

        #region Ambush Settings config
        // Read or generate the ambush settings config from Ambush/settings.json
        public static AmbushGeneralSettingsSerialized LoadAmbushSettings()
        {
            AmbushGeneralSettingsSerialized config;
            string filePath = GetPathTo(pathSettingsAmbushes);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<AmbushGeneralSettingsSerialized>(json);
                    // For list of strings asset path should only be alpahanumeric + "/" symbols indicating asset path, sanitize or validate?? todo

                    // Ensure rank is within bounds
                    // Get enum length so it works in future updates too dont have to hardcode any off that here
                    Array ranks = Enum.GetValues(typeof(ERank));
                    int max = ranks.Cast<int>().Max();
                    if (config.MinRankForRanged >= max || config.MinRankForRanged < 0)
                    {
                        config.MinRankForRanged = Mathf.Clamp(config.MinRankForRanged, 0, max - 1);
                    }
                    // cap lethality because of lerp
                    config.AmbushWeaponLethality = Mathf.Clamp01(config.AmbushWeaponLethality);
                }
                catch (JsonSerializationException ex)
                {
                    config = new AmbushGeneralSettingsSerialized();
                    config.RangedWeaponAssetPaths = new List<string> { "Avatar/Equippables/M1911" };
                    config.MeleeWeaponAssetPaths = new List<string> { "Avatar/Equippables/Knife" };
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Ambush/settings.json config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new AmbushGeneralSettingsSerialized();
                    config.RangedWeaponAssetPaths = new List<string> { "Avatar/Equippables/M1911" };
                    config.MeleeWeaponAssetPaths = new List<string> { "Avatar/Equippables/Knife" };
                    MelonLogger.Warning("Failed to read Ambush/settings.json config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Could not find settings.json at Ambush/settings.json. Generating default template.");
                config = new AmbushGeneralSettingsSerialized();
                config.RangedWeaponAssetPaths = new List<string> { "Avatar/Equippables/M1911" };
                config.MeleeWeaponAssetPaths = new List<string> { "Avatar/Equippables/Knife" };

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
            }

            return config;
        }

        #endregion

        #region Drive By triggers config
        // Read and generate the drive by locations from DriveBy/driveby.json

        [Serializable]
        public class DriveByTriggersSerialized
        {
            public List<DriveByTrigger> triggers;
        }

        private static DriveByTriggersSerialized GenerateDefault()
        {
            DriveByTriggersSerialized loadedTriggers;
            loadedTriggers = new();
            loadedTriggers.triggers = new();
            GenerateDefaultDriveByTriggers();

            foreach (var kvp in driveByLocations)
                loadedTriggers.triggers.Add(kvp.Key);

            return loadedTriggers;
        } 

        public static DriveByTriggersSerialized LoadDriveByConfig()
        {
            DriveByTriggersSerialized loadedTriggers = null;
            string filePath = GetPathTo(pathDriveBys);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    loadedTriggers = JsonConvert.DeserializeObject<DriveByTriggersSerialized>(json);
                }
                catch (JsonSerializationException ex)
                {
                    loadedTriggers = GenerateDefault();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer DriveBy/driveby.json config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    loadedTriggers = GenerateDefault();
                    MelonLogger.Warning("Failed to read DriveBy/driveby.json config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Could not find a file at DriveBy/driveby.json. Generating default template.");

                loadedTriggers = GenerateDefault();

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new UnityContractResolver()
                };

                string json = JsonConvert.SerializeObject(loadedTriggers, Formatting.Indented, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
            }

            return loadedTriggers;
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

        public static List<QualityItemInstance> LoadStolenItems()
        {
            StolenItemsList stolenItems = new();
            string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
            int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
            string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
            string filePath = GetPathTo(Path.Combine(pathCartelStolen, fileName));

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    stolenItems = JsonConvert.DeserializeObject<StolenItemsList>(json);
                }
                catch (JsonSerializationException ex)
                {
                    stolenItems = new();
                    MelonLogger.Error("Failed to deserialize Cartel Stolen items data: " + ex.Message);
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

            // set static balance
            CartelInventory.cartelCashAmount = stolenItems.balance;

            List<QualityItemInstance> newQualityItemList = new List<QualityItemInstance>();
            if (stolenItems.items != null && stolenItems.items.Count > 0)
            {
                foreach (SerializeStolenItems seri in stolenItems.items)
                {
                    // Validate fields
                    if (seri.ID == null || seri.ID == string.Empty)
                    {
                        MelonLogger.Msg("Stolen Item ID is null");
                        continue;
                    }
                    else if (seri.Quality < 0 || seri.Quality > 4)
                    {
                        MelonLogger.Msg("Stolen Quality not allowed");
                        continue;
                    }
                    else if (seri.Quantity >= int.MaxValue)
                    {
                        MelonLogger.Msg("Stolen Quantity is over the max limit");
                        continue;
                    }

#if MONO
                    ItemDefinition def = ScheduleOne.Registry.GetItem(seri.ID);
                    ItemInstance item = def.GetDefaultInstance(seri.Quantity);
                    if (item is QualityItemInstance inst)
                    {
                        switch (seri.Quality)
                        {
                            case 0:
                                inst.Quality = EQuality.Trash;
                                break;
                            case 1:
                                inst.Quality = EQuality.Poor;
                                break;
                            case 2:
                                inst.Quality = EQuality.Standard;
                                break;
                            case 3:
                                inst.Quality = EQuality.Premium;
                                break;
                            case 4:
                                inst.Quality = EQuality.Heavenly;
                                break;
                            default:
                                break;
                        }
                        newQualityItemList.Add(inst);
                    }
                    else
                    {
                        MelonLogger.Msg("Tried to load item thats not quality item instance");
                    }
#else
                    ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(seri.ID);
                    ItemInstance item = def.GetDefaultInstance(seri.Quantity);
                    QualityItemInstance temp = item.TryCast<QualityItemInstance>();
                    if (temp != null)
                    {
                        switch (seri.Quality)
                        {
                            case 0:
                                temp.Quality = EQuality.Trash;
                                break;
                            case 1:
                                temp.Quality = EQuality.Poor;
                                break;
                            case 2:
                                temp.Quality = EQuality.Standard;
                                break;
                            case 3:
                                temp.Quality = EQuality.Premium;
                                break;
                            case 4:
                                temp.Quality = EQuality.Heavenly;
                                break;
                            default:
                                break;
                        }
                        newQualityItemList.Add(temp);
                    }
                    else
                    {
                        MelonLogger.Msg("Tried to load item thats not quality item instance");
                    }
#endif

                }
            }

            return newQualityItemList;
        }

        public static void Save(List<QualityItemInstance> stolenItems)
        {
            StolenItemsList itemsList = new();
            itemsList.items = new();
            lock (cartelItemLock)
            {
                foreach (QualityItemInstance item in stolenItems)
                {
                    SerializeStolenItems newItem = new() { ID = item.ID, Quality = (int)item.Quality, Quantity = item.Quantity };
                    itemsList.items.Add(newItem);
                }
                // balance
                itemsList.balance = Mathf.Round(CartelInventory.cartelCashAmount);

                try
                {
                    string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
                    int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
                    string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
                    string filePath = GetPathTo(Path.Combine(pathCartelStolen, fileName));

                    string json = JsonConvert.SerializeObject(itemsList, Formatting.Indented);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.WriteAllText(filePath, json);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("Failed to save Cartel Stolen Items data: " + ex);
                }
            }


        }
        #endregion

        #region Cartel Dealer config
        public static CartelDealerConfig LoadDealerConfig()
        {
            CartelDealerConfig config = new();
            string filePath = GetPathTo(pathDealerConfig);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<CartelDealerConfig>(json);
                    config.CartelDealerWalkSpeed = Mathf.Clamp(config.CartelDealerWalkSpeed, 1f, 7f);
                    config.CartelDealerHP = Mathf.Clamp(config.CartelDealerHP, 10f, 2000f);
                    config.CartelDealerLethality = Mathf.Clamp01(config.CartelDealerLethality);
                    config.SafetyThreshold = Mathf.Clamp(config.SafetyThreshold, -1.0f, 1.0f); // Ensure limits
                    config.DealerActivityIncreasePerDay = Mathf.Clamp(config.DealerActivityIncreasePerDay, 0.0f, 1f); // Ensure limits
                    config.DealerActivityDecreasePerKill = Mathf.Clamp(config.DealerActivityDecreasePerKill, 0.0f, 1f); // Ensure limits
                    config.StealDealerContractChance = Mathf.Clamp(config.StealDealerContractChance, 0f, 1f); // at 0.0 disables 
                    config.StealPlayerPendingChance = Mathf.Clamp(config.StealPlayerPendingChance, 0f, 1f); // at 0.0 disables
                }
                catch (JsonSerializationException ex)
                {
                    config = new CartelDealerConfig();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Dealer config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new CartelDealerConfig();
                    MelonLogger.Warning("Failed to read CartelEnforcer Dealer config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Dealer config, creating directory and template.");
                config = new CartelDealerConfig();
                Save(config);
            }
            return config;
        }

        public static void Save(CartelDealerConfig dealerConfig)
        {
            try
            {
                string json = JsonConvert.SerializeObject(dealerConfig, Formatting.Indented);
                string filePath = GetPathTo(pathDealerConfig);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                MelonLogger.Warning($"    CartelEnforcer Dealer config written to: {filePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Dealer config: " + ex);
            }
        }

        #endregion

        #region Cartel Influence Config
        public static InfluenceConfig LoadInfluenceConfig()
        {
            InfluenceConfig config = new();
            string filePath = GetPathTo(pathInfluenceConfig);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<InfluenceConfig>(json);

                    config.interceptFail = ClampInfluence(config.interceptFail, true);
                    config.interceptSuccess = ClampInfluence(config.interceptSuccess, false);

                    config.deadDropFail = ClampInfluence(config.deadDropFail, true);
                    config.deadDropSuccess = ClampInfluence(config.deadDropSuccess, false);

                    config.gatheringFail = ClampInfluence(config.gatheringFail, true);
                    config.gatheringSuccess = ClampInfluence(config.gatheringSuccess, false);

                    config.robberyPlayerEscape = ClampInfluence(config.robberyPlayerEscape, true);
                    config.robberyGoonEscapeSuccess = ClampInfluence(config.robberyGoonEscapeSuccess, true);
                    config.robberyGoonDead = ClampInfluence(config.robberyGoonDead, false);
                    config.robberyGoonEscapeDead = ClampInfluence(config.robberyGoonEscapeDead, false);

                    config.sabotageBombDefused = ClampInfluence(config.sabotageBombDefused, false);
                    config.sabotageGoonKilled = ClampInfluence(config.sabotageGoonKilled, false);
                    config.sabotageBombExploded = ClampInfluence(config.sabotageBombExploded, true);

                    config.passiveInfluenceGainPerDay = ClampInfluence(config.passiveInfluenceGainPerDay, true);

                    config.cartelDealerDied = ClampInfluence(config.cartelDealerDied, false);
                    config.ambushDefeated = ClampInfluence(config.ambushDefeated, false);
                    config.graffitiInfluenceReduction = ClampInfluence(config.graffitiInfluenceReduction, false);
                    config.customerUnlockInfluenceChange = ClampInfluence(config.customerUnlockInfluenceChange, false);


                }
                catch (JsonSerializationException ex)
                {
                    config = new InfluenceConfig();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer influence config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new InfluenceConfig();
                    MelonLogger.Warning("Failed to read CartelEnforcer influence config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Influence config, creating directory and template.");
                config = new InfluenceConfig();
                Save(config);
            }
            return config;
        }

        public static void Save(InfluenceConfig influenceConfig)
        {
            try
            {
                string json = JsonConvert.SerializeObject(influenceConfig, Formatting.Indented);
                string filePath = GetPathTo(pathInfluenceConfig);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                MelonLogger.Warning($"    CartelEnforcer Influence config written to: {filePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Influence config: " + ex);
            }
        }

        private static float ClampInfluence(float value, bool clampPositive)
        {
            if (!clampPositive)
            {
                return Mathf.Clamp(value, -1f, 0f);
            }
            else
            {
                return Mathf.Clamp(value, 0f, 1f);
            }
        }
        #endregion

        #region Cartel Allied Extensions Config
        [Serializable]
        public class CartelAlliedConfig
        {
            public float WestvilleCartelDealerCut = 0.3f;
            public float WestvilleCartelSigningFee = 6000f;

            public float DowntownCartelDealerCut = 0.4f;
            public float DowntownCartelSigningFee = 12000f;

            public float DocksCartelDealerCut = 0.5f;
            public float DocksCartelSigningFee = 18000f;

            public float SuburbiaCartelDealerCut = 0.55f;
            public float SuburbiaCartelSigningFee = 24000f;

            public float UptownCartelDealerCut = 0.60f;
            public float UptownCartelSigningFee = 36000f;

            public int PersuadeCooldownMins = 60;
            public int SupplyQuestCooldownHours = 48;
        }

        public static CartelAlliedConfig LoadAlliedConfig()
        {
            CartelAlliedConfig config = new();
            string filePath = GetPathTo(pathAlliedConfig);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<CartelAlliedConfig>(json);
                }
                catch (JsonSerializationException ex)
                {
                    config = new CartelAlliedConfig();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Allied config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new CartelAlliedConfig();
                    MelonLogger.Warning("Failed to read CartelEnforcer Allied config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Allied config, creating directory and template.");
                config = new CartelAlliedConfig();
                Save(config);
            }
            return config;
        }

        public static void Save(CartelAlliedConfig alliedConfig)
        {
            try
            {
                string json = JsonConvert.SerializeObject(alliedConfig, Formatting.Indented);
                string filePath = GetPathTo(pathAlliedConfig);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                //MelonLogger.Warning($"    CartelEnforcer Allied config written to: {pathAlliedConfig}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Allied Config Data: " + ex);
            }
        }

        #endregion

        #region Cartel Allied Extensions Persistence
        // So here all the quest related allied stuff

        [Serializable]
        public class CartelAlliedQuests
        {
            public bool alliedIntroCompleted = false;
            public int timesPersuaded = 0;
            public int hoursUntilNextSupplies = 48;
        }

        public static CartelAlliedQuests LoadAlliedQuests() 
        {
            CartelAlliedQuests config = new();
            string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
            int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
            string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
            string filePath = GetPathTo(Path.Combine(pathAlliedPersist, fileName));

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<CartelAlliedQuests>(json);
                }
                catch (JsonSerializationException ex)
                {
                    config = new CartelAlliedQuests();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Allied Quests config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new CartelAlliedQuests();
                    MelonLogger.Warning("Failed to read CartelEnforcer Allied Quests config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Allied Quests config, creating directory and template.");
                config = new CartelAlliedQuests();
                Save(config);
            }
            return config;
        }

        public static void Save(CartelAlliedQuests alliedQuestsState)
        {
            try
            {
                string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
                int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
                string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
                string filePath = GetPathTo(Path.Combine(pathAlliedPersist, fileName));
                string json = JsonConvert.SerializeObject(alliedQuestsState, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                // MelonLogger.Warning($"    CartelEnforcer Allied Quest data written to: {saveDestination}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Allied Quests Data: " + ex);
            }


        }
        #endregion

        #region Event frequency config load
        public static EventFrequencyConfig LoadEventFrequencyConfig()
        {
            EventFrequencyConfig config = new();
            string filePath = GetPathTo(pathEventFrequency);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    config = JsonConvert.DeserializeObject<EventFrequencyConfig>(json);
                    // Validate config values
                    List<int> indicesToRemove = new();
                    for (int i = 0; i < config.events.Count; i++)
                    {
                        if (config.events[i].Identifier == string.Empty)
                        {
                            indicesToRemove.Add(i);
                            MelonLogger.Warning("Identifier cannot be empty for event frequency found in file at: " + filePath);
                            continue;
                        }

                        config.events[i].CooldownHours = Mathf.Clamp(config.events[i].CooldownHours, 0, int.MaxValue);
                        config.events[i].InfluenceRequirement = Mathf.Clamp01(config.events[i].InfluenceRequirement);
                        config.events[i].RandomTimeRangePercentage = Mathf.Clamp01(config.events[i].RandomTimeRangePercentage);
                    }

                    if (indicesToRemove.Count > 0)
                    {
                        List<EventFrequency> newList = new();
                        for (int i = 0; i < config.events.Count; i++)
                        {
                            if (indicesToRemove.Contains(i)) continue;
                            else
                                newList.Add(config.events[i]);
                        }
                        config.events = newList;
                    }

                }
                catch (JsonSerializationException ex)
                {
                    config = new();
                    config.InitializeDefault();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Event Frequency config: " + ex.Message);
                }
                catch (Exception ex)
                {
                    config = new();
                    config.InitializeDefault();
                    MelonLogger.Warning("Failed to read CartelEnforcer Event Frequency config: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Event Frequency config, creating directory and template.");
                config = new();
                config.InitializeDefault();
                Save(config);
            }
            return config;
        }

        public static void Save(EventFrequencyConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                string filePath = GetPathTo(pathEventFrequency);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
                MelonLogger.Warning($"    CartelEnforcer Event frequency config written to: {filePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Event frequency config: " + ex);
            }
        }

        #endregion

        #region Event cooldowns persistence
        public static CurrentEventCooldowns LoadPersistentCooldowns()
        {
            CurrentEventCooldowns cooldowns = new();
            string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
            int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
            string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
            string filePath = GetPathTo(Path.Combine(pathEventFrequencyPersist, fileName));
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    cooldowns = JsonConvert.DeserializeObject<CurrentEventCooldowns>(json);

                    // Validate sabotage cooldowns to contain expected string keys
                    bool isValidConfig = true;
                    if (cooldowns.SabotageCooldowns != null)
                    {
                        if (cooldowns.SabotageCooldowns.Count != 3)
                            isValidConfig = false;

                        if (!cooldowns.SabotageCooldowns.Keys.Contains("Laundromat"))
                            isValidConfig = false;

                        if (!cooldowns.SabotageCooldowns.Keys.Contains("Post Office"))
                            isValidConfig = false;

                        if (!cooldowns.SabotageCooldowns.Keys.Contains("Taco Ticklers"))
                            isValidConfig = false;
                    }
                    else
                    {
                        isValidConfig = false;
                    }

                    if (!isValidConfig)
                    {
                        MelonLogger.Error("Cartel Enforcer Frequency persistent data has incorrect sagotage cooldowns. Resetting values");
                        int sabotageHrs = FrequencyOverrides.GetActivityHours("Sabotage");
                        sabotageHrs = sabotageHrs != 0 ? sabotageHrs : UnityEngine.Random.Range(16, 64);
                        cooldowns.SabotageCooldowns = new()
                        {
                            {"Laundromat", sabotageHrs },
                            {"Post Office", sabotageHrs },
                            {"Taco Ticklers", sabotageHrs },
                        };
                    }
                }
                catch (JsonSerializationException ex)
                {
                    cooldowns = new();
                    cooldowns.InitializeDefault();
                    MelonLogger.Error("Failed to deserialize CartelEnforcer Frequency persistent data: " + ex.Message);
                }
                catch (Exception ex)
                {
                    cooldowns = new();
                    cooldowns.InitializeDefault();
                    MelonLogger.Warning("Failed to read CartelEnforcer Frequency persistent data: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Missing CartelEnforcer Frequency persistent data, creating directory and template.");
                cooldowns = new();
                cooldowns.InitializeDefault();
                Save(cooldowns);
            }
            return cooldowns;
        }

        public static void Save(CurrentEventCooldowns cooldowns)
        {
            try
            {
                string orgName = LoadManager.Instance.ActiveSaveInfo.OrganisationName;
                int slotNumber = LoadManager.Instance.ActiveSaveInfo.SaveSlotNumber;
                string fileName = $"{slotNumber}_{SanitizeAndFormatName(orgName)}";
                string filePath = GetPathTo(Path.Combine(pathEventFrequencyPersist, fileName));
                string json = JsonConvert.SerializeObject(cooldowns, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Frequency persistent data: " + ex);
            }
        }
        #endregion

    }
    #endregion
}
