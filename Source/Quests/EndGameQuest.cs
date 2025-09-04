

using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.AI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.Police;
using ScheduleOne.PlayerScripts;
using static ScheduleOne.AvatarFramework.AvatarSettings;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.ItemFramework;
using ScheduleOne.Economy;
using ScheduleOne.Interaction;
using ScheduleOne.Levelling;
using ScheduleOne.Money;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.Property;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.ObjectScripts;
using static ScheduleOne.UI.Items.FilterConfigPanel.SearchCategory;
using ScheduleOne.NPCs.Other;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Messaging;
using ScheduleOne.Dialogue;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.VoiceOver;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Managing;
#else
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.PlayerScripts;
using static Il2CppScheduleOne.AvatarFramework.AvatarSettings;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.VoiceOver;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet.Managing;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.ObjectScripts;
using static Il2CppScheduleOne.UI.Items.FilterConfigPanel.SearchCategory;
using Il2CppScheduleOne.NPCs.Other;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
    public static class EndGameQuest
    {

        #region End Game Quest Unexpected Alliances
        public static bool completed = false;
        public static int StageDeadDropsObserved = 0;
        public static NPC fixer;
        public static CartelGoon bossGoon;
        public static Quest_DefeatEnforcer activeQuest = null;
        public static int fixerDiagIndex = 0;

        public static bool PreRequirementsMet()
        {
            
            if (!InstanceFinder.IsServer) 
                return false;
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                return false;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                return false;
#endif


            // Suburbia region, has to have atleast 5 customers unlocked
            int numUnlocked = 0;
#if MONO
            using (List<Customer>.Enumerator enumerator = Customer.UnlockedCustomers.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.NPC.Region == EMapRegion.Suburbia)
                    {
                        numUnlocked++;
                    }
                }
            }

#else
            for (int i = 0; i < Customer.UnlockedCustomers.Count; i++)
            {
                if (Customer.UnlockedCustomers[i].NPC.Region == EMapRegion.Suburbia)
                {
                    numUnlocked++;
                }
            }
#endif
            if (numUnlocked < 5)
                return false;

            if ((int)NetworkSingleton<LevelManager>.Instance.Rank < 5) // If not atleast enforcer rank
                return false; 


            return true;
        }

        #region Fixer dialogue
        public static IEnumerator GenDialogOption()
        {
            NPC npc = UnityEngine.Object.FindObjectOfType<Fixer>(true);
            if (npc != null)
                fixer = npc;


            DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "How do we get rid of the Benzies?";
            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-$5000</color>)";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnQuestChosen(controller); });
#else
            void OnQuestChosenWrapped()
            {
                OnQuestChosen(controller);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnQuestChosenWrapped);
#endif
            fixerDiagIndex = controller.AddDialogueChoice(choice);
            yield return null;
        }

        public static IEnumerator DisposeChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(fixerDiagIndex);
            controller.Choices = oldChoices;
            Log("[END GAME QUEST]    Disposed Choice");
            yield return null;
        }

        public static void OnQuestChosen(DialogueController controller)
        {
            controller.handler.ContinueSubmitted();

            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= 5000f;
            if (hasCash)
            {
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-5000f, true, false);
                MelonCoroutines.Start(DisposeChoice(controller));
                MelonCoroutines.Start(EventInstructions(controller));
                MelonCoroutines.Start(GenerateQuestState());
            }
            else
            {
                controller.npc.PlayVO(EVOLineType.Annoyed);
            }
            
        }
        #endregion

        #region Contact NPC dialogue
        public static IEnumerator GenContactDialog(NPC npc, Action cb)
        {
            DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            choice.ChoiceText = "Who are you?";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnOptionSelected(controller, cb); });
#else
            void OnOptionSelectedWrapped()
            {
                OnOptionSelected(controller, cb);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnOptionSelectedWrapped);
#endif
            controller.AddDialogueChoice(choice);
            yield return null;
        }

        public static void OnOptionSelected(DialogueController controller, Action cb)
        {
            controller.handler.ContinueSubmitted();
            MelonCoroutines.Start(DisposeChoice(controller));
            MelonCoroutines.Start(ContactDialogue(controller, cb));
        }

        public static IEnumerator ContactDialogue(DialogueController controller, Action cb)
        {
            List<string> dialog = dialogOptions[UnityEngine.Random.Range(0, dialogOptions.Count)];
            controller.npc.PlayVO(EVOLineType.Concerned);
            controller.handler.WorldspaceRend.ShowText("It doesn't matter who I am. We have a bigger issue at our hands.", 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText("The cartel has been running Hyland Point for too long.", 5f);
            yield return Wait5;
            controller.handler.WorldspaceRend.ShowText("We have intel that Thomas' high ranking soldier is nearby that house up the dirt road.", 5f);
            yield return Wait5;
            controller.handler.WorldspaceRend.ShowText("This is not your basic goon, they are a Brute. One of the best soldiers he has.", 5f);
            yield return Wait5;
            controller.handler.WorldspaceRend.ShowText("Go and take them down. I'll make sure nobody comes snooping around.", 5f);
            yield return Wait5;

            // Walk away + despawn??

            if (cb != null)
                cb();
            yield return null;
        }
        #endregion

        // 3 Alternatives for the worldspace dialogue
        public static List<List<string>> dialogOptions = new()
        {
            new List<string>()
            {
                "I ain't having the Benzies come messing with my operation. Not here, not ever.",
                "First move, we gotta rattle them a little bit.",
                "Go make a couple of their dead drops disappear.",
                "After that, I'll set you up with the right people to finish the job."
            },

            new List<string>()
            {
                "The Benzies are getting bold. I can't have that.",
                "I'm about to show them how we do things out here. No games.",
                "Find their dead drops and clean 'em out. A couple of them going missing will send a message.",
                "Once you get that done, I'll get you a meeting with someone who can help with the rest."
            },

            new List<string>()
            {
                "I don't want the Benzies to ruin my business here either.",
                "We need to send a message. They got to leave.",
                "They have been moving product through dead drops. You know what to do.",
                "Get started and I'll set up a meeting with someone who can help..."
            }
        };
        public static IEnumerator EventInstructions(DialogueController controller)
        {
            List<string> dialog = dialogOptions[UnityEngine.Random.Range(0, dialogOptions.Count)];
            controller.handler.WorldspaceRend.ShowText(dialog[0], 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText(dialog[1], 5f);
            yield return Wait5;
            controller.handler.WorldspaceRend.ShowText(dialog[2], 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText(dialog[3], 5f);
            yield return Wait5;

            yield return null;
        }
        private static IEnumerator GenerateQuestState()
        {
            Log("Starting");
            GameObject newQuestObject = new GameObject();
            Log("Add Component");
            activeQuest = newQuestObject.AddComponent<Quest_DefeatEnforcer>();
            Log("HandleInit");
            yield return MelonCoroutines.Start(HandleInit(activeQuest));
            newQuestObject.SetActive(true);
            activeQuest.enabled = true;
            Log("SetupSelf");
            activeQuest.SetupSelf();

            yield return null;
        }
        private static IEnumerator HandleInit(Quest_DefeatEnforcer quest)
        {
#if MONO
            quest.InitializeQuest("Unexpected Alliances", "Investigate and intercept Cartel Activity", Array.Empty<QuestEntryData>(), Guid.NewGuid().ToString());
#else
            //Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<QuestEntryData> entryData = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<QuestEntryData>(0);
            //quest.InitializeQuest("Unexpected Alliances", "Investigate and intercept Cartel Activity", entryData, Il2CppSystem.Guid.NewGuid().ToString());
#endif
            yield return null;
        }
        public static IEnumerator QuestReward(CartelGoon goon)
        {
            yield return Wait025;
            Func<string, ItemDefinition> GetItem;

#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
            ItemDefinition defWatch = GetItem("goldwatch");
            ItemInstance goldWatch = defWatch.GetDefaultInstance(1);
            goon.Inventory.InsertItem(goldWatch, true);

            ItemDefinition defChain = GetItem("goldchain");
            ItemInstance goldChain = defChain.GetDefaultInstance(1);
            goon.Inventory.InsertItem(goldChain, true);

            // Change globally customer relation
#if MONO
            using (List<Customer>.Enumerator enumerator = Customer.UnlockedCustomers.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    yield return Wait025;
                    if (enumerator.Current.NPC.RelationData.RelationDelta != 5.0f)
                        enumerator.Current.NPC.RelationData.ChangeRelationship(0.25f, true);
                }
            }
#else
            for (int i = 0; i < Customer.UnlockedCustomers.Count; i++)
            {
                yield return Wait025;
                if (Customer.UnlockedCustomers[i].NPC.RelationData.RelationDelta != 5.0f)
                    Customer.UnlockedCustomers[i].NPC.RelationData.ChangeRelationship(0.25f, true);
            }
#endif
            // Change influence in all unlocked regions
            if (InstanceFinder.IsServer)
            {
                foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    yield return Wait5; // play animation
                    float current = NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(unlmapReg).Influence;
                    if (current > 0.0f)
                    {
                        float result = current * 0.75f;
                        float delta = current - result;
                        float rounded = Mathf.Round(delta * 100) / 100;
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(unlmapReg, -rounded);
                    } 
                }
            }

            yield return Wait60;
            if (bossGoon.IsGoonSpawned)
            {
                // Reset all non default stats that would carry on modified
                bossGoon.Health.MaxHealth = 100f;
                bossGoon.Health.Health = 100f;
                bossGoon.Health.Revive();
                bossGoon.Movement.MoveSpeedMultiplier = 0.8f;
                bossGoon.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                bossGoon.Behaviour.ScheduleManager.EnableSchedule();
                bossGoon.Despawn();
                bossGoon = null;
            }

 
            yield return null;
        }
        #endregion

        #region Create Manor Infiltration quest
        public static NPC ray;
        public static Quest_InfiltrateManor activeManorQuest = null;
        public static bool manorCompleted = false;
        public static int rayChoiceIndex = 0;
        public static Vector3 standPos = new(70.96f, 1.46f, 16.03f);

        public static IEnumerator GenManorDialogOption()
        {
#if MONO
            NPC npc = UnityEngine.Object.FindObjectOfType<ScheduleOne.NPCs.CharacterClasses.Ray>(true);
#else
            NPC npc = UnityEngine.Object.FindObjectOfType<Il2CppScheduleOne.NPCs.CharacterClasses.Ray>(true);
#endif
            if (npc != null)
                ray = npc;


            DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "What can you tell me about the owner of that manor?";
            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-$2500</color>)";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnManorQuestChosen(controller); });
#else
            void OnQuestChosenWrapped()
            {
                OnManorQuestChosen(controller);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnQuestChosenWrapped);
#endif
            rayChoiceIndex = controller.AddDialogueChoice(choice);
            //DialogueHandler handler = npc.DialogueHandler.gameObject.GetComponent<DialogueHandler>();

            yield return null;
        }
        public static void OnManorQuestChosen(DialogueController controller)
        {
            controller.handler.ContinueSubmitted();

            bool inTimeWindow = NetworkSingleton<TimeManager>.Instance.CurrentTime >= 1815 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 1900;

            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= 2500f;
            bool isInPos = (Vector3.Distance(ray.CenterPoint, standPos) < 0.4f);
            if (hasCash && inTimeWindow && isInPos)
            {
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-2500f, true, false);
                MelonCoroutines.Start(DisposeRayChoice(controller));
                MelonCoroutines.Start(EventManorInstructions(controller));
                MelonCoroutines.Start(GenerateManorQuestState());
            }
            else
            {
                controller.npc.PlayVO(EVOLineType.Annoyed);
            }

        }

        public static IEnumerator DisposeRayChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;
