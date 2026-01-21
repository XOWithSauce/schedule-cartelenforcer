using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.ConfigLoader;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.CartelPersuade;
using static CartelEnforcer.SuppliesModule;

#if MONO
using ScheduleOne.Quests;
using ScheduleOne.Economy;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Map;
using ScheduleOne.Money;
using ScheduleOne.VoiceOver;
using ScheduleOne.GameTime;
using ScheduleOne.UI;
using ScheduleOne.UI.Handover;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI.Phone.ContactsApp;
using ScheduleOne.Persistence;
#else
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Handover;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Phone.ContactsApp;
using Il2CppScheduleOne.Persistence;
#endif

namespace CartelEnforcer
{

    public static class AlliedExtension
    {
        public static CartelAlliedConfig alliedConfig;
        public static CartelAlliedQuests alliedQuests;
        public static DialogueContainer persuadeContainer;
        public static DialogueNodeData entryNode;

        // If this truce quest gets started right when the state changes it bugs out and throws a jumpscare?
        // maybe its the active dialogue with thomas that triggers it
#if MONO
        public static IEnumerator DelayedStateChange(ECartelStatus oldStatus, ECartelStatus newStatus)
#else
        public static IEnumerator DelayedStateChange(Il2Cpp.ECartelStatus oldStatus, Il2Cpp.ECartelStatus newStatus)
#endif
        {
            yield return Wait5;
            // While game ongoing
#if MONO
            if (oldStatus == ECartelStatus.Unknown && newStatus == ECartelStatus.Truced)

#else
            if (oldStatus == (Il2Cpp.ECartelStatus)0 && newStatus == Il2Cpp.ECartelStatus.Truced)
#endif
            {
                Log("[ALLIEDEXT] Status Change: adding truce dialogue");
                // Player accepted truce with cartel
                foreach (CartelDealer d in DealerActivity.allCartelDealers)
                {
                    AddPersuadeDialogue(d);
                }
                // the mod still tracks the quest need to revert that in config manually it seems
                if (!alliedQuests.alliedIntroCompleted && activeTruceIntro == null)
                {
                    // Wait until exit dialogue because setting up quest during it caused big bug
                    bool CanStart()
                    {
                        return !Singleton<DialogueCanvas>.Instance.isActive && !Singleton<HandoverScreen>.Instance.IsOpen && PlayerSingleton<PlayerCamera>.Instance.activeUIElementCount <= 0;
                    }
#if MONO
                    // Does mono need anything before waituntil?
                    yield return new WaitUntil(CanStart);
#else
                    yield return new WaitUntil((Il2CppSystem.Func<bool>)CanStart);
#endif
                    // When Can start can exit earliest then wait a moment incase the player just 
                    // exited dialogue
                    yield return Wait05;
                    coros.Add(MelonCoroutines.Start(SetupTruceIntroQuest()));
                }
            }
#if MONO
            else if (oldStatus == ECartelStatus.Truced && newStatus == ECartelStatus.Hostile)
#else
            else if (oldStatus == Il2Cpp.ECartelStatus.Truced && newStatus == Il2Cpp.ECartelStatus.Hostile)
#endif
            {
                Log("[ALLIEDEXT] Status Change: Removing hired cartel dealers and assigned customers");

                // If Player recruited cartel dealers
                foreach (CartelDealer d in DealerActivity.allCartelDealers)
                {
                    if (d.IsRecruited)
                    {
                        d.IsRecruited = false;

                        if (d.AssignedCustomers.Count == 0)
                            continue;

                        // Remove assigned customers
                        for (int i = d.AssignedCustomers.Count - 1; i >= 0; i--)
                        {
                            if (d.AssignedCustomers[i] != null)
                            {
                                d.RemoveCustomer(d.AssignedCustomers[i]);
                            }
                        }
                    }
                    if (d.HasBeenRecommended)
                        d.HasBeenRecommended = false;

                    if (d.RelationData.Unlocked)
                        d.RelationData.Unlocked = false;
                }
            }
            yield return null;
        }

#if MONO
        public static void OnAlliedStateChange(ECartelStatus oldStatus, ECartelStatus newStatus)
#else
        public static void OnAlliedStateChange(Il2Cpp.ECartelStatus oldStatus, Il2Cpp.ECartelStatus newStatus)
#endif
        {
            Log("OnAlliedStateChange");
            coros.Add(MelonCoroutines.Start(DelayedStateChange(oldStatus, newStatus)));
            return;
        }

