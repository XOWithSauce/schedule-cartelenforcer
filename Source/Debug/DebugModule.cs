using System.Collections;
using MelonLoader;
using UnityEngine;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.FrequencyOverrides;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.MiniQuest;
using static CartelEnforcer.EndGameQuest;
using System.Diagnostics;

#if MONO
using ScheduleOne.Quests;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.NPCs;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.Building;
using ScheduleOne.EntityFramework;
using ScheduleOne.Properties;
using ScheduleOne.Property;
using ScheduleOne.Interaction;
using ScheduleOne.Storage;
using ScheduleOne.ObjectScripts;
using ScheduleOne.NPCs.Schedules;
using FishNet.Object;
using FishNet.Managing.Object;
using FishNet.Managing;
using TMPro;
#else
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Properties;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppFishNet.Object;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Managing;
using Il2CppTMPro;
#endif

namespace CartelEnforcer
{
    public static class DebugModule
    {
        public static bool debounce = false; // Keyboard Input

        // Coordinate ui elements for debug
        public static TextMeshProUGUI _positionText;
        public static Transform _playerTransform;

        [Conditional("DEBUG")]
        public static void Log(string msg)
        {
            if (currentConfig.debugMode)
                MelonLogger.Msg(msg);
        }

        public static IEnumerator OnInputReplaceGoonPool()
        {
            GoonPool goonPool = NetworkSingleton<Cartel>.Instance.GoonPool;
            CartelGoon[] originalGoons = goonPool.goons;

            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;
            NetworkObject nob = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "CartelGoon")
                {
                    nob = prefab;
                    break;
                }
            }


            Log("Swapping array count: " + NetworkSingleton<Cartel>.Instance.GoonPool.goons.Length);

            int originalCount = originalGoons.Length;

            int extra = 5;
            int newCount = originalCount + extra;

            CartelGoon[] newGoons = new CartelGoon[newCount];

            System.Array.Copy(originalGoons, newGoons, originalCount);

            CartelGoon goonPrefab = originalGoons.FirstOrDefault();

            if (goonPrefab != null)
            {
                for (int i = originalCount; i < newCount; i++)
                {
                    NetworkObject nobNew = UnityEngine.Object.Instantiate<NetworkObject>(nob);
                    CartelGoon newGoon = nobNew.GetComponent<CartelGoon>();
                    newGoon.transform.parent = NPCManager.Instance.NPCContainer;
                    NPCManager.NPCRegistry.Add(newGoon);
                    yield return Wait05;
                    netManager.ServerManager.Spawn(nobNew);
                    yield return Wait05;
                    newGoon.gameObject.SetActive(true);
                    yield return Wait2;
                    newGoon.Movement.enabled = true;
                    newGoon.gameObject.SetActive(true);
                    newGoon.Despawn();
                    newGoons[i] = newGoon;
                    goonPool.unspawnedGoons.Add(newGoon);
                }
                // Replace the old array with the new one.
                goonPool.goons = newGoons;
            }

            foreach(CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
            {
                goon.Health.Revive();
                yield return Wait01;
                // fix having unassigned values here otherwise they afk on first spawn
                if (goon.Behaviour.ScheduleManager.ActionList[0] is NPCEvent_StayInBuilding stayInside)
                {
                    stayInside.Resume();
                }
                yield return Wait05;
                goon.Despawn();
            }
            Log("Array swapped now count: " + NetworkSingleton<Cartel>.Instance.GoonPool.goons.Length);
            yield return Wait2;
            debounce = false;
            yield return null;
        }
        public static IEnumerator OnInputPlaceSafe()
        {
            RV rv = UnityEngine.Object.FindObjectOfType<RV>();
            Transform target = rv.transform.Find("RV/rv/Small Safe");
            if (target == null)
            {
                Log("No target");
                yield break;
            }
            GameObject newSafe = UnityEngine.Object.Instantiate(target.gameObject, Map.Instance.transform);
            newSafe.transform.position = Player.Local.transform.position + Vector3.forward * 3f;
            Safe safeComp = newSafe.GetComponent<Safe>();

            yield return Wait2;
            newSafe.SetActive(true);
            yield return Wait2;
            newSafe.SetActive(true);
            StorageDoorAnimation doorAnim = newSafe.GetComponent<StorageDoorAnimation>();
            doorAnim.Open();
            yield return Wait05;
            doorAnim.Close();

            Func<string, ItemDefinition> GetItem;
#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif

            ItemDefinition defGun = GetItem("m1911");
            ItemInstance gunInst = defGun.GetDefaultInstance(1);
            safeComp.InsertItem(gunInst, true);

            ItemDefinition defMag = GetItem("m1911mag");
            ItemInstance magInst = defMag.GetDefaultInstance(1);
            safeComp.InsertItem(magInst, true);
            int goldBarQty = UnityEngine.Random.Range(1, 4);

            ItemDefinition goldBarDef = GetItem("goldbar");
            ItemInstance goldBar = goldBarDef.GetDefaultInstance(goldBarQty);
            safeComp.InsertItem(goldBar, true);

            ItemDefinition defCash = GetItem("cash");
            int qty = 0;
            if (UnityEngine.Random.Range(0f, 1f) > 0.666f)
                qty = UnityEngine.Random.Range(1000, 4000);
            else
                qty = UnityEngine.Random.Range(800, 3000);

            qty = (int)Math.Round((double)qty / 100) * 100;
            if (qty > 2000)
                defCash.StackLimit = qty;
            ItemInstance cashInstance = defCash.GetDefaultInstance();


            safeComp.InsertItem(cashInstance, true);

            debounce = false;
            yield return null;
        }