#if MONO
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).EndTime = 2200;
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).Duration = 240;
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).Start();
#else
            NPCEvent_LocationBasedAction temp = ray.Behaviour.ScheduleManager.ActiveAction.TryCast<NPCEvent_LocationBasedAction>();
            if (temp != null)
            {
                temp.EndTime = 2200;
                temp.Duration = 240;
                temp.Start();
            }
#endif
            ray.Behaviour.ScheduleManager.DisableSchedule();

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(rayChoiceIndex);
            controller.Choices = oldChoices;
            Log("[END GAME QUEST]    Disposed Choice");
            yield return null;
        }

        // 3 Alternatives for the worldspace dialogue
        public static List<List<string>> dialogManorOptions = new()
        {
            new List<string>()
            {
                "Look, the Benzies family have been here for longer than I can remember.",
                "Thomas Benzie owns the manor but you don't want to mess with him.",
                "Honestly, he is a criminal and overall thats bad for my business.",
                "Property values have been plummeting so I wouldn't mind him leaving..."
            },

            new List<string>()
            {
                "Oh, Thomas? I don't know much about him. The Benzies family runs the town.",
                "He paid off a lot of people to get where he is today.",
                "Cops, lawyers, even some folks at Town Hall...",
                "He's been trying to get me to sell him my agency."
            },

            new List<string>()
            {
                "His manor is a monument to his ego. He bought it just to show off.",
                "Don't go near the place. He has people guarding the property.",
                "You'll get yourself killed poking around.",
                "He's not someone you want to mess with."
            }

        };

        public static IEnumerator EventManorInstructions(DialogueController controller)
        {
            List<string> dialog = dialogManorOptions[UnityEngine.Random.Range(0, dialogManorOptions.Count)];
            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText(dialog[0], 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Concerned);
            controller.handler.WorldspaceRend.ShowText(dialog[1], 5f);
            yield return Wait5;
            controller.handler.WorldspaceRend.ShowText(dialog[2], 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Surprised);
            controller.handler.WorldspaceRend.ShowText(dialog[3], 5f);
            yield return Wait5;

            yield return null;
        }
        private static IEnumerator GenerateManorQuestState()
        {
            Log("Starting");
            GameObject newQuestObject = new GameObject();
            Log("Add Component");
            activeManorQuest = newQuestObject.AddComponent<Quest_InfiltrateManor>();
            Log("HandleInit");
            yield return MelonCoroutines.Start(HandleManorInit(activeManorQuest));
            newQuestObject.SetActive(true);
            activeManorQuest.enabled = true;
            Log("SetupSelf");
            activeManorQuest.SetupSelf();

            yield return null;
        }
        private static IEnumerator HandleManorInit(Quest_InfiltrateManor quest)
        {
#if MONO
            quest.InitializeQuest("Unexpected Alliances", "Investigate and intercept Cartel Activity", Array.Empty<QuestEntryData>(), Guid.NewGuid().ToString());
#else
            //Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<QuestEntryData> entryData = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<QuestEntryData>(0);
            //quest.InitializeQuest("Unexpected Alliances", "Investigate and intercept Cartel Activity", entryData, Il2CppSystem.Guid.NewGuid().ToString());
#endif
            yield return null;
        }

        // 2nd quest entry dialog gen and lines
        public static IEnumerator GenRaySecondDialog(Action cb)
        {
            Log("Generating second dialogue");
            DialogueController controller = ray.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "Is there any way I can get in the Manor?";
            choice.ChoiceText = $"{text}";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnSecondDialogChosen(controller, cb); });
#else
            void OnQuestChosenWrapped()
            {
                OnSecondDialogChosen(controller, cb);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnQuestChosenWrapped);
#endif
            controller.AddDialogueChoice(choice);
            yield return null;
        }
        public static void OnSecondDialogChosen(DialogueController controller, Action cb)
        {
            controller.handler.ContinueSubmitted();

            MelonCoroutines.Start(DisposeChoice(controller));
            MelonCoroutines.Start(PlaySecondRayDialog(controller, cb));
        }
        public static IEnumerator PlaySecondRayDialog(DialogueController controller, Action cb)
        {
            controller.npc.PlayVO(EVOLineType.No);
            controller.handler.WorldspaceRend.ShowText("I don't think so. The whole place is fenced off.", 5f);
            yield return Wait5;
            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText("If you manage to make it over the fence, there is a way for me to get you inside.", 7f);
            yield return Wait5;
            yield return Wait2;
            controller.handler.WorldspaceRend.ShowText("Here, take my master key. It's from back when the Manor was built.", 6f);
            yield return Wait2;
            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.npc.SetAnimationTrigger("GrabItem");
            yield return Wait2;
            yield return Wait2;
            controller.handler.WorldspaceRend.ShowText("It won't work on the main entrance, but try the back door.", 5f);
            yield return Wait5;

            if (cb != null)
                cb();

            yield return null;
        }
        public static IEnumerator QuestManorReward()
        {
            yield return Wait025;

            for (int i = 0; i < Business.Businesses.Count; i++)
            {
                yield return Wait025;
                Business.Businesses[i].Price = Business.Businesses[i].Price * 0.85f;
            }

            for (int i = 0; i < Property.Properties.Count; i++)
            {
                yield return Wait025;
                Property.Properties[i].Price = Property.Properties[i].Price * 0.85f;
            }

            // Change influence in all unlocked regions
            if (InstanceFinder.IsServer)
            {
                foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    yield return Wait5; // play animation
                    float current = NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(unlmapReg).Influence;
                    if (current > 0.0f)
                    {
                        float result = current * 0.85f;
                        float delta = current - result;
                        float rounded = Mathf.Round(delta * 100) / 100;
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(unlmapReg, -rounded);
                    }
                }
            }

            yield return null;
        }
#endregion


    }