        // On world load
        public static IEnumerator SetupAlliedExtension()
        {
            yield return Wait10;
            Log("[ALLIEDEXT] Setting up Allied features");
            alliedConfig = LoadAlliedConfig();
            alliedQuests = LoadAlliedQuests();
            Log("[ALLIEDEXT]     Loaded Config");

            // Prepare the persuade feature dialogue container
            persuadeContainer = CartelPersuade.InitPersuadeContainer();
            // Init the Supplies quest module
            InitSuppliesModule();

            TimeManager timeInstance = NetworkSingleton<TimeManager>.Instance;
            timeInstance.onMinutePass.Add(new Action(ReduceCooldown));
            Log($"[ALLIEDEXT]     Added minpass reduce cooldown");
#if MONO
            NetworkSingleton<TimeManager>.Instance.onHourPass += OnHourPassEvaluateSupply;
#else
            NetworkSingleton<TimeManager>.Instance.onHourPass += (Il2CppSystem.Action)OnHourPassEvaluateSupply;
#endif
            // So when the status is still unknown, or truced here it should add the listener to the delegates?
            // To manage state change whenever it goes from unknown to truced or truced to hostile
            // But if the player goes and disables the truce feature and then loads world while having the dealers recruited it then doesnt unrecruit them
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Unknown || NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced)
#else
            if (NetworkSingleton<Cartel>.Instance.Status == (Il2Cpp.ECartelStatus)0 || NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced)
#endif
            {
                Cartel instance = NetworkSingleton<Cartel>.Instance;
#if MONO
                instance.OnStatusChange += OnAlliedStateChange;
#else
                instance.OnStatusChange += (Il2CppSystem.Action<Il2Cpp.ECartelStatus, Il2Cpp.ECartelStatus>)OnAlliedStateChange;
#endif
            }
            Log("[ALLIEDEXT]     Applied status callback");

            Log("[ALLIEDEXT]     Parsing Dealers");
            if (DealerActivity.allCartelDealers == null)
                DealerActivity.allCartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);

            if (DealerActivity.allCartelDealers == null || DealerActivity.allCartelDealers.Length == 0)
            {
                Log("[ALLIEDEXT] Failed to initiate AlliedExtensions");
                yield break;
            }

#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced)
#else
            if (NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced)
#endif
            {
                // If world was loaded with cartel already truced, go and reset the dealer cuts and add dialogue options for non recruited cartel
                // Also change mugshot sprite to be the Benzies logo + change First Name to include Region

                bool westvilleRecruited = false;
                foreach (CartelDealer d in DealerActivity.allCartelDealers)
                {
                    switch (d.Region)
                    {
                        case EMapRegion.Westville:
                            d.Cut = alliedConfig.WestvilleCartelDealerCut;
                            break;

                        case EMapRegion.Downtown:
                            d.Cut = alliedConfig.DowntownCartelDealerCut;
                            break;

                        case EMapRegion.Docks:
                            d.Cut = alliedConfig.DocksCartelDealerCut;
                            break;

                        case EMapRegion.Suburbia:
                            d.Cut = alliedConfig.SuburbiaCartelDealerCut;
                            break;

                        case EMapRegion.Uptown:
                            d.Cut = alliedConfig.UptownCartelDealerCut;
                            break;

                        default:
                            d.Cut = 30f;
                            break;
                    }

                    // Change mugshot and first name
                    d.MugshotSprite = benziesLogo;
                    d.FirstName = $"{d.FirstName} ({d.Region})";

                    // Set flag for automatic start of the intro allied quest
                    if (d.Region == EMapRegion.Westville && d.IsRecruited)
                        westvilleRecruited = true;

                    if (d.IsRecruited)
                    {
                        // Already has default dialogue options
                        Log($"[ALLIEDEXT]     - {d.Region} already recruited");
                        continue;
                    }
                    else if (!d.IsRecruited && d.HasBeenRecommended)
                    {
                        // Doesnt have dialogue options to recruit, manage inv or take money
                        // And player has succesfully persuaded the cartel dealer
                        Log($"[ALLIEDEXT]     + {d.Region} add base");
                        AddBaseDialogue(d);
                    }
                    else if (!d.IsRecruited && !d.HasBeenRecommended)
                    {
                        Log($"[ALLIEDEXT]     + {d.Region} add persuade");
                        CartelPersuade.AddPersuadeDialogue(d);
                    }
                }

                // If the allied intro quest not yet completed and cartel truced and first dealer not yet recruited
                if (!alliedQuests.alliedIntroCompleted && activeTruceIntro == null && !westvilleRecruited)
                {
                    coros.Add(MelonCoroutines.Start(SetupTruceIntroQuest()));
                }

            }


