using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.AlliedCartelDialogue;
using static CartelEnforcer.AlliedExtension;

#if MONO
using ScheduleOne.Economy;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using ScheduleOne.UI;
using ScheduleOne.ItemFramework;
using ScheduleOne.Core.Avatar;
using ScheduleOne.Core.Avatar.Properties;
#else
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Core.Avatar;
using Il2CppScheduleOne.Core.Avatar.Properties;
#endif


namespace CartelEnforcer
{
    public static class CartelPersuade
    {
        public static readonly float CLOTHING_SIMILARITY_BASE_CHANCE = 0.01f;
        public static readonly float CLOTHING_SIMILARITY_MAX_CHANCE = 0.85f;

        public static readonly float CARTEL_INFLUENCE_BASE_CHANCE = 0.02f;
        public static readonly float CARTEL_INFLUENCE_MAX_CHANCE = 0.35f;

        public static readonly float THREATEN_CARTEL_BASE_CHANCE = 0.01f;
        public static readonly float THREATEN_CARTEL_MAX_CHANCE = 0.60f;

        public static readonly float SPREAD_RUMOURS_BASE_CHANCE = 0.03f;
        public static readonly float SPREAD_RUMOURS_MAX_CHANCE = 0.45f;

        public static int persuadeCooldown = 60;

        public static readonly float colorSimilarityThreshold = 0.2f;

        #region Persuade dialogues
        public static void ReduceCooldown()
        {
            if (!currentConfig.alliedExtensions) return;

            if (persuadeCooldown > 0)
                persuadeCooldown--;
        }

        public static Conversation InitPersuadeContainer()
        {
            string GetGuid()
            {
#if MONO
                return Guid.NewGuid().ToString();
#else
                return Il2CppSystem.Guid.NewGuid().ToString();
#endif
            }
            // assing this same to all cartel dealers override container
            // assign it only when the cartel dealer has not been recommended!
            Conversation persuadeOverride = ScriptableObject.CreateInstance<Conversation>();
            persuadeOverride.name = "CARTEL_ENFORCER_PERSUADE"; // Object.name
            persuadeOverride.allowExit = true;
            persuadeOverride.BranchNodeData = new(); // Can leave empty
            persuadeOverride.DialogueNodeData = new(); // Fill this with the created nodes
            persuadeOverride.NodeLinks = new(); // Link logic with guids

            // Node 1 Data section

            entryNode = new DialogueNodeData();
            entryNode.DialogueNodeLabel = "ENTRY";
            entryNode.DialogueText = dealerEntryNodeTexts[UnityEngine.Random.Range(0, dealerEntryNodeTexts.Count)];
            entryNode.Guid = GetGuid();
            entryNode.Position = new Vector2(-284f, 572f);
            entryNode.VoiceLine = EVOLineType.Greeting;
            entryNode.choices = new DialogueChoiceData[2];

            // make node11 choice
            DialogueChoiceData node1StartPersuadeChoice = new();
            node1StartPersuadeChoice.ChoiceLabel = "START_PERSUADE";
            node1StartPersuadeChoice.ChoiceText = "<color=#6BBCFF>[Persuade]</color> Just hear me out. You could work for me instead.";
            node1StartPersuadeChoice.Guid = GetGuid();
            node1StartPersuadeChoice.ShowWorldspaceDialogue = true;

            // make node1 exit
            DialogueChoiceData node1ExitChoice = new();
            node1ExitChoice.ChoiceLabel = "EXIT";
            node1ExitChoice.ChoiceText = "Nevermind.";
            node1ExitChoice.Guid = GetGuid();
            node1ExitChoice.ShowWorldspaceDialogue = true;

            entryNode.choices[0] = node1StartPersuadeChoice;
            entryNode.choices[1] = node1ExitChoice;

            // Node 2 Data section
            DialogueNodeData node2 = new DialogueNodeData();
            node2.DialogueNodeLabel = "ENTRY";
            node2.DialogueText = "I'm not so sure about that...";
            node2.Guid = GetGuid();
            node2.Position = new Vector2(-284f, 572f);
            node2.VoiceLine = EVOLineType.Surprised;
            node2.choices = new DialogueChoiceData[5];
            // give node2 choices from the dialogue keys
            for (int i = 0; i < 5; i++)
            {
                DialogueChoiceData newChoice = new();
                newChoice.ChoiceLabel = alliedDialogueKeys[i];
                newChoice.ChoiceText = alliedDialogue[alliedDialogueKeys[i]][0];
                newChoice.Guid = GetGuid();
                newChoice.ShowWorldspaceDialogue = true;
                node2.choices[i] = newChoice;
            }

            // Link the node1 choice11 to open node2
            NodeLinkData node1LinkData = new NodeLinkData();
            node1LinkData.BaseDialogueOrBranchNodeGuid = entryNode.Guid; // Base Dialogue for the persuasion
            node1LinkData.BaseChoiceOrOptionGUID = node1StartPersuadeChoice.Guid; // First choice
            node1LinkData.TargetNodeGuid = node2.Guid; // Persuade choices

            persuadeOverride.DialogueNodeData.Add(entryNode);
            persuadeOverride.DialogueNodeData.Add(node2);
            persuadeOverride.NodeLinks.Add(node1LinkData);
            Log($"Persuade override created");

            return persuadeOverride;
        }

