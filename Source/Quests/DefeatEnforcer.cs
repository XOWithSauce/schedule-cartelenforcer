

using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.Police;
using ScheduleOne.PlayerScripts;
using static ScheduleOne.AvatarFramework.AvatarSettings;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Interaction;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.NPCs.Other;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
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
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
using Il2CppFishNet.Managing;
using Il2CppScheduleOne.NPCs.Other;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
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
        private bool rageStageStarted = false;
        private int fightElapsed = 0;
        private float questDifficultyScalar;

        private NPC contactNPC = null;

        // store the combat variables
        private float GiveUpRange = 0f;
        private int GiveUpAfterSuccessfulHits = 0;
        private float DefaultSearchTime = 0f;


        #region Base Complete, Fail, End overrides
        // Because one of these throws il2cpp version ViolationAccessException or NullReferenceException and doesnt show stack / doesnt show stack outside of the below functions
        // simplified from source and removed networking so its client only for now
        public override void Complete(bool network = true)
        {
            Log("Quest_DefeatEnforcer: Complete method called.");
            try
            {
                if (this.State == EQuestState.Completed)
                {
                    return;
                }
                if (InstanceFinder.IsServer && !Singleton<LoadManager>.Instance.IsLoading)
                    NetworkSingleton<LevelManager>.Instance.AddXP(this.CompletionXP);

                this.SetQuestState(EQuestState.Completed, false);

                NetworkSingleton<QuestManager>.Instance.PlayCompleteQuestSound();
                this.End();

                Log("Quest_DefeatEnforcer: Base Complete method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_DefeatEnforcer: An error occurred in base.Complete: {ex.Message}");
                throw;
            }
        }

        public override void Fail(bool network = true)
        {
            Log("Quest_DefeatEnforcer: Fail method called.");
            try
            {
                this.SetQuestState(EQuestState.Failed, false);
                this.End();
                Log("Quest_DefeatEnforcer: Base Fail method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_DefeatEnforcer: An error occurred in base.Fail: {ex.Message}");
                throw;
            }
        }

        public override void End()
        {
            MelonLogger.Msg("Quest_DefeatEnforcer: End method called.");
            try
            {
                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;
#if MONO
                instance.onHourPass = (Action)Delegate.Remove(instance.onHourPass, new Action(this.HourPass));
                instance.onMinutePass.Remove((Action)this.MinPass);
#else
                instance.onHourPass -= (Il2CppSystem.Action)this.HourPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)this.MinPass);