            Log("[ALLIEDEXT] Finished setup");
            yield return null;
        }

        public static IEnumerator EvaluateAlliedState()
        {
            if (!currentConfig.alliedExtensions)
                yield break;

            while (registered)
            {
#if MONO
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
#else
                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced)
#endif
                {
                    yield return Wait60;
                    continue;
                }



            }
            yield return null;
        }

        #region Base dialogues
        // These only need to be added whenever the cartel dealer is NOT recruited
        // The data persists and uses the default dialogue options after the first recruit which works fine.
        // But again on state change needs to have iteration back to unrecruit them

#if IL2CPP
        public delegate bool ManagedChoiceValid(out string invalidReason);
#endif

        public static void AddBaseDialogue(CartelDealer d)
        {

            DialogueController controller = d.DialogueHandler.gameObject.GetComponent<DialogueController>();

            #region Recruit Cartel Dealer Dialogue
            DialogueController.DialogueChoice choiceRecruit = new();
            // Recruit payment amount
            float paid = GetCartelRecruitPayment(d.Region);
            choiceRecruit.ChoiceText = $"Help me distribute my product (<color=#FF3008>-${paid}</color>)";
            choiceRecruit.Enabled = true;

            bool CanRecruitCartel(out string reason)
            {
                reason = "";

                // Check if its already recruited
                if (d.IsRecruited)
                {
                    reason = "Cartel Dealer is already recruited.";
                    return false;
                }

                // If the dealers region is not unlocked
                if (Singleton<Map>.Instance.GetUnlockedRegions().Count <= (int)d.Region)
                {
                    reason = "Region is not unlocked yet.";
                    return false;
                }

                // If the cartel dealer in previous region has been recruited or also if its westville then pass that bc no previous
                if (d.Region != EMapRegion.Westville)
                {
                    int prev = (int)d.Region - 1;
                    // Get the previous region dealer and check if it is not recruited then return false
                    if (!NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities((EMapRegion)prev).CartelDealer.IsRecruited)
                    {
                        reason = $"Recruit {(EMapRegion)prev} Cartel Dealer first!";
                        return false;
                    }
                }


                if (!IsTrucedDialogueEnabled())
                {
                    reason = "Cartel is not Truced.";
                    return false;
                }

                // Check that atleast half of the respective regions customers are unlocked
#if MONO
                List<Customer> unlocked = new(Customer.UnlockedCustomers);
#else
                List<Customer> unlocked = new();
                foreach (Customer c in Customer.UnlockedCustomers)
                {
                    if (!unlocked.Contains(c))
                        unlocked.Add(c);
                }
#endif
                int unlockedInRegion = 0;
                int totalInRegion = 0;
                foreach (Customer c in unlocked)
                {
                    if (c.NPC.Region == d.Region)
                    {
                        totalInRegion++;
                        
                        if (c.NPC.RelationData.Unlocked)
                            unlockedInRegion++;
                    }
                }

                if (totalInRegion == 0 || unlockedInRegion == 0 || unlockedInRegion / totalInRegion < 0.5f)
                {
                    int required = Mathf.RoundToInt((float)totalInRegion * 0.5f) - unlockedInRegion;
                    reason = $"Unlock {required} more {d.Region} customers.";
                    return false;
                }

                return true;
            }
#if MONO
            // public delegate bool IsChoiceValid(out string reason)
            choiceRecruit.isValidCheck = new DialogueController.DialogueChoice.IsChoiceValid(CanRecruitCartel);
#else
            // param1 Il2CppSystem.Object @Obj
            // param2 IntPtr method
            // if this is not possible the entire dialogue logic needs to be rewritten specifically for il2cpp
            // TODO: Test this in il2cpp
            ManagedChoiceValid managedCanRecruit = new ManagedChoiceValid(CanRecruitCartel);
            choiceRecruit.isValidCheck = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<DialogueController.DialogueChoice.IsChoiceValid>(managedCanRecruit);
#endif


#if MONO
            choiceRecruit.onChoosen.AddListener(() => { OnRecruitSelected(controller, paid, d); });
#else
            void OnRecruitOptionSelectedWrapped()
            {
                OnRecruitSelected(controller, paid);
            }
            choiceRecruit.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnRecruitOptionSelectedWrapped);
