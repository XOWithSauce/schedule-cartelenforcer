using System.Collections;
using MelonLoader;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.ConfigLoader;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.FrequencyOverrides;

#if MONO
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Cartel;
using ScheduleOne.Combat;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Map;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using ScheduleOne.Vehicles.AI;
using ScheduleOne.Persistence;
using FishNet;
#else
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.AI;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
#endif


namespace CartelEnforcer
{
    public static class DriveByEvent
    {
        // Drive By logic
        public static LandVehicle driveByVeh;
        public static VehicleAgent driveByAgent;
        public static VehicleTeleporter driveByTp;
        public static bool driveByActive = false;
        public static Thomas thomasInstance;
        public static ParkData driveByParking;
        public static int hoursUntilDriveBy = 5;
        public static Dictionary<DriveByTrigger, EMapRegion> driveByLocations = new();

#if MONO
        private static RaycastHit[] _raycastHitBuffer = new RaycastHit[8];
#else
        // Because NonAlloc Raycast return is of type Il2CppStructArray and not array
        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> _raycastHitBuffer = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit>(8);
#endif

        [Serializable]
        public class DriveByTrigger
        {
            public string name;
            public Vector3 triggerPosition;
            public float radius;
            public Vector3 spawnEulerAngles;
            public Vector3 startPosition;
            public Vector3 endPosition;
        }

        public static IEnumerator InitializeDriveByData()
        {
            DriveByTriggersSerialized ser = ConfigLoader.LoadDriveByConfig();
            driveByLocations = new();

            // if user added custom triggers, check the regions
            foreach (DriveByTrigger trig in ser.triggers)
                driveByLocations.Add(trig, Singleton<Map>.Instance.GetRegionFromPosition(trig.triggerPosition));



            yield return Wait2;
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
                    //if (driveByVeh.isStatic)
                    //    driveByVeh.isStatic = false;

                    ParkData data = new();
                    data.spotIndex = -1; // set visible false
                    ParkingLot lot = UnityEngine.Object.FindObjectOfType<ParkingLot>(); // find any parking lot
#if MONO
                    data.lotGUID = new Guid(lot.BakedGUID);
#else
                    data.lotGUID = new Il2CppSystem.Guid(lot.BakedGUID);
#endif
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
                    yield return Wait2;
                    if (!registered) yield break;
#if MONO
                    if (thomasInstance.Behaviour.CombatBehaviour.currentWeapon is AvatarRangedWeapon wep)
                    {
                        wep.MaxUseRange = 45f;
                        wep.MinUseRange = 8f;
                        wep.HitChance_MaxRange = 0.4f;
                        wep.HitChance_MinRange = 0.9f;
                        wep.MaxFireRate = 0.1f;
                        wep.CooldownDuration = 0.1f;
                        wep.Damage = 33f;
                    }
#else
                    AvatarRangedWeapon wep = null;
                    try
                    {
                        wep = thomasInstance.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
                    } 
                    catch (InvalidCastException ex)
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
#endif
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

            hoursUntilDriveBy = eventCooldowns.DriveByCooldown;
        }

