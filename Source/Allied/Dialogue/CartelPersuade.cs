using UnityEngine;
using UnityEngine.UI;
using System.Collections;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.AlliedCartelDialogue;
using static CartelEnforcer.AlliedExtension;


#if MONO
using ScheduleOne.Economy;
using ScheduleOne.PlayerScripts;
using ScheduleOne.AvatarFramework;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.VoiceOver;
using ScheduleOne.UI;
using ScheduleOne.ItemFramework;
#else
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.ItemFramework;
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
            if (persuadeCooldown > 0)
                persuadeCooldown--;
        }

        public static DialogueContainer InitPersuadeContainer()
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
            DialogueContainer persuadeOverride = ScriptableObject.CreateInstance<DialogueContainer>();
            persuadeOverride.name = "CARTEL_ENFORCER_PERSUADE"; // Object.name
            persuadeOverride.allowExit = true;
            persuadeOverride.BranchNodeData = new(); // Can leave empty
            persuadeOverride.DialogueNodeData = new(); // Fill this with the created nodes
            persuadeOverride.NodeLinks = new(); // Link logic with guids
            Log("[ALLIEDEXT] Persuade container created");

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
            node1StartPersuadeChoice.ChoiceText = "<color=#6BBCFF>[Persuade]</color> Just hear me out. You could work with me instead.";
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
            Log("[ALLIEDEXT] Node 1 created");

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
            Log($"[ALLIEDEXT] Persuade override created");

            return persuadeOverride;
        }

        public static void AddPersuadeDialogue(CartelDealer d)
        {
            if (d == null)
            {
                Log("[ALLIEDEXT]    Dealer null cant add persuade");
                return;
            }
            DialogueController controller = d.DialogueHandler.GetComponent<DialogueController>();
            if (controller == null)
            {
                Log("[ALLIEDEXT]    DGController is null");
                return;
            }

            controller.OverrideContainer = persuadeContainer;

            void OnConversationStarted()
            {
                Log("[ALLIEDEXT] Conversation Started");
                DialogueHandler.activeDialogueNode = entryNode;
            }

            d.DialogueHandler.onConversationStart.AddListener((UnityEngine.Events.UnityAction)OnConversationStarted);

            return;
        }

        public static IEnumerator UpdatePersuadeCooldownText()
        {
            // While the persuade dialogue container is open in the first entry node
            // Update the first dialogue choice not possible text with time
            while (DialogueHandler.activeDialogue != null && 
                DialogueHandler.activeDialogueNode != null && 
                DialogueHandler.activeDialogue.name == "CARTEL_ENFORCER_PERSUADE" && 
                DialogueHandler.activeDialogueNode.Guid == entryNode.Guid)
            {
                if (Singleton<DialogueCanvas>.Instance.dialogueChoices.Count == 0) break;
                DialogueChoiceEntry entry = Singleton<DialogueCanvas>.Instance.dialogueChoices[0];
                // If cd hits 0 while dialogue open
                if (persuadeCooldown == 0 && entry.notPossibleGameObject.activeSelf)
                {
                    // needs to manually enable it again while dialogue open?
                    entry.notPossibleGameObject.SetActive(false);
                    entry.button.interactable = true;
                    ColorBlock colors = entry.button.colors;
                    colors.disabledColor = colors.pressedColor;
                    entry.button.colors = colors;
                    entry.text.GetComponent<RectTransform>().offsetMax = new Vector2(0f, 0f);
                }
                else // update the cd text
                {
                    string invalidReason = $"<color=#DE3F31>Wait {persuadeCooldown} minutes before trying again.</color>";
                    entry.notPossibleText.text = invalidReason.ToUpper();
                }

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
            AvatarSettings playerSettings = Player.Local.CurrentAvatarSettings.GetAvatarSettings();
            // For each dealer body layer setting, check each player body layer setting
            for (int i = 0; i < d.Avatar.CurrentSettings.BodyLayerSettings.Count; i++)
            {
               
                AvatarSettings.LayerSetting setting = d.Avatar.CurrentSettings.BodyLayerSettings[i];
                if (setting.layerPath == null || setting.layerPath == string.Empty)
                    continue; // go next

                totalApparel++;

                for (int j = 0; j < playerSettings.BodyLayerSettings.Count; j++)
                {
                    bool currentColorMatched = false;
                    bool currentPathMatched = false;
                    AvatarSettings.LayerSetting playerSetting = playerSettings.BodyLayerSettings[j];
                    if (playerSetting.layerPath == null || setting.layerPath == string.Empty)
                        continue; // go next

                    if (CalculateColorSimilarity(setting.layerTint, playerSetting.layerTint) > colorSimilarityThreshold)
                        currentColorMatched = true;
                    if (ClothingPathMatches(setting.layerPath, playerSetting.layerPath))
                        currentPathMatched = true;

                    if (currentPathMatched && currentColorMatched)
                    {
                        apparelMatched++;
                        break; // Go check the next dealer body layer
                    }
                }
            }

            // For each dealer accessory setting, check each player accessory setting
            for (int i = 0; i < d.Avatar.appliedAccessories.Length; i++)
            {
                if (d.Avatar.appliedAccessories[i] == null) continue;

                totalApparel++;

                Accessory setting = d.Avatar.appliedAccessories[i];
                for (int j = 0; j < Player.Local.Avatar.CurrentSettings.AccessorySettings.Count; j++)
                {
                    if (Player.Local.Avatar.CurrentSettings.AccessorySettings[j] == null) continue;

                    bool currentColorMatched = false;
                    bool currentPathMatched = false;
                    AvatarSettings.AccessorySetting playerSetting = Player.Local.Avatar.CurrentSettings.AccessorySettings[j];

                    // For color in the accessory it has to first check what color is in use in both
                    Color dealerAccessoryColor = Color.black;

                    // parse one possible color from the first mesh
                    if (setting.meshesToColor.Count() > 0)
                        dealerAccessoryColor = setting.meshesToColor[0].material.color;
                    else if (setting.skinnedMeshesToColor.Length > 0)
                        dealerAccessoryColor = setting.skinnedMeshesToBind[0].material.color;
                    else
                        Log("[ALLIEDEXT] no mesh color found");

                    if (CalculateColorSimilarity(dealerAccessoryColor, playerSetting.color) < colorSimilarityThreshold)
                        currentColorMatched = true;
                    
                    if (ClothingPathMatches(setting.AssetPath, playerSetting.path))
                        currentPathMatched = true;

                    if (currentPathMatched && currentColorMatched)
                    {
                        apparelMatched++;
                        break; // Go check the next dealer accessory layer
                    }
                }
            }

            if (totalApparel == 0)
            {
                return 0f;
            }

            float similarity = Mathf.Clamp01(((float)apparelMatched / (float)totalApparel) * 1.2f);
            float probability = ExpCurve(CLOTHING_SIMILARITY_BASE_CHANCE, CLOTHING_SIMILARITY_MAX_CHANCE, similarity);
            Log("[ALLIEDEXT] Total Similarity:" + similarity + "-> " + probability);
            return probability;
        }

        public static float CalculateColorSimilarity(Color a, Color b)
        {
            // at 0f they are equal of color
            // at 1f they are opposite color
            float result = Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b)) / Mathf.Sqrt(3f);
            Log("[ALLIEDEXT] Clothing color diff: " + result);
            return result;
        }

        public static bool ClothingPathMatches(string a, string b)
        {
            // Where string a is the cartel member clothing and b is the player clothing
            if (a == "Avatar/Layers/Top/Tucked T-Shirt") // Because tucked t shirt doesnt exist in shop, player cant buy
                a = "Avatar/Layers/Top/T-Shirt";
            // Otherwise this should work to check the paths of clothing
            Log($"[ALLIEDEXT] Clothing path same: {(a==b)}\n    {a}\n    {b}");

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
            int prev = PlayerSingleton<PlayerInventory>.Instance.PreviousEquippedSlotIndex - 1;
            string id = "";
            if (prev != -1 && prev < 8)
            {
                ItemInstance item = Player.Local.Inventory[prev].ItemInstance;
                if (item != null)
                    id = item.ID;
                else
                    Log("[ALLIEDEXT] Item Instance is null");
            }
            else
                return 0f;

            float weaponThreath = 0f;
            switch (id)
            {
                case "baseballbat":
                    weaponThreath = 0.2f;
                    break;

                case "fryingpan":
                    weaponThreath = 0.3f;
                    break;

                case "machete":
                    weaponThreath = 0.5f;
                    break;

                case "revolver":
                    weaponThreath = 0.65f;
                    break;

                case "m1911":
                    weaponThreath = 0.8f;
                    break;

                case "pumpshotgun":
                    weaponThreath = 0.95f;
                    break;

                default:
                    weaponThreath = 0.1f;
                    break;

            }
            float probability = ExpCurve(THREATEN_CARTEL_BASE_CHANCE, THREATEN_CARTEL_MAX_CHANCE, weaponThreath);
            return probability;
        }

        public static float CalculateRumourProbability()
        {
            float probability = ExpCurve(SPREAD_RUMOURS_BASE_CHANCE, SPREAD_RUMOURS_MAX_CHANCE, (float)alliedQuests.timesPersuaded / 10f);
            return probability;
        }
        #endregion
    }

}
