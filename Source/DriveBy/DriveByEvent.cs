using System.Collections;
using MelonLoader;
using UnityEngine;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;


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
        public static List<DriveByTrigger> driveByLocations = new();
#if MONO
        private static RaycastHit[] _raycastHitBuffer = new RaycastHit[8];
#else
        // Because NonAlloc Raycast return is of type Il2CppStructArray and not array
        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> _raycastHitBuffer = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit>(8);
#endif

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
            yield return Wait5;
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
                radius = 7f,
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
                radius = 6f,
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
                radius = 6f,
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
                radius = 8f,
                startPosition = new Vector3(93.06f, 2.30f, -37.39f),
                spawnEulerAngles = new Vector3(6f, 0f, 0f),
                endPosition = new Vector3(30.11f, 1.37f, 72.90f)
            };
            driveByLocations.Add(raysEstateTrigger);

            // 12. Behind the Taco Ticklers
            DriveByTrigger tacoTicklerTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-33.19f, 1.46f, 85.58f),
                radius = 6f,
                startPosition = new Vector3(-17.13f, 1.37f, 61.00f),
                spawnEulerAngles = new Vector3(0f, 0f, 0f),
                endPosition = new Vector3(37.97f, -2.63f, 132.08f)
            };
            driveByLocations.Add(tacoTicklerTrigger);

            // 13. Skatepark and surrounding area
            DriveByTrigger skateparkTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(-49.25f, 1.09f, 75.80f),
                radius = 10f,
                startPosition = new Vector3(-93.33f, -1.43f, 70.90f),
                spawnEulerAngles = new Vector3(-8f, 110f, 0f),
                endPosition = new Vector3(-16.86f, 1.37f, -5.03f)
            };
            driveByLocations.Add(skateparkTrigger);

            // 14. Behind Crimson Canary
            DriveByTrigger crimsonTrigger = new DriveByTrigger
            {
                triggerPosition = new Vector3(48.51f, 1.46f, 66.55f),
                radius = 8f,
                startPosition = new Vector3(27.37f, 1.37f, 49.58f),
                spawnEulerAngles = new Vector3(0f, 90f, 0f),
                endPosition = new Vector3(90.05f, 1.37f, 22.96f)
            };
            driveByLocations.Add(crimsonTrigger);

            Log("Succesfully configured Drive By Triggers");
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

            Log("Starting Drive By Evaluation");
            float elapsedSec = 0f;
            while (registered)
            {
                yield return Wait2;
                if (!registered) yield break;
#if MONO
                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                {
                    yield return Wait60;
                    if (!registered) yield break;

                    continue;
                }
#else
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                {
                    yield return Wait60;
                    if (!registered) yield break;

                    continue;
                }
#endif

                elapsedSec += 2f;

                if (driveByActive)
                {
                    yield return Wait60;
                    if (!registered) yield break;

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
                        yield return Wait05;
                        elapsedSec += 0.5f;
                        if (!registered) yield break;

                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition) <= trig.radius)
                        {
                            if (!driveByActive)
                                coros.Add(MelonCoroutines.Start(BeginDriveBy(trig)));
                            break;
                        }
                    }
                }
                else
                {
                    yield return Wait30;
                    if (!registered) yield break;

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
            Log($"[DRIVE BY]TRIG Pos = {trig.triggerPosition}");
            Player player = Player.GetClosestPlayer(trig.triggerPosition, out _);
            Log($"[DRIVE BY] Player = {player}");

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
            Log($"[DRIVE BY] Setting Target to {player.GetComponent<ICombatTargetable>().NetworkObject}");
            thomasInstance.Behaviour.CombatBehaviour.SetTarget(null, player.GetComponent<ICombatTargetable>().NetworkObject);
            thomasInstance.Behaviour.CombatBehaviour.SetWeaponRaised(true);
            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayerMask = LayerMask.GetMask("Terrain", "Default", "Vehicle");

            Vector3 offsetPosition;
            Vector3 toPlayer;
            float angleToPlayer;
            bool wepHits;
            Log("Falling into driveby");
            while (driveByActive)
            {
                yield return Wait025;
                if (!registered) yield break;
                if (bulletsShot >= maxBulletsShot) break;

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
            thomasInstance.gameObject.SetActive(false);
            Log("[DRIVE BY] Drive By Complete");
            hoursUntilDriveBy = UnityEngine.Random.Range(16, 48);
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