#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_DefeatEnforcer : Quest
    {
#if IL2CPP
        public Quest_DefeatEnforcer(IntPtr ptr) : base(ptr) { }

        public Quest_DefeatEnforcer() : base(ClassInjector.DerivedConstructorPointer<Quest_DefeatEnforcer>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private bool contactMade = false;
        private RectTransform rtIcon;
        private bool bossCombatBegun = false;
        private int fightElapsed = 0;

#if IL2CPP
        // Because by default the property uses this.Entries in Enumberable.Count, which probably causes the bug when the this.entries is il2cpp system ienumerable but expecting system ienumerable?
        public new int ActiveEntryCount
        {
            get
            {
                if (this.Entries == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (var entry in this.Entries)
                {
                    if (entry.State == EQuestState.Active)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
#endif

        public void SetupSelf()
        {
            Log("SetupSelfStart");
            
            Log("QuestInit");
            this.name = "Quest_DefeatEnforcer";
            Expires = false;
            title = "Unexpected Alliances";
            CompletionXP = 1000;
            Description = "Investigate and intercept Cartel Activity";
            TrackOnBegin = true;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = true;

            onActiveState = new UnityEvent();
            onComplete = new UnityEvent();
            onInitialComplete = new UnityEvent();
            onQuestBegin = new UnityEvent();
            onQuestEnd = new UnityEvent<EQuestState>();
            onTrackChange = new UnityEvent<bool>();

#if MONO
            this.SetGUID(Guid.NewGuid());
#else
            this.SetGUID(Il2CppSystem.Guid.NewGuid());
#endif
            Transform target = NetworkSingleton<QuestManager>.Instance.QuestContainer?.GetChild(0);
            if (target != null)
            {
                this.transform.SetParent(target);
            }

            MakeIcon();
            MakeUIPrefab();
            MakePOI();

            // Create the QuestEntry GameObjects and parent them.
            GameObject investigateObject = new GameObject("QuestEntry_Investigate");
            investigateObject.transform.SetParent(this.transform);

            GameObject contactObject = new GameObject("QuestEntry_WaitForContact");
            contactObject.transform.SetParent(this.transform);

            GameObject defeatObject = new GameObject("QuestEntry_DefeatEnforcer");
            defeatObject.transform.SetParent(this.transform);

            QuestEntry investigate = investigateObject.AddComponent<QuestEntry>();
            QuestEntry contact = contactObject.AddComponent<QuestEntry>();
            QuestEntry defeat = defeatObject.AddComponent<QuestEntry>();


            Log("Setting Entries");
            this.QuestEntry_Investigate = investigate;
            this.QuestEntry_WaitForContact = contact;
            this.QuestEntry_DefeatBoss = defeat;
#if MONO
            this.Entries = new()
                {
                    investigate, contact, defeat
                };
#else
            this.Entries = new();
            this.Entries.Add(investigate);
            this.Entries.Add(contact);
            this.Entries.Add(defeat);
#endif
            Log("Config Entries");

            investigate.SetEntryTitle("Intercept Cartel Dead Drops (0/2)");
            investigate.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            investigate.PoILocation.name = "InvestigateEntry_POI";
            investigate.PoILocation.transform.SetParent(investigate.transform);
            investigate.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            //investigate.CreatePoI();
            investigate.AutoUpdatePoILocation = true;
            investigate.SetState(EQuestState.Active, true);
            investigate.ParentQuest = this;
            investigate.CompleteParentQuest = false;
            void OnInvestigateComplete()
            {
                QuestEntry_WaitForContact.Begin();
                QuestEntry_WaitForContact.PoI.gameObject.SetActive(false);
                QuestEntry_WaitForContact.compassElement.Visible = false;
                investigate.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);
            }
            investigate.onComplete.AddListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);

            contact.SetEntryTitle("Wait for Manny to contact you.");
            contact.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            contact.PoILocation.name = "ContactEntry_POI";
            contact.PoILocation.transform.SetParent(contact.transform);
            contact.PoILocation.transform.position = new Vector3(128.27f, 1.56f, 88.96f);
            //contact.CreatePoI();
            contact.AutoUpdatePoILocation = true;
            contact.SetState(EQuestState.Inactive, false);
            contact.ParentQuest = this;
            contact.CompleteParentQuest = false;
            void OnContactComplete()
            {
                QuestEntry_DefeatBoss.Begin();
                contact.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnContactComplete);
            }
            contact.onComplete.AddListener((UnityEngine.Events.UnityAction)OnContactComplete);

            defeat.SetEntryTitle("Defeat the Cartel Brute");
            defeat.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            defeat.PoILocation.name = "DefeatEntry_POI";
            defeat.PoILocation.transform.SetParent(defeat.transform);
            defeat.PoILocation.transform.position = new Vector3(156.38f, 6.40f, 123.95f);
            //defeat.CreatePoI();
            defeat.AutoUpdatePoILocation = true;
            defeat.SetState(EQuestState.Inactive, false);
            defeat.ParentQuest = this;
            defeat.CompleteParentQuest = false;
            //void OnDefeatComplete()
            //{
            //
            //    defeat.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnDefeatComplete);
            //}
            //defeat.onComplete.AddListener((UnityEngine.Events.UnityAction)OnDefeatComplete);

            Quest.Quests.Add(this);

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
#endif
            instance.onMinutePass.Add(new Action(this.MinPass)); // Is this already added on the InitializeQuest method??
            coros.Add(MelonCoroutines.Start(StartQuestDetail()));
        }
        private IEnumerator StartQuestDetail() // todo fixme this dumb
        {
            StageDeadDropsObserved = 0;
            if (this.IconPrefab == null)
                this.IconPrefab = this.transform.Find("BenziesLogoQuest").GetComponent<RectTransform>();
            SetupHudUI();

            QuestEntry_Investigate.compassElement.Visible = false;
            QuestEntry_Investigate.PoI.gameObject.SetActive(false);

            this.hudUI.MainLabel.text = "Unexpected Alliances";
            SetIsTracked(true);
            SetQuestState(EQuestState.Active);
            yield return null;
        }

        #region Quest prefabs
        private void MakeIcon()
        {
            GameObject logo = new("BenziesLogoQuest");
            Image imgComp = logo.AddComponent<Image>();
            imgComp.sprite = benziesLogo;
            RectTransform rt = logo.AddComponent<RectTransform>();
            logo.AddComponent<CanvasRenderer>();
            logo.transform.SetParent(this.transform);

            rtIcon = rt;
            this.IconPrefab = rt;
        }

        private void MakeUIPrefab()
        {
            GameObject go = new("CartelEnforcerLogo");
            GameObject IconContainer = new("IconContainer");
            GameObject MainLabel = new("MainLabel");
            IconContainer.transform.parent = go.transform;
            MainLabel.transform.parent = go.transform;

            RectTransform rtr1 = go.AddComponent<RectTransform>();
            rtr1.anchoredPosition = new Vector2(0f, 0f);
            rtr1.anchorMax = new Vector2(0.5f, 0.5f);
            rtr1.anchorMin = new Vector2(0.5f, 0.5f);
            rtr1.offsetMax = new Vector2(25f, 25f);
            rtr1.offsetMin = new Vector2(-25f, -25f);
            rtr1.pivot = new Vector2(0.5f, 0.5f);
            rtr1.sizeDelta = new Vector2(50f, 50f);

            go.AddComponent<CanvasRenderer>();
            go.AddComponent<Image>();

            RectTransform rtr2 = IconContainer.AddComponent<RectTransform>();
            rtr2.sizeDelta = new Vector2(50f, 50f);
            Image logo = IconContainer.AddComponent<Image>();
            logo.sprite = benziesLogo;

            RectTransform rtr3 = MainLabel.AddComponent<RectTransform>();
            rtr3.anchoredPosition = new Vector2(0f, -46f);
            rtr3.anchorMax = new Vector2(0.5f, 0.5f);
            rtr3.anchorMin = new Vector2(0.5f, 0.5f);
            rtr3.offsetMax = new Vector2(250f, 14f);
            rtr3.offsetMin = new Vector2(-250f, -106f);
            rtr3.pivot = new Vector2(-250f, -106f);
            rtr3.sizeDelta = new Vector2(500f, 120f);
            MainLabel.AddComponent<CanvasRenderer>();
            MainLabel.AddComponent<Text>();
            UiPrefab = go;
            go.transform.parent = this.transform;

            return;
        }

        private void MakePOI()
        {
            GameObject poiPrefabObject = new GameObject($"CartelEnforcer_POI");
            poiPrefabObject.transform.SetParent(this.transform);
            poiPrefabObject.SetActive(false);
            POI poi = poiPrefabObject.AddComponent<POI>();
            poi.AutoUpdatePosition = true;
            poi.MainText = "Test";
            poi.DefaultMainText = "TestText";
            poi.UIPrefab = UiPrefab;
            PoIPrefab = poiPrefabObject;
            return;
        }
        #endregion

        private IEnumerator ContactSpawn()
        {
            Log("Spawning Contact NPC");
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;
            NetworkObject nob = null;
            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "PoliceNPC")
                {
                    nob = prefab;
                    break;
                }
            }
            if (nob == null)
            {
                Log("No Police Base Found for spawn");
                yield break;
            }
            Log("Spawn Base Object");

            NetworkObject copNet = UnityEngine.Object.Instantiate<NetworkObject>(nob);
            NPC myNpc = copNet.gameObject.GetComponent<NPC>();
            myNpc.AutoGenerateMugshot = false;
            myNpc.ID = $"CartelEnforcer_Contact_NPC";
            myNpc.FirstName = "Unknown";
            myNpc.LastName = "";
            myNpc.transform.parent = NPCManager.Instance.NPCContainer;
            NPCManager.NPCRegistry.Add(myNpc);
            yield return Wait05;
            netManager.ServerManager.Spawn(copNet);
            yield return Wait05;
            copNet.gameObject.SetActive(true);
            myNpc.Health.Invincible = true;
            myNpc.Behaviour.CombatBehaviour.Disable_Networked(null);
            
            myNpc.intObj.onHovered.RemoveAllListeners();
            myNpc.intObj.SetMessage("Talk");
            myNpc.intObj.interactionState = InteractableObject.EInteractableState.Default;

            PoliceOfficer offc = copNet.gameObject.GetComponent<PoliceOfficer>();

            #region Avatar
            var originalBodySettings = offc.Avatar.CurrentSettings.BodyLayerSettings;
#if MONO
            List<LayerSetting> bodySettings = new();
#else
            Il2CppSystem.Collections.Generic.List<LayerSetting> bodySettings = new();
#endif
            foreach (var layer in originalBodySettings)
            {
                bodySettings.Add(new LayerSetting
                {
                    layerPath = layer.layerPath,
                    layerTint = layer.layerTint
                });
            }

            var originalAccessorySettings = offc.Avatar.CurrentSettings.AccessorySettings;
#if MONO
            List<AccessorySetting> accessorySettings = new();
#else
            Il2CppSystem.Collections.Generic.List<AccessorySetting> accessorySettings = new();
#endif
            foreach (var acc in originalAccessorySettings)
            {
                accessorySettings.Add(new AccessorySetting
                {
                    path = acc.path,
                    color = acc.color
                });
            }

            for (int i = 0; i < bodySettings.Count; i++)
            {
                var layer = bodySettings[i];
                layer.layerPath = "";
                layer.layerTint = Color.white;
                bodySettings[i] = layer;
            }

            for (int i = 0; i < accessorySettings.Count; i++)
            {
                var acc = accessorySettings[i];
                acc.path = "";
                acc.color = Color.white;
                accessorySettings[i] = acc;
            }

            var jeans = bodySettings[2];
            jeans.layerPath = "Avatar/Layers/Bottom/Jeans";
            jeans.layerTint = new Color(0.306f, 0.416f, 0.569f);
            bodySettings[2] = jeans;
            var shirt = bodySettings[3];
            shirt.layerPath = "Avatar/Layers/Top/RolledButtonUp";
            shirt.layerTint = new Color(0.020f, 0.188f, 0.420f);
            bodySettings[3] = shirt;

            var cap = accessorySettings[0];
            cap.path = "Avatar/Accessories/Head/Cap/Cap";
            cap.color = new Color(0.149f, 0.149f, 0.149f);
            accessorySettings[0] = cap;
            var vest = accessorySettings[1];
            vest.path = "Avatar/Accessories/Chest/BulletproofVest/BulletproofVest";
            vest.color = new Color(0.3962f, 0.3962f, 0.3962f);
            accessorySettings[1] = vest;
            var sneakers = accessorySettings[2];
            sneakers.path = "Avatar/Accessories/Feet/Sneakers/Sneakers";
            sneakers.color = new Color(0.149f, 0.149f, 0.149f);
            accessorySettings[2] = sneakers;
            var glasses = accessorySettings[3];
            glasses.path = "Avatar/Accessories/Head/LegendSunglasses/LegendSunglasses";
            glasses.color = new Color(0.717f, 0.717f, 0.717f);
            accessorySettings[3] = glasses;

            offc.Avatar.CurrentSettings.BodyLayerSettings = bodySettings;
            offc.Avatar.CurrentSettings.AccessorySettings = accessorySettings;
            offc.Avatar.ApplyBodyLayerSettings(offc.Avatar.CurrentSettings);
            offc.Avatar.ApplyAccessorySettings(offc.Avatar.CurrentSettings);

            if (offc.Avatar.UseImpostor)
                offc.Avatar.Impostor.SetAvatarSettings(offc.Avatar.CurrentSettings);

            if (offc.Avatar.onSettingsLoaded != null)
                offc.Avatar.onSettingsLoaded.Invoke();

#endregion
            offc.Movement.Agent.enabled = false;
            Vector3 spawnPos = QuestEntry_WaitForContact.PoILocation.position;
            offc.Movement.Warp(new Vector3(128.27f, 1.56f, 88.96f));
            offc.Movement.Stop();
            offc.Behaviour.ScheduleManager.DisableSchedule();
            offc.Awareness.VisionCone.VisionEnabled = false;
            offc.Movement.PauseMovement();
            offc.ChatterEnabled = false;
            offc.Movement.Agent.enabled = true;
            yield return Wait2;
            Log("Reset Pos");
            // because for some reason the cop just tps back to station and sets invis in building
            offc.Movement.Agent.enabled = true;
            offc.Avatar.gameObject.SetActive(true);
            offc.Movement.Warp(new Vector3(128.27f, 1.56f, 88.96f));
            offc.transform.rotation = Quaternion.Euler(0f, 160f, 0f);

            void OnDialogComplete()
            {
                QuestEntry_WaitForContact.Complete();
                MelonCoroutines.Start(RunContactDespawn(myNpc, copNet, netManager));
                MelonCoroutines.Start(RunBossSpawn());
            }
            Action callback = new Action(OnDialogComplete);
            MelonCoroutines.Start(GenContactDialog(myNpc, callback));


            Log("Send Message");
            fixer.MSGConversation.SendMessage(new Message("I set up a meeting for you. He is waiting near the church.", Message.ESenderType.Other, false, -1), true, true);

            Log("Set Waypoint");
            QuestEntry_WaitForContact.CreateCompassElement();
            yield return null;
        }

        public IEnumerator RunContactDespawn(NPC npc, NetworkObject obj, NetworkManager mgr)
        {
            yield return Wait30;
            NPCManager.NPCRegistry.Remove(npc);
            mgr.ServerManager.Despawn(obj);
            if (npc != null)
                if (npc.gameObject != null)
                    GameObject.Destroy(npc.gameObject);
            yield return null;
        }

        private IEnumerator RunBossSpawn()
        {
            Vector3 spawnPos = QuestEntry_DefeatBoss.PoILocation.position;
            CartelGoon _bossGoon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);

            _bossGoon.Behaviour.ScheduleManager.DisableSchedule();
            // because for some reason the avatar goes off and same with nav
            yield return Wait2;
            if (!_bossGoon.Avatar.gameObject.activeSelf) _bossGoon.Avatar.gameObject.SetActive(true);
            if (_bossGoon.Movement.Agent != null && _bossGoon.Movement.Agent.enabled == false) _bossGoon.Movement.Agent.enabled = true;

            #region Cracked Shotgun
            _bossGoon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/PumpShotgun");
            yield return Wait05;
#if MONO
            if (_bossGoon.Behaviour.CombatBehaviour.currentWeapon is AvatarRangedWeapon wep)
            {
                wep.MaxUseRange = 30f;
                wep.MinUseRange = 0.4f;
                wep.HitChance_MaxRange = 0.1f;
                wep.HitChance_MinRange = 0.7f;
                wep.MaxFireRate = 2.6f;
                wep.CooldownDuration = 0.8f;
                wep.Damage = 55f;
                wep.ReloadTime = 2.3f;
                wep.RaiseTime = 1.3f;
                wep.ImpactForce = 28f;
                wep.AimTime_Max = 1.2f;
                wep.RepositionAfterHit = true;
            }
#else
            AvatarRangedWeapon wep = null;
            try
            {
                wep = _bossGoon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
            } 
            catch (InvalidCastException ex)
            {
                MelonLogger.Warning("Failed to Cast Thomas Gun Weapon Instance: " + ex);
            }

            if (wep != null)
            {
                wep.MaxUseRange = 30f;
                wep.MinUseRange = 0.4f;
                wep.HitChance_MaxRange = 0.1f;
                wep.HitChance_MinRange = 0.7f;
                wep.MaxFireRate = 2.6f;
                wep.CooldownDuration = 0.8f;
                wep.Damage = 55f;
                wep.ReloadTime = 2.3f;
                wep.RaiseTime = 1.3f;
                wep.ImpactForce = 28f;
                wep.AimTime_Max = 1.2f;
                wep.RepositionAfterHit = true;
            }
#endif
#endregion
            Log("Setup Boss Weapon");
            yield return Wait05;
            #region Movement and Health
            _bossGoon.Health.MaxHealth = 420f;
            _bossGoon.Health.Health = 420f;
            _bossGoon.Health.Revive();
            _bossGoon.Movement.MoveSpeedMultiplier = 0.4f;
            #endregion
            Log("Setup Boss Move & Health");
            yield return Wait05;
            #region Avatar
            _bossGoon.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);

            var originalAccessorySettings = _bossGoon.Avatar.CurrentSettings.AccessorySettings;