        public static void AddPersuadeDialogue(CartelDealer d)
        {
            if (d == null)
            {
                Log("Dealer null cant add persuade");
                return;
            }
            DialogueController controller = d.DialogueHandler.GetComponent<DialogueController>();
            if (controller == null)
            {
                Log("DGController is null");
                return;
            }

            controller.OverrideContainer = persuadeContainer;

            void OnConversationStarted()
            {
                DialogueHandler.ActiveDialogueNode = entryNode;
            }

            d.DialogueHandler.onConversationStart.AddListener((UnityEngine.Events.UnityAction)OnConversationStarted);

            return;
        }

        public static IEnumerator UpdatePersuadeCooldownText()
        {
            // While the persuade dialogue container is open in the first entry node
            // Update the first dialogue choice not possible text with time
            while (DialogueHandler.ActiveDialogue != null && 
                DialogueHandler.ActiveDialogueNode != null && 
                DialogueHandler.ActiveDialogue.name == "CARTEL_ENFORCER_PERSUADE" && 
                DialogueHandler.ActiveDialogueNode.Guid == entryNode.Guid)
            {
                if (Singleton<DialogueCanvas>.Instance.dialogueChoices.Count == 0) break;
                
                 DialogueChoiceEntry entry = Singleton<DialogueCanvas>.Instance.dialogueChoices[0];
                // If cd hits 0 while dialogue open
                if (persuadeCooldown == 0 && entry.NotPossibleGameObject.activeSelf)
                {
                    // needs to manually enable it again while dialogue open?
                    entry.NotPossibleGameObject.SetActive(false);
                    entry.Button.interactable = true;
                    ColorBlock colors = entry.Button.colors;
                    colors.disabledColor = colors.pressedColor;
                    entry.Button.colors = colors;
                    entry.Label.GetComponent<RectTransform>().offsetMax = new Vector2(0f, 0f);
                }
                else // update the cd text
                {
                    string invalidReason = $"<color=#DE3F31>On cooldown: {persuadeCooldown}</color>";
                    entry.NotPossibleText.text = invalidReason.ToUpper();
                }
                

                Log("Evaluate text");
                yield return Wait1;
                if (!registered) yield break;
            }

            yield return null;
        }

#endregion

        #region Dialogue Choice Probability
        public static float ExpCurve(float baseVal, float maxVal, float t) 
        {
            return baseVal * Mathf.Pow(maxVal / baseVal, t);
        }