#endif
            controller.AddDialogueChoice(choiceRecruit);
#endregion

            #region Open Cartel Dealer Inventory option
            DialogueController.DialogueChoice choiceInventory = new();
            choiceInventory.ChoiceText = "Open Inventory";
            choiceInventory.Enabled = true;

            bool CanOpenCartelInventory(out string reason)
            {
                reason = "";
                // Check if its not recruited
                if (!d.IsRecruited)
                {
                    reason = "Cartel Dealer is not recruited.";
                    return false;
                }

                if (!IsTrucedDialogueEnabled())
                {
                    reason = "Cartel is not Truced.";
                    return false;
                }

                return true;
            }

#if MONO
            choiceInventory.isValidCheck = new DialogueController.DialogueChoice.IsChoiceValid(CanOpenCartelInventory);
#else
            // param1 Il2CppSystem.Object @Obj
            // param2 IntPtr method
            // if this is not possible the entire dialogue logic needs to be rewritten specifically for il2cpp
            // TODO: Test this in il2cpp
            ManagedChoiceValid managedOpenInv = new ManagedChoiceValid(CanOpenCartelInventory);
            choiceInventory.isValidCheck = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<DialogueController.DialogueChoice.IsChoiceValid>(managedOpenInv);
#endif


#if MONO
            choiceInventory.onChoosen.AddListener(() => { OnInventorySelected(controller, d); });
#else
            void OnInventoryOptionSelectedWrapped()
            {
                OnInventorySelected(controller);
            }
            choiceInventory.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnInventoryOptionSelectedWrapped);
#endif
            controller.AddDialogueChoice(choiceInventory);

#endregion

            #region Take Cut from Cartel Dealer Dialogue
            DialogueController.DialogueChoice choicePayment = new();
            choicePayment.ChoiceText = "I'm here to collect my cut.";
            choicePayment.Enabled = true;

            bool CanTakeCartelDealerPayment(out string reason)
            {
                reason = "";
                // Check if its not recruited
                if (!d.IsRecruited)
                {
                    reason = "Cartel Dealer is not recruited.";
                    return false;
                }
                if (d.Cash == 0f)
                {
                    reason = "Cartel Dealer does not have cash.";
                    return false;
                }

                if (!IsTrucedDialogueEnabled())
                {
                    reason = "Cartel is not Truced.";
                    return false;
                }

                return true;
            }