#if MONO
            List<AccessorySetting> accessorySettings = new();
#else
            Il2CppSystem.Collections.Generic.List<AccessorySetting> accessorySettings = new();
#endif
            foreach (var acc in originalAccessorySettings)
            {
                accessorySettings.Add(new AccessorySetting
                {
                    path = acc.path,
                    color = acc.color
                });
            }
            for (int i = 0; i < accessorySettings.Count; i++)
            {
                var acc = accessorySettings[i];
                acc.path = "";
                acc.color = Color.white;
                accessorySettings[i] = acc;
            }

            var vest = accessorySettings[0];
            vest.path = "Avatar/Accessories/Chest/BulletproofVest/BulletproofVest";
            vest.color = new Color(0.1f, 0.5f, 0.1f);
            accessorySettings[0] = vest;
            var chain = accessorySettings[1];
            chain.path = "Avatar/Accessories/Neck/GoldChain/GoldChain";
            chain.color = new Color(0.96f, 0.79f, 0.23f);
            accessorySettings[1] = chain;
            var watch = accessorySettings[2];
            watch.path = "Avatar/Accessories/Hands/Polex/Polex";
            watch.color = new Color(0.96f, 0.79f, 0.23f);
            accessorySettings[2] = watch;

            _bossGoon.Avatar.CurrentSettings.AccessorySettings = accessorySettings;
            _bossGoon.Avatar.ApplyAccessorySettings(_bossGoon.Avatar.CurrentSettings);

            if (_bossGoon.Avatar.UseImpostor)
                _bossGoon.Avatar.Impostor.SetAvatarSettings(_bossGoon.Avatar.CurrentSettings);

            if (_bossGoon.Avatar.onSettingsLoaded != null)
                _bossGoon.Avatar.onSettingsLoaded.Invoke();

            #endregion
            Log("Setup Boss Avatar");
            // because for some reason the avatar goes off and same with nav
            yield return Wait2;
            _bossGoon.ExitBuilding();
            _bossGoon.Movement.Warp(spawnPos);
            if (!_bossGoon.Avatar.gameObject.activeSelf) _bossGoon.Avatar.gameObject.SetActive(true);
            if (_bossGoon.Movement.Agent != null && _bossGoon.Movement.Agent.enabled == false) _bossGoon.Movement.Agent.enabled = true;

            bossGoon = _bossGoon;

            void OnBossDied()
            {
                QuestEntry_DefeatBoss.Complete();
                completed = true;
                MelonCoroutines.Start(QuestReward(bossGoon));
                this.Complete(true);

                bossGoon.Health.onDieOrKnockedOut.RemoveListener((UnityEngine.Events.UnityAction)OnBossDied);
            }
            bossGoon.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)OnBossDied);

            QuestEntry_DefeatBoss.CreateCompassElement();
            yield return null;
        }

        public override void MinPass()
        {
#if MONO
            base.MinPass(); // Is this necessary in mono or does cause recursion??

#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_Investigate.State == EQuestState.Active)
            {
                QuestEntry_Investigate.SetEntryTitle($"Intercept Cartel Dead Drops ({StageDeadDropsObserved}/2)");
                if (QuestEntry_Investigate.PoI.gameObject.activeSelf)
                {
                    QuestEntry_Investigate.PoI.gameObject.SetActive(false);
                }
                if (StageDeadDropsObserved >= 2)
                {
                    Log("Completed first stage");
                    QuestEntry_Investigate.Complete();
                }
            }
            else if (QuestEntry_DefeatBoss.State == EQuestState.Active)
            {
                if (bossGoon != null)
                {
                    QuestEntry_DefeatBoss.SetEntryTitle($"Defeat the Cartel Brute | HP:{bossGoon.Health.Health}");
                    Player p = Player.GetClosestPlayer(bossGoon.transform.position, out float dist);

                    if (dist < 13f && !bossCombatBegun)
                    {
                        bossCombatBegun = true;
                        bossGoon.Behaviour.CombatBehaviour.SetTarget(null, p.GetComponent<ICombatTargetable>().NetworkObject);
                        bossGoon.Behaviour.CombatBehaviour.Enable_Networked(null);
                    }

                    if (bossCombatBegun)
                    {
                        fightElapsed++;

                        // Check distance of boss to player & Check distance of Boss to the area & check elapsed time under 2min
                        if (dist > 70f || Vector3.Distance(bossGoon.CenterPoint, QuestEntry_DefeatBoss.PoILocation.position) > 70f || fightElapsed > 120)
                        {
                            // Player Out of range or Boss is over 70 units from spawn pos or time has elapsed over 2min

                            QuestEntry_DefeatBoss.SetState(EQuestState.Failed, true);
                            completed = true;
                            this.Fail(true);
                            bossGoon.Despawn();
                            ResetGoonBoss();
                        }

                    }



                }
            }

        }

        private void HourPass()
        {
            Log("HourPass In Quest");
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_Investigate.State == EQuestState.Active)
            {
                Log("State Investigate");
            }
            else if (QuestEntry_WaitForContact.State == EQuestState.Active)
            {
                if (!contactMade)
                {
                    Log("State WaitContact");
                    if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 59 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 199)
                    {
                        // 30% chance to skip messaging tonight
                        float chanceToSkip = 0.30f;
                        if (UnityEngine.Random.Range(0f, 1f) > chanceToSkip)
                        {
                            contactMade = true;
                            QuestEntry_WaitForContact.compassElement.Visible = true;
                            QuestEntry_WaitForContact.PoI.gameObject.SetActive(true);
                            // What to do here, spawn the cop there under a light 
                            // send text msg
                            coros.Add(MelonCoroutines.Start(ContactSpawn()));
                        }
                    }
                }
                else
                {
                    Log("Wait For Player To Arrive To NPC and initiate dialogue");
                }

            }
            else if (QuestEntry_DefeatBoss.State == EQuestState.Active)
            {
                // Setup trigger to spawn the boss and configure values
            }

            
        }

        private void ResetGoonBoss()
        {
            if (bossGoon != null)
            {
                // Reset all non default stats that would carry on modified
                bossGoon.Health.MaxHealth = 100f;
                bossGoon.Movement.MoveSpeedMultiplier = 0.8f;
                bossGoon.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                bossGoon.Behaviour.ScheduleManager.EnableSchedule();
            }

            return;
        }


        public QuestEntry QuestEntry_Investigate;

        public QuestEntry QuestEntry_WaitForContact;

        public QuestEntry QuestEntry_DefeatBoss;

    }


