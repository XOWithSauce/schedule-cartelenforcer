using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using static CartelEnforcer.CartelInventory;


#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;
#else
using Il2CppScheduleOne.Cartel;
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
#if MONO
                    ItemDefinition def = ScheduleOne.Registry.GetItem(seri.ID);
#else
                    ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(seri.ID);
#endif
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
                    newQualityItemList.Add(item as QualityItemInstance);
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

    }
#endregion
}