#if MONO
            choicePayment.isValidCheck = new DialogueController.DialogueChoice.IsChoiceValid(CanTakeCartelDealerPayment);
#else
            // param1 Il2CppSystem.Object @Obj
            // param2 IntPtr method
            // if this is not possible the entire dialogue logic needs to be rewritten specifically for il2cpp
            // TODO: Test this in il2cpp
            ManagedChoiceValid managedTakeMoney = new ManagedChoiceValid(CanTakeCartelDealerPayment);
            choicePayment.isValidCheck = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<DialogueController.DialogueChoice.IsChoiceValid>(managedTakeMoney);
#endif

#if MONO
            choicePayment.onChoosen.AddListener(() => { OnTakeMoneySelected(controller, d); });
#else
            void OnTakeMoneyOptionSelectedWrapped()
            {
                OnTakeMoneySelected(controller);
            }
            choicePayment.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnTakeMoneyOptionSelectedWrapped);
#endif
            controller.AddDialogueChoice(choicePayment);

#endregion

            return;
        }

        public static float GetCartelRecruitPayment(EMapRegion region)
        {
            float paid;
            switch (region)
            {
                case EMapRegion.Westville:
                    paid = 3000f;
                    break;

                case EMapRegion.Downtown:
                    paid = 7000f;
                    break;

                case EMapRegion.Docks:
                    paid = 12000f;
                    break;

                case EMapRegion.Suburbia:
                    paid = 16000f;
                    break;

                case EMapRegion.Uptown:
                    paid = 24000f;
                    break;

                default:
                    paid = 1000f;
                    break;
            }
            return paid;
        }

        // recruit Callback
        public static void OnRecruitSelected(DialogueController controller, float paid, CartelDealer d = null)
        {
            controller.handler.ContinueSubmitted();

            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= paid;
            if (hasCash)
            {
                // if truced and influence in region not at 0
                float regionInfluence = NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(d.Region).Influence;
                NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(d.Region, -regionInfluence);
                d.PlayVO(EVOLineType.Thanks, true);
                d.IsRecruited = true;
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-paid, true, false);
            }
            else
            {
                controller.npc.Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 6f, 1);
                controller.npc.PlayVO(EVOLineType.Annoyed, true);
            }
        }

        public static void OnInventorySelected(DialogueController controller, CartelDealer d = null)
        {
            d.TradeItems();
        }

        public static void OnTakeMoneySelected(DialogueController controller, CartelDealer d = null)
        {
            d.CollectCash();
            d.PlayVO(EVOLineType.Acknowledge, true);
            controller.handler.ContinueSubmitted();
        }

