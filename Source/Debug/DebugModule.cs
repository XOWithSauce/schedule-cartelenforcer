using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using HarmonyLib;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.MiniQuest;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.SabotageEvent;
using static CartelEnforcer.StealBackCustomer;
using static CartelEnforcer.AlliedExtension;
using static CartelEnforcer.ConsoleModule;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.NPCs;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ConsoleType = ScheduleOne.Console;
using TMPro;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using ConsoleType = Il2CppScheduleOne.Console;
#endif

namespace CartelEnforcer
{
    public static class DebugModule
    {
        // Coordinate ui elements for debug
        public static TextMeshProUGUI _positionText;
        public static Transform _playerTransform;

        [Conditional("DEBUG")] // Strips out of build the 30kb worth of strings from debug logging
        public static void Log(string msg, [CallerMemberName] string memberName = "")
        {
            if (currentConfig.debugMode)
                MelonLogger.Msg($"[{memberName}] {msg}");
        }

        // Keep console related logging separate and contained within build string db
        public static void LogRelease(string msg, [CallerMemberName] string memberName = "")
        {
            MelonLogger.Msg($"[{memberName}] {msg}");
        }

        #region Console inputs and commands

        public static Dictionary<string, ConsoleCommandBase> consoleTargets = new()
        {
            // Events
            { "robbery", new RobberyTarget() },
            { "driveby", new DriveByTarget() },
            { "miniquest", new MiniQuestTarget() },
            { "intercept", new InterceptDealTarget() },
            { "gathering", new GatheringTarget() },
            { "sabotage", new SabotageTarget() },
            { "stealback", new StealBackCustomerTarget() },

            // Quests
            { "alliedsupplies", new AlliedSuppliesTarget() },
            { "alliedintro", new AlliedIntroTarget() },
            { "truebrothers", new AlliedTrueBrothersTarget() },
            { "unexpectedalliances", new UnexpectedAlliancesTarget() },
            { "infiltratemanor", new InfiltrateManorTarget() },
            { "fourwheels", new FourWheelsTarget() },
        };
        public static void RunCommand(List<string> args)
        {
            if (args.Count == 2 && args[1].ToLower() == "help")
            {
                Help();
                return;
            }

            if (args.Count < 3)
            {
                LogRelease("Usage: cartelenforcer (action) (target)\n    Try: cartelenforcer help");
                return;
            }

            string actionStr = args[1].ToLower();
            string targetStr = args[2].ToLower();
            // Try parse index

            if (!consoleTargets.TryGetValue(targetStr, out ConsoleCommandBase target))
            {
                LogRelease($"Unknown command target '{targetStr}'");
                return;
            }

            CommandSupport requestedMethod = actionStr switch
            {
                "list" => CommandSupport.List,
                "start" => CommandSupport.Start,
                _ => CommandSupport.None
            };

            if ((target.SupportedMethods & requestedMethod) == 0)
            {
                LogRelease($"Command target '{targetStr}' does not support requested method '{requestedMethod}'");
                return;
            }

            switch (requestedMethod)
            {
                case CommandSupport.List:
                    target.List();
                    break;

                case CommandSupport.Start:
                    target.Start();
                    break;

            }
        }

        public static void Help()
        {
            string listmessage = "";
            listmessage += "\nSupported Commands:";

            foreach (ConsoleCommandBase target in consoleTargets.Values)
            {
                listmessage += $"\n\n# {target.Name.ToUpper()}";
                if (target.SupportedMethods.HasFlag(CommandSupport.List))
                    listmessage += $"\ncartelenforcer list {target.Name}";
                if (target.SupportedMethods.HasFlag(CommandSupport.Start))
                    listmessage += $"\ncartelenforcer start {target.Name}";
            }
            LogRelease(listmessage);
            return;
        }
        #endregion

        #region Function inputs for triggering events

        public static IEnumerator OnInputGenerateAlliedIntroQuest()
        {
            if (!alliedQuests.alliedIntroCompleted && activeTruceIntro == null)
            {
                coros.Add(MelonCoroutines.Start(SetupTruceIntroQuest()));
            }
            Log("Generated Allied Intro Quest");
            yield break;
        }

        public static IEnumerator OnInputGenerateAlliedSupplyQuest()
        {
            // if the quest is not been activated
            if (activeAlliedSupplies == null && !alliedSuppliesActive)
            {
                coros.Add(MelonCoroutines.Start(SetupTruceSuppliesQuest()));
            }
            // else quest already exists and can be reactivated
            else if (activeAlliedSupplies != null && !alliedSuppliesActive)
            {
                activeAlliedSupplies.ResetSelf();
            }
            LogRelease("Generated Allied Supply Quest");
            yield break;
        }

        // Debug tool starts instant driveby on nearest and logs info
        public static IEnumerator OnInputStartDriveBy()
        {
            LogRelease("Starting Instant Drive By");
            Player.Local.Health.RecoverHealth(100f);
            float nearest = 150f;
            DriveByTrigger trig = null;
            foreach (var kvp in driveByLocations)
            {
                float distanceTo = Vector3.Distance(Player.Local.CenterPointTransform.position, kvp.Key.triggerPosition);
                if (distanceTo <= nearest)
                {
                    trig = kvp.Key;
                    nearest = distanceTo;
                }
            }
            coros.Add(MelonCoroutines.Start(BeginDriveBy(trig)));
            yield break;
        }

