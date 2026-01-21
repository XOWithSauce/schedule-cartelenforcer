using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Collections;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.AlliedCartelDialogue;
using static CartelEnforcer.AlliedExtension;
using static CartelEnforcer.CartelPersuade;

#if MONO
using ScheduleOne.Economy;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Combat;
using ScheduleOne.Cartel;
using ScheduleOne.Dialogue;
using ScheduleOne.DevUtilities;
using ScheduleOne.VoiceOver;
using ScheduleOne.Persistence;
#else
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.Persistence;
#endif


namespace CartelEnforcer
{
    // Patch the Dialogue Controller for cartel dealers Check Choice and Choice Callback for custom dialogues
    // Show reasoning for disabled choice opt patch here
    [HarmonyPatch(typeof(DialogueController_Dealer), "CheckChoice")]
    public static class DialogueController_Dealer_CheckChoice_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(DialogueController_Dealer __instance, ref string choiceLabel, ref bool __result, ref string invalidReason)
        {
            Log("[ALLIEDEXT] Check Choice Postfix");

            // If the dealer is not cartel dealer dont patch
            if (__instance.Dealer.DealerType == EDealerType.PlayerDealer) return;
            // If the allied extensions features not enabled dont patch this method
            if (!currentConfig.alliedExtensions) return;

            // Check if the custom dialogue is active
            if (DialogueHandler.activeDialogue == null) return;

