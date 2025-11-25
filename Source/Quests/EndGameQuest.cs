

using System.Collections;
using MelonLoader;
using UnityEngine;
using HarmonyLib;
using UnityEngine.UI;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.InterceptEvent;

#if MONO
using ScheduleOne.Law;
using ScheduleOne.PlayerScripts;
using ScheduleOne.ItemFramework;
using ScheduleOne.Economy;
using ScheduleOne.Levelling;
using ScheduleOne.Money;
using ScheduleOne.Map;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Property;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Dialogue;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.VoiceOver;
using ScheduleOne.NPCs.Behaviour;
using FishNet;
#else
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.VoiceOver;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppFishNet;
#endif

namespace CartelEnforcer
{

    // Fix shotgun ragdolls + flinches, but only for manor goons, boss and car meetup goons
    // Previously this was used to fix a bug where it drops weapon when impacted, in 0.4.1f13 no longer an issue
    // Now this provides just additional difficulty by preventing those mechanics

    [HarmonyPatch(typeof(NPC), "ProcessImpactForce")]
    public static class NPC_ProcessImpactForce_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(NPC __instance, Vector3 forcePoint, Vector3 forceDirection, ref float force)
        {
            if (activeQuest == null && activeManorQuest == null && activeCarMeetupQuest == null) return true;

            // because after quest complete it auto disables object we check that the quest is active and not completed
            if (activeQuest != null && activeQuest.gameObject.activeSelf) 
            {
                if (bossGoon != null && bossGoon.GUID == __instance.GUID)
                {
                    force = 10f;
                }
            }
            else if (activeManorQuest != null && activeManorQuest.gameObject.activeSelf)
            {
                if (manorGoonGuids.Count > 0 && manorGoonGuids.Contains(__instance.GUID.ToString()))
                {
                    force = 10f;
                }
            }
            else if (activeCarMeetupQuest != null && activeCarMeetupQuest.gameObject.activeSelf)
            {
                if (activeCarMeetupQuest.spawnedGoonsGuids.Count > 0 && activeCarMeetupQuest.spawnedGoonsGuids.Contains(__instance.GUID.ToString()))
                {
                    force = 10f;
                }
            }