        public static void GenerateDefaultDriveByTriggers()
        {
            Log("Configuring Drive By Triggers");
            // 1. Suburbia Bus Stop
            DriveByTrigger uptownTrigger = new DriveByTrigger
            {
                name = "Suburbia Bus Stop",
                triggerPosition = new Vector3(110.39f, 5.36f, -111.69f),
                radius = 2f,
                startPosition = new Vector3(144.68f, 5.6f, -103.69f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(17.40f, 1.37f, -103.53f)
            };
            driveByLocations.Add(uptownTrigger, EMapRegion.Suburbia);

            // 2. Suburbia Park Area (same event as the bus stop but trigger diff)
            DriveByTrigger uptownParkTrigger = new DriveByTrigger
            {
                name = "Suburbia Gazebo Park",
                triggerPosition = new Vector3(84.99f, 5.36f, -122.38f),
                radius = 7f,
                startPosition = new Vector3(144.68f, 5.6f, -103.69f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(17.40f, 1.37f, -103.53f)
            };
            driveByLocations.Add(uptownParkTrigger, EMapRegion.Suburbia);

            // 3. Barn path towards road
            DriveByTrigger barnTrigger = new DriveByTrigger
            {
                name = "Barn path towards road",
                triggerPosition = new Vector3(163.29f, 1.18f, -9.95f),
                radius = 6f,
                startPosition = new Vector3(155.20f, 1.37f, 22.79f),
                spawnEulerAngles = new Vector3(0f, 240f, 0f),
                endPosition = new Vector3(89.85f, 5.37f, -81.81f)
            };
            driveByLocations.Add(barnTrigger, EMapRegion.Uptown);

            // 4. Mollys house
            DriveByTrigger mollysTrigger = new DriveByTrigger
            {
                name = "Mollys apartment",
                triggerPosition = new Vector3(-168.44f, -2.54f, 88.32f),
                radius = 15f,
                startPosition = new Vector3(-146.40f, -2.63f, 39.92f),
                spawnEulerAngles = new Vector3(0f, 310f, 0f),
                endPosition = new Vector3(-111.28f, -2.64f, 123.40f)
            };
            driveByLocations.Add(mollysTrigger, EMapRegion.Westville);

            // 5. Car Wash Computer
            DriveByTrigger carWashTrigger = new DriveByTrigger
            {
                name = "Car Wash Computer",
                triggerPosition = new Vector3(-6.05f, 1.21f, -17.83f),
                radius = 5f,
                startPosition = new Vector3(-19.96f, 1.37f, 20.44f),
                spawnEulerAngles = new Vector3(0f, 180f, 0f),
                endPosition = new Vector3(-10.61f, 1.37f, -102.43f)
            };
            driveByLocations.Add(carWashTrigger, EMapRegion.Downtown); // or docks

            // 6. Laundromat computer
            DriveByTrigger laundromatTrigger = new DriveByTrigger
            {
                name = "Laundromat computer",
                triggerPosition = new Vector3(-29.19f, 1.96f, 26.13f),
                radius = 4f,
                startPosition = new Vector3(-17.17f, 1.37f, -22.86f),
                spawnEulerAngles = new Vector3(0f, 0f, 0f),
                endPosition = new Vector3(-16.98f, -2.51f, 123.42f)
            };
            driveByLocations.Add(laundromatTrigger, EMapRegion.Docks);

            // 7. Grocery Market
            DriveByTrigger groceryMarketTrigger = new DriveByTrigger
            {
                name = "Grocery Market Downtown",
                triggerPosition = new Vector3(17.62f, 1.61f, -3.31f),
                radius = 6f,
                startPosition = new Vector3(-11.64f, 1.15f, -43.54f),
                spawnEulerAngles = new Vector3(0f, 60f, 0f),
                endPosition = new Vector3(33.02f, 1.15f, 65.28f)
            };
            driveByLocations.Add(groceryMarketTrigger, EMapRegion.Downtown);

            // 8. Jane's RV
            DriveByTrigger janeRVTrigger = new DriveByTrigger
            {
                name = "Jane's RV",
                triggerPosition = new Vector3(-36.74f, 0.14f, -77.14f),
                radius = 11f,
                startPosition = new Vector3(3.98f, 1.37f, -103.35f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(-16.90f, 1.37f, 52.00f)
            };
            driveByLocations.Add(janeRVTrigger, EMapRegion.Docks);

            // 9. In front of Town Hall stairs
            DriveByTrigger townHallTrigger = new DriveByTrigger
            {
                name = "Town hall stairs",
                triggerPosition = new Vector3(51.28f, 1.48f, 31.03f),
                radius = 6f,
                startPosition = new Vector3(29.97f, 1.37f, 81.63f),
                spawnEulerAngles = new Vector3(0f, 180f, 0f),
                endPosition = new Vector3(-12.39f, 1.37f, -40.43f)
            };
            driveByLocations.Add(townHallTrigger, EMapRegion.Downtown);

            // 10. Alley between Ham Legal and Hyland Tower
            DriveByTrigger alleyTrigger = new DriveByTrigger
            {
                name = "Alley between Ham Legal and Hyland Tower",
                triggerPosition = new Vector3(84.01f, 1.46f, 63.21f),
                radius = 2f,
                startPosition = new Vector3(61.99f, 1.37f, 49.59f),
                spawnEulerAngles = new Vector3(0f, 90f, 0f),
                endPosition = new Vector3(90.05f, 5.37f, -79.52f)
            };
            driveByLocations.Add(alleyTrigger, EMapRegion.Uptown);

            // 11. Road in front of Ray's Estate and Blueball Boutique
            DriveByTrigger raysEstateTrigger = new DriveByTrigger
            {
                name = "Road in front of Ray's Estate and Blueball Boutique",
                triggerPosition = new Vector3(74.65f, 1.37f, -3.24f),
                radius = 8f,
                startPosition = new Vector3(93.06f, 2.30f, -37.39f),
                spawnEulerAngles = new Vector3(6f, 0f, 0f),
                endPosition = new Vector3(30.11f, 1.37f, 72.90f)
            };
            driveByLocations.Add(raysEstateTrigger, EMapRegion.Uptown);

            // 12. Behind the Taco Ticklers
            DriveByTrigger tacoTicklerTrigger = new DriveByTrigger
            {
                name = "Behind the Taco Ticklers",
                triggerPosition = new Vector3(-33.19f, 1.46f, 85.58f),
                radius = 6f,
                startPosition = new Vector3(-17.13f, 1.37f, 61.00f),
                spawnEulerAngles = new Vector3(0f, 0f, 0f),
                endPosition = new Vector3(-28.73f, -2.63f, 132.68f)
            };
            driveByLocations.Add(tacoTicklerTrigger, EMapRegion.Northtown);

            // 13. Skatepark and surrounding area
            DriveByTrigger skateparkTrigger = new DriveByTrigger
            {
                name = "Skatepark and surrounding area",
                triggerPosition = new Vector3(-49.25f, 1.09f, 75.80f),
                radius = 10f,
                startPosition = new Vector3(-93.33f, -1.43f, 70.90f),
                spawnEulerAngles = new Vector3(-8f, 110f, 0f),
                endPosition = new Vector3(-16.86f, 1.37f, -5.03f)
            };
            driveByLocations.Add(skateparkTrigger, EMapRegion.Northtown);

            // 14. Behind Crimson Canary
            DriveByTrigger crimsonTrigger = new DriveByTrigger
            {
                name = "Behind Crimson Canary",
                triggerPosition = new Vector3(48.51f, 1.46f, 66.55f),
                radius = 8f,
                startPosition = new Vector3(27.37f, 1.37f, 49.58f),
                spawnEulerAngles = new Vector3(0f, 90f, 0f),
                endPosition = new Vector3(90.05f, 1.37f, 22.96f)
            };
            driveByLocations.Add(crimsonTrigger, EMapRegion.Downtown);

            // 15. Suburbia Jeremys house
            DriveByTrigger jeremyTrigger = new DriveByTrigger
            {
                name = "Suburbia Jeremys house",
                triggerPosition = new Vector3(69.55f, 5.93f, -117.93f),
                radius = 2f,
                startPosition = new Vector3(144.68f, 5.6f, -103.69f),
                spawnEulerAngles = new Vector3(0f, 270f, 0f),
                endPosition = new Vector3(17.40f, 1.37f, -103.53f)
            };
            driveByLocations.Add(jeremyTrigger, EMapRegion.Suburbia);

            Log("Succesfully configured Drive By Triggers");
        }

        public static IEnumerator EvaluateDriveBy()
        {
            
            yield return Wait5;
            if (!registered) yield break;
            if (!InstanceFinder.NetworkManager.IsServer)
            {
                Log("Not Server instance, returning from Drive By Evaluation");
                yield break;
            }
            float influenceRequirement = GetByID("DriveBy").InfluenceRequirement;
            Log("Starting Drive By Evaluation");
            while (registered)
            {
                yield return Wait2;
                if (!registered) yield break;
                if (Singleton<SaveManager>.Instance.IsSaving || isSaving) continue;
                if (!currentConfig.driveByEnabled) continue;
#if MONO
                bool isHostile = NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile;
#else
                bool isHostile = NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile;
#endif
                bool isInTimeFrame = TimeManager.Instance.IsCurrentTimeWithinRange(2230, 500);
                if (!isHostile || driveByActive || !isInTimeFrame || hoursUntilDriveBy > 0)
                {
                    yield return Wait60;
                    if (!registered) yield break;
                    continue;
                }

                foreach (var kvp in driveByLocations)
                {
                    yield return Wait05;
                    if (!registered) yield break;
                    // not in range
                    if (Vector3.Distance(Player.Local.CenterPointTransform.position, kvp.Key.triggerPosition) > kvp.Key.radius)
                        continue;
                    // in vehicle
                    if (Player.Local.CurrentVehicle != null) 
                        continue;
                    // if influence req. is used, check if its met
                    if (influenceRequirement >= 0f)
                    {
                        if (NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(kvp.Value).Influence < influenceRequirement)
                        {
                            Log("Player detected at trigger, but influence requirement not met");
                            continue; // not enough influence to run drive by
                        }
                    }

                    if (!driveByActive)
                    {
                        coros.Add(MelonCoroutines.Start(BeginDriveBy(kvp.Key)));
                        break;
                    }
                }
            }
            yield break;
        }

        public static IEnumerator BeginDriveBy(DriveByTrigger trig)
        {
            if (driveByActive || !registered) yield break;
            driveByActive = true;
            Log("Beginning Drive By Event");
            Log($"TRIG Pos = {trig.triggerPosition}");
            Player player = Player.GetClosestPlayer(trig.triggerPosition, out _);

            driveByVeh.ExitPark_Networked(null, false);
            driveByVeh.SetTransform_Server(trig.startPosition, Quaternion.Euler(trig.spawnEulerAngles));
            driveByTp.MoveToRoadNetwork(false);
#if MONO
            driveByAgent.Navigate(trig.endPosition, null, DriveByNavComplete);
#else
            driveByAgent.Navigate(trig.endPosition, null, (Il2CppScheduleOne.Vehicles.AI.VehicleAgent.NavigationCallback)DriveByNavComplete);
#endif
            driveByAgent.AutoDriving = true;

            thomasInstance.gameObject.SetActive(true);

            coros.Add(MelonCoroutines.Start(DriveByShooting(player)));
            yield return null;
        }

        public static IEnumerator DriveByShooting(Player player)
        {
            float distToPlayer;
            int maxBulletsShot = UnityEngine.Random.Range(4, 9);
            int bulletsShot = 0;
            thomasInstance.Behaviour.CombatBehaviour.SetTarget(player.GetComponent<ICombatTargetable>().NetworkObject);
            thomasInstance.Behaviour.CombatBehaviour.SetWeaponRaised(true);
            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayerMask = LayerMask.GetMask("Terrain", "Default", "Vehicle");

            Vector3 offsetPosition;
            Vector3 toPlayer;
            float angleToPlayer;
            bool wepHits;
            int maxIter = 80;
            int j = 0;
            Log("Falling into driveby");
            while (driveByActive)
            {
                j++;
                yield return Wait025;
                if (!registered) yield break;
                if (bulletsShot >= maxBulletsShot) break;
                if (j >= maxIter) break;

                distToPlayer = Vector3.Distance(thomasInstance.transform.position, player.CenterPointTransform.position);
                offsetPosition = thomasInstance.transform.position + thomasInstance.transform.up * 1.7f - thomasInstance.transform.right * 0.8f;
                toPlayer = player.CenterPointTransform.position - offsetPosition;
                angleToPlayer = Vector3.SignedAngle(thomasInstance.transform.forward, toPlayer, Vector3.up);
                wepHits = false;
                int hitsFound = Physics.RaycastNonAlloc(offsetPosition, toPlayer, _raycastHitBuffer, 50f);
                Array.Sort(_raycastHitBuffer, 0, hitsFound, new HitComparer());
                wepHits = false;
                for (int i = 0; i < hitsFound; i++)
                {
                    RaycastHit hit = _raycastHitBuffer[i];

                    if ((obstacleLayerMask & 1 << hit.collider.gameObject.layer) != 0)
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

                if (!wepHits) continue;
                if (angleToPlayer < -10f && angleToPlayer > -80f)
                {
                    if (distToPlayer < 15f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.1f || angleToPlayer < -20f && angleToPlayer > -80f)
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 25f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.2f || angleToPlayer < -25f && angleToPlayer > -70f)
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 35f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.4f || angleToPlayer < -30f && angleToPlayer > -60f)
                        {
                            thomasInstance.Behaviour.CombatBehaviour.Shoot();
                            bulletsShot++;
                        }
                    }
                    else if (distToPlayer < 45f)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) > 0.7f || angleToPlayer < -40f && angleToPlayer > -80f)
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
            thomasInstance.gameObject.SetActive(false);
            Log("Drive By Complete");
            coros.Add(MelonCoroutines.Start(ResetDriveByHours()));
        }

        public static IEnumerator ResetDriveByHours()
        {
            if (SaveManager.Instance.IsSaving || isSaving)
            {
#if MONO
                WaitUntil notSaving = new WaitUntil(() => !SaveManager.Instance.IsSaving && !isSaving);
#else
                WaitUntil notSaving = new WaitUntil((Il2CppSystem.Func<bool>)(() => !SaveManager.Instance.IsSaving && !isSaving));
#endif
                yield return notSaving;
            }


            int hours = GetActivityHours("DriveBy");
            if (hours != 0)
            {
                hoursUntilDriveBy = hours;
            }
            else
            {
                // Player wants "game default" value for drive by cooldown
                // that does not exist
                // Use random cooldown
                hoursUntilDriveBy = UnityEngine.Random.Range(24, 68);
            }

            driveByActive = false;
        }

        public class HitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y)
            {
                bool xHasCollider = x.collider != null;
                bool yHasCollider = y.collider != null;
                if (xHasCollider && !yHasCollider)
                {
                    return -1;
                }
                if (!xHasCollider && yHasCollider)
                {
                    return 1;
                }
                if (!xHasCollider && !yHasCollider)
                {
                    return 0;
                }
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
