using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using static CartelEnforcer.AmbushOverrides;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.DriveByEvent;
using static CartelEnforcer.FrequencyOverrides;
using static CartelEnforcer.InfluenceOverrides;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.MiniQuest;

#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.UI;
#else
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI;
#endif


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
        public const string Version = "1.4.0";
        public const string DownloadLink = null;
    }

    public class CartelEnforcer : MelonMod
    {
        public static CartelEnforcer Instance { get; private set; }
        public static ModConfig currentConfig;
        public static List<object> coros = new();
        public static bool registered = false;
        private bool firstTimeLoad = false;
        private static bool isSaving = false;

        #region No Suffering for GC anymore because of this code region
        public static WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
        public static WaitForSeconds Wait025 = new WaitForSeconds(0.25f);
        public static WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
        public static WaitForSeconds Wait2 = new WaitForSeconds(2f);
        public static WaitForSeconds Wait5 = new WaitForSeconds(5f);
        public static WaitForSeconds Wait30 = new WaitForSeconds(30f);
        public static WaitForSeconds Wait60 = new WaitForSeconds(60f);
        #endregion

        public override void OnInitializeMelon()
        {
            base.OnInitializeMelon();
            Instance = this;
            currentConfig = ConfigLoader.Load();
            MelonLogger.Msg("Cartel Enforcer Mod Loaded");
        }

        #region Unity Methods
        public override void OnUpdate()
        {
            if (!registered || currentConfig == null)
                return;
            if (currentConfig.debugMode)
            {
                if (_playerTransform != null && _positionText != null)
                {
                    Vector3 playerPos = _playerTransform.position;
                    string formattedPosition = $"X: {playerPos.x:F2}\nY: {playerPos.y:F2}\nZ: {playerPos.z:F2}";
                    _positionText.text = formattedPosition;
                }

                if (Input.GetKey(KeyCode.LeftControl))
                {
                    // SEE Debug #region in code for InputFunctions
                    // Left CTRL + R to Start Rob Dealer Function to nearest dealer
                    if (Input.GetKey(KeyCode.R))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputStartRob());
                        }
                    }
                    // Left CTRL + G to Start Drive By Instant 
                    else if (Input.GetKey(KeyCode.G))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputStartDriveBy());
                        }
                    }
                    // Left CTRL + H to Give Mini Quest Instantly to one of the NPCs 
                    else if (Input.GetKey(KeyCode.H))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputGiveMiniQuest());
                        }
                    }
                    // Left CTRL + L to Log Big Blop of info
                    else if (Input.GetKey(KeyCode.L))
                    {
                        if (!debounce)
                        {
                            debounce = true;
                            MelonCoroutines.Start(OnInputInternalLog());
                        }
                    }

                    // Left CTRL + T Intercept random deal
                    else if (Input.GetKey(KeyCode.T))
                    {
                        debounce = true;
                        MelonCoroutines.Start(OnInputInterceptContract());
                    }
                }
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 1)
            {
                if (LoadManager.Instance != null && !registered && !firstTimeLoad)
                {
                    firstTimeLoad = true;
#if MONO
                    LoadManager.Instance.onLoadComplete.AddListener(OnLoadCompleteCb);
#else
                    LoadManager.Instance.onLoadComplete.AddListener((UnityEngine.Events.UnityAction)OnLoadCompleteCb);
#endif
                }
            }
            if (buildIndex != 1)
            {
                if (registered)
                {
                    ExitPreTask();
                }
            }
        }
        #endregion

        #region Mod Initialization and Coroutine load order

        private void OnLoadCompleteCb()
        {
            if (registered) return;
            registered = true;

            currentConfig = ConfigLoader.Load();

            cartelStolenItems = ConfigLoader.LoadStolenItems();

            if (currentConfig.driveByEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateDriveBy()));

            coros.Add(MelonCoroutines.Start(InitializeAmbush()));

            if (currentConfig.miniQuestsEnabled)
                coros.Add(MelonCoroutines.Start(InitializeAndEvaluateMiniQuest()));

            if (currentConfig.interceptDeals)
            {
                coros.Add(MelonCoroutines.Start(FetchUIElementsInit()));
                coros.Add(MelonCoroutines.Start(EvaluateCartelIntercepts()));
            }

            if (currentConfig.debugMode)
                MelonCoroutines.Start(MakeUI());
        }

        public static IEnumerator InitializeAndEvaluateDriveBy()
        {
            yield return MelonCoroutines.Start(InitializeDriveByData());

            coros.Add(MelonCoroutines.Start(EvaluateDriveBy()));
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnDriveByAreaVisual());
        }

        public static IEnumerator InitializeAmbush()
        {
            yield return MelonCoroutines.Start(ApplyGameDefaultAmbush());
            yield return MelonCoroutines.Start(AddUserModdedAmbush());
            yield return MelonCoroutines.Start(AfterAmbushInitComplete());
        }

        public static IEnumerator AfterAmbushInitComplete()
        {
            yield return MelonCoroutines.Start(PopulateParameterMap());
            yield return MelonCoroutines.Start(ApplyInfluenceConfig());


            coros.Add(MelonCoroutines.Start(TickOverrideHourPass()));
            Log("Adding HourPass Function to callbacks");
#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += OnHourPassReduceCartelRegActHours;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)OnHourPassReduceCartelRegActHours;
#endif
            if (currentConfig.debugMode)
                yield return MelonCoroutines.Start(SpawnAmbushAreaVisual());
        }

        public static IEnumerator InitializeAndEvaluateMiniQuest()
        {
            yield return InitMiniQuest();
            Log("Adding DayPass Function for Mini Quest");
#if MONO
            NetworkSingleton<TimeManager>.Instance.onDayPass += OnDayPassNewDiag;
#else
            NetworkSingleton<TimeManager>.Instance.onDayPass += (Il2CppSystem.Action)OnDayPassNewDiag;
#endif
            coros.Add(MelonCoroutines.Start(EvaluateMiniQuestCreation()));
            yield return null;
        }

        #endregion

        #region Harmony Patches for Saving and Coroutine safety
        static void ExitPreTask()
        {
            //MelonLogger.Msg("Pre-Exit Task");
            registered = false;
            foreach (object coro in coros)
            {
                if (coro != null)
                    MelonCoroutines.Stop(coro);
            }
            driveByActive = false;
            interceptingDeal = false;
            coros.Clear();
            // Now mostly just the different mod related lists that got populated in init, reset and clear to repopulate everything on new load
            mapReg.Clear();
            driveByLocations.Clear();
            regActivityHours.Clear();
            actFreqMapping.Clear();
            targetNPCs.Clear();
            cartelStolenItems.Clear();
            emptyDrops.Clear();
            targetNPCsList.Clear();
            targetNPCs.Clear();
#if IL2CPP
            CartelActivities_TryStartActivityPatch.activitiesReadyToStart.Clear();
            CartelActivities_TryStartActivityPatch.validRegionsForActivity.Clear();
#endif
        }

        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { typeof(string) })]
        public static class SaveManager_Save_String_Patch
        {
            public static bool Prefix(SaveManager __instance, string saveFolderPath)
            {
                if (!isSaving)
                {
                    isSaving = true;
                    lock (cartelItemLock)
                    {

                        ConfigLoader.Save(cartelStolenItems);
                    }
                }
                isSaving = false;
                return true;
            }
        }
        [HarmonyPatch(typeof(SaveManager), "Save", new Type[] { })]
        public static class SaveManager_Save_Patch
        {
            public static bool Prefix(SaveManager __instance)
            {
                if (!isSaving)
                {
                    isSaving = true;
                    lock (cartelItemLock)
                    {

                        ConfigLoader.Save(cartelStolenItems);
                    }
                }
                isSaving = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(LoadManager), "ExitToMenu")]
        public static class LoadManager_ExitToMenu_Patch
        {
            public static bool Prefix(LoadManager __instance, SaveInfo autoLoadSave = null, MainMenuPopup.Data mainMenuPopup = null, bool preventLeaveLobby = false)
            {
                if (!isSaving)
                {
                    isSaving = true;
                    lock (cartelItemLock)
                    {
                        ConfigLoader.Save(cartelStolenItems);
                    }
                }
                isSaving = false;
                ExitPreTask();
                return true;
            }
        }

        [HarmonyPatch(typeof(DeathScreen), "LoadSaveClicked")]
        public static class DeathScreen_LoadSaveClicked_Patch
        {
            public static bool Prefix(DeathScreen __instance)
            {
                ExitPreTask();
                return true;
            }
        }
#endregion


    }
}