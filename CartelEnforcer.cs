using MelonLoader;
using System.Collections;
using UnityEngine;
using ScheduleOne.Persistence;
using Newtonsoft.Json;
using MelonLoader.Utils;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Cartel;
using ScheduleOne.Map;
using Newtonsoft.Json.Serialization;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using TMPro;
using System;
using ScheduleOne.Economy;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using ScheduleOne.ItemFramework;
using System.Reflection;
using ScheduleOne.Dialogue;
using ScheduleOne.Messaging;
using ScheduleOne.ObjectScripts.Cash;
using ScheduleOne.Money;
using ScheduleOne.Combat;

[assembly: MelonInfo(typeof(CartelEnforcer.CartelEnforcer), CartelEnforcer.BuildInfo.Name, CartelEnforcer.BuildInfo.Version, CartelEnforcer.BuildInfo.Author, CartelEnforcer.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CartelEnforcer
{
    public static class BuildInfo
    {
        public const string Name = "Cartel Enforcer";
        public const string Description = "Cartel - Modded and configurable";
        public const string Author = "XOWithSauce";
        public const string Company = null;
        public const string Version = "1.1.0";
        public const string DownloadLink = null;
    }

    #region Persistent JSON Files and Serialization
    [System.Serializable]
    public class ModConfig
    {
        public bool debugMode = true; // While in debug mode, spawn visuals for Cartel Ambushes, Enable Debug Log Messages, 
    }

    // Serializer for base CartelAmbushLocation
    [System.Serializable]
    public class NewAmbushConfig
    {
        public int mapRegion = 0; // Maps out to 0 = Northtown, 5 = Uptown
        public Vector3 ambushPosition = Vector3.zero; // Needed for detection radius check, instantiate new monobeh base at this location
        public List<Vector3> spawnPoints = new(); // note min 4 spawn points, instantiate as child obj new empty transform objects to fill base class AmbushPoints variable
        public float detectionRadius = 10f; // How far player can be at maximum from ambushPosition variable, default 10
    }

    // Serialize this class to json file for configure
    [System.Serializable]
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
    public class CartelEnforcer : MelonMod
    {
        public static ModConfig currentConfig;
        public static ListNewAmbush ambushConfig;
        public static ListNewAmbush gameDefaultAmbush;

        static bool registered = false;
        private bool firstTimeLoad = false;
        static bool debounce = false;

        // Coordinate ui elements
        private static TextMeshProUGUI _positionText;
        private static Transform _playerTransform;

        #region Basic Mod Utils
        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();
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
                }
            }
        }
        private void OnLoadCompleteCb()
        {
            if (registered) return;
            currentConfig = ConfigLoader.Load();

            MelonCoroutines.Start(ApplyGameDefaultAmbush());
            if (currentConfig.debugMode)
                MelonCoroutines.Start(MakeUI());
            registered = true;
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

            if (currentConfig.debugMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.R))
            {
                if (!debounce)
                {
                    debounce = true;
                    MelonCoroutines.Start(OnInputTask());
                }
            }
        }
        #endregion

        #region Robbing Dealers spawns CartelGoons and actually moves items + cash to inventory

        // Debug mode try to rob nearest dealer to test functionality
        public static IEnumerator OnInputTask()
        {
            yield return new WaitForSeconds(1f);
            debounce = false;

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
            yield return null;
        }

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
                if (!IsPlayerNearby(__instance))
                {
                    Log("Player not nearby, allowing original TryRobDealer logic to proceed.");
                    return true;
                }

                Log("Player is nearby! Initiating combat robbery.");

                __instance.MSGConversation.SendMessage(new Message(
                    "HELP BOSS!! Benzies are trying to ROB ME!!",
                    Message.ESenderType.Other, false, -1), true, true);

                MelonCoroutines.Start(RobberyCombatCoroutine(__instance));

                return false;
            }

            private static IEnumerator RobberyCombatCoroutine(Dealer dealer)
            {
                yield return new WaitForSeconds(1f);
                Vector3 spawnPos = Vector3.zero;
                do
                {
                    yield return new WaitForSeconds(0.5f);
                    Log("Finding Spawn Robber Position");
                    Vector3 randomDirection = UnityEngine.Random.onUnitSphere;
                    randomDirection.y = 0;
                    randomDirection.Normalize();
                    float randomRadius = UnityEngine.Random.Range(8f, 16f);
                    Vector3 randomPoint = dealer.transform.position + (randomDirection * randomRadius);
                    dealer.Movement.GetClosestReachablePoint(targetPosition: randomPoint, out spawnPos);
                } while (spawnPos == Vector3.zero); // Because GetClosestReachablePoint can return V3.Zero as default (unreachable)

                List<CartelGoon> spawnedGoons = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnMultipleGoons(spawnPos, 1, false);
                if (spawnedGoons.Count == 0)
                {
                    Log("Failed to spawn goon. Robbery failed.");
                    yield break;
                }

                CartelGoon goon = spawnedGoons[0];
                goon.Movement.Warp(spawnPos);
                yield return new WaitForSeconds(0.5f);
                goon.Behaviour.CombatBehaviour.DefaultWeapon = null;

                dealer.Behaviour.CombatBehaviour.SetTarget(null, dealer.NetworkObject);
                dealer.Behaviour.CombatBehaviour.Enable_Networked(null);
                goon.AttackEntity(dealer);

                MelonCoroutines.Start(StateRobberyCombat(dealer, goon));
            }
        }

        public static IEnumerator StateRobberyCombat(Dealer dealer, CartelGoon goon)
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
                elapsed++;
                Log($"In Combat:\n    Dealer:{dealer.Health.Health}\n    Goon:{goon.Health.Health}");
            }

            if (dealer.Health.IsDead || !dealer.IsConscious || dealer.Health.IsKnockedOut)
            {
                // Dealer is dead Partial rob
                Log("Dealer was defeated! Initiating partial robbery.");

                // Calculate available space, is there better way to do this?? perhaps game methods have something already
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
                MelonCoroutines.Start(NavigateGoonEsacpe(goon));
            }
            else if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                // Goon is dead or knocked out,defended robbery
                Log("Goon was defeated! Robbery attempt defended.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                MelonCoroutines.Start(DespawnSoon(goon));
            }
            else if (Vector3.Distance(Player.Local.CenterPointTransform.position, goon.CenterPointTransform.position) > 90f)
            {
                // Player is out of range

                // For now just make a hacky way to prevent robbery
                // if outside range, then full defend
                Log("Player outside of range. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                MelonCoroutines.Start(DespawnSoon(goon));
            }
            else if (elapsed >= 60)
            {
                Log("State Timed Out. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                MelonCoroutines.Start(DespawnSoon(goon));
            }
        }
        public static IEnumerator DespawnSoon(CartelGoon goon)
        {
            yield return new WaitForSeconds(30f);
            if (goon.IsGoonSpawned)
                goon.Despawn();
            yield return null;
        }

        public static IEnumerator NavigateGoonEsacpe(CartelGoon goon)
        {
            yield return new WaitForSeconds(0.5f);
            // After succesful robbery, navigate goon towards nearest CartelDealer apartment door
            CartelDealer[] cartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            float distance = 150f;
            Vector3 destination = Vector3.zero;
            foreach (CartelDealer d in cartelDealers)
            {
                yield return new WaitForSeconds(0.1f);
                if (d.isInBuilding && d.CurrentBuilding != null)
                {
                    NPCEnterableBuilding building = d.CurrentBuilding;
                    ScheduleOne.Doors.StaticDoor door = building.GetClosestDoor(goon.CenterPointTransform.position, false);
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
            MelonCoroutines.Start(ApplyAdrenalineRush(goon));

            if (destination == Vector3.zero || !goon.Movement.CanGetTo(closest)) // If the destination look up fails or cant traverse to
            {
                Log("No Destination or can not traverse to it");
                MelonCoroutines.Start(DespawnSoon(goon));
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
                elapsedNav++;
                
                Log("Escaping...");
                Log($"Current Beh: {goon.Behaviour.activeBehaviour}");
                if (goon.Movement.CurrentDestination != appliedDest)
                    goon.Movement.SetDestination(appliedDest);

                if (goon.Behaviour.activeBehaviour.ToString().Contains("Follow Schedule"))
                    continue;

                if (goon.Behaviour.activeBehaviour != null && goon.Behaviour.activeBehaviour is CombatBehaviour)
                {
                    Log("Resetting Combat Behaviour!");
                    goon.Behaviour.activeBehaviour.SendEnd();
                    yield return new WaitForSeconds(0.2f);
                    goon.Movement.SetDestination(closest);
                }
            }

            if (goon.isInBuilding)
            {
                // The goon successfully escaped.
                Log("Goon Escaped to Cartel Dealer!");
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
                MelonCoroutines.Start(DespawnSoon(goon));
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
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);

            yield return new WaitForSeconds(5f);
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);
            goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origRun, 0.2f);

            yield return new WaitForSeconds(5f);
            goon.Movement.WalkSpeed = origWalk;
            goon.Movement.RunSpeed = origRun;
        }
        #endregion

        #region Load and Apply Serialized Ambush Data
        public static IEnumerator ApplyGameDefaultAmbush()
        {
            yield return new WaitForSeconds(3f);
            ambushConfig = ConfigLoader.LoadAmbushConfig();
            gameDefaultAmbush = ConfigLoader.LoadDefaultAmbushConfig();
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
            Log("Done Applying Game Defaults");
            MelonCoroutines.Start(AddUserModdedAmbush());
            yield return null;
        }

        public static IEnumerator AddUserModdedAmbush()
        {
            yield return new WaitForSeconds(20f);
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

            if (currentConfig.debugMode)
                MelonCoroutines.Start(SpawnAmbushAreaVisual());
        }

        #endregion

        #region Debug Mode Content
        public static void Log(string msg)
        {
            if (currentConfig.debugMode)
                MelonLogger.Msg(msg);
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
            Log("Spawning Debug visuals");
            // prevent stripping
            var meshRenderer = new MeshRenderer();
            var meshFilter = new MeshFilter();
            var boxCollider = new BoxCollider();
            var capsuleCollider = new CapsuleCollider();

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
                    cube.transform.position = loc.transform.position + new Vector3(0, 30f, 0);
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