            return true;
        }

    }

    // Fix the car meetup getting easily reported to police
    [HarmonyPatch(typeof(CallPoliceBehaviour), "IsTargetValid")]
    public static class Behaviour_IsTargetValid_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(CallPoliceBehaviour __instance)
        {
            if (activeCarMeetupQuest != null && activeCarMeetupQuest.QuestEntry_StopCarMeetup?.State == EQuestState.Active)
            {
                __instance.Target = null;
            }
            return true;
        }

    }

    public static class EndGameQuest
    {
        #region End Game Quest Unexpected Alliances
        public static bool completed = false;
        public static int StageDeadDropsObserved = 0;
        public static int StageGatheringsDefeated = 0;
        public static NPC fixer;
        public static CartelGoon bossGoon;
        public static Quest_DefeatEnforcer activeQuest = null;
        public static int fixerDiagIndex = 0;

        public static bool inContactDialogue = false;

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

            // Suburbia region, has to have atleast 1 customer unlocked
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
            if (numUnlocked < 1)
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
        public static IEnumerator DisposeContactChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(0);
            controller.Choices = oldChoices;
            Log("[END GAME QUEST]    Disposed Choice");
            yield return null;
        }
        public static void OnOptionSelected(DialogueController controller, Action cb)
        {
            controller.handler.ContinueSubmitted();
            MelonCoroutines.Start(DisposeContactChoice(controller));
            MelonCoroutines.Start(ContactDialogue(controller, cb));
        }

        public static IEnumerator ContactDialogue(DialogueController controller, Action cb)
        {
            inContactDialogue = true;
            float lerpWait = Mathf.Lerp(10f, 5f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);
            List<string> dialog = dialogOptions[UnityEngine.Random.Range(0, dialogOptions.Count)];
            controller.npc.PlayVO(EVOLineType.Concerned);
            controller.handler.WorldspaceRend.ShowText("It doesn't matter who I am. We have a bigger issue at our hands.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText("The cartel has been running Hyland Point for too long.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText("We have intel that Thomas' high ranking soldier is nearby that house up the dirt road.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText("This is not your basic goon, they are a Brute. One of the best soldiers he has.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText("Go and take them down. I'll make sure nobody comes snooping around.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;


            Log("Running callback");
            if (cb != null)
                cb();

            inContactDialogue = false;
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
            float lerpWait = Mathf.Lerp(10f, 5f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);
            List<string> dialog = dialogOptions[UnityEngine.Random.Range(0, dialogOptions.Count)];
            controller.handler.WorldspaceRend.ShowText(dialog[0], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText(dialog[1], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText(dialog[2], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText(dialog[3], lerpWait);
            yield return waitObj;
            if (!registered) yield break;


            yield return null;
        }
        private static IEnumerator GenerateQuestState()
        {
            Log("Starting");
            GameObject newQuestObject = new GameObject();
            Log("Add Component");
            activeQuest = newQuestObject.AddComponent<Quest_DefeatEnforcer>();
            newQuestObject.SetActive(true);
            activeQuest.enabled = true;
            Log("SetupSelf");
            activeQuest.SetupSelf();

            yield return null;
        }
        public static IEnumerator QuestReward(CartelGoon goon)
        {
            yield return Wait025;
            if (!registered) yield break;

            goon.Inventory.Clear();

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

            ItemDefinition defGun = GetItem("pumpshotgun");
            ItemInstance gunInst = defGun.GetDefaultInstance(1);
            goon.Inventory.InsertItem(gunInst, true);

            ItemDefinition defShell = GetItem("shotgunshell");
            ItemInstance shell = defShell.GetDefaultInstance(UnityEngine.Random.Range(4, 10));
            goon.Inventory.InsertItem(shell, true);

            // Change globally customer relation
            for (int i = 0; i < Customer.UnlockedCustomers.Count; i++)
            {
                yield return Wait025;
                if (!registered) yield break;

                if (Customer.UnlockedCustomers[i].NPC.RelationData.RelationDelta != 5.0f)
                    Customer.UnlockedCustomers[i].NPC.RelationData.ChangeRelationship(0.25f, true);
            }

            // Change influence in all unlocked regions
            if (InstanceFinder.IsServer)
            {
                foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    if (unlmapReg == EMapRegion.Northtown) continue;
                    yield return Wait5; // play animation
                    if (!registered) yield break;

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

            // last reward lower Law intensity by 25%
            float change = Mathf.Clamp(Singleton<LawController>.Instance.internalLawIntensity * 0.25f, 0f, 2.5f);
            if (change != 0f)
                Singleton<LawController>.Instance.ChangeInternalIntensity(-change);

            yield return Wait60;
            if (!registered) yield break;

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
                bossGoon.Behaviour.CombatBehaviour.Disable_Networked(null);
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
        public static GameObject safePrefab = null;
        public static List<CartelGoon> manorGoons = new();
        public static List<string> manorGoonGuids = new();

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

            bool inTimeWindow = NetworkSingleton<TimeManager>.Instance.CurrentTime >= 1815 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 1858;

            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= 2500f;
            bool isInPos = (Vector3.Distance(ray.CenterPoint, standPos) < 1f);
            if (hasCash && inTimeWindow && isInPos)
            {
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-2500f, true, false);
                MelonCoroutines.Start(DisposeRayChoice(controller));
                MelonCoroutines.Start(EventManorInstructions(controller));
                MelonCoroutines.Start(GenerateManorQuestState());
            }
            else
            {
                controller.handler.WorldspaceRend.ShowText("Talk with me at the courthouse between 18:15 and 19:00.", 7f);
                controller.npc.PlayVO(EVOLineType.Annoyed);
                Log("Refuse Quest Give Reason");
                Log($"In Position: {isInPos} (distance: {Vector3.Distance(ray.CenterPoint, standPos)})");
                Log("Has Cash: " + hasCash);
                Log("In Time Window: " + inTimeWindow);

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
            float lerpWait = Mathf.Lerp(10f, 5f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);
            List<string> dialog = dialogManorOptions[UnityEngine.Random.Range(0, dialogManorOptions.Count)];
            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText(dialog[0], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Concerned);
            controller.handler.WorldspaceRend.ShowText(dialog[1], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText(dialog[2], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Surprised);
            controller.handler.WorldspaceRend.ShowText(dialog[3], lerpWait);
            yield return waitObj;
            if (!registered) yield break;


            yield return null;
        }
        private static IEnumerator GenerateManorQuestState()
        {
            Log("Starting");
            GameObject newQuestObject = new GameObject();
            Log("Add Component");
            activeManorQuest = newQuestObject.AddComponent<Quest_InfiltrateManor>();
            newQuestObject.SetActive(true);
            activeManorQuest.enabled = true;
            Log("SetupSelf");
            activeManorQuest.SetupSelf();

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
            rayChoiceIndex = controller.AddDialogueChoice(choice);
            yield return null;
        }
        public static void OnSecondDialogChosen(DialogueController controller, Action cb)
        {
            controller.handler.ContinueSubmitted();

            MelonCoroutines.Start(DisposeRaySecondChoice(controller));
            MelonCoroutines.Start(PlaySecondRayDialog(controller, cb));
        }
        public static IEnumerator DisposeRaySecondChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(rayChoiceIndex);
            controller.Choices = oldChoices;
            Log("[END GAME QUEST]    Disposed Choice");

            yield return Wait30;
            if (!registered) yield break;

            coros.Add(MelonCoroutines.Start(ResetRayAFK()));
            yield return null;
        }

        public static IEnumerator ResetRayAFK()
        {
#if MONO
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).EndTime = 1800;
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).Duration = 60;
            (ray.Behaviour.ScheduleManager.ActiveAction as NPCEvent_LocationBasedAction).Start();
#else
            NPCEvent_LocationBasedAction temp = ray.Behaviour.ScheduleManager.ActiveAction.TryCast<NPCEvent_LocationBasedAction>();
            if (temp != null)
            {
                temp.EndTime = 1800;
                temp.Duration = 60;
            }
#endif
            ray.Behaviour.ScheduleManager.EnableSchedule();
            yield return null;
        }

        public static IEnumerator PlaySecondRayDialog(DialogueController controller, Action cb)
        {
            float lerpWait = Mathf.Lerp(10f, 5f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);

            float lerpWaitLong = Mathf.Lerp(15f, 7f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObjLong = new WaitForSeconds(lerpWaitLong);

            controller.npc.PlayVO(EVOLineType.No);
            controller.handler.WorldspaceRend.ShowText("I don't think so. The whole place is fenced off.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText("If you manage to make it over the fence, there is a way for me to get you inside.", lerpWaitLong);
            yield return waitObjLong;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText("Here, take my master key. It's from back when the Manor was built.", lerpWait + 2f);
            yield return Wait2;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.npc.SetAnimationTrigger("GrabItem");
            yield return waitObj;
            if (!registered) yield break;

            controller.handler.WorldspaceRend.ShowText("It won't work on the main entrance, but try the back door.", 5f);
            yield return waitObj;
            if (!registered) yield break;


            if (cb != null)
                cb();

            yield return null;
        }
        public static IEnumerator QuestManorReward()
        {
            yield return Wait025;
            if (!registered) yield break;

            for (int i = 0; i < Business.Businesses.Count; i++)
            {
                yield return Wait025;
                if (!registered) yield break;

                Business.Businesses[i].Price = Business.Businesses[i].Price * 0.85f;
            }

            for (int i = 0; i < Property.Properties.Count; i++)
            {
                yield return Wait025;
                if (!registered) yield break;

                Property.Properties[i].Price = Property.Properties[i].Price * 0.85f;
            }

            // Change influence in all unlocked regions
            if (InstanceFinder.IsServer)
            {
                foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    if (unlmapReg == EMapRegion.Northtown) continue;
                    yield return Wait5; // play animation
                    if (!registered) yield break;

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

        #region Car Meetup Quest
        public static NPC crankyFrank = null;
        public static NPC jeremy = null;
        public static Quest_CarMeetup activeCarMeetupQuest = null;

        public static Vector3 frankStandPos = new(-55.29f, -3.64f, 167.14f);
        public static Vector3 jeremyStandPos = new(69.00f, 5.93f, -119.09f);

        public static bool carMeetupCompleted = false;

        public static int frankDiagIndex = -1;
        public static int jeremyDiagIndex = -1;
        public static bool jeremyDialogueActive = false;

        public static bool CarQuestPreRequirementsMet()
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

            // Docks region, has to have atleast 3 customer unlocked
            int numUnlocked = 0;
#if MONO
            using (List<Customer>.Enumerator enumerator = Customer.UnlockedCustomers.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.NPC.Region == EMapRegion.Docks)
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


            if (numUnlocked < 3)
                return false;

            if ((int)NetworkSingleton<LevelManager>.Instance.Rank < 3) // If not atleast hustler rank
                return false;
            return true;
        }

        public static IEnumerator GenFrankOption()
        {
            if (crankyFrank == null)
            {
#if MONO
                NPC npc = UnityEngine.Object.FindObjectOfType<ScheduleOne.NPCs.CharacterClasses.Frank>(true);
#else
                NPC npc = UnityEngine.Object.FindObjectOfType<Il2CppScheduleOne.NPCs.CharacterClasses.Frank>(true);
#endif
                if (npc != null)
                    crankyFrank = npc;
            }

            DialogueController controller = crankyFrank.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "Have you seen any Benzies around here?";
            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-$3500</color>)";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnCarQuestChosen(controller); });
#else
            void OnQuestChosenWrapped()
            {
                OnCarQuestChosen(controller);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnQuestChosenWrapped);
#endif
            frankDiagIndex = controller.AddDialogueChoice(choice);
            yield return null;
        }

        public static void OnCarQuestChosen(DialogueController controller)
        {
            controller.handler.ContinueSubmitted();

            bool inTimeWindow = NetworkSingleton<TimeManager>.Instance.CurrentTime >= 1600 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 1800;

            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= 3500f;
            bool isInPos = (Vector3.Distance(crankyFrank.CenterPoint, frankStandPos) < 0.7f);
            if (hasCash && inTimeWindow && isInPos)
            {
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-3500f, true, false);
                MelonCoroutines.Start(DisposeFrankChoice(controller));
                MelonCoroutines.Start(EventFrankInstructions(controller));
            }
            else
            {
                controller.handler.WorldspaceRend.ShowText("Meet me at the north waterfront between 16:00 and 18:00.", 7f);
                controller.npc.PlayVO(EVOLineType.Annoyed);
            }
        }

        public static IEnumerator DisposeFrankChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            if (frankDiagIndex != -1)
            {
                var oldChoices = controller.Choices;
                oldChoices.RemoveAt(frankDiagIndex);
                controller.Choices = oldChoices;
                Log("[END GAME QUEST]    Disposed Choice");

                frankDiagIndex = -1;
            }
            yield return null;
        }

        // 3 Alternatives for the worldspace dialogue
        public static List<List<string>> frankDialogOptions = new()
        {
            new List<string>()
            {
                "I've seen some green clunkers speeding down the street like they're running from something.",
                "They always pull up behind that obnoxious red building. Hard to miss.",
                "You should ask Jeremy. He might know who owns the cars.",
            },

            new List<string>()
            {
                "Bunch of damn green cars driving around late at night.",
                "They always cruise past here then turn down the alley next to that ugly red building.",
                "You know Jeremy, the Car Shop guy? He should know who they are.",
            },

            new List<string>()
            {
                "The only thing I've seen is some shiny green cars causing trouble after the sun goes down.",
                "Yeah, those punks always hide behind that red building over there.",
                "Go bug Jeremy, the car guy. He knows who's in those cars.",
            }
        };

        public static IEnumerator EventFrankInstructions(DialogueController controller)
        {
            controller.npc.Behaviour.ScheduleManager.DisableSchedule();
            float lerpWait = Mathf.Lerp(11f, 6f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);
            List<string> dialog = frankDialogOptions[UnityEngine.Random.Range(0, frankDialogOptions.Count)];
            controller.npc.PlayVO(EVOLineType.Angry);
            controller.handler.WorldspaceRend.ShowText(dialog[0], lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Annoyed);
            controller.handler.WorldspaceRend.ShowText(dialog[1], lerpWait + 2f);
            controller.npc.Movement.FacePoint(new Vector3(-32.68f, -2.54f, 168.73f), lerpTime: 1f);
            yield return Wait2;
            if (!registered) yield break;

            controller.npc.Movement.FacePoint(Player.GetClosestPlayer(controller.npc.CenterPoint, out _).CenterPointTransform.position, lerpTime: 1f);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText(dialog[2], lerpWait);
            yield return waitObj;
            if (!registered) yield break;
            controller.npc.Behaviour.ScheduleManager.EnableSchedule();

            MelonCoroutines.Start(GenerateCarQuestState());
            yield return null;
        }

        private static IEnumerator GenerateCarQuestState()
        {
            Log("Starting");
            GameObject newQuestObject = new GameObject();
            activeCarMeetupQuest = newQuestObject.AddComponent<Quest_CarMeetup>();
            newQuestObject.SetActive(true);
            activeCarMeetupQuest.enabled = true;
            activeCarMeetupQuest.SetupSelf();
            yield return null;
        }

        // Jeremy Dialogue opt for 1st quest entry
        public static IEnumerator GenJeremyOption(Action cb)
        {
            if (jeremy == null)
            {
#if MONO
                NPC npc = UnityEngine.Object.FindObjectOfType<ScheduleOne.NPCs.CharacterClasses.Jeremy>(true);
#else
                NPC npc = UnityEngine.Object.FindObjectOfType<Il2CppScheduleOne.NPCs.CharacterClasses.Jeremy>(true);
#endif
                if (npc != null)
                    jeremy = npc;
            }


            DialogueController controller = jeremy.DialogueHandler.gameObject.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new();
            string text = "Who is buying green cars from you?";
            choice.ChoiceText = $"{text} (Bribe <color=#FF3008>-$6000</color>)";
            choice.Enabled = true;
#if MONO
            choice.onChoosen.AddListener(() => { OnJeremyOptionSelected(controller, cb); });
#else
            void OnQuestChosenWrapped()
            {
                OnJeremyOptionSelected(controller, cb);
            }
            choice.onChoosen.AddListener((UnityEngine.Events.UnityAction)OnQuestChosenWrapped);
#endif
            jeremyDiagIndex = controller.AddDialogueChoice(choice);
            yield return null;
        }

        public static void OnJeremyOptionSelected(DialogueController controller, Action cb)
        {


            controller.handler.ContinueSubmitted();
            controller.npc.Behaviour.ScheduleManager.transform.GetChild(0)?.gameObject?.SetActive(false);
            bool inTimeWindow = NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2059 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 2359;
            bool playerHasPoliceAttention = Player.Local.CrimeData.CurrentPursuitLevel != PlayerCrimeData.EPursuitLevel.None; // because next stage fails immediate in minpass if there is pursuit level
            bool hasCash = NetworkSingleton<MoneyManager>.Instance.cashBalance >= 6000f;
            bool isInPos = (Vector3.Distance(jeremy.CenterPoint, jeremyStandPos) < 10f);
            if (hasCash && inTimeWindow && isInPos && !playerHasPoliceAttention)
            {
                NetworkSingleton<MoneyManager>.Instance.ChangeCashBalance(-6000f, true, false);
                MelonCoroutines.Start(DisposeJeremyChoice(controller));
                MelonCoroutines.Start(JeremyDialogue(controller, cb));
            }
            else
            {
                if (playerHasPoliceAttention)
                {
                    controller.handler.WorldspaceRend.ShowText("Get out of here! I don't want police at my house!", 8f);
                    controller.npc.PlayVO(EVOLineType.Angry);
                    controller.npc.Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 8f, 1);
                }
                else
                {
                    controller.handler.WorldspaceRend.ShowText("Talk to me after 21:00 at my house.", 8f);
                    controller.npc.PlayVO(EVOLineType.Annoyed);
                }
                controller.npc.Behaviour.ScheduleManager.transform.GetChild(0)?.gameObject?.SetActive(true);
            }
        }

        public static IEnumerator DisposeJeremyChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            if (jeremyDiagIndex != -1)
            {
                var oldChoices = controller.Choices;
                oldChoices.RemoveAt(jeremyDiagIndex);
                controller.Choices = oldChoices;
                Log("[END GAME QUEST]    Disposed Choice");

                jeremyDiagIndex = -1;
            }
            yield return null;
        }

        public static IEnumerator JeremyDialogue(DialogueController controller, Action cb)
        {
            jeremyDialogueActive = true;

            float lerpWait = Mathf.Lerp(10f, 5f, currentConfig.endGameQuestMonologueSpeed);
            WaitForSeconds waitObj = new WaitForSeconds(lerpWait);
            controller.npc.PlayVO(EVOLineType.Thanks);
            controller.handler.WorldspaceRend.ShowText("You got to be careful now. The Benzies Cartel is no joke.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Think);
            controller.handler.WorldspaceRend.ShowText("Thomas Benzie has been putting in alot of orders for those Green SUVs.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            controller.npc.PlayVO(EVOLineType.Acknowledge);
            controller.handler.WorldspaceRend.ShowText("I don't really ask around. He always pays more than the retail price.", lerpWait);
            yield return waitObj;
            if (!registered) yield break;

            jeremyDialogueActive = false;

            if (cb != null)
                cb();

            controller.npc.Behaviour.ScheduleManager.transform.GetChild(0)?.gameObject?.SetActive(true);
            yield return null;
        }

        public static IEnumerator QuestCarMeetupReward()
        {
            yield return Wait5;

            // Change influence in all unlocked regions
            if (InstanceFinder.IsServer)
            {
                foreach (EMapRegion unlmapReg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    if (unlmapReg == EMapRegion.Northtown) continue;
                    yield return Wait5; // play animation
                    if (!registered) yield break;

                    float current = NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(unlmapReg).Influence;
                    if (current > 0.0f)
                    {
                        float result = current * 0.90f;
                        float delta = current - result;
                        float rounded = Mathf.Round(delta * 100) / 100;
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(unlmapReg, -rounded);
                    }
                }
            }

            
            yield return null;
        }

        #endregion

        // Shared UI related code
        #region Quest UI prefabs
        public static RectTransform MakeIcon(Transform parent)
        {
            GameObject logo = new("BenziesLogoQuest");
            Image imgComp = logo.AddComponent<Image>();
            imgComp.sprite = benziesLogo;
            RectTransform rt = logo.AddComponent<RectTransform>();
            logo.AddComponent<CanvasRenderer>();
            logo.transform.SetParent(parent);
            return rt;
        }
        public static GameObject MakeUIPrefab(Transform parent)
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
            go.transform.parent = parent;
            return go;
        }
        public static GameObject MakePOI(Transform parent, GameObject UiPrefab)
        {
            GameObject poiPrefabObject = new GameObject($"CartelEnforcer_POI");
            poiPrefabObject.transform.SetParent(parent);
            poiPrefabObject.SetActive(false);
            POI poi = poiPrefabObject.AddComponent<POI>();
            poi.AutoUpdatePosition = true;
            poi.MainText = "Test";
            poi.DefaultMainText = "TestText";
            poi.UIPrefab = UiPrefab;
            return poiPrefabObject;
        }

        #endregion

    }

}
