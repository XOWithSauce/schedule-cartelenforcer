using System.Collections;
using UnityEngine;
using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Levelling;
using ScheduleOne.Economy;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Economy;
#endif

namespace CartelEnforcer
{
    /*
     Todo config the ranged weapons in ambushes the goon always loses versus melee weps because of how the combat works
    to fix it ranged weps probably need to have the minRanged usage values and raise time and maybe even combat beh can shoot while moving???
     */
    public static class AmbushOverrides
    {

        public static ListNewAmbush ambushConfig;
        public static ListNewAmbush gameDefaultAmbush;
        public static AmbushGeneralSettingsSerialized ambushSettings;

        // These 2 are for the ambush class + dealer death spawned defenders, populated based on Ambush/settings.json
        public static AvatarWeapon[] MeleeWeapons;
        public static AvatarWeapon[] RangedWeapons;
        public static IEnumerator ApplyGameDefaultAmbush()
        {
            yield return Wait5;
            if (!registered) yield break;

            // Load the positional configs and game default positions
            ambushConfig = ConfigLoader.LoadAmbushConfig();
            gameDefaultAmbush = ConfigLoader.LoadDefaultAmbushConfig();

            // Load the general settings from settings.json
            ambushSettings = ConfigLoader.LoadAmbushSettings();

            Log("Loaded Ambush Config Data");

            CartelRegionActivities[] regAct = UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true);
            Log("Applying Game Defaults Cartel Ambushes");

            List<Vector3> defaultData;
            NewAmbushConfig loadedConfig;
            int i = 0;
            foreach (CartelRegionActivities act in regAct)
            {
                foreach (CartelAmbushLocation loc in act.AmbushLocations)
                {
                    defaultData = loc.AmbushPoints.Select(tr => tr.position).ToList();
                    loadedConfig = gameDefaultAmbush.addedAmbushes.ElementAt(i);
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

        // TODO there was something in logs about user modded ambushes accessing index out of bounds? what was that
        public static IEnumerator AddUserModdedAmbush() // Todo optimize this code is ugly
        {
            yield return Wait2;
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
                    newAmbushObj.SetActive(false); // To not call the awake for CartelAmbushLocation
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
                        string name = $"SP{(j == 0 ? "" : " (" + j.ToString() + ")")}";
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
                    newAmbushObj.SetActive(true);

                    // Important to add this at the end -> otherwise the networked object refuses to swap out the array for locations
                    newAmbushObj.transform.parent = nextParent;
                }

                Log("Done adding User Modded Ambushes");
            }
            else
            {
                Log("No User Added Ambushes found");
            }
        }
        public static IEnumerator SetAmbushGeneralSettings()
        {
            Log("Setting Ambush general settings...");

            if (ambushSettings.RangedWeaponAssetPaths != null)
            {
                RangedWeapons = new AvatarWeapon[ambushSettings.RangedWeaponAssetPaths.Count];
            }
            if (ambushSettings.MeleeWeaponAssetPaths != null)
            {
                MeleeWeapons = new AvatarWeapon[ambushSettings.MeleeWeaponAssetPaths.Count];
            }

            int assetPaths = ambushSettings.RangedWeaponAssetPaths.Count;
            Log("  Ranged asset paths count: " + assetPaths);
            for (int i = 0; i < assetPaths; i++) 
            {
                Log("  Load: " + i);
                // Instantiate Load into array
#if MONO
                GameObject gameObject = Resources.Load(ambushSettings.RangedWeaponAssetPaths[i]) as GameObject;
#else
                UnityEngine.Object obj = Resources.Load(ambushSettings.RangedWeaponAssetPaths[i]);
                GameObject gameObject = obj.TryCast<GameObject>();
#endif
                Log("    Resource Loaded: " + i);
                Log("  Cast AvatarEquippable: " + i);
                AvatarEquippable equippable = UnityEngine.Object.Instantiate<GameObject>(gameObject, new Vector3(0f, -5f, 0f), Quaternion.identity, null).GetComponent<AvatarEquippable>();
                Log("    Cast Done: " + i);

                if (equippable == null) 
                { 
                    Log($"Equippable failed to load from {ambushSettings.RangedWeaponAssetPaths[i]}");
                    continue;
                }
#if MONO
                Log("  AvatarWeaponCast: " + i);
                if (equippable is AvatarWeapon rangedWeapon)
                {
                    Log("    AvatarWeaponCast Done: " + i);
                    RangedWeapons[i] = rangedWeapon;
                }
#else
                AvatarWeapon weapon = equippable.TryCast<AvatarWeapon>();
                if (weapon != null)
                    RangedWeapons[i] = weapon;
#endif
                Log($"Succesfully loaded {ambushSettings.RangedWeaponAssetPaths[i]}");
            }

            assetPaths = ambushSettings.MeleeWeaponAssetPaths.Count;
            Log("Melee asset paths count: " + assetPaths);
            for (int i = 0; i < assetPaths; i++)
            {
                // same
#if MONO
                GameObject gameObject = Resources.Load(ambushSettings.MeleeWeaponAssetPaths[i]) as GameObject;
#else
                UnityEngine.Object obj = Resources.Load(ambushSettings.MeleeWeaponAssetPaths[i]);
                GameObject gameObject = obj.TryCast<GameObject>();
#endif

                AvatarEquippable equippable = UnityEngine.Object.Instantiate<GameObject>(gameObject, new Vector3(0f, -5f, 0f), Quaternion.identity, null).GetComponent<AvatarEquippable>();
                if (equippable == null)
                {
                    Log($"Equippable failed to load from {ambushSettings.MeleeWeaponAssetPaths[i]}");
                    continue;
                }
#if MONO
                if (equippable is AvatarWeapon weapon)
                    MeleeWeapons[i] = weapon;
#else
                AvatarWeapon weapon = equippable.TryCast<AvatarWeapon>();
                if (weapon != null)
                    MeleeWeapons[i] = weapon;
#endif
                Log($"Succesfully loaded {ambushSettings.MeleeWeaponAssetPaths[i]}");
            }

            FullRank MinRankForRanged = new ((ERank)ambushSettings.MinRankForRanged, 1); // cast from int thats validated in config load
            Ambush.MIN_RANK_FOR_RANGED_WEAPONS = MinRankForRanged;

            // assign pointer to the weapon fields
            CartelActivities instanceActivities = NetworkSingleton<Cartel>.Instance.Activities;
            foreach (CartelActivity globalActivity in instanceActivities.GlobalActivities)
            {
#if MONO        
                if (globalActivity is Ambush ambush)
                {
                    ambush.RangedWeapons = RangedWeapons;
                    ambush.MeleeWeapons = MeleeWeapons;
                    
                }
#else
                Ambush temp = globalActivity.TryCast<Ambush>();
                if (temp != null)
                {
                    temp.RangedWeapons = RangedWeapons;
                    temp.MeleeWeapons = MeleeWeapons;
                }
#endif
            }
            Log("Finished applying ambush general settings");
            yield return null;
        }

        // harmony patch for the after deal ambush
        [HarmonyPatch(typeof(Ambush), "ContractReceiptRecorded")]
        public static class Ambush_ContractReceiptRecorded_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Ambush __instance, ContractReceipt receipt)
            {
                return ambushSettings.AfterDealAmbushEnabled;
            }
        }
    }
}