        public static IEnumerator OnInputStartRob()
        {
            Transform playerLocal = Player.Local.transform;
            Dealer[] allDealers = UnityEngine.Object.FindObjectsOfType<Dealer>(true);
            Dealer nearest = null;
            float distanceToP = 160f;
            foreach (Dealer d in allDealers)
            {
                if (d.DealerType == EDealerType.CartelDealer) continue;
                if (!d.IsRecruited) continue;
                if (d.isInBuilding) continue;

                yield return Wait01;
                float dist = Vector3.Distance(d.transform.position, playerLocal.position);
                if (dist < distanceToP)
                {
                    distanceToP = dist;
                    nearest = d;
                }
            }
            LogRelease("Starting robbery on nearest dealer");
            nearest.TryRobDealer();
            yield break;
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
                    LogRelease($"Started Mini Quest for NPC: {random.fullName}");
                }
                else
                {
                    LogRelease($"Failed to give Mini Quest for NPC: {random.fullName}, try again");
                }
            }
            yield break;
        }

        // Start Cartel Intercept Contract
        public static IEnumerator OnInputInterceptContract()
        {
            coros.Add(MelonCoroutines.Start(StartInterceptDeal()));
            LogRelease("Started Intercept Deals event");
            yield break;
        }

        // plant bomb at nearest business to player location
        public static IEnumerator OnInputStartSabotage()
        {
            sabotageEventActive = true;
            SabotageEventLocation selected = null;
            float distance = 500f;
            foreach (SabotageEventLocation loc in locations)
            {
                if (Vector3.Distance(loc.business.transform.position, Player.Local.CenterPointTransform.position) < distance)
                {
                    distance = Vector3.Distance(loc.business.transform.position, Player.Local.CenterPointTransform.position);
                    selected = loc;
                }
            }

            LogRelease($"Starting sabotage event in 10sec at: {selected.business.PropertyName}");
            yield return Wait10;
            if (!registered) yield break;

            coros.Add(MelonCoroutines.Start(GoonPlantBomb(selected)));
            yield break;
        }

        // test steal back feature
        public static IEnumerator OnInputStealNearestCustomer()
        {
            Customer nearest = null;
            float distance = 15f;
            foreach (Customer c in Customer.UnlockedCustomers)
            {
                if (Vector3.Distance(Player.Local.CenterPointTransform.position, c.NPC.CenterPoint) < distance)
                {
                    nearest = c;
                    distance = Vector3.Distance(Player.Local.CenterPointTransform.position, c.NPC.CenterPoint);
                }
            }
            if (nearest == null) yield break;

            LogRelease($"Stealing Nearest Customer: {nearest.NPC.fullName}");
            StealCustomer(nearest.NPC);

            yield break;
        }


        #endregion

        public static IEnumerator GodMode()
        {
            for (; ; )
            {
                yield return Wait01;
                if (!registered) yield break;
                if (Player.Local.Health.CurrentHealth < 100f)
                    Player.Local.Health.RecoverHealth(100f);
            }

            yield break;
        }

        // display player pos 
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

        // positional trigger visuals
        public static IEnumerator SpawnAmbushAreaVisual()
        {
            Log("Spawning Debug visuals for Ambush Areas");

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
            // Shader select order
            Shader standardShader = Shader.Find("Unlit/Color");
            if (standardShader == null)
                standardShader = Shader.Find("Standard");

            Material sphereMaterial = new Material(standardShader);
            sphereMaterial.color = new Color(255f / 255f, 145f / 255f, 0f / 255f);

            foreach (var kvp in driveByLocations)
            {
                DriveByTrigger trig = kvp.Key;
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

    // Patch the Console Submit command functions to add the Debug commands
#if MONO
    [HarmonyPatch(typeof(ConsoleType), "SubmitCommand", new Type[] { typeof(List<string>) })]
#else
    [HarmonyPatch(typeof(ConsoleType), "SubmitCommand", new Type[] { typeof(Il2CppSystem.Collections.Generic.List<string>) })]
#endif
    public static class Console_SubmitCommand_ListString_Patch
    {
#if MONO
        public static bool Prefix(ConsoleType __instance, List<string> args)
        {
#else
        public static bool Prefix(ConsoleType __instance, Il2CppSystem.Collections.Generic.List<string> args)
        {
            List<string> managedArgs = new();
            foreach (string arg in args) // convert from il2cpp list object to normal
                managedArgs.Add(arg);
#endif

            if (args.Count == 0) return true;
            if (args[0].ToLower() == "cartelenforcer")
            {
#if MONO
                DebugModule.RunCommand(args);
#else
                DebugModule.RunCommand(managedArgs);
#endif
                return true;
            }
            return true;

        }
    }


    // This because it needs to be patched for the above patch to work
    [HarmonyPatch(typeof(ConsoleType), "SubmitCommand", new Type[] { typeof(string) })]
    public static class Console_SubmitCommand_String_Patch
    {
        public static bool Prefix(ConsoleType __instance, string args)
        {
            return true;
        }
    }

}
