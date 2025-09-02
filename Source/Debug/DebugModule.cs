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