#endif
                Log("Quest_DefeatEnforcer: Base End method finished successfully.");
            }
            catch (Exception ex)
            {
                Log($"Quest_DefeatEnforcer: An error occurred in base.End: {ex.Message}");
                throw;
            }

            this.gameObject.SetActive(false);
        }
        #endregion

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
                foreach (QuestEntry entry in this.Entries)
                {
                    if (entry == null)
                    {
                        MelonLogger.Warning("Quest Entry got GC'd");
                    }
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
            // calc difficulty scalar
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
            questDifficultyScalar = 1f + allInfluenceNormalized;

            Log("QuestInit");
            this.name = "Quest_DefeatEnforcer";
            Expires = false;
            title = "Unexpected Alliances";
            CompletionXP = Mathf.RoundToInt(850f * questDifficultyScalar);
            Description = "Investigate and intercept Cartel Activity";
            TrackOnBegin = true;
            autoInitialize = false;
            AutoCompleteOnAllEntriesComplete = false;
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

            // UI related code and the benzies logo
            RectTransform rt = MakeIcon(this.transform);
            rtIcon = rt;
            this.IconPrefab = rt;
            UiPrefab = MakeUIPrefab(this.transform);
            PoIPrefab = MakePOI(this.transform, UiPrefab);

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

            investigate.SetEntryTitle("• Intercept Cartel Dead Drops (0/2)\n• Defeat Cartel Gatherings (0/1)");
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
                if (investigate != null && investigate.State == EQuestState.Failed) return;
                if (contact == null) return;

                contact.Begin();
                if (contact.PoI != null && contact.PoI.gameObject != null)
                    contact.PoI.gameObject.SetActive(false);
                if (contact.compassElement != null)
                    contact.compassElement.Visible = false;
                investigate.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);
            }
            investigate.onComplete.AddListener((UnityEngine.Events.UnityAction)OnInvestigateComplete);

            contact.SetEntryTitle("• Wait for Manny to contact you");
            contact.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            contact.PoILocation.name = "ContactEntry_POI";
            contact.PoILocation.transform.SetParent(contact.transform);
            contact.PoILocation.transform.position = new Vector3(128.27f, 1.56f, 88.96f);
            contact.AutoUpdatePoILocation = true;
            contact.SetState(EQuestState.Inactive, false);
            contact.ParentQuest = this;
            contact.CompleteParentQuest = false;
            void OnContactComplete()
            {
                if (contact != null && contact.State == EQuestState.Failed) return;
                if (defeat == null) return;

                defeat.Begin();
                if (defeat.PoI != null)
                    defeat.PoI.gameObject.SetActive(false);
                if (defeat.compassElement != null)
                    defeat.compassElement.Visible = false;
                contact.onComplete.RemoveListener((UnityEngine.Events.UnityAction)OnContactComplete);
            }
            contact.onComplete.AddListener((UnityEngine.Events.UnityAction)OnContactComplete);

            defeat.SetEntryTitle("• Defeat the Cartel Brute");
            defeat.PoILocation = UnityEngine.Object.Instantiate(PoIPrefab).transform;
            defeat.PoILocation.name = "DefeatEntry_POI";
            defeat.PoILocation.transform.SetParent(defeat.transform);
            defeat.PoILocation.transform.position = new Vector3(156.38f, 6.70f, 123.95f);
            defeat.AutoUpdatePoILocation = true;
            defeat.SetState(EQuestState.Inactive, false);
            defeat.ParentQuest = this;
            defeat.CompleteParentQuest = false;

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
#endif
            instance.onMinutePass.Add(new Action(this.MinPass));
            StartQuestDetail();
        }

        private void StartQuestDetail() // todo fixme this dumb
        {
            if (this.IconPrefab == null)
                this.IconPrefab = this.transform.Find("BenziesLogoQuest").GetComponent<RectTransform>();
            SetupHudUI();

            if (hudUI != null)
            {
                if (hudUI.MainLabel != null)
                    this.hudUI.MainLabel.text = "Unexpected Alliances";
                this.hudUI.gameObject.SetActive(true);
            }

            if (QuestEntry_Investigate != null)
            {
                QuestEntry_Investigate.CreateCompassElement();
                if (QuestEntry_Investigate.compassElement != null)
                    QuestEntry_Investigate.compassElement.Visible = false;

                if (QuestEntry_Investigate.PoI != null)
                    QuestEntry_Investigate.PoI.gameObject.SetActive(false);
            }

            SetIsTracked(true);
            SetQuestState(EQuestState.Active);

            return;
        }
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
            myNpc.ID = $"CartelEnforcer_Contact_NPC";
            myNpc.FirstName = "Unknown";
            myNpc.LastName = "";
            myNpc.transform.parent = NPCManager.Instance.NPCContainer;
            NPCManager.NPCRegistry.Add(myNpc);
            yield return Wait05;
            if (!registered) yield break;

            netManager.ServerManager.Spawn(copNet);
            yield return Wait05;
            if (!registered) yield break;

            copNet.gameObject.SetActive(true);
            myNpc.Health.Invincible = true;
            myNpc.Behaviour.CombatBehaviour.Disable_Networked(null);
            myNpc.Behaviour.CombatBehaviour.enabled = false;

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
            Log("Set offc stats");
            offc.Movement.Agent.enabled = false;
            Vector3 spawnPos = QuestEntry_WaitForContact.PoILocation.position;
            offc.Movement.Warp(new Vector3(128.27f, 1.56f, 88.96f));
            offc.Behaviour.ScheduleManager.DisableSchedule();
            offc.Awareness.VisionCone.VisionEnabled = false;
            offc.ChatterEnabled = false;
            offc.Movement.Agent.enabled = true;
            yield return Wait2;
            if (!registered) yield break;

            Log("Reset Pos");

            // because for some reason the cop just tps back to station and sets invis in building
            offc.Movement.Agent.enabled = true;
            offc.Avatar.gameObject.SetActive(true);
            offc.Movement.Warp(new Vector3(128.27f, 1.56f, 88.96f));
            offc.Movement.WarpToNavMesh();
            yield return Wait01;
            if (!registered) yield break;

            offc.Movement.Stop();
            offc.Movement.Agent.enabled = false;
            offc.Movement.enabled = false;
            offc.transform.rotation = Quaternion.Euler(0f, 160f, 0f);

            void OnDialogComplete()
            {
                QuestEntry_WaitForContact.Complete();
                MelonCoroutines.Start(RunContactDespawn(myNpc));
                MelonCoroutines.Start(RunBossSpawn());
            }
            Action callback = new Action(OnDialogComplete);
            MelonCoroutines.Start(GenContactDialog(myNpc, callback));

            Log("Send Message");
            fixer.MSGConversation.SendMessage(new Message("I set up a meeting for you. He is waiting near the church until 4am.", Message.ESenderType.Other, false, -1), true, true);

            Log("Set Waypoint");
            QuestEntry_WaitForContact.CreateCompassElement();

            contactNPC = myNpc;

            yield return null;
        }

        public IEnumerator RunContactDespawn(NPC npc = null, bool immediate = false)
        {
            if (!immediate)
                yield return Wait30;
            if (!registered) yield break;

            if (npc == null && contactNPC != null)
                npc = contactNPC;

            if (npc != null)
                NPCManager.NPCRegistry.Remove(npc);

            if (npc != null)
                if (npc.gameObject != null)
                    GameObject.Destroy(npc.gameObject);
            yield return null;
        }

        private IEnumerator RunBossSpawn()
        {
            Log("Boss Spawning");
            Vector3 spawnPos = QuestEntry_DefeatBoss.PoILocation.position;
            CartelGoon _bossGoon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);
            _bossGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            _bossGoon.Behaviour.ScheduleManager.DisableSchedule();
            bossGoon = _bossGoon;
            if (_bossGoon.Health.IsDead || _bossGoon.Health.IsKnockedOut)
                _bossGoon.Health.Revive();
            yield return Wait05;
            if (!registered) yield break;
            // because for some reason the avatar goes off and same with nav
            if (_bossGoon.isInBuilding)
            {
                Log("Exit Building!!");
                _bossGoon.ExitBuilding();
            }
            if (!_bossGoon.Avatar.gameObject.activeSelf) _bossGoon.Avatar.gameObject.SetActive(true);
            if (_bossGoon.Movement.Agent != null && _bossGoon.Movement.Agent.enabled == false) _bossGoon.Movement.Agent.enabled = true;


            yield return Wait05;
            if (!registered) yield break;

            #region Movement and Health
            _bossGoon.Health.MaxHealth = Mathf.Round(Mathf.Lerp(500f, 1000f, questDifficultyScalar - 1f) / 10f) * 10f;
            _bossGoon.Health.Health = Mathf.Round(Mathf.Lerp(500f, 1000f, questDifficultyScalar - 1f) / 10f) * 10f;
            _bossGoon.Movement.MoveSpeedMultiplier = 0.4f;
            _bossGoon.SetScale(1.35f);
            #endregion
            Log("Setup Boss Move & Health");
            yield return Wait05;
            if (!registered) yield break;

            coros.Add(MelonCoroutines.Start(EquipBossWeapon()));

            #region Avatar

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
            if (_bossGoon.isInBuilding)
            {
                _bossGoon.ExitBuilding();
            }
            _bossGoon.Movement.Warp(spawnPos);
            if (_bossGoon.Health.IsKnockedOut || _bossGoon.Health.IsDead)
            {
                _bossGoon.Health.Revive();
            }
            yield return Wait05;
            if (!registered) yield break;

            if (!_bossGoon.Avatar.gameObject.activeSelf) _bossGoon.Avatar.gameObject.SetActive(true);
            if (_bossGoon.Movement.Agent != null && _bossGoon.Movement.Agent.enabled == false) _bossGoon.Movement.Agent.enabled = true;

            if (GiveUpRange == 0f)
            {
                GiveUpRange = _bossGoon.Behaviour.CombatBehaviour.GiveUpRange;
                GiveUpAfterSuccessfulHits = _bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                DefaultSearchTime = _bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime;
            }

            _bossGoon.Behaviour.CombatBehaviour.GiveUpRange = 70f;
            _bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 200;
            _bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime = 300f;

            void OnBossDied()
            {
                completed = true;
                MelonCoroutines.Start(QuestReward(bossGoon));
                this.Complete(true);

                bossGoon.Health.onDieOrKnockedOut.RemoveListener((UnityEngine.Events.UnityAction)OnBossDied);
            }
            bossGoon.Health.onDieOrKnockedOut.AddListener((UnityEngine.Events.UnityAction)OnBossDied);

            yield return null;
        }

        private IEnumerator EquipBossWeapon()
        {
            #region Cracked Shotgun
            bossGoon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/PumpShotgun");
            yield return Wait05;
            if (!registered) yield break;

            if (bossGoon.Behaviour.CombatBehaviour.currentWeapon != null)
            {

#if MONO
                if (bossGoon.Behaviour.CombatBehaviour.currentWeapon is AvatarRangedWeapon wep)
                {

                    wep.MaxUseRange = Mathf.Round(25f * questDifficultyScalar);
                    wep.MinUseRange = 0.4f;
                    wep.HitChance_MaxRange = Mathf.Lerp(0.08f, 0.15f, questDifficultyScalar - 1f);
                    wep.HitChance_MinRange = Mathf.Lerp(0.65f, 0.85f, questDifficultyScalar - 1f);
                    wep.MaxFireRate = 2.6f - (questDifficultyScalar - 1f);
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
                    wep = bossGoon.Behaviour.CombatBehaviour.currentWeapon.Cast<AvatarRangedWeapon>();
                } 
                catch (InvalidCastException ex)
                {
                    MelonLogger.Warning("Failed to Cast Gun Weapon Instance: " + ex);
                }

                if (wep != null)
                {
                    wep.MaxUseRange = Mathf.Round(25f * questDifficultyScalar);
                    wep.MinUseRange = 0.4f;
                    wep.HitChance_MaxRange = Mathf.Lerp(0.08f, 0.15f, questDifficultyScalar - 1f);
                    wep.HitChance_MinRange = Mathf.Lerp(0.65f, 0.85f, questDifficultyScalar - 1f);
                    wep.MaxFireRate = 2.6f - (questDifficultyScalar-1f);
                    wep.CooldownDuration = 0.8f;
                    wep.Damage = 55f;
                    wep.ReloadTime = 2.3f;
                    wep.RaiseTime = 1.3f;
                    wep.ImpactForce = 28f;
                    wep.AimTime_Max = 1.2f;
                    wep.RepositionAfterHit = true;
                }
#endif
            }

            if (bossGoon.Behaviour.CombatBehaviour.DefaultWeapon == null && bossGoon.Behaviour.CombatBehaviour.currentWeapon != null)
                bossGoon.Behaviour.CombatBehaviour.DefaultWeapon = bossGoon.Behaviour.CombatBehaviour.currentWeapon;
            #endregion
            Log("Setup Boss Weapon");
        }

        private IEnumerator RunRageStage()
        {
            DrinkItem drinkAct = bossGoon.transform.Find("Aux/Drink").GetComponent<DrinkItem>();
            Log("RunRage Stage");
            bool healthRegenerated = false;
            while (registered)
            {
                yield return Wait2;
                if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;

                if (!bossGoon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                    bossGoon.Behaviour.CombatBehaviour.Enable_Networked(null);

                if (bossGoon.Behaviour.CombatBehaviour.currentWeapon == null)
                {
                    coros.Add(MelonCoroutines.Start(EquipBossWeapon()));
                }

                if (UnityEngine.Random.Range(0f, 1f) > 0.95f && !healthRegenerated)
                {
                    if (drinkAct != null)
                    {
                        bossGoon.Movement.PauseMovement();
                        drinkAct.Begin();
                        for (int i = 0; i < 3; i++)
                        {
                            yield return Wait2;
                            if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) yield break;
                            bossGoon.Health.Health += Mathf.RoundToInt(Mathf.Lerp(35f, 65f, questDifficultyScalar - 1f));
                        }
                        drinkAct.End();
                        healthRegenerated = true;
                    }
                    else
                    {
                        Log("DrinkAction is null");
                    }
                    Player p = Player.GetClosestPlayer(bossGoon.transform.position, out float dist);
                    yield return Wait01;
                    if (!registered) yield break;

                    bossGoon.Movement.ResumeMovement();

                    if (bossGoon.Behaviour.CombatBehaviour.Target == null)
                        bossGoon.Behaviour.CombatBehaviour.SetTarget(p.GetComponent<ICombatTargetable>().NetworkObject);
                    if (!bossGoon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                        bossGoon.Behaviour.CombatBehaviour.Enable_Networked(null);
                    yield return Wait01;
                    if (!registered) yield break;

                    coros.Add(MelonCoroutines.Start(EquipBossWeapon()));
                }
                yield return Wait05;
                if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;

                if (UnityEngine.Random.Range(0f, 1f) > 0.95f)
                {
                    for (int i = 0; i < 6; i++)
                    {

                        yield return Wait05;
                        if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;

                        bossGoon.Movement.MoveSpeedMultiplier = Mathf.Lerp(bossGoon.Movement.MoveSpeedMultiplier, 1f, 0.1f);
                    }
                    bossGoon.Movement.MoveSpeedMultiplier = 0.4f;
                }
            }

            yield return null;
        }

        public override void MinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || completed || this.State != EQuestState.Active) return; 
