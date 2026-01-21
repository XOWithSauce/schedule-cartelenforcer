using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

using static CartelEnforcer.CartelInventory;

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
        public bool debugMode = false; // While in debug mode, spawn visuals for Cartel Ambushes, Enable Debug Log Messages, etc.

        // From -1.0 to 0.0 to 1.0,
        // -1.0= Activity is ~10 times less frequent
        // 0.0 = Activity is at game default frequency 
        // 1.0 = Activity is ~10 times more frequent
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
        // For drive by its different, random range cooldown is defined by the range -1 -> 1
        public float driveByFrequency = 0.7f;

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
        private static string pathModConfig = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "config.json");
        private static string pathAmbushes = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Ambush", "ambush.json");
        private static string pathDefAmbushes = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Ambush", "default.json");
        private static string pathSettingsAmbushes = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Ambush", "settings.json");
        private static string pathDealerConfig = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Dealers", "dealer.json");
        private static string pathCartelStolen = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "CartelItems"); // Filename {organization}.json
        private static string pathInfluenceConfig = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Influence", "influence.json");
        private static string pathAlliedConfig = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Allied", "config.json");
        private static string pathAlliedPersist = Path.Combine(MelonEnvironment.ModsDirectory, "CartelEnforcer", "Allied", "QuestData"); // Filename {organization}.json

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
                    config.endGameQuestMonologueSpeed = Mathf.Clamp(config.endGameQuestMonologueSpeed, 0f, 1f);

                    if (config.alliedExtensions && !config.endGameQuest)
                    {
                        MelonLogger.Warning("Cartel Enforcer Allied Extensions depend on End Game Quests. Enabling End Game Quest config automatically.");
                        config.endGameQuest = true;
                    }
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
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(pathModConfig));
                File.WriteAllText(pathModConfig, json);
                MelonLogger.Warning($"    CartelEnforcer basic mod config written to: {pathModConfig}");
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
                string json = JsonConvert.SerializeObject(config, Formatting.Indented, settings);
                Directory.CreateDirectory(Path.GetDirectoryName(pathAmbushes));
                File.WriteAllText(pathAmbushes, json);
                MelonLogger.Warning($"    CartelEnforcer User Added Ambush config written to: {pathAmbushes}");

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

                Directory.CreateDirectory(Path.GetDirectoryName(pathDefAmbushes));
                File.WriteAllText(pathDefAmbushes, json);
                MelonLogger.Warning($"    CartelEnforcer Default Ambush config written to: {pathDefAmbushes}");

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
            if (File.Exists(pathSettingsAmbushes))
            {
                try
                {
                    string json = File.ReadAllText(pathSettingsAmbushes);
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
                Directory.CreateDirectory(Path.GetDirectoryName(pathSettingsAmbushes));
                File.WriteAllText(pathSettingsAmbushes, json);
            }

            return config;
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


        }
        #endregion

        #region Cartel Dealer config
        public static CartelDealerConfig LoadDealerConfig()
        {
            CartelDealerConfig config = new();
            if (File.Exists(pathDealerConfig))
            {
                try
                {
                    string json = File.ReadAllText(pathDealerConfig);
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
                Directory.CreateDirectory(Path.GetDirectoryName(pathDealerConfig));
                File.WriteAllText(pathDealerConfig, json);
                MelonLogger.Warning($"    CartelEnforcer Dealer config written to: {pathDealerConfig}");
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
            if (File.Exists(pathInfluenceConfig))
            {
                try
                {
                    string json = File.ReadAllText(pathInfluenceConfig);
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
                Directory.CreateDirectory(Path.GetDirectoryName(pathInfluenceConfig));
                File.WriteAllText(pathInfluenceConfig, json);
                MelonLogger.Warning($"    CartelEnforcer Influence config written to: {pathInfluenceConfig}");
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
            public float DowntownCartelDealerCut = 0.4f;
            public float DocksCartelDealerCut = 0.5f;
            public float SuburbiaCartelDealerCut = 0.55f;
            public float UptownCartelDealerCut = 0.60f;
            public int PersuadeCooldownMins = 60;
            public int SupplyQuestCooldownHours = 48;
        }

        public static CartelAlliedConfig LoadAlliedConfig()
        {
            CartelAlliedConfig config = new();
            if (File.Exists(pathAlliedConfig))
            {
                try
                {
                    string json = File.ReadAllText(pathAlliedConfig);
                    config = JsonConvert.DeserializeObject<CartelAlliedConfig>(json);
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
                Directory.CreateDirectory(Path.GetDirectoryName(pathAlliedConfig));
                File.WriteAllText(pathAlliedConfig, json);
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

        // this gets serialized as null ?
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
            string orgName = LoadManager.Instance.ActiveSaveInfo?.OrganisationName;
            string fileName = SanitizeAndFormatName(orgName);
            if (File.Exists(Path.Combine(pathAlliedPersist, fileName)))
            {
                try
                {
                    string json = File.ReadAllText(Path.Combine(pathAlliedPersist, fileName));
                    config = JsonConvert.DeserializeObject<CartelAlliedQuests>(json);
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
                string orgName = LoadManager.Instance.ActiveSaveInfo?.OrganisationName;
                string fileName = SanitizeAndFormatName(orgName);
                string saveDestination = Path.Combine(pathAlliedPersist, fileName);
                string json = JsonConvert.SerializeObject(alliedQuestsState, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(saveDestination));
                File.WriteAllText(saveDestination, json);
                // MelonLogger.Warning($"    CartelEnforcer Allied Quest data written to: {saveDestination}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Failed to save CartelEnforcer Allied Quests Data: " + ex);
            }


        }
        #endregion

    }
    #endregion
}