#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_InfiltrateManor : Quest
    {
#if IL2CPP
        public Quest_InfiltrateManor(IntPtr ptr) : base(ptr) { }

        public Quest_InfiltrateManor() : base(ClassInjector.DerivedConstructorPointer<Quest_InfiltrateManor>())
            => ClassInjector.DerivedConstructorBody(this);
#endif

        private GameObject UiPrefab = null;
        private RectTransform rtIcon;

        private List<Vector3> forestSearchLocs = new()
        {
            new Vector3(164.96f, 3.10f, -32.65f),
            new Vector3(151.45f, 3.20f, -35.21f),
            new Vector3(139.41f, 1.96f, -45.71f),
            new Vector3(135.26f, 2.72f, -66.56f)
        };
        private int forestPosSearched = 0; // indexing for above search location one by one update compass

        private List<CartelGoon> manorGoons = new();
        private int manorGoonsAlive = 4;
        private Dictionary<Vector3, bool> roomsPositions = new()
        {
            { new Vector3(166.58f, 15.61f, -52.99f), false },
            { new Vector3(160.65f, 15.61f, -52.97f), false },
            { new Vector3(160.65f, 15.61f, -61.00f), false },
            { new Vector3(166.58f, 15.61f, -61.00f), false }
        };
        private int roomsVisited = 0;

#if IL2CPP
        // Because by default the property uses this.Entries in Enumberable.Count, which probably causes the bug when the this.entries is il2cpp system ienumerable but expecting system ienumerable?
        public new int ActiveEntryCount
        {
            get
            {
                if (this.Entries == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (var entry in this.Entries)
                {
                    if (entry.State == EQuestState.Active)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
#endif

        public void SetupSelf()
        {
            Log("SetupSelfStart");

            Log("QuestInit");
            this.name = "Quest_InfiltrateManor";
            Expires = false;
            title = "Infiltrate Manor";
            CompletionXP = 850;
            Description = "Find info about Thomas Benzie and break into Manor";
            TrackOnBegin = true;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = true;

            onActiveState = new UnityEvent();
            onComplete = new UnityEvent();
            onInitialComplete = new UnityEvent();
            onQuestBegin = new UnityEvent();
            onQuestEnd = new UnityEvent<EQuestState>();
            onTrackChange = new UnityEvent<bool>();

#if MONO
            this.SetGUID(Guid.NewGuid());
#else
            this.SetGUID(Il2CppSystem.Guid.NewGuid());
#endif
            Transform target = NetworkSingleton<QuestManager>.Instance.QuestContainer?.GetChild(0);
            if (target != null)
            {
                this.transform.SetParent(target);
            }

            MakeIcon();
            MakeUIPrefab();
            MakePOI();

            // Create the QuestEntry GameObjects and parent them.

            GameObject investigateWoodsObject = new GameObject("QuestEntry_InvestigateWoods");
            investigateWoodsObject.transform.SetParent(this.transform);

            GameObject returnToRayObject = new GameObject("QuestEntry_ReturnToRay");
            returnToRayObject.transform.SetParent(this.transform);

            GameObject waitForNightObject = new GameObject("QuestEntry_WaitForNight");
            waitForNightObject.transform.SetParent(this.transform);

            GameObject breakInObject = new GameObject("QuestEntry_BreakIn");
            breakInObject.transform.SetParent(this.transform);

            GameObject defeatGoonsObject = new GameObject("QuestEntry_DefeatManorGoons");
            defeatGoonsObject.transform.SetParent(this.transform);

            GameObject searchResidenceObject = new GameObject("QuestEntry_SearchResidence");
            searchResidenceObject.transform.SetParent(this.transform);

            GameObject escapeManorObject = new GameObject("QuestEntry_EscapeManor");
            escapeManorObject.transform.SetParent(this.transform);

            QuestEntry investigate = investigateWoodsObject.AddComponent<QuestEntry>();
            QuestEntry returnToRay = returnToRayObject.AddComponent<QuestEntry>();
            QuestEntry waitForNight = waitForNightObject.AddComponent<QuestEntry>();
            QuestEntry breakIn = breakInObject.AddComponent<QuestEntry>();
            QuestEntry defeatGoons = defeatGoonsObject.AddComponent<QuestEntry>();
            QuestEntry searchResidence = searchResidenceObject.AddComponent<QuestEntry>();
            QuestEntry escapeManor = escapeManorObject.AddComponent<QuestEntry>();


            Log("Setting Entries");
            this.QuestEntry_InvestigateWoods = investigate;
            this.QuestEntry_ReturnToRay = returnToRay;
            this.QuestEntry_WaitForNight = waitForNight;
            this.QuestEntry_BreakIn = breakIn;
            this.QuestEntry_DefeatManorGoons = defeatGoons;
            this.QuestEntry_SearchResidence = searchResidence;
            this.QuestEntry_EscapeManor = escapeManor;
#if MONO
            this.Entries = new()
                {
                    investigate, returnToRay, waitForNight, breakIn, defeatGoons, searchResidence, escapeManor
                };
#else
            this.Entries = new();
            this.Entries.Add(investigate);
            this.Entries.Add(returnToRay);
            this.Entries.Add(waitForNight);
            this.Entries.Add(breakIn);
            this.Entries.Add(defeatGoons);
            this.Entries.Add(searchResidence);
            this.Entries.Add(escapeManor);
#endif
            Log($"Entries list type: {this.Entries.GetType()}");
            Log("Config Entries");

            investigate.SetEntryTitle("Investigate the hillside forest near Manor (0/4)");
            investigate.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            investigate.PoILocation.name = "InvestigateEntry_POI";
            investigate.PoILocation.transform.SetParent(investigate.transform);
            investigate.PoILocation.transform.position = new Vector3(164.96f, 3.10f, -32.65f);
            investigate.AutoUpdatePoILocation = true;
            investigate.SetState(EQuestState.Active, true);
            investigate.ParentQuest = this;
            investigate.CompleteParentQuest = false;
            void OnInvestigateComplete()
            {
                QuestEntry_ReturnToRay.Begin();
                QuestEntry_ReturnToRay.CreateCompassElement();
                QuestEntry_ReturnToRay.PoI.gameObject.SetActive(true);
                QuestEntry_ReturnToRay.compassElement.Visible = true;
                QuestEntry_ReturnToRay.SetPoILocation(ray.transform.position);

                MelonCoroutines.Start(GenRaySecondDialog(QuestEntry_ReturnToRay.Complete));
                investigate.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);
            }
            investigate.onComplete.AddListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);

            returnToRay.SetEntryTitle("Return to Ray and ask for more information");
            returnToRay.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            returnToRay.PoILocation.name = "ReturnToRayEntry_POI";
            returnToRay.PoILocation.transform.SetParent(returnToRay.transform);
            returnToRay.PoILocation.transform.position = new Vector3(77.30f, 1.46f, -12.85f);
            returnToRay.AutoUpdatePoILocation = true;
            returnToRay.SetState(EQuestState.Inactive, false);
            returnToRay.ParentQuest = this;
            returnToRay.CompleteParentQuest = false;
            void OnReturnToRayComplete()
            {
                QuestEntry_WaitForNight.Begin();
                QuestEntry_WaitForNight.PoI.gameObject.SetActive(false);
                if (QuestEntry_WaitForNight.compassElement != null)
                    QuestEntry_WaitForNight.compassElement.Visible = false;
                coros.Add(MelonCoroutines.Start(this.SetupManor()));

                returnToRay.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnReturnToRayComplete);
            }
            returnToRay.onComplete.AddListener((UnityEngine.Events.UnityAction)OnReturnToRayComplete);

            waitForNight.SetEntryTitle("Wait for night time");
            waitForNight.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            waitForNight.PoILocation.name = "WaitForNightEntry_POI";
            waitForNight.PoILocation.transform.SetParent(waitForNight.transform);
            waitForNight.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            waitForNight.AutoUpdatePoILocation = true;
            waitForNight.SetState(EQuestState.Inactive, false);
            waitForNight.ParentQuest = this;
            waitForNight.CompleteParentQuest = false;
            void OnWaitForNightComplete()
            {
                this.QuestEntry_BreakIn.Begin();
                QuestEntry_BreakIn.CreateCompassElement();
                QuestEntry_BreakIn.PoI.gameObject.SetActive(true);
                if (QuestEntry_BreakIn.compassElement != null)
                    QuestEntry_BreakIn.compassElement.Visible = true;
                waitForNight.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnWaitForNightComplete);
            }
            waitForNight.onComplete.AddListener((UnityEngine.Events.UnityAction)OnWaitForNightComplete);

            breakIn.SetEntryTitle("Break into Manor through the back door");
            breakIn.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            breakIn.PoILocation.name = "BreakInEntry_POI";
            breakIn.PoILocation.transform.SetParent(breakIn.transform);
            breakIn.PoILocation.transform.position = new Vector3(163.37f, 11.86f, -50.12f);
            breakIn.AutoUpdatePoILocation = true;
            breakIn.SetState(EQuestState.Inactive, false);
            breakIn.ParentQuest = this;
            breakIn.CompleteParentQuest = false;
            void OnBreakInComplete()
            {
                coros.Add(MelonCoroutines.Start(SpawnManorGoons()));
                QuestEntry_DefeatManorGoons.Begin();
                QuestEntry_DefeatManorGoons.PoI.gameObject.SetActive(false);
                if (QuestEntry_DefeatManorGoons.compassElement != null)
                    QuestEntry_DefeatManorGoons.compassElement.Visible = false;
                breakIn.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnBreakInComplete);
            }
            breakIn.onComplete.AddListener((UnityEngine.Events.UnityAction)OnBreakInComplete);

            defeatGoons.SetEntryTitle("Defeat the Manor Goons");
            defeatGoons.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            defeatGoons.PoILocation.name = "DefeatGoonsEntry_POI";
            defeatGoons.PoILocation.transform.SetParent(defeatGoons.transform);
            defeatGoons.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            defeatGoons.AutoUpdatePoILocation = true;
            defeatGoons.SetState(EQuestState.Inactive, false);
            defeatGoons.ParentQuest = this;
            defeatGoons.CompleteParentQuest = false;
            void OnDefeatGoonsComplete()
            {
                QuestEntry_SearchResidence.Begin();
                QuestEntry_SearchResidence.CreateCompassElement();
                QuestEntry_SearchResidence.PoI.gameObject.SetActive(true);
                if (QuestEntry_SearchResidence.compassElement != null)
                    QuestEntry_SearchResidence.compassElement.Visible = true;
                QuestEntry_SearchResidence.SetPoILocation(roomsPositions.Keys.FirstOrDefault());
                defeatGoons.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnDefeatGoonsComplete);
            }
            defeatGoons.onComplete.AddListener((UnityEngine.Events.UnityAction)OnDefeatGoonsComplete);

            searchResidence.SetEntryTitle("Investigate the upstairs rooms (0/4)");
            searchResidence.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            searchResidence.PoILocation.name = "SearchResidenceEntry_POI";
            searchResidence.PoILocation.transform.SetParent(searchResidence.transform);
            searchResidence.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            searchResidence.AutoUpdatePoILocation = true;
            searchResidence.SetState(EQuestState.Inactive, false);
            searchResidence.ParentQuest = this;
            searchResidence.CompleteParentQuest = false;
            void OnSearchResidenceComplete()
            {
                QuestEntry_EscapeManor.Begin();
                QuestEntry_EscapeManor.PoI.gameObject.SetActive(false);
                if (QuestEntry_EscapeManor.compassElement != null)
                    QuestEntry_EscapeManor.compassElement.Visible = false;
                Player.Local.CrimeData.SetPursuitLevel(PlayerCrimeData.EPursuitLevel.Investigating);
#if MONO
                PoliceStation.PoliceStations.FirstOrDefault().Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#else
                PoliceStation.PoliceStations[0].Dispatch(1, Player.Local, PoliceStation.EDispatchType.Auto, true);
#endif
                searchResidence.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);
            }
            searchResidence.onComplete.AddListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);

            escapeManor.SetEntryTitle("Escape the Manor before the Police arrive");
            escapeManor.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            escapeManor.PoILocation.name = "EscapeManorEntry_POI";
            escapeManor.PoILocation.transform.SetParent(escapeManor.transform);
            escapeManor.PoILocation.transform.position = new Vector3(0f, 0f, 0f);
            escapeManor.AutoUpdatePoILocation = true;
            escapeManor.SetState(EQuestState.Inactive, false);
            escapeManor.ParentQuest = this;
            escapeManor.CompleteParentQuest = false;
            //void OnEscapeManorComplete() Handle this in minpass
            //{
            //    QuestEntry_EscapeManor.Begin();
            //    escapeManor.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);
            //}
            //escapeManor.onComplete.AddListener((UnityEngine.Events.UnityAction)OnSearchResidenceComplete);


            Quest.Quests.Add(this);

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
#endif
            instance.onMinutePass.Add(new Action(this.MinPass)); // Is this already added on the InitializeQuest method??
            coros.Add(MelonCoroutines.Start(StartQuestDetail()));
        }

        private IEnumerator StartQuestDetail()
        {
            if (this.IconPrefab == null)
                this.IconPrefab = this.transform.Find("BenziesLogoQuest").GetComponent<RectTransform>();
            SetupHudUI();

            if (hudUI != null)
            {
                if (hudUI.MainLabel != null)
                    this.hudUI.MainLabel.text = "Infiltrate Manor";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_InvestigateWoods != null)
            {
                QuestEntry_InvestigateWoods.CreateCompassElement();
                if (QuestEntry_InvestigateWoods.compassElement != null)
                    QuestEntry_InvestigateWoods.compassElement.Visible = true;

                if (QuestEntry_InvestigateWoods.PoI != null)
                    QuestEntry_InvestigateWoods.PoI.gameObject.SetActive(true);
            }
            
            SetIsTracked(true);
            SetQuestState(EQuestState.Active);
            
            yield return null;
        }

        private IEnumerator SetupManor()
        {
            Log("SETUP MANOR");
            Manor manor = UnityEngine.Object.FindObjectOfType<Manor>(true);
            if (manor == null)
            {
                Log("Manor is null");
                yield break;
            }

            // Disable the dupe doors for now, remember to re-enable later
            Transform doorsTr = manor.transform.Find("Doors");
            if (doorsTr != null)
            {
                doorsTr.gameObject.SetActive(false);
            }
            else
            {
                Log("Manor Doors is null");
                yield break;
            }

            Log("Fetching original Door Container");
            // First setup original door to work and also the trigger to open because it doesnt have it
            Transform door = manor.OriginalContainer.transform.Find("MansionDoor (1)");
            if (door == null)
            {
                Log("Manor DOOR in orig container is NULL");
                yield break;
            }

            Transform doorContainer = door.Find("Container"); // Lerp rotation on this one because it works with hinges
            if (doorContainer == null)
            {
                Log("Manor DOORCONTAINER in container is NULL");
                yield break;
            }
            Log("Fetched original Door Container");
            InteractableObject exteriorIntObj = doorContainer.GetChild(0).GetComponent<InteractableObject>();
            InteractableObject interiorIntObj = doorContainer.GetChild(1).GetComponent<InteractableObject>();
            Log("Fetched INT objects");

            void onDoorInteracted()
            {
                coros.Add(MelonCoroutines.Start(LerpDoorRotation(doorContainer, QuestEntry_BreakIn.Complete)));
                exteriorIntObj.onInteractStart.RemoveListener((UnityEngine.Events.UnityAction)onDoorInteracted);
            }
            exteriorIntObj.message = "Open Door";
            exteriorIntObj.onInteractStart.AddListener((UnityEngine.Events.UnityAction)onDoorInteracted);
            interiorIntObj.message = "Open Door";
            Log("Configured int objects");

            // then setup lights because they are buggy if we dont disable the optimize light feature
            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };
            Log("Setup rooms done");

            foreach (Transform room in rooms)
            {
                room.gameObject.SetActive(true);
                LightOptimizer lightOptimizer = room.gameObject.GetComponent<LightOptimizer>();
                Log("LightOpt fetched");
                if (lightOptimizer == null)
                {
                    Log("Light Optimizer is null!");
                    continue;
                }
                if (lightOptimizer.lights == null || lightOptimizer.lights.Count() == 0)
                {
                    Log("Light optimizer light list is empty or null");
                    continue;
                }
                yield return Wait05;
                foreach (OptimizedLight optimizedLight in lightOptimizer.lights)
                {
                    yield return Wait05;
                    Log("LightOpt Swapping");
                    optimizedLight.Enabled = false;
                    optimizedLight.enabled = false;
                    if (optimizedLight._LightExists)
                    {
                        optimizedLight._Light.enabled = true;
                    }
                    Log("LightOpt Done");
                }
            }


            yield return null;
        }

        private IEnumerator CleanupManor()
        {
            // revert everythign setup manor does except the door thingy thats ok to keep
            Manor manor = UnityEngine.Object.FindObjectOfType<Manor>(true);
            manor.transform.Find("Doors").gameObject.SetActive(true);

            List<Transform> rooms = new()
            {
                manor.OriginalContainer.transform.GetChild(3),
                manor.OriginalContainer.transform.GetChild(4),
                manor.OriginalContainer.transform.GetChild(5)
            };
            foreach (Transform room in rooms)
            {
                LightOptimizer lightOptimizer = room.gameObject.GetComponent<LightOptimizer>();
                yield return Wait05;
                foreach (OptimizedLight optimizedLight in lightOptimizer.lights)
                {
                    yield return Wait05;
                    optimizedLight.Enabled = true;
                    optimizedLight.enabled = true;
                    if (optimizedLight._LightExists)
                    {
                        optimizedLight._Light.enabled = false;
                    }
                }
            }
            
            foreach (CartelGoon goon in manorGoons)
            {
                yield return Wait05;
                goon.Movement.MoveSpeedMultiplier = 1f;
                goon.Health.MaxHealth = 100f;
                goon.Health.Health = 100f;
                if (goon.Health.IsDead)
                    goon.Health.Revive();
                if (goon.IsSpawned)
                    goon.Despawn();

            }

            yield return null;
        }

        private IEnumerator LerpDoorRotation(Transform tr, Action cb)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, 55f, 0f);

            Quaternion startRotation = tr.localRotation;

            float duration = 2.5f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                tr.localRotation = Quaternion.Lerp(startRotation, targetRotation, progress);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            tr.localRotation = targetRotation;

            if (cb != null)
                cb();

            yield return null;
        }

        private IEnumerator SpawnManorGoons()
        {
            ItemInstance item;
            Func<string, ItemDefinition> GetItem;
            Log("Roompos Keys to list");
            List<Vector3> roomPositionsList = roomsPositions.Keys.ToList();

            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 3)
            {
                do
                {
#if MONO
                    NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.FirstOrDefault().Despawn();
#else
                    int count = NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Count - 1;
                    if (count != -1)
                        NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons[count].Despawn();
#endif
                } while (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count < 3);
            }

            for (int i = 0; i < NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count; i++)
            {
                if (i > 4) break;
                Log("SpawnGoon");
                CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(roomPositionsList[i]);
                if (!goon.gameObject.activeSelf)
                {
                    goon.gameObject.SetActive(true);
                }
                if (!goon.Avatar.enabled || !goon.Avatar.gameObject.activeSelf)
                {
                    goon.Avatar.gameObject.SetActive(true);
                    goon.Avatar.enabled = true;
                }
                goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/Knife");
                yield return Wait025;
                if (!registered) yield break;
#if MONO
                if (goon.Behaviour.CombatBehaviour.currentWeapon is AvatarMeleeWeapon wep)
                {
                    wep.AttackRadius = wep.AttackRadius * UnityEngine.Random.Range(1.2f, 1.8f);
                    wep.AttackRange = wep.AttackRange * UnityEngine.Random.Range(1.2f, 1.8f);
                    wep.MaxUseRange = 2.3f;
                    wep.MinUseRange = 0.01f;
                    wep.CooldownDuration = wep.AttackRange * UnityEngine.Random.Range(1.0f, 3.0f);
                    wep.Damage = Mathf.Round(UnityEngine.Random.Range(18f, 28f));
                }
#else
                AvatarMeleeWeapon wep = null;
                try
                {
                    Log("CastWep");
                    wep = goon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarMeleeWeapon>();
                } 
                catch (InvalidCastException ex)
                {
                    MelonLogger.Warning("Failed to Cast Thomas Gun Weapon Instance: " + ex);
                }

                if (wep != null)
                {
                    wep.AttackRadius = wep.AttackRadius * UnityEngine.Random.Range(1.2f, 1.6f);
                    wep.AttackRange = wep.AttackRange * UnityEngine.Random.Range(1.0f, 1.5f);
                    wep.MaxUseRange = 2.3f;
                    wep.MinUseRange = 0.01f;
                    wep.CooldownDuration = wep.AttackRange * UnityEngine.Random.Range(1.0f, 3.0f);
                    wep.Damage = Mathf.Round(UnityEngine.Random.Range(13f, 25f));
                }
#endif

                goon.Inventory.AddCash(Mathf.Round(UnityEngine.Random.Range(100f, 600f)));
                Log("Set HP and movespeed");
                goon.Health.MaxHealth = 200f;
                goon.Health.Health = 200f;
                goon.Movement.MoveSpeedMultiplier = UnityEngine.Random.Range(1.4f, 1.75f);

#if MONO
                GetItem = ScheduleOne.Registry.GetItem;
#else
                GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif
                if (UnityEngine.Random.Range(0f, 1f) > 0.666f)
                {
                    if (UnityEngine.Random.Range(0f, 1f) > 0.5f)
                    {
                        ItemDefinition def = GetItem("silverwatch");
                        item = def.GetDefaultInstance(1);
                        goon.Inventory.InsertItem(item, true);
                    }
                    else
                    {
                        ItemDefinition def = GetItem("silverchain");
                        item = def.GetDefaultInstance(1);
                        goon.Inventory.InsertItem(item, true);
                    }
                }

                yield return Wait025;
                if (!registered) yield break;
                goon.Behaviour.CombatBehaviour.SetTarget(null, Player.Local.GetComponent<ICombatTargetable>().NetworkObject);
                goon.Behaviour.CombatBehaviour.Enable_Networked(null);

                void onGoonDie()
                {
                    manorGoonsAlive--;
                    goon.Health.onDieOrKnockedOut.RemoveListener((UnityEngine.Events.UnityAction)onGoonDie);
                }

                goon.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)onGoonDie);
                manorGoons.Add(goon);
            }

            manorGoonsAlive = manorGoons.Count();

            yield return null;
        }

        #region Quest prefabs
        private void MakeIcon()
        {
            GameObject logo = new("BenziesLogoQuest");
            Image imgComp = logo.AddComponent<Image>();
            imgComp.sprite = benziesLogo;
            RectTransform rt = logo.AddComponent<RectTransform>();
            logo.AddComponent<CanvasRenderer>();
            logo.transform.SetParent(this.transform);

            rtIcon = rt;
            this.IconPrefab = rt;
        }

        private void MakeUIPrefab()
        {
            GameObject go = new("CartelEnforcerLogo");
            GameObject IconContainer = new("IconContainer");
            GameObject MainLabel = new("MainLabel");
            IconContainer.transform.parent = go.transform;
            MainLabel.transform.parent = go.transform;

            RectTransform rtr1 = go.AddComponent<RectTransform>();
            rtr1.anchoredPosition = new Vector2(0f, 0f);
            rtr1.anchorMax = new Vector2(0.5f, 0.5f);
            rtr1.anchorMin = new Vector2(0.5f, 0.5f);
            rtr1.offsetMax = new Vector2(25f, 25f);
            rtr1.offsetMin = new Vector2(-25f, -25f);
            rtr1.pivot = new Vector2(0.5f, 0.5f);
            rtr1.sizeDelta = new Vector2(50f, 50f);

            go.AddComponent<CanvasRenderer>();
            go.AddComponent<Image>();

            RectTransform rtr2 = IconContainer.AddComponent<RectTransform>();
            rtr2.sizeDelta = new Vector2(50f, 50f);
            Image logo = IconContainer.AddComponent<Image>();
            logo.sprite = benziesLogo;

            RectTransform rtr3 = MainLabel.AddComponent<RectTransform>();
            rtr3.anchoredPosition = new Vector2(0f, -46f);
            rtr3.anchorMax = new Vector2(0.5f, 0.5f);
            rtr3.anchorMin = new Vector2(0.5f, 0.5f);
            rtr3.offsetMax = new Vector2(250f, 14f);
            rtr3.offsetMin = new Vector2(-250f, -106f);
            rtr3.pivot = new Vector2(-250f, -106f);
            rtr3.sizeDelta = new Vector2(500f, 120f);
            MainLabel.AddComponent<CanvasRenderer>();
            MainLabel.AddComponent<Text>();
            UiPrefab = go;
            go.transform.parent = this.transform;

            return;
        }

        private void MakePOI()
        {
            GameObject poiPrefabObject = new GameObject($"CartelEnforcer_POI");
            poiPrefabObject.transform.SetParent(this.transform);
            poiPrefabObject.SetActive(false);
            POI poi = poiPrefabObject.AddComponent<POI>();
            poi.AutoUpdatePosition = true;
            poi.MainText = "Test";
            poi.DefaultMainText = "TestText";
            poi.UIPrefab = UiPrefab;
            PoIPrefab = poiPrefabObject;
            return;
        }
        #endregion

        public override void MinPass()
        {
#if MONO
            base.MinPass();

#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_InvestigateWoods.State == EQuestState.Active)
            {
                QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor ({forestPosSearched}/4)");

                if (forestSearchLocs == null) return;

                if (forestPosSearched == 4)
                {
                    QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the hillside forest near Manor (4/4)");
                    QuestEntry_InvestigateWoods.Complete();
                    return;
                }

                if (Vector3.Distance(Player.Local.CenterPointTransform.position, forestSearchLocs[forestPosSearched]) < 5f)
                {
                    forestPosSearched++;
                    if (forestPosSearched < forestSearchLocs.Count)
                    {
                        QuestEntry_InvestigateWoods.SetPoILocation(forestSearchLocs[forestPosSearched]);
                    }
                    else
                    {
                        QuestEntry_InvestigateWoods.Complete();
                        return; // Return after completing the quest in this path as well.
                    }
                }

                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200)
                {
                    this.Fail(true);
                }
            }
            else if (QuestEntry_ReturnToRay.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200)
                {
                    this.Fail(true);
                }
            }
            else if (QuestEntry_DefeatManorGoons.State == EQuestState.Active)
            {
                if (manorGoons != null && manorGoons.Count > 0 && manorGoonsAlive == 0)
                {
                    QuestEntry_DefeatManorGoons.Complete();
                }
            }
            else if (QuestEntry_SearchResidence.State == EQuestState.Active)
            {
                QuestEntry_InvestigateWoods.SetEntryTitle($"Investigate the upstairs rooms ({roomsVisited}/4)");

                bool allRoomsVisited = true;
                foreach (var roomEntry in roomsPositions)
                {
                    Vector3 roomPosition = roomEntry.Key;
                    bool hasBeenVisited = roomEntry.Value;

                    if (!hasBeenVisited)
                    {
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, roomPosition) < 0.85f)
                        {
                            roomsPositions[roomPosition] = true;
                            roomsVisited++;
                            break;
                        }
                    }
                }

                foreach (var roomEntry in roomsPositions)
                {
                    if (!roomEntry.Value)
                    {
                        QuestEntry_SearchResidence.SetPoILocation(roomEntry.Key);
                        allRoomsVisited = false;
                        break;
                    }
                }

                if (allRoomsVisited)
                {
                    QuestEntry_SearchResidence.Complete();
                }
            }
            else if (QuestEntry_EscapeManor.State == EQuestState.Active)
            {
                if (Player.Local.CrimeData.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None)
                {
                    // Not being hunted
                    if (Vector3.Distance(QuestEntry_BreakIn.PoILocation.position, Player.Local.CenterPointTransform.position) > 60f)
                    {
                        QuestEntry_EscapeManor.Complete();
                        // Far enough escaped from the back door position
                        coros.Add(MelonCoroutines.Start(CleanupManor()));
                        coros.Add(MelonCoroutines.Start(QuestManorReward()));
                        manorCompleted = true;
                        this.Complete();
                    }
                }
            }
        }

        private void HourPass()
        {
            Log("HourPass In Quest");
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_WaitForNight.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2200 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 2359)
                {
                    QuestEntry_WaitForNight.Complete();
                }
            }
            

        }

        private QuestEntry QuestEntry_InvestigateWoods;
        private QuestEntry QuestEntry_ReturnToRay;
        private QuestEntry QuestEntry_WaitForNight;
        private QuestEntry QuestEntry_BreakIn;
        private QuestEntry QuestEntry_DefeatManorGoons;
        private QuestEntry QuestEntry_SearchResidence;
        private QuestEntry QuestEntry_EscapeManor;

    }

}
