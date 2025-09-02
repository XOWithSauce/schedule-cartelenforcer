

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
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
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
using Il2CppScheduleOne.NPCs.Other;
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
using Il2CppScheduleOne.Persistence.Datas;
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
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppInterop.Runtime.Injection;
using Il2Cpp;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI;
#endif

namespace CartelEnforcer
{
    public static class EndGameQuest
    {
        public static bool completed = false;
        public static int StageDeadDropsObserved = 0;
        public static NPC fixer;
        public static CartelGoon bossGoon;
        public static Quest_DefeatEnforcer activeQuest = null;

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
            controller.AddDialogueChoice(choice);
            yield return null;
        }

        public static IEnumerator DisposeChoice(DialogueController controller)
        {
            yield return Wait05;
            if (!registered) yield break;

            var oldChoices = controller.Choices;
            oldChoices.RemoveAt(oldChoices.Count - 1);
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
        private IEnumerator StartQuestDetail()
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

}