#if MONO
            base.MinPass(); // Is this necessary in mono or does cause recursion??

#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_Investigate != null && QuestEntry_Investigate.State == EQuestState.Active && !completed)
            {
                if (QuestEntry_Investigate.compassElement != null && QuestEntry_Investigate.compassElement.Visible)
                    QuestEntry_Investigate.compassElement.Visible = false;

                if (QuestEntry_Investigate.PoI != null && QuestEntry_Investigate.PoI.gameObject.activeSelf)
                    QuestEntry_Investigate.PoI.gameObject.SetActive(false);

                if (QuestEntry_Investigate != null && QuestEntry_Investigate.entryUI != null && this.hudUIExists)
                    QuestEntry_Investigate.SetEntryTitle($"• Intercept Cartel Dead Drops ({StageDeadDropsObserved}/2)\n• Defeat Cartel Gatherings ({StageGatheringsDefeated}/1)");

                if (StageDeadDropsObserved >= 2 && StageGatheringsDefeated >= 1)
                {
                    Log("Completed first stage");
                    QuestEntry_Investigate.Complete();
                    return;
                }
            }
            else if (QuestEntry_DefeatBoss != null && QuestEntry_DefeatBoss.State == EQuestState.Active && !completed)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 659 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 800)
                {
                    completed = true;

                    QuestEntry_DefeatBoss.SetState(EQuestState.Failed);
                    // player slept through the night boss disappears and quest fails
                    bossGoon.Despawn();
                    ResetGoonBoss();
                    this.Fail();
                    return;
                }


                if (bossGoon != null && !completed)
                {
                    Log("MinPass QE Defeat Boss");
                    QuestEntry_DefeatBoss.SetEntryTitle($"• Defeat the Cartel Brute \nHP:{Mathf.RoundToInt(bossGoon.Health.Health)}");
                    Player p = Player.GetClosestPlayer(bossGoon.transform.position, out float dist);

                    if (dist < 16f && !bossCombatBegun)
                    {
                        bossCombatBegun = true;
                        bossGoon.Behaviour.CombatBehaviour.SetTarget(p.GetComponent<ICombatTargetable>().NetworkObject);
                        bossGoon.Behaviour.CombatBehaviour.Enable_Networked(null);
                    }

                    if (bossCombatBegun)
                    {
                        fightElapsed++;


                        if (!rageStageStarted)
                        {
                            if (!bossGoon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                                bossGoon.Behaviour.CombatBehaviour.Enable_Networked(null);

                            if (bossGoon.Behaviour.CombatBehaviour.currentWeapon == null || bossGoon.Behaviour.CombatBehaviour.IsCurrentWeaponMelee())
                            {
                                coros.Add(MelonCoroutines.Start(EquipBossWeapon()));
                            }

                            if (bossGoon.Health.Health < 230f || fightElapsed > 40)
                            {
                                rageStageStarted = true;
                                coros.Add(MelonCoroutines.Start(RunRageStage()));
                            }
                        }
                        if (rageStageStarted)
                        {

                        }

                        // Check distance of boss to player & Check distance of Boss to the area & check elapsed time under 5min
                        if (dist > 70f || Vector3.Distance(bossGoon.CenterPoint, QuestEntry_DefeatBoss.PoILocation.position) > 70f || fightElapsed > 300)
                        {
                            completed = true;

                            QuestEntry_DefeatBoss.SetState(EQuestState.Failed);
                            // Player Out of range or Boss is over 70 units from spawn pos or time has elapsed over 5min
                            bossGoon.Despawn();
                            ResetGoonBoss();
                            this.Fail();
                            return;
                        }

                    }

                }
            }

        }

        private void HourPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || completed || this.State != EQuestState.Active) return;

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
                    if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 0 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 100)
                    {
                        contactMade = true;

                        if (QuestEntry_WaitForContact != null)
                        {
                            try
                            {
                                QuestEntry_WaitForContact.compassElement.Visible = true;
                                QuestEntry_WaitForContact.PoI.gameObject.SetActive(true);
                                QuestEntry_WaitForContact.SetEntryTitle($"• Read Manny's text message.");
                            }
                            catch (NullReferenceException ex)
                            {
                                MelonLogger.Warning("Quest Entry encountered an error: " + ex);
                            }
                        }
                        else
                            MelonLogger.Warning("Quest Entry got GC'd");
                        coros.Add(MelonCoroutines.Start(ContactSpawn()));
                    }
                }
                else if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 359 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 402 && !inContactDialogue && contactMade)
                {
                    completed = true;

                    QuestEntry_WaitForContact.SetState(EQuestState.Failed);
                    QuestEntry_DefeatBoss.SetState(EQuestState.Failed);

                    // Quest time out player did not attend meeting, not currently in dialogue with contact -> despawn contact fail quest, cleanup
                    MelonCoroutines.Start(RunContactDespawn(contactNPC, true));
                    this.Fail();
                    return;
                }
                else if (contactMade)
                {
                    Log("Wait For Player To Arrive To NPC and initiate dialogue");
                }

            }

        }

        private void ResetGoonBoss()
        {
            if (bossGoon != null)
            {
                if (bossGoon.Behaviour.CombatBehaviour.Active)
                    bossGoon.Behaviour.CombatBehaviour.Disable_Networked(null);
                // Reset all non default stats that would carry on modified
                bossGoon.Health.MaxHealth = 100f;
                bossGoon.Movement.MoveSpeedMultiplier = 0.8f;
                bossGoon.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                bossGoon.Behaviour.ScheduleManager.EnableSchedule();
                bossGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);

                bossGoon.Behaviour.CombatBehaviour.GiveUpRange = GiveUpRange;
                bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = GiveUpAfterSuccessfulHits;
                bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime = DefaultSearchTime;
            }

            return;
        }

        public QuestEntry QuestEntry_Investigate;

        public QuestEntry QuestEntry_WaitForContact;

        public QuestEntry QuestEntry_DefeatBoss;

    }
}