#endregion

        public static bool IsTrucedDialogueEnabled()
        {
#if MONO
            return (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced || currentConfig.debugMode);
#else
            return (NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced || currentConfig.debugMode);
#endif
        }


    }
    
    // Patch the Cartel Deal Manager while Truced to avoid changing the cartel status to hostile upon expiry
    [HarmonyPatch(typeof(CartelDealManager), "ExpireDeal")]
    public static class CartelDealManager_ExpireDeal_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(CartelDealManager __instance)
        {
            // If the allied extensions features not enabled dont patch this method
            if (!currentConfig.alliedExtensions) return true;

            __instance.DealQuest.Expire(true);
            __instance.SendExpiryMessage();
            __instance.ActiveDeal = null;
            // Block the original because it has the hostile change function call
            // NetworkSingleton<Cartel>.Instance.SetStatus(null, ECartelStatus.Hostile, true);
            return false;
        }
    }

    // The influence doesnt really show on the map nor ingame when changing while truced
    // Because of hostile check in class CartelInfluenceChangePopup
    // The function in question is defined inside the Show function so cant be patched
    // so it would be a prefix that checks if its truced -> copy the inner coroutine logic -> return false, else return true
    // This patch is almost identical but it prevents multiple simultaneous changes from showing while truced
    [HarmonyPatch(typeof(CartelInfluenceChangePopup), "Show")]
    public static class CartelInfluenceChangePopup_Show_Patch
    {
        public static bool showEnqueued = false;

        [HarmonyPrefix]
        public static bool Prefix(CartelInfluenceChangePopup __instance, EMapRegion region, float oldInfluence, float newInfluence)
        {
            // If the allied extensions features not enabled dont patch this method
            if (!currentConfig.alliedExtensions) return true;

            // If Cartel is other than truced no need to patch this method
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced) return true;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced) return true;
#endif

            // Other guards based on source code
            if (Singleton<LoadManager>.Instance.IsLoading) return false;
            if (newInfluence >= oldInfluence) return false;

            // Then here call InfluenceChangeAnimation
            if (!showEnqueued)
            {
                showEnqueued = true;
                coros.Add(MelonCoroutines.Start(InfluenceChangeAnimation(__instance, region, oldInfluence, newInfluence)));
            }
            // block the original
            return false;
        }

        // Based on source code these are used to block it
        public static bool CanShowInfluenceChange()
        {
            return !Singleton<DialogueCanvas>.Instance.isActive && 
                !Singleton<DealCompletionPopup>.Instance.IsPlaying && 
                !Singleton<NewCustomerPopup>.Instance.IsPlaying;
        }
        // Basically same as the sealed class coroutine that originally handles the animation
        // inside the Show() function
        public static IEnumerator InfluenceChangeAnimation(CartelInfluenceChangePopup __instance, EMapRegion region, float oldInfluence, float newInfluence)
        {
            // Waituntil can show influence change
#if MONO
            yield return new WaitUntil(CanShowInfluenceChange);
#else
            yield return new WaitUntil((Il2CppSystem.Func<bool>)CanShowInfluenceChange);
#endif

            yield return new WaitForSeconds(0.5f);
            __instance.SetDisplayedInfluence(oldInfluence);
            __instance.TitleLabel.text = $"Benzies' Influence in {region}";
            __instance.Anim.Play();
            yield return new WaitForSeconds(0.8f);
            float elapsed = 0f;
            while (elapsed <= 1.5f)
            {
                elapsed += Time.deltaTime;
                float displayedInfluence = Mathf.Lerp(oldInfluence, newInfluence, elapsed / 1.5f);
                __instance.SetDisplayedInfluence(displayedInfluence);
                yield return new WaitForEndOfFrame();
            }
            __instance.SetDisplayedInfluence(newInfluence);

            showEnqueued = false;
            yield return null;
        }
    }

    // What about the contacts/region can those be easily retoggled while truced to still show influence?
    // Phone ContactsApp class
    // In SetSelectedRegion it has a check for hostile cartel
    // This can be postfixed entirely actually, just write the inverse enable logic back in postfix if truced
    // And then additionally can change the reasoning text for why the region is locked?

    [HarmonyPatch(typeof(ContactsApp), "SetSelectedRegion")]
    public static class ContactsApp_SetSelectedRegion_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ContactsApp __instance, EMapRegion region, bool selectNPC)
        {
            // If the allied extensions features not enabled dont patch this method
            if (!currentConfig.alliedExtensions) return;

            // If not truced dont patch
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced) return;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced) return;
#endif

            // Northtown doesnt have influence, do nothing
            if (region == EMapRegion.Northtown) return;

            // For the slider? This needs to be done for each except northtown
            float influence = NetworkSingleton<Cartel>.Instance.Influence.GetInfluence(region);
            __instance.LowerContainer.gameObject.SetActive(true);
            __instance.HorizontalScrollbarRectTransform.anchoredPosition = new Vector2(0f, 77f); //vis=true=77f
            __instance.UnlockRegionSliderNotch.gameObject.SetActive(true); // Show each slider even while previous ones not unlocked
            __instance.InfluenceSlider.value = influence;
            __instance.InfluenceCountLabel.text = Mathf.RoundToInt(influence*1000f).ToString() + " / 1000";
            __instance.InfluenceText.text = "Cartel influence in " + region.ToString();

            // Now after this all regions should show influence while truced
            // and also show that they are locked
            // while working with cartel

            MapRegionData regionData = Singleton<Map>.Instance.GetRegionData(region);
            if (regionData.IsUnlocked) return;

            // Disable the unavailable region lock for all regions docks and above
            if ((int)region >= (int)EMapRegion.Docks && __instance.RegionLocked_Unavailable.gameObject.activeSelf)
                __instance.RegionLocked_Unavailable.gameObject.SetActive(false);

            __instance.RegionLockedContainer.gameObject.SetActive(true); // activate locked container
            __instance.RegionLocked_CartelInfluence.gameObject.SetActive(true); // influence lock
            // Influence modified title text
            EMapRegion prevRegion = region - 1;
            __instance.RegionLocked_CartelInfluence_Text.text = $"Reduce cartel influence in {prevRegion}\n\n\nto unlock this region.";

            // Cant directly fit the text into that cartel influence text
            // needs to have another game object with text ui component with correct RectTransform
            // Here if exists then modify, else add and modify? that way it generates it when needed
            Transform customText = __instance.RegionLocked_CartelInfluence_Text.transform.Find("CARTEL_ENFORCER_TEXT");
            if (customText == null)
            {
                GameObject newTextObj = new("CARTEL_ENFORCER_TEXT");
                newTextObj.transform.parent = __instance.RegionLocked_CartelInfluence_Text.transform;
                Text textComp = newTextObj.AddComponent<Text>();
                textComp.text = $"Or recruit the {prevRegion} Cartel Dealer";
                // Modify rect transform
                RectTransform rt = newTextObj.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.anchoredPosition3D = Vector3.zero;
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.offsetMax = new Vector2(150.5f, 214.25f);
                rt.offsetMin = new Vector2(-150.5f, -214.25f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(301f, 428.5f);

                // modify text design
                textComp.alignment = UnityEngine.TextAnchor.MiddleCenter;
                textComp.fontSize = 28;
                textComp.resizeTextMinSize = 0;
                textComp.color = new Color(1f, 0.23f, 0.23f, 0.7f);
                Transform rankTr = __instance.RegionLocked_CartelInfluence_Text.transform.Find("Rank");
                if (rankTr != null)
                {
                    Text rankText = rankTr.GetComponent<Text>();
                    if (rankText != null)
                    {
                        textComp.font = rankText.font;
                    }
                }

                // Transform comp
                newTextObj.transform.localEulerAngles = Vector3.zero;
                newTextObj.transform.localRotation = Quaternion.identity;
                newTextObj.transform.localScale = Vector3.one;
                newTextObj.transform.localPosition = new Vector3(0f, -105f, 0f);
            }
            else
            {
                customText.gameObject.GetComponent<Text>().text = $"Or recruit the {prevRegion} Cartel Dealer";
            }

            return;
        }
    }

    // When player wants to progress to defeat the cartel even while truced
    // The last quest Defeat Cartel OnSleepEnd must be patched to bypass the Hostile check
    [HarmonyPatch(typeof(Quest_DefeatCartel), "OnSleepEnd")]
    public static class Quest_DefeatCartel_OnSleepEnd_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Quest_DefeatCartel __instance)
        {
            // If Allied Extensions are not enabled in mod, dont patch
            if (!currentConfig.alliedExtensions) return true;

            // else flip the check for cartel status in original condition block for begin
#if MONO
            if (__instance.State == EQuestState.Inactive && Singleton<Map>.Instance.GetRegionData(EMapRegion.Uptown).IsUnlocked && NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Truced)
#else
            if (__instance.State == EQuestState.Inactive && Singleton<Map>.Instance.GetRegionData(EMapRegion.Uptown).IsUnlocked && NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Truced)
#endif
            {
                // When returning true after this function the original source runs and wont begin the quest, but
                // since the prerequirements are same, now start the quest here
                __instance.Begin(true);
            }

            return true;
        }
    }

}