        public static float CalculateClothingSimilarity(Dealer d)
        {
            int totalApparel = 0;
            int apparelMatched = 0;

            if (Player.Local.Avatar.Appearance == null)
            {
                Log("Could not find player avatar settings");
                return 0f;
            }
#if MONO
            List<AvatarObject> playerSettings = Player.Local.Avatar.Appearance._wornObjects;

#else
            Il2CppSystem.Collections.Generic.List<AvatarObject> playerSettings = Player.Local.Avatar.Appearance._wornObjects;
#endif
            // For each dealer worn object check each player worn object

            for (int i = 0; i < d.Avatar.Appearance._wornObjects.Count; i++)
            {
                AvatarObject setting = d.Avatar.Appearance._wornObjects[i];
                if (setting._name == null || setting._name == string.Empty)
                    continue; // go next

                totalApparel++;

                for (int j = 0; j < playerSettings.Count; j++)
                {
                    bool currentColorMatched = false;
                    bool currentPathMatched = false;

                    AvatarObject playerSetting = playerSettings[j];
                    if (playerSetting._name == null || playerSetting._name == string.Empty)
                        continue; // go next

                    Log($"Calculate color similarity ({setting._name}) for {setting._properties._colors.Length} num colors");

                    Log($"Cartel: {setting._name} - Player: {playerSetting._name}");

                    if (CalculateColorSimilarity(setting._properties, playerSetting._properties) > colorSimilarityThreshold)
                        currentColorMatched = true;

                    if (ClothingPathMatches(setting._name, playerSetting._name))
                        currentPathMatched = true;

                    if (currentPathMatched && currentColorMatched)
                    {
                        apparelMatched++;
                        break; // Go check the next dealer body layer
                    }
                }
            }

            if (totalApparel == 0)
            {
                return 0f;
            }

            float similarity = Mathf.Clamp01(((float)apparelMatched / (float)totalApparel) * 1.2f);
            float probability = ExpCurve(CLOTHING_SIMILARITY_BASE_CHANCE, CLOTHING_SIMILARITY_MAX_CHANCE, similarity);
            Log("Total Similarity:" + similarity + "-> " + probability);
            return probability;
        }

        public static float CalculateColorSimilarity(AvatarPropertyCollection aCollection, AvatarPropertyCollection bCollection)
        {
            // at 0f they are equal of color
            // at 1f they are opposite color
            float result = 1f;
            // Because this can be null the color is probably custom clothing texture
            ColorProperty propertyA = aCollection.GetFirstColorProperty();
            ColorProperty propertyB = bCollection.GetFirstColorProperty();
            if (propertyA != null && propertyB != null)
            {
                Color a = aCollection.GetFirstColorProperty().Value;
                Color b = bCollection.GetFirstColorProperty().Value;
                result = Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b)) / Mathf.Sqrt(3f);
            }
            else
            {
                Log("Color property does not exist in clothing!");
            }

            Log("Clothing color diff: " + result);
            return result;
        }

        public static bool ClothingPathMatches(string a, string b)
        {
            // Where string a is the cartel member clothing and b is the player clothing
            Log($"Clothing path same: {(a==b)}\n    {a}\n    {b}");
            return a == b;
        }

        public static float CalculateInfluenceProbability()
        {

            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
            float probability = ExpCurve(CARTEL_INFLUENCE_BASE_CHANCE, CARTEL_INFLUENCE_MAX_CHANCE, allInfluenceNormalized);
            return probability;
        }

        public static float CalculateThreathenProbability()
        {
            Log("Calculate Threathen probability");
            int prev = PlayerSingleton<PlayerInventory>.Instance.PriorEquippedSlotIndex;
            if (prev == null)
            {
                Log("Inventory prior equipped slot field is unassigned");
                return 0f;
            }    
            string id = "";
            Log("Previous slot equipped: " + prev.ToString());
            if (prev >= 0 && prev < 8)
            {
                ItemInstance item = Player.Local._inventory[prev].ItemInstance;
                if (item != null)
                {
                    id = item.ID;
                    Log($"ID {id}");
                }
                else
                {
                    Log("Item Instance is null");
                    return 0f;
                }
            }
            else
            {
                Log("Invalid inventory slot index");
                return 0f;
            }
            float weaponThreath = 0f;
            switch (id)
            {
                case "baseballbat":
                    weaponThreath = 0.05f;
                    break;

                case "fryingpan":
                    weaponThreath = 0.09f;
                    break;

                case "machete":
                    weaponThreath = 0.18f;
                    break;

                case "revolver":
                    weaponThreath = 0.35f;
                    break;

                case "m1911":
                    weaponThreath = 0.4f;
                    break;

                case "pumpshotgun":
                    weaponThreath = 0.5f;
                    break;

                case "goldenm1911":
                    weaponThreath = 0.75f;
                    break;

                default:
                    weaponThreath = 0.01f;
                    break;

            }
            float probability = ExpCurve(THREATEN_CARTEL_BASE_CHANCE, THREATEN_CARTEL_MAX_CHANCE, weaponThreath);
            return probability;
        }

        public static float CalculateRumourProbability()
        {
            float probability = ExpCurve(SPREAD_RUMOURS_BASE_CHANCE, SPREAD_RUMOURS_MAX_CHANCE, (float)alliedQuests.timesPersuaded / 10f);
            if (currentConfig.debugMode)
                probability = 0.99f;
            return probability;
        }
#endregion
    }

}
