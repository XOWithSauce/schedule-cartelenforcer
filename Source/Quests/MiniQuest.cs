using System.Collections;
using MelonLoader;
using UnityEngine;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InfluenceOverrides;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.Quests;
using ScheduleOne.GameTime;
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Levelling;
using ScheduleOne.Money;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.VoiceOver;
#else
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.VoiceOver;
#endif

namespace CartelEnforcer
{
    public static class MiniQuest
    {

        public static Dictionary<NPC, NpcQuestStatus> targetNPCs = new Dictionary<NPC, NpcQuestStatus>();
        public static List<NPC> targetNPCsList = new(); // avoid creating new ones
        public static List<DeadDrop> emptyDrops = new(); // same thing as the npcs list just update this one
        public class NpcQuestStatus
        {
            public bool HasAskedQuestToday { get; set; }
            public bool HasActiveQuest { get; set; }
        }

        // Mini Quest Dead Drops
        public static List<string> rareDrops = new()
        {
            "silverwatch",
            "goldwatch",
            "silverchain",
            "goldchain",
            "oldmanjimmys",
            "brutdugloop",
            "sewerkey"
        };
        public static List<string> commonDrops = new()
        {
            "cocaine",
            "meth",
            "greencrackseed",
            "ogkushseed",
        };

        public static IEnumerator InitMiniQuest()
        {
            Anna anna = UnityEngine.Object.FindObjectOfType<Anna>();
            if (anna != null)
                targetNPCs.Add(anna, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Fiona fiona = UnityEngine.Object.FindObjectOfType<Fiona>();
            if (fiona != null)
                targetNPCs.Add(fiona, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Dean dean = UnityEngine.Object.FindObjectOfType<Dean>();
            if (dean != null)
                targetNPCs.Add(dean, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Mick mick = UnityEngine.Object.FindObjectOfType<Mick>();
            if (mick != null)
                targetNPCs.Add(mick, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Jeff jeff = UnityEngine.Object.FindObjectOfType<Jeff>();
            if (jeff != null)
                targetNPCs.Add(jeff, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Dan dan = UnityEngine.Object.FindObjectOfType<Dan>();
            if (dan != null)
                targetNPCs.Add(dan, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Marco marco = UnityEngine.Object.FindObjectOfType<Marco>();
            if (marco != null)
                targetNPCs.Add(marco, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            Herbert herbert = UnityEngine.Object.FindObjectOfType<Herbert>();
            if (herbert != null)
                targetNPCs.Add(herbert, new NpcQuestStatus { HasAskedQuestToday = false, HasActiveQuest = false });

            targetNPCsList = targetNPCs.Keys.ToList();
            Log("Finished Initializing MiniQuest NPCs");
            yield return null;
        }

        public static void OnDayPassNewDiag()
        {
            Log("[DAY PASS] Resetting Mini Quest Dialogue Flags");
            foreach (NPC npc in targetNPCsList)
            {
                targetNPCs[npc].HasAskedQuestToday = false;
            }
        }

        public static IEnumerator EvaluateMiniQuestCreation()
        {
            Log("Starting Mini Quest Dialogue Random Generation");
            WaitForSeconds upperWait = new WaitForSeconds(240f);
            bool questGenerated = false;
            bool passStatus = false;
            bool passTime = false;

            while (registered)
            {

#if MONO
                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status == ECartelStatus.Hostile)
                    passStatus = true;
                else
                    passStatus = false;
#else
                if (NetworkSingleton<Cartel>.Instance.Status == Il2Cpp.ECartelStatus.Hostile)
                    passStatus = true;
                else
                    passStatus = false;
#endif

                if (TimeManager.Instance.CurrentTime >= 659 && TimeManager.Instance.CurrentTime <= 2359) // Dont generate at midnight
                    passTime = true;
                else
                    passTime = false;

                if (passStatus && passTime)
                {
                    Log("[MINI QUEST] Try Generate");
                    NPC random = targetNPCsList[UnityEngine.Random.Range(0, targetNPCsList.Count)];
                    if (targetNPCs.ContainsKey(random))
                    {
                        if (!targetNPCs[random].HasActiveQuest && !targetNPCs[random].HasAskedQuestToday)
                        {
                            // If The NPC is not yet unlocked we try to roll a chance
                            float chance = 1f;
                            if (!random.RelationData.Unlocked)
                            {
                                chance = UnityEngine.Random.Range(0f, 1f);
                            }
                            if (chance > 0.20f)
                            {
                                targetNPCs[random].HasActiveQuest = true;
                                InitMiniQuestDialogue(random);
                                questGenerated = true;
                            }
                        }
                    }
                }

                if (questGenerated)
                    yield return upperWait;
                else
                    yield return Wait30;
                if (!registered) yield break;

                questGenerated = false;
            }
        }

        public static void InitMiniQuestDialogue(NPC npc)
        {
            DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "";
            float paid = 500f;
            if (npc.RelationData != null)
                paid = Mathf.Lerp(500f, 100f, npc.RelationData.NormalizedRelationDelta);
            if (float.IsNaN(paid))
                paid = 500f;
            paid = Mathf.Round(paid / 20f) * 20f;

            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0:
                    text = "Have you heard anything new about the Benzies?";
                    break;

                case 1:
                    text = "Any rumours about the Cartel you could share?";
                    break;

                case 2:
                    text = "What's the word around town? I need info on the Benzies.";
                    break;
            }
            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-${paid}</color>)";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnMiniQuestChosen(choice, npc, controller, paid); });
#else
            void OnMiniQuestChosenWrapped()
            {
                OnMiniQuestChosen(choice, npc, controller, paid);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnMiniQuestChosenWrapped);
#endif
            controller.AddDialogueChoice(choice);
            Log("[MINI QUEST]    Created Mini Quest Dialogue for: " + npc.FirstName);
            return;
        }

        public static void OnMiniQuestChosen(DialogueController.DialogueChoice choice, NPC npc, DialogueController controller, float paid)
        {
            Log("[MINI QUEST]    Option Chosen");
            float chance = Mathf.Lerp(0.40f, 0.75f, npc.RelationData.NormalizedRelationDelta); // At max rela 75% chance to accept
            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= paid;
            if ((TimeManager.Instance.CurrentTime >= 1200 || TimeManager.Instance.CurrentTime <= 1800))
            {
                // If inside of 12:00 - 18:00 window, Higher likelihood to accept quest and also tell exact location
                chance = Mathf.Lerp(chance, 1.0f, chance);
            }

            if (CartelGathering.areGoonsGathering)
            {
                // If the gathering is active we decrease the chance to give the dead drop mini quest so that 
                // the chance can also dynamically prefer this feature if its active, linear decrease 50%
                chance = Mathf.Lerp(chance, 0.0f, 0.5f);
            }

            if (UnityEngine.Random.Range(0f, 1f) < chance && hasCash)
            {
                // Start mini quest
                Log("[MINI QUEST]    Start Quest");

                emptyDrops.Clear(); // From fresh
                foreach(DeadDrop drop in DeadDrop.DeadDrops)
                {
                    if (drop.Storage.ItemCount == 0)
                        emptyDrops.Add(drop);
                }
                if (emptyDrops.Count == 0)
                    return;

                DeadDrop random = emptyDrops[UnityEngine.Random.Range(0, emptyDrops.Count)];

                string location = "";
                bool hasPreposition = false;
                if (UnityEngine.Random.Range(0f, 1f) > chance) // At max rela 25% chance to tell only region
                    location = random.Region.ToString() + " region";
                else
                {
                    location = random.DeadDropName;
                    location = location.ToLower();
                    List<string> splits = location.Split(' ').ToList();

                    if (splits.Count > 0 && (splits[0] == "under" || splits[0] == "behind"))
                    {
                        hasPreposition = true;
                    }
                }


                List<ItemInstance> listItems = new();
                ItemInstance item;
                int qty;
                Func<string, ItemDefinition> GetItem;

#if MONO
                GetItem = ScheduleOne.Registry.GetItem;
#else
                GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif

                // First take from loot pool select 1
                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                {
                    ItemDefinition def = GetItem(commonDrops[UnityEngine.Random.Range(0, commonDrops.Count)]);
                    qty = UnityEngine.Random.Range(3, 11);
                    item = def.GetDefaultInstance(qty);
                    listItems.Add(item);
                }
                else
                {
                    ItemDefinition def = GetItem(rareDrops[UnityEngine.Random.Range(0, rareDrops.Count)]);
                    qty = 1;
                    item = def.GetDefaultInstance(qty);
                    listItems.Add(item);
                }
                // Then take from stolen items
                List<ItemInstance> fromStolenPool = new();
                if (cartelStolenItems.Count > 0)
                {
                    fromStolenPool = GetFromPool(2);
                    if (fromStolenPool.Count > 0)
                        listItems.AddRange(fromStolenPool);
                }

                coros.Add(MelonCoroutines.Start(CreateDropContent(random, listItems, npc, fromStolenPool, hasPreposition)));
                controller.handler.ContinueSubmitted();
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-paid, true, false);
                string prep = "";
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 0:
                        prep = hasPreposition ? "" : "around";
                        controller.handler.WorldspaceRend.ShowText($"I heard them talk about some drop {prep} {location}...", 9f);
                        break;

                    case 1:
                        prep = hasPreposition ? "" : "near";
                        controller.handler.WorldspaceRend.ShowText($"There are rumours about a suspicious drop {prep} {location}!", 9f);
                        break;

                    case 2:
                        prep = hasPreposition ? "" : "near";
                        controller.handler.WorldspaceRend.ShowText($"Yes! I heard they stashed something {prep} {location}!", 9f);
                        break;

                    case 3:
                        prep = hasPreposition ? "" : "around";
                        controller.handler.WorldspaceRend.ShowText($"I saw one of them hide something in a dead drop {prep} {location}.", 9f);
                        break;

                    case 4:
                        prep = hasPreposition ? "" : "at";
                        controller.handler.WorldspaceRend.ShowText($"They have been stashing items {prep} {location}.", 9f);
                        break;
                }
                coros.Add(MelonCoroutines.Start(HurryUpMessage(controller)));
            }
            else if (CartelGathering.areGoonsGathering && CartelGathering.currentGatheringLocation != null && hasCash)
            {
                Log("[MINI QUEST] Display Gathering Loc");
                controller.handler.ContinueSubmitted();
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-paid, true, false);
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        controller.handler.WorldspaceRend.ShowText($"I saw a group of them at the {CartelGathering.currentGatheringLocation.description}", 15f);
                        npc.PlayVO(EVOLineType.Think, false);
                        break;

                    case 1:
                        controller.handler.WorldspaceRend.ShowText($"There is a group of Benzies at the {CartelGathering.currentGatheringLocation.description}", 15f);
                        npc.PlayVO(EVOLineType.Thanks, false);
                        break;

                    case 2:
                        controller.handler.WorldspaceRend.ShowText($"The Benzies are gathering at the {CartelGathering.currentGatheringLocation.description}", 15f);
                        npc.PlayVO(EVOLineType.Concerned, false);
                        break;
                }
            }
            else
            {
                Log("[MINI QUEST] RefuseQuestGive");
                controller.handler.ContinueSubmitted();
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        controller.handler.WorldspaceRend.ShowText($"I've heard nothing...", 15f);
                        npc.PlayVO(EVOLineType.No, false);
                        break;

                    case 1:
                        controller.handler.WorldspaceRend.ShowText($"No! Leave me alone!", 15f);
                        npc.Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 6f, 1);
                        npc.PlayVO(EVOLineType.Annoyed, false);
                        break;

                    case 2:
                        controller.handler.WorldspaceRend.ShowText($"I'm afraid to talk about it...", 15f);
                        npc.PlayVO(EVOLineType.Concerned, false);
                        break;
                }
            }
            targetNPCs[npc].HasActiveQuest = false;
            targetNPCs[npc].HasAskedQuestToday = true;
            coros.Add(MelonCoroutines.Start(DisposeChoice(controller, npc)));
            return;
        }

        public static IEnumerator HurryUpMessage(DialogueController controller)
        {
            yield return Wait10;
            controller.handler.WorldspaceRend.ShowText($"You need to hurry before they grab it!", 5f);
            yield return null;
        }

        public static IEnumerator DisposeChoice(DialogueController controller, NPC npc)
        {
            yield return Wait05;
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(oldChoices.Count - 1);
            controller.Choices = oldChoices;
            Log("[MINI QUEST]    Disposed Choice");
            yield return null;
        }

        public static IEnumerator CreateDropContent(DeadDrop entity, List<ItemInstance> filledItems, NPC npc, List<ItemInstance> itemsFromPool = null, bool useShortTimeout = true)
        {
            yield return Wait5;
            if (!registered) yield break;
            bool changeInfluence = ShouldChangeInfluence(entity.Region);

            Log($"[MINI QUEST]    MiniQuest Drop at: {entity.DeadDropName}");
            for (int i = 0; i < filledItems.Count; i++)
            {
                entity.Storage.InsertItem(filledItems[i], true);
                Log($"[MINI QUEST]    MiniQuest Reward: {filledItems[i].Name} x {filledItems[i].Quantity}");
            }
            bool opened = false;

#if MONO
            System.Action onOpenedAction = null;
#else
            Il2CppSystem.Action onOpenedAction = null;

#endif
            void WrapOnOpenCallback()
            {
                Log("[MINI QUEST] Quest Complete");
                NetworkSingleton<LevelManager>.Instance.AddXP(100);
                opened = true;
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, influenceConfig.deadDropSuccess);
                if (activeQuest != null && activeQuest.State == EQuestState.Active && activeQuest.QuestEntry_Investigate != null && activeQuest.QuestEntry_Investigate.State == EQuestState.Active && StageDeadDropsObserved <= 1)
                    StageDeadDropsObserved += 1;

                if (onOpenedAction != null)
                {
                    entity.Storage.onOpened -= onOpenedAction;
                    onOpenedAction = null;
                }
            }
#if MONO
            onOpenedAction = (System.Action)WrapOnOpenCallback;
#else
            onOpenedAction = (Il2CppSystem.Action)WrapOnOpenCallback;
#endif
            entity.Storage.onOpened += onOpenedAction;


            yield return Wait60;
            if (!registered) yield break;

            // if the player only knows region, add 1min
            if (!useShortTimeout)
            {
                yield return Wait60;
                if (!registered) yield break;
            }

            if (!opened)
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(entity.Region, influenceConfig.deadDropFail);
                // Return stolen items back to pool
                if (itemsFromPool != null && itemsFromPool.Count > 0)
                {
                    CartelStealsItems(itemsFromPool);
                }
                // clear the inserted reward + possible stolen items
                entity.Storage.ClearContents();
            }

            if (onOpenedAction != null)
            {
                entity.Storage.onOpened -= onOpenedAction;
                onOpenedAction = null;
            }

            Log($"[MINI QUEST] Removed MiniQuest Reward.");
            yield return null;
        }

    }
}