            if (DialogueHandler.activeDialogue.name == "CARTEL_ENFORCER_PERSUADE")
            {
                __instance.OverrideContainer.DialogueNodeData[0].DialogueText = dealerEntryNodeTexts[UnityEngine.Random.Range(0, dealerEntryNodeTexts.Count)];

                if (choiceLabel == "EXIT")
                {
                    __result = true;
                    return;
                }
                if (__instance.Dealer.IsRecruited || __instance.Dealer.HasBeenRecommended)
                {
                    invalidReason = "Can't persuade this dealer.";
                    __result = false; // Because it shouldnt be interactable
                }
                if (choiceLabel == "START_PERSUADE")
                {
                    if (persuadeCooldown != 0)
                    {
                        invalidReason = $"<color=#DE3F31>Wait {persuadeCooldown} minutes before trying again.</color>";
                        // Update the cooldown text while the dialogue is open
                        coros.Add(MelonCoroutines.Start(UpdatePersuadeCooldownText()));
                        __result = false;
                    }
                    else
                    {
                        __result = true;
                    }
                }
                else
                {
                    __result = true;
                }
            }
            return;
        }
    }
    // First it should check the custom entry which opens choices
    // That triggers the chance calculation for the next choices and then changes the displayed text on them
    // After selecting any of the applicable choice labels 
    // Use the percentage that is currently displayed on it
    // And then compute the chance -> callback action for failure/success
    [HarmonyPatch(typeof(DialogueController_Dealer), "ChoiceCallback")]
    public static class DialogueController_Dealer_ChoiceCallback_Patch
    {
        public static readonly float redColorThreshold = 0.12f;
        public static readonly float yellowColorThreshold = 0.25f;

        public static IEnumerator EnableDelayed(DialogueController_Dealer __instance, string choiceLabel)
        {
            AlliedExtension.alliedQuests.timesPersuaded = 0;
            __instance.Dealer.HasBeenRecommended = true;

            // Close the dialogue container
            __instance.handler.EndDialogue();
            yield return Wait05;
            if (!registered) yield break;

            // Get rid of the override container
            __instance.ClearOverrideContainer();
            // Add the base options (recruit, manage inv, take money)
#if MONO
            AlliedExtension.AddBaseDialogue(__instance.Dealer as CartelDealer);
#else
            CartelDealer temp = __instance.Dealer.TryCast<CartelDealer>();
            if (temp != null)
            {
                AlliedExtension.AddBaseDialogue(temp);
            }
#endif

            if (choiceLabel == "THREATEN_CARTEL")
            {
                __instance.Dealer.SetPanicked();
                __instance.npc.PlayVO(EVOLineType.Scared, false);
            }
            else
            {
                __instance.npc.PlayVO(EVOLineType.Think, false);
            }
            yield return Wait1;
            if (!registered) yield break;

            float paid = AlliedExtension.GetCartelRecruitPayment(__instance.Dealer.Region);
            switch (UnityEngine.Random.Range(0, 5))
            {
                case 0:
                    __instance.handler.WorldspaceRend.ShowText($"Pay me ${paid} and I'll work for you.", 10f);
                    break;

                case 1:
                    __instance.handler.WorldspaceRend.ShowText($"Come back with ${paid}. Then we can talk.", 10f);
                    break;

                case 2:
                    __instance.handler.WorldspaceRend.ShowText($"My price is ${paid}. Take it or leave it!", 10f);
                    break;

                case 3:
                    __instance.handler.WorldspaceRend.ShowText($"Fine! For ${paid} cash I'm in.", 10f);
                    break;
            }

            yield return null;
        }

        [HarmonyPrefix]
        public static bool Prefix(DialogueController_Dealer __instance, string choiceLabel)
        {
            Log("[ALLIEDEXT] Choice Callback Prefix for label: " + choiceLabel);
            // If the dealer is not cartel dealer dont patch
            if (__instance.Dealer.DealerType == EDealerType.PlayerDealer) return true;
            // If the allied extensions features not enabled dont patch this method
            if (!currentConfig.alliedExtensions) return true;

            // Check if the custom dialogue is active
            if (DialogueHandler.activeDialogue?.name != "CARTEL_ENFORCER_PERSUADE") return true;

            if (choiceLabel == "START_PERSUADE")
            {
                Log("[ALLIEDEXT] START PERSUADE");
                if (__instance.OverrideContainer.DialogueNodeData.Count != 2)
                {
                    Log("[ALLIEDEXT] Override container is missing nodes");
                    return true;
                }
                // compute the chance
                persuasionChances["CLOTHING_SIMILARITY"] = CalculateClothingSimilarity(__instance.Dealer);
                persuasionChances["CARTEL_INFLUENCE"] = CalculateInfluenceProbability();
                persuasionChances["THREATEN_CARTEL"] = CalculateThreathenProbability();
                persuasionChances["SPREAD_RUMOURS"] = CalculateRumourProbability();
                persuadeCooldown = alliedConfig.PersuadeCooldownMins;
                Log("[ALLIEDEXT] Chance calculated");
                Log("[ALLIEDEXT] Reset nodes");
                for (int i = 0; i < __instance.OverrideContainer.DialogueNodeData[1].choices.Length; i++)
                {
                    DialogueChoiceData choice = __instance.OverrideContainer.DialogueNodeData[1].choices[i];
                    List<string> textTemplates;
                    float chance = 0f;
                    string colorHex = "#000000";
                    // Why doesnt this change color foreach?
                    if (alliedDialogue.TryGetValue(choice.ChoiceLabel, out textTemplates) && persuasionChances.TryGetValue(choice.ChoiceLabel, out chance))
                    {
                        // Change the chance text color based on probability
                        if (chance <= redColorThreshold)
                            colorHex = "#F08473";
                        else if (chance > redColorThreshold && chance <= yellowColorThreshold)
                            colorHex = "#E2E872";
                        else
                            colorHex = "#65E07C";

                        string formattedChoiceText = $"{textTemplates[UnityEngine.Random.Range(0, textTemplates.Count)]} <color={colorHex}>({Mathf.RoundToInt(chance * 100)}% Chance)</color>";
                        choice.ChoiceText = formattedChoiceText;
                    }
                }
                Log($"[ALLIEDEXT] Nodes reset");
            }
            else if (choiceLabel == "EXIT")
            {
                // Close the dialogue container
                __instance.handler.EndDialogue();
                __instance.handler.WorldspaceRend.ShowText($"Just a waste of my time...", 5f);
                __instance.npc.PlayVO(EVOLineType.Annoyed, false);
            }
            else // one of the 4 inner choices
            {
                Log($"[ALLIEDEXT] PERSUADE OPTIONS");
                NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(__instance.Dealer.Region, influenceConfig.cartelDealerPersuaded);

                if (!(!registered || SaveManager.Instance.IsSaving || isSaving))
                {
                    if (AlliedExtension.alliedQuests != null)
                        AlliedExtension.alliedQuests.timesPersuaded++;
                    else
                        Log("[ALLIEDEXT] Allied Quests are null");
                }

                // Check if chance hits
                float chance = 0f;
                if (AlliedCartelDialogue.persuasionChances.TryGetValue(choiceLabel, out chance))
                {
                    if (chance != 0f && UnityEngine.Random.Range(0f, 1f) < chance)
                    {
                        Log("[ALLIEDEXT] Chance hits");
                        coros.Add(MelonCoroutines.Start(EnableDelayed(__instance, choiceLabel)));
                    }
                    else
                    {
                        Log("[ALLIEDEXT] Chance Doesnt hit");

                        switch (UnityEngine.Random.Range(0, 5))
                        {
                            case 0:
                                __instance.handler.WorldspaceRend.ShowText($"Piss off mate!", 10f);
                                __instance.npc.PlayVO(EVOLineType.Annoyed, false);
                                break;

                            case 1:
                                __instance.handler.WorldspaceRend.ShowText($"I'm busy...", 10f);
                                __instance.npc.PlayVO(EVOLineType.No, false);
                                break;

                            case 2:
                                __instance.handler.WorldspaceRend.ShowText($"Get out of here!", 10f);
                                __instance.npc.PlayVO(EVOLineType.Angry, false);
                                break;

                            case 3:
                                __instance.handler.WorldspaceRend.ShowText($"Keep walking bloke!", 10f);
                                __instance.npc.PlayVO(EVOLineType.Annoyed, false);
                                break;
                        }
#if MONO
                        //__instance.handler.EndDialogue();
#else
                        // Maybe there needs to be strict typing
                        // its ControlledDialogueHandler type where as intellisense fills out DialogueHandler in handler property
                        //ControlledDialogueHandler cdHandler = __instance.handler.TryCast<ControlledDialogueHandler>();
                        //if (cdHandler != null)
                        //    cdHandler.EndDialogue();

                        // so test without it?
                        // without it, it still ends dialogue as usual?
#endif

                        if (choiceLabel == "THREATEN_CARTEL")
                        {
                            __instance.Dealer.Behaviour.CombatBehaviour.SetTarget(Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
                            __instance.Dealer.Behaviour.CombatBehaviour.Enable_Networked();
                        }
                    }
                }


            }

            return true;
        }
    }


}