        public static IEnumerator OnInputGenerateManorQuest()
        {
            Log("Generating Manor Quest");
            yield return Wait2;
            coros.Add(MelonCoroutines.Start(GenManorDialogOption()));
            debounce = false;
            Log("Generating Quest Done");
            yield return null;
        }

        public static IEnumerator OnInputGenerateEndQuest()
        {
            Log("Generating Quest");
            yield return Wait2;
            coros.Add(MelonCoroutines.Start(GenDialogOption()));
            debounce = false;
            Log("Generating Quest Done");
            yield return null;
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
                if (distanceTo <= nearest)
                {
                    trig = trigItem;
                    nearest = distanceTo;
                }
            }

            Log("Nearest Drive By Trigger");
            Log($"Distance: {Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition)}");
            Log($"In Radius: {Vector3.Distance(Player.Local.CenterPointTransform.position, trig.triggerPosition) <= trig.radius}");
            coros.Add(MelonCoroutines.Start(BeginDriveBy(trig)));
            yield return Wait05;
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
                yield return Wait01;
                if (d is not CartelDealer)
                {
                    regularDealers.Add(d);
                }
            }
            Dealer nearest = null;
            float distanceToP = 100f;
            foreach (Dealer d in regularDealers)
            {
                yield return Wait01;
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
            yield return Wait2;
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
            yield return Wait05;

            Log("\nActivity Frequency Table\n---------------");
            foreach (HrPassParameterMap map in actFreqMapping)
            {
                Log($"\n{map.itemDesc}\n  Ticks Passed: {map.modTicksPassed}\n  Mod HoursUntilNext: {map.currentModHours}\n  Instance HoursUntilNext: {map.Getter()}\n******");
            }
            Log("---------------\n\n\n");
            yield return Wait05;

            Log("\nCartel Stolen Items\n---------------");
            foreach (QualityItemInstance itemInst in cartelStolenItems)
            {
                Log($"\n  Item: {itemInst.Name}\n  Quantity: {itemInst.Quantity}\n  Quality: {itemInst.Quality}\n******");
            }
            Log("---------------\n\n\n");

            yield return Wait05;

            Log("\nMini Quest NPC Status\n---------------");
            foreach (NPC npc in targetNPCs.Keys.ToList())
            {
                Log($"  Name: {npc.name}");
                Log($"    Has Active Quest: {targetNPCs[npc].HasActiveQuest}");
                Log($"    Has Asked Today: {targetNPCs[npc].HasAskedQuestToday}");
            }
            Log("---------------\n\n\n");
            yield return Wait05;

            debounce = false;
        }

        // Start Cartel Intercept Contract
        public static IEnumerator OnInputInterceptContract()
        {
            MelonCoroutines.Start(StartInterceptDeal());
            yield return Wait05;
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
            // prevent stripping
            var meshRenderer = new MeshRenderer();
#if MONO
            var meshFilter = new MeshFilter();
            var boxCollider = new BoxCollider();
            var capsuleCollider = new CapsuleCollider();
#endif

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
                    cube.transform.position = loc.transform.position + new Vector3(0, 25f + rad, 0);
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
            // prevent stripping
            var meshRenderer = new MeshRenderer();
#if MONO
            var meshFilter = new MeshFilter();
            var sphereCollider = new SphereCollider();
#endif
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
                sphere.transform.position = trig.triggerPosition + new Vector3(0, 20f + rad * 2, 0);
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
    }
}
