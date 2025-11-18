
using System.Collections;
using UnityEngine;
using MelonLoader;

using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelEnforcer;

#if MONO
using ScheduleOne.Interaction;
using ScheduleOne.PlayerScripts.Health;
using ScheduleOne.ItemFramework;
using ScheduleOne.Property;
using ScheduleOne.Storage;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.DevUtilities;
using ScheduleOne.Cartel;
using ScheduleOne.Money;
using ScheduleOne.Map;
using ScheduleOne.VoiceOver;
using FishNet;
#else
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.VoiceOver;
using Il2CppFishNet;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
    public static class SabotageEvent
    {

        public static GameObject intBomb = null;
        public static InteractableObject bombInteractable = null;

        public static Light bombLight = null;
        public static Material bombCubeMat = null;
        public static AudioSource bombSound = null;
        
        public static GameObject reactiveFire = null;

        public static List<SabotageEventLocation> locations = new();

        public static List<Player> burningPlayers = new();

        public static int interactionsUntilDefuse = 6;
        public static bool bombDefused = false;

        public static bool sabotageEventActive = false;

        static readonly float maxExplosionDistance = 12f;
        static readonly float maxConcussionDistance = 7f;
        static readonly float maxExplosionDmg = 90f;
        static readonly float minExplosionDmg = 35f;

        static readonly float maxFireTickDamage = 16f;
        static readonly float minFireTickDamage = 6f;

        static readonly int fireTicks = 7;

        public static void PopulateBombLocations()
        {
            foreach (Business business in Business.Businesses)
            {
                if (business.PropertyName == "Laundromat")
                {
                    locations.Add(new SabotageEventLocation(
                        business,
                        new Tuple<Vector3, Vector3>(
                            new Vector3(-27.75f, 0.59f, 22.8f), new Vector3(0f, 0f, 0f)
                        ),
                        new Tuple<Vector3, Vector3>(
                            new Vector3(-26.9f, -0.4f, 25.09f), new Vector3(0f, 65f, 0f)
                        ),
                        new List<Vector3>()
                        {
                            new Vector3(-5.49f, 1.61f, 30.52f), 
                            new Vector3(-43.82f, 1.46f, 49.71f), 
                            new Vector3(-7.46f, 1.36f, 10.72f), 
                            new Vector3(-11.14f, 1.46f, 67.10f)
                        }
                    ));
                }
                else if (business.PropertyName == "Post Office")
                {
                    locations.Add(new SabotageEventLocation(
                        business,
                        new Tuple<Vector3, Vector3>(
                            new Vector3(47.94f, 0.15f, -0.45f), new Vector3(0f, 0f, 0f)
                        ),
                        new Tuple<Vector3, Vector3>(
                            new Vector3(47.8f, 0f, -1.15f), new Vector3(0f, 90f, 0f)
                        ),
                        new List<Vector3>()
                        {
                            new Vector3(56.62f, 1.36f, -25.80f), 
                            new Vector3(7.77f, 1.36f, 19.38f), 
                            new Vector3(65.30f, 1.45f, 33.30f),
                        }
                    ));
                }
                else if (business.PropertyName == "Taco Ticklers")
                {
                    locations.Add(new SabotageEventLocation(
                        business,
                        new Tuple<Vector3, Vector3>(
                            new Vector3(-34f, 0.24f, 78.38f), new Vector3(90f, 270f, 0f)
                        ),
                        new Tuple<Vector3, Vector3>(
                            new Vector3(-34f, -0.3f, 80f), new Vector3(0f, 15f, 0f)
                        ),
                        new List<Vector3>()
                        {
                            new Vector3(-5.49f, 1.61f, 30.52f), 
                            new Vector3(-30.13f, 1.45f, 97.50f), 
                            new Vector3(-43.82f, 1.46f, 49.71f), 
                            new Vector3(-11.14f, 1.46f, 67.10f)
                        }
                    ));
                }
                else
                {
                    continue;
                }
            }

            Log("[SABOTAGE] Finished populating business bomb locations");
            return;
        }

        public static void PrepareBombFXObjects()
        {
            RV rv = UnityEngine.Object.FindObjectOfType<RV>();
            // THIS ONE FOR THE FX FIRE + get bomb obj
            // FX gameobject, instatiate new and disabled version which can be moved around with coordinates and on enabled it plays animation...
            GameObject targetFireExplosionFX = rv.transform.Find("Destroyed RV/FX")?.gameObject;

            reactiveFire = UnityEngine.Object.Instantiate(targetFireExplosionFX);
            if (reactiveFire.gameObject.activeSelf)
                reactiveFire.SetActive(false);
            reactiveFire.name = "CartelEnforcer_ReactiveFire";

            // add to fire fx box collider with size and set is trigger = true, check for player - collider interaction for dealing damage overtime
            BoxCollider fireBc = reactiveFire.AddComponent<BoxCollider>();
            fireBc.size = new Vector3(2.7f, 3f, 8.3f);
            fireBc.center = new Vector3(0.4f, 0f, 0f);
            fireBc.isTrigger = true;

            Rigidbody fireRb = reactiveFire.AddComponent<Rigidbody>(); // needed for trigger with player
            fireRb.isKinematic = true;

            FireCollisionHandler fireHandler = reactiveFire.AddComponent<FireCollisionHandler>();

            // Then the bomb, it can be instantiated based on definition, when exploding bomb it just disables it and moves to next location
            GameObject bombGo = null;
            Func<string, ItemDefinition> GetItem;

#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
            ItemInstance bombInstance = null;
            ItemDefinition def = GetItem("bomb");
            bombInstance = def.GetDefaultInstance();

#if MONO
            if (bombInstance is StorableItemInstance storable)
            {
                if (storable != null && storable.StoredItem != null)
                {
                    Log("Parsing bomb go");
                    bombGo = storable.StoredItem.gameObject;
                }
            }
#else
            StorableItemInstance storable = bombInstance.TryCast<StorableItemInstance>();
            if (storable != null && storable.StoredItem != null)
            {
                Log("Parsing bomb go");
                bombGo = storable.StoredItem.gameObject;
            }
#endif
            // Instantiate new Bomb object, preparing for int object
            intBomb = UnityEngine.Object.Instantiate(bombGo);
            intBomb.SetActive(false);
            intBomb.name = "CartelEnforcer_InteractableBomb";

            // Then that is Bomb_Stored(Clone) basically
            GameObject bombMeshObj = intBomb.transform.Find("Bomb/bomb").gameObject;
            bombMeshObj.AddComponent<BoxCollider>();

            bombInteractable = intBomb.AddComponent<InteractableObject>();
            bombInteractable.message = "x 6 - Defuse bomb";

            void OnBombInteract()
            {
                bombInteractable.message = $"x {interactionsUntilDefuse} - Defuse bomb";
                if (interactionsUntilDefuse > 0)
                {
                    interactionsUntilDefuse--;
                }
                else
                {
                    bombLight.intensity = 1.8f;
                    bombLight.color = Color.green;
                    bombCubeMat.color = Color.green;
                    bombDefused = true;
                    coros.Add(MelonCoroutines.Start(DespawnBombSoon()));
                }
            }
            bombInteractable.onInteractStart.AddListener((UnityEngine.Events.UnityAction)OnBombInteract);

            // Add the red light component
            Transform lightTransform = new GameObject("BombLight").transform;
            lightTransform.parent = intBomb.transform;
            lightTransform.localPosition = new Vector3(0f, 0.23f, 0f);
            bombLight = lightTransform.gameObject.AddComponent<Light>();
            bombLight.color = Color.red;
            bombLight.intensity = 1.8f;
            bombLight.range = 0.4f;
            bombLight.type = LightType.Point;

            // Primitive cube as unlit material that blinks red/grey
            Shader standardShader = Shader.Find("Unlit/Color");
            bombCubeMat = new Material(standardShader);
            bombCubeMat.color = Color.grey;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.parent = intBomb.transform;

            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            mr.material = bombCubeMat;

            cube.transform.localScale = new Vector3(0.023f, 0.01f, 0.01f);
            cube.transform.position = new Vector3(0f, 0.052f, -0.016f);

            // Audio source for bomb beeping
            bombSound = intBomb.AddComponent<AudioSource>();
            bombSound.maxDistance = 7f;
            bombSound.minDistance = 1f;
            bombSound.pitch = 0.8f;
            bombSound.spatialize = true;
            bombSound.spatialBlend = 1f;
            bombSound.spread = 0.7f;
            bombSound.rolloffMode = AudioRolloffMode.Linear;
            bombSound.velocityUpdateMode = AudioVelocityUpdateMode.Dynamic;
            bombSound.volume = 0.12f;
            // Fetch the PoliceVO beeping sound for the audio clip
            PoliceChatterVO voObj = UnityEngine.Object.FindObjectOfType<PoliceChatterVO>(true);
            if (voObj == null)
                Log("[SABOTAGE] Could not find police chatter vo");
            else
                bombSound.clip = voObj.StartEndBeep.AudioSource.clip;

            Log("[SABOTAGE] Instantiated gameobjects for event");

            return;
        }

        public static IEnumerator EvaluateBombEvent()
        {
            while (true)
            {
                yield return Wait60;
                if (!registered) yield break;

                if (!InstanceFinder.IsServer)
                    yield break;
#if MONO
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    continue;
#else
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                    continue;
#endif

                if (sabotageEventActive) continue;

                if (NetworkSingleton<Cartel>.Instance.GoonPool.UnspawnedGoonCount == 0) continue;

                // Check each location, meet either condition
                // active laundering operation or player is inside business?
                SabotageEventLocation selected = null;
                foreach (SabotageEventLocation location in locations)
                {
                    if (location.hoursUntilEnabled != 0) continue;

                    if (location.business.LaunderingOperations.Count != 0 && location.business.currentLaunderTotal > location.business.appliedLaunderLimit * 2f)
                    {
                        Log("[SABOTAGE] Selected by active launder operation and launder threshold met");
                        selected = location;
                        break;
                    }

                    Player nearbyPlayer = Player.GetClosestPlayer(location.bombLocation.Item1, out float distance);
                    if (distance < 20f && nearbyPlayer.CurrentBusiness != null && nearbyPlayer.CurrentBusiness == location.business)
                    {
                        Log("[SABOTAGE] Selected by player inside business");
                        selected = location;
                        break;
                    }

                }
                if (selected == null) 
                {
                    Log("[SABOTAGE] No valid locations for sabotage");
                    continue;
                }


                // Reset cooldown based on influence
                float allInfluence = 0f;
                foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
                {
                    allInfluence += data.Influence;
                }
                // When all influence is close to being max, the business sabotage becomes more infrequent
                // when cartel gets more and more weaker as game progresses, this event should in theory become more common
                float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
                int minHrs = Mathf.RoundToInt(Mathf.Lerp(8f, 16f, allInfluenceNormalized));
                int maxHrs = Mathf.RoundToInt(Mathf.Lerp(18f, 48f, allInfluenceNormalized));
                selected.hoursUntilEnabled = UnityEngine.Random.Range(minHrs, maxHrs);

                // pass
                sabotageEventActive = true;
                coros.Add(MelonCoroutines.Start(GoonPlantBomb(selected)));
            }
            yield return null;
        }

        public static IEnumerator GoonPlantBomb(SabotageEventLocation location)
        {
            Singleton<NotificationsManager>.Instance.SendNotification(
                location.business.PropertyName, 
                $"<color=#FF1E12>Business Sabotage Alert!</color>", 
                NetworkSingleton<MoneyManager>.Instance.LaunderingNotificationIcon, 
                10f, 
                true
            );

            // Decide goon spawn pos
            Vector3 randomPoint = location.sabotagerSpawns[UnityEngine.Random.Range(0, location.sabotagerSpawns.Count)];
            CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(randomPoint);
            goon.Movement.WarpToNavMesh();
            Log($"[SABOTAGE] Sabotager spawned at {goon.CenterPointTransform.position}");
            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            goon.Behaviour.ScheduleManager.DisableSchedule();
            goon.Movement.SetDestination(location.bombLocation.Item1);

            goon.Movement.MoveSpeedMultiplier = 1.3f;

            float distanceToBombLoc = 50f;
            int n = 0;
            // While not in range and traverse lasted less than 30 sec, continue
            while (distanceToBombLoc > 2f && n < 120)
            {
                n++;
                yield return Wait025; // wait traverse
                if (!registered) yield break;
                if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                {
                    // todo reward for killing before plant
                    Log("[SABOTAGE] Goon killed while traverse");
                    EventEnded(location, -0.050f);
                    sabotageEventActive = false;
                    coros.Add(MelonCoroutines.Start(DespawnGoonSoon(goon)));
                    yield break;
                }

                distanceToBombLoc = Vector3.Distance(goon.CenterPoint, location.bombLocation.Item1);
            }

            if (n >= 120 && distanceToBombLoc > 2f)
            {
                Log("[SABOTAGE] Failed traverse timeout");
                sabotageEventActive = false;
                coros.Add(MelonCoroutines.Start(DespawnGoonSoon(goon)));
                yield break;
            }

            goon.Movement.Stop();

            goon.Movement.FacePoint(location.bombLocation.Item1, lerpTime: 0.4f);

            yield return Wait05;
            if (!registered) yield break;
            if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                Log("[SABOTAGE] Goon killed while planting");
                EventEnded(location, -0.050f);
                sabotageEventActive = false;
                coros.Add(MelonCoroutines.Start(DespawnGoonSoon(goon)));
                yield break;
            }

            goon.Avatar.Animation.SetCrouched(true);

            yield return Wait05;
            if (!registered) yield break;
            if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                // Todo reward for killing goon before plant
                Log("[SABOTAGE] Goon killed while planting");
                EventEnded(location, -0.050f);
                sabotageEventActive = false;
                coros.Add(MelonCoroutines.Start(DespawnGoonSoon(goon)));
                yield break;
            }

            goon.SetAnimationTrigger("GrabItem");
            SpawnBomb(location);

            yield return Wait05;
            if (!registered) yield break;

            goon.Avatar.Animation.SetCrouched(false);
            // should walk back inside on its own
            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
            goon.Behaviour.ScheduleManager.EnableSchedule();

            goon.Movement.MoveSpeedMultiplier = 1.6f;

            coros.Add(MelonCoroutines.Start(DespawnGoonSoon(goon)));

            yield return null;
        }

        public static void SpawnBomb(SabotageEventLocation location)
        {
            bombDefused = false;
            interactionsUntilDefuse = UnityEngine.Random.Range(26, 68);
            bombCubeMat.color = Color.grey;
            bombLight.intensity = 0f;

            intBomb.transform.SetPositionAndRotation(location.bombLocation.Item1, Quaternion.Euler(location.bombLocation.Item2));
            intBomb.SetActive(true);
            bombInteractable.message = $"x {interactionsUntilDefuse} - Defuse bomb";

            coros.Add(MelonCoroutines.Start(BombActivated(location)));
            return;
        }

        public static IEnumerator DespawnBombSoon()
        {
            yield return Wait10;
            intBomb.SetActive(false);
            bombCubeMat.color = Color.grey;
            bombLight.intensity = 0f;
            bombSound.volume = 0.1f;
            yield return null;
        }

        public static IEnumerator BombActivated(SabotageEventLocation location)
        {
            int maxWaitSecs = 120;
            int timeWaited = 0;

            float distanceToBomb = 50f;

            bool proximityInitiated = false;
            int ticksInProximity = 0;
            int maxTicksInProximity = 7; // e.g. 14sec after first tick toggle

            bool timeInitiated = false;

            while (!timeInitiated && !bombDefused)
            {
                timeInitiated = (timeWaited >= maxWaitSecs);

                Player.GetClosestPlayer(location.bombLocation.Item1, out distanceToBomb);
                if (!proximityInitiated)
                {
                    proximityInitiated = (distanceToBomb < 6f);
                }
                if (proximityInitiated)
                {
                    Log($"[SABOTAGE] Bomb exploding after {maxTicksInProximity-ticksInProximity} ticks");
                    
                    if (ticksInProximity % 2 == 0)
                    {
                        bombCubeMat.color = Color.grey;
                        bombLight.intensity = 0f;
                    }
                    else
                    {
                        bombCubeMat.color = Color.red;
                        bombLight.intensity = 1.8f;
                        bombLight.color = Color.red;
                    }
                        
                    bombSound.Play();
                    ticksInProximity++;
                    if (ticksInProximity >= maxTicksInProximity) break;
                }

                yield return Wait2;
                if (!registered) yield break;
                timeWaited += 2;
            }

            
            yield return Wait05;
            if (bombDefused)
            {
                EventEnded(location, -0.150f);
                Log("[SABOTAGE] Bomb succesfully defused");
                sabotageEventActive = false;
                yield break;
            }

            coros.Add(MelonCoroutines.Start(ExplosionEvent(location)));

            yield return null;
        }

        public static IEnumerator ExplosionEvent(SabotageEventLocation location)
        {
            Log("[SABOTAGE] Trigger Explosion");

            intBomb.SetActive(false);
            bombCubeMat.color = Color.grey;
            bombLight.intensity = 0f;
            bombSound.volume = 0.1f;

            reactiveFire.transform.SetPositionAndRotation(location.fireLocation.Item1, Quaternion.Euler(location.fireLocation.Item2));
            reactiveFire.SetActive(true);

            Player player = Player.GetClosestPlayer(location.bombLocation.Item1, out float distance);
            if (distance <= maxExplosionDistance)
            {
                float t = distance / maxExplosionDistance;

                float explosionDamage = Mathf.Round(Mathf.Lerp(maxExplosionDmg, minExplosionDmg, t));
                Log($"[SABOTAGE] Direct explosion damage: {explosionDamage}, dist: {distance}");
                player.Health.TakeDamage(explosionDamage);

                if (distance <= maxConcussionDistance)
                {
                    player.Seizure = true;
                    player.Disoriented = true;
                }
            }

            Log("[SABOTAGE] Cancel business launder");
            foreach (LaunderingOperation operation in location.business.LaunderingOperations)
            {
                operation.amount = 0f;
                operation.completionTime_Minutes = 1;
            }

            yield return Wait5;
            if (!registered) yield break;
            if (distance <= maxConcussionDistance)
            {
                player.Seizure = false;
                player.Disoriented = false;
            }

            EventEnded(location, 0.200f);

            yield return Wait30;
            if (!registered) yield break;

            // scale down fire and setactive false
            float currentScale = 1f;
            float targetScale = 0.05f;
            float scaleDownStep = 0.025f;
            while (currentScale >= targetScale)
            {
                yield return Wait025;
                currentScale -= scaleDownStep;
                reactiveFire.transform.localScale = new Vector3(currentScale, currentScale, currentScale);
            }
            reactiveFire.SetActive(false);
            reactiveFire.transform.localScale = new Vector3(1f, 1f, 1f);

            sabotageEventActive = false;
            bombDefused = false;
            Log("[SABOTAGE] End sabotage event");

            yield return null;
        }

        public static IEnumerator SetPlayerBurning(Player player)
        {
            if (burningPlayers.Contains(player))
                yield break;

            burningPlayers.Add(player);

            for (int i = 0; i < fireTicks; i++)
            {
                yield return Wait05;
                if (!registered) yield break;
                if (!player.Health.IsAlive) break;

                float dmg = UnityEngine.Random.Range(minFireTickDamage, maxFireTickDamage);
                Log($"[SABOTAGE] Fire damage: {dmg}, tick {i}");

                player.Health.TakeDamage(dmg, true, true);
            }

            if (burningPlayers.Contains(player))
                burningPlayers.Remove(player);

            yield return null;
        }

        public static IEnumerator DespawnGoonSoon(CartelGoon goon)
        {
            yield return Wait30;
            if (!registered) yield break;

            goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
            goon.Behaviour.ScheduleManager.EnableSchedule();

            if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                goon.Health.Revive();

            goon.Despawn();

            if (goon.Behaviour.CombatBehaviour.Active)
                goon.Behaviour.CombatBehaviour.Disable_Networked(null);

            goon.Movement.MoveSpeedMultiplier = 1f;

            yield return null;
        }

        public static void EventEnded(SabotageEventLocation location, float influenceChange)
        {
            EMapRegion reg = Map.Instance.GetRegionFromPosition(location.business.transform.position);
            if (InstanceFinder.IsServer)
            {
                if (reg == EMapRegion.Northtown)
                    reg = EMapRegion.Westville;
                
                NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(reg, influenceChange);
            }

        }

        public class SabotageEventLocation
        {
            public Business business { get; private set; }
            public int hoursUntilEnabled { get; set; }
            public Tuple<Vector3, Vector3> bombLocation { get; private set; }
            public Tuple<Vector3, Vector3> fireLocation { get; private set; }

            public List<Vector3> sabotagerSpawns { get; private set; }

            public SabotageEventLocation(Business business, Tuple<Vector3, Vector3> bombLocation, Tuple<Vector3, Vector3> fireLocation, List<Vector3> sabotagerSpawns)
            {
                this.business = business;
                this.bombLocation = bombLocation;
                this.fireLocation = fireLocation;
                this.sabotagerSpawns = sabotagerSpawns;
                this.hoursUntilEnabled = UnityEngine.Random.Range(3, 8);
            }

            public void HourPass()
            {
                if (!InstanceFinder.IsServer)
                    return;
#if MONO
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    return;
#else
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                    return;
#endif
                this.hoursUntilEnabled = Mathf.Clamp(hoursUntilEnabled - 1, 0, 48);
            }
        }
    }

#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class FireCollisionHandler : MonoBehaviour
    {
#if IL2CPP
        public FireCollisionHandler(IntPtr ptr) : base(ptr) { }

        public FireCollisionHandler() : base(ClassInjector.DerivedConstructorPointer<FireCollisionHandler>())
            => ClassInjector.DerivedConstructorBody(this);
#endif
        private void OnTriggerEnter(Collider collision)
        {
            GameObject other = collision.gameObject;
            int otherLayer = other.layer;

            if (otherLayer != 6) return;

            Player playerComp = collision.GetComponentInParent<Player>();
            if (playerComp == null)
            {
                Log("No player component found");
                return;
            }

            coros.Add(MelonCoroutines.Start(SabotageEvent.SetPlayerBurning(playerComp)));
        }
    }


}