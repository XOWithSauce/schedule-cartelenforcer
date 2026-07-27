using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.PlayerScripts;
using static ScheduleOne.AvatarFramework.AvatarSettings;
using ScheduleOne.Combat;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Cartel;
using ScheduleOne.GameTime;
using ScheduleOne.Quests;
using ScheduleOne.DevUtilities;
using ScheduleOne.NPCs.Other;
using ScheduleOne.Levelling;
using ScheduleOne.Persistence;
using FishNet;
#else
using Il2CppScheduleOne.PlayerScripts;
using static Il2CppScheduleOne.AvatarFramework.AvatarSettings;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Persistence;
using Il2CppFishNet;
using Il2CppScheduleOne.NPCs.Other;
using Il2CppInterop.Runtime.Injection;
#endif

namespace CartelEnforcer
{
#if IL2CPP
    [RegisterTypeInIl2Cpp]
#endif
    public class Quest_DefeatEnforcer : ModQuestBase
    {
        protected readonly DefeatEnforcerHelper _helper;
#if MONO
        public Quest_DefeatEnforcer()
        {
            _helper = new DefeatEnforcerHelper(this);
        }
#else
        public Quest_DefeatEnforcer(IntPtr ptr) : base(ptr) 
        { 
            _helper = new DefeatEnforcerHelper(this);
        }

        public Quest_DefeatEnforcer() : base(ClassInjector.DerivedConstructorPointer<Quest_DefeatEnforcer>())
            => ClassInjector.DerivedConstructorBody(this);

#endif
        public bool bossHasSpawned = false;
        public bool bossCombatBegun = false;
        public bool rageStageStarted = false;
        public int fightElapsed = 0;
        public float questDifficultyScalar;

        // store the combat variables
        public bool hasSavedCombatVariables = false;
        public float GiveUpRange = 0f;
        public int GiveUpAfterSuccessfulHits = 0;
        public float DefaultSearchTime = 0f;

        public int mannyMessagesSent = 0;
        public bool mannyMessageRead = false;
        public bool isBossSpawning = false;

        public QuestEntry QuestEntry_Investigate;
        private UnityAction _investigateAction;

        public QuestEntry QuestEntry_WaitForContact;
        private UnityAction _contactAction;

        public QuestEntry QuestEntry_DefeatBoss;
        public UnityAction bossDiedAction = null;

        public readonly List<Vector3> encounterPositions = new()
        {
            new Vector3(156.38f, 6.70f, 123.95f),
            new Vector3(65.6153f, 3.0466f, -46.6993f),
            new Vector3(29.5507f, 0.6506f, -69.1599f),
            new Vector3(121.7651f, 1.4617f, -46.0731f),
        };
        public Vector3 bossSelectedPosition = Vector3.zero;

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
            Log("Quest_DefeatEnforcer: End method called.");
            try
            {
                if (hudUI != null)
                    hudUI.Complete();

                TimeManager instance = NetworkSingleton<TimeManager>.Instance;
                if (instance == null) return;

                var action = (Action)OnMinPass;

#if MONO
                instance.onHourPass = (Action)Delegate.Remove(instance.onHourPass, new Action(this.HourPass));
                instance.onMinutePass.Remove(action);
#else
                instance.onHourPass -= (Il2CppSystem.Action)this.HourPass;
                instance.onMinutePass.Remove((Il2CppSystem.Action)action);
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

        public void SetupSelf()
        {
            // calc difficulty scalar
            float allInfluence = 0f;
            foreach (CartelInfluence.RegionInfluenceData data in NetworkSingleton<Cartel>.Instance.Influence.regionInfluence)
            {
                allInfluence += data.Influence;
            }
            float allInfluenceNormalized = allInfluence / NetworkSingleton<Cartel>.Instance.Influence.regionInfluence.Count;
            questDifficultyScalar = 1f + allInfluenceNormalized;

            bossSelectedPosition = encounterPositions[UnityEngine.Random.Range(0, encounterPositions.Count)];

            _helper.InitializeQuest("Unexpected Alliances", xp: Mathf.RoundToInt(850f * questDifficultyScalar));

            _investigateAction = (UnityAction)OnInvestigateComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_Investigate,
                name: "Investigate",
                title: "• Intercept Cartel Dead Drops (0/2)\nOR\n• Defeat Cartel Gatherings (0/1)",
                new PoIConfig(false, false, false),
                _investigateAction);

            _contactAction = (UnityAction)OnContactComplete;
            _helper.InitializeQuestEntry(ref QuestEntry_WaitForContact,
                name: "Contact",
                title: "Wait for Manny to contact you",
                new PoIConfig(false, false, false),
                _contactAction);

            _helper.InitializeQuestEntry(ref QuestEntry_DefeatBoss,
                name: "Defeat",
                title: "Defeat the Cartel Brute",
                new PoIConfig(true, false, false, poiPosition: bossSelectedPosition));

            TimeManager instance = NetworkSingleton<TimeManager>.Instance;
            var action = (Action)OnMinPass;
#if MONO
            instance.onHourPass = (Action)Delegate.Combine(instance.onHourPass, new Action(this.HourPass));
            instance.onMinutePass.Add(action);
#else
            instance.onHourPass += (Il2CppSystem.Action)this.HourPass;
            instance.onMinutePass += (Il2CppSystem.Action)action;
#endif
            _helper.StartQuestFromEntry(QuestEntry_Investigate);

            if (QuestEntry_Investigate != null)
            {
                if (QuestEntry_Investigate.compassElement != null)
                    QuestEntry_Investigate.compassElement.Visible = false;
                else
                {
                    QuestEntry_Investigate.CreateCompassElement();
                    QuestEntry_Investigate.compassElement.Visible = false;
                }
            }
        }
        private void SendMannyMessage()
        {
            switch (mannyMessagesSent)
            {
                case 0:
                    fixer.SendTextMessage("One of the cartel brutes is hiding out in the woods. I sent you the location.");
                    break;

                case 1:
                    fixer.SendTextMessage("Hurry up! Go take down the Benzies thug. They will leave the area soon.");
                    break;

                case 2:
                    fixer.SendTextMessage("I marked the location on your map. Head there and take the down their brute.");
                    break;

                case 3:
                    fixer.SendTextMessage("Nevermind. I guess you were not the right person for the job.");
                    break;
            }
            mannyMessagesSent++;
            return;
        }
        public override void OnMinPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || defeatEnforcerCompleted || this.State != EQuestState.Active) return;
#if MONO
            base.OnMinPass();
#endif
            if (!InstanceFinder.IsServer)
            {
                return;
            }
            if (QuestEntry_Investigate != null && QuestEntry_Investigate.State == EQuestState.Active)
            {
                if (QuestEntry_Investigate != null && QuestEntry_Investigate.entryUI != null && this.hudUIExists)
                    QuestEntry_Investigate.SetEntryTitle($"• Intercept Cartel Dead Drops ({StageDeadDropsObserved}/2)\nOR\n• Defeat Cartel Gatherings ({StageGatheringsDefeated}/1)");

                if (StageDeadDropsObserved >= 2 || StageGatheringsDefeated >= 1)
                    QuestEntry_Investigate.Complete();

                return;
            }
            if (QuestEntry_WaitForContact != null && QuestEntry_WaitForContact.State == EQuestState.Active)
            {
                if (fixer != null && fixer.MSGConversation != null && fixer.MSGConversation.isOpen && mannyMessagesSent > 0)
                {
                    mannyMessageRead = true;
                    QuestEntry_WaitForContact.Complete();
                }

                if (mannyMessagesSent > 2)
                {
                    Log("Fail quest timeout");
                    Fail();
                }
                return;

            }
            else if (QuestEntry_DefeatBoss != null && QuestEntry_DefeatBoss.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 659 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 800)
                {
                    defeatEnforcerCompleted = true;

                    // player slept through the night boss disappears and quest fails
                    if (bossGoon != null)
                    {
                        bossGoon.Despawn();
                        ResetGoonBoss();
                    }
                    Fail();
                    return;
                }

                if (bossGoon == null && !isBossSpawning && !bossHasSpawned) 
                {
                    PlayerManager.GetClosestPlayer(QuestEntry_DefeatBoss.PoILocation.transform.position, out float dist);
                    if (dist < 40f)
                    {
                        isBossSpawning = true;
                        coros.Add(MelonCoroutines.Start(_helper.RunBossSpawn()));
                        return;
                    }
                }

                if (bossGoon != null && bossHasSpawned)
                {
                    QuestEntry_DefeatBoss.SetEntryTitle($"Defeat the Cartel Brute\nHP:{Mathf.RoundToInt(bossGoon.Health.Health)}");
                    Player p = PlayerManager.GetClosestPlayer(bossGoon.transform.position, out float dist);

                    if (dist < 16f && !bossCombatBegun)
                    {
                        bossCombatBegun = true;
                        bossGoon.Behaviour.CombatBehaviour.SetTarget(p.GetComponent<ICombatTargetable>().NetworkObject);
                        bossGoon.Behaviour.CombatBehaviour.Enable_Networked();
                    }

                    if (bossCombatBegun)
                    {
                        fightElapsed++;


                        if (!rageStageStarted)
                        {
                            if (bossGoon.Behaviour.activeBehaviour == null || bossGoon.Behaviour.activeBehaviour != bossGoon.Behaviour.CombatBehaviour)
                            {
                                if (bossGoon.Behaviour.CombatBehaviour.Target == null)
                                    bossGoon.Behaviour.CombatBehaviour.SetTarget(p.GetComponent<ICombatTargetable>().NetworkObject);

                                bossGoon.Behaviour.CombatBehaviour.Enable_Networked();
                            }

                            if (bossGoon.Behaviour.CombatBehaviour.currentWeapon == null || bossGoon.Behaviour.CombatBehaviour.IsCurrentWeaponMelee())
                            {
                                coros.Add(MelonCoroutines.Start(_helper.EquipBossWeapon()));
                            }

                            if (bossGoon.Health.Health < 230f || fightElapsed > 40)
                            {
                                rageStageStarted = true;
                                coros.Add(MelonCoroutines.Start(_helper.RunRageStage()));
                            }
                        }

                        // Check distance of boss to player & Check distance of Boss to the area & check elapsed time under 5min
                        if (dist > 70f || Vector3.Distance(bossGoon.CenterPoint, bossSelectedPosition) > 70f || fightElapsed > 300)
                        {
                            defeatEnforcerCompleted = true;

                            QuestEntry_DefeatBoss.SetState(EQuestState.Failed);
                            // Player Out of range or Boss is over 70 units from spawn pos or time has elapsed over 5min
                            bossGoon.Despawn();
                            ResetGoonBoss();
                            Fail();
                            return;
                        }
                    }
                }
            }

            return;
        }

        private void HourPass()
        {
            if (!registered || SaveManager.Instance.IsSaving || defeatEnforcerCompleted || this.State != EQuestState.Active) return;

            if (!InstanceFinder.IsServer)
            {
                return;
            }
            else if (QuestEntry_WaitForContact.State == EQuestState.Active)
            {
                if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 2159 || NetworkSingleton<TimeManager>.Instance.CurrentTime <= 200)
                {
                    if (mannyMessagesSent == 0)
                        QuestEntry_WaitForContact.SetEntryTitle($"Read Mannys text message.");

                    SendMannyMessage();
                }
                else if (NetworkSingleton<TimeManager>.Instance.CurrentTime >= 359 && NetworkSingleton<TimeManager>.Instance.CurrentTime <= 402 && !mannyMessageRead)
                {
                    defeatEnforcerCompleted = true;
                    Fail();
                }
            }
            return;
        }
        private void OnInvestigateComplete()
        {
            if (QuestEntry_Investigate != null && QuestEntry_Investigate.State == EQuestState.Failed) return;
            if (QuestEntry_WaitForContact == null) return;

            QuestEntry_WaitForContact.Begin();

            UpdateQuestMapLogo(QuestEntry_WaitForContact);
            if (QuestEntry_WaitForContact.PoI != null && QuestEntry_WaitForContact.PoI.UI != null)
                QuestEntry_WaitForContact.PoI.UI.gameObject.SetActive(false);
            if (QuestEntry_WaitForContact.compassElement != null)
                QuestEntry_WaitForContact.compassElement.Visible = false;
            if (_investigateAction != null)
            {
                QuestEntry_Investigate.onComplete.RemoveListener(_investigateAction);
                _investigateAction = null;
            }
            return;
        }
        private void OnContactComplete()
        {
            if (QuestEntry_WaitForContact != null && QuestEntry_WaitForContact.State == EQuestState.Failed) return;
            if (QuestEntry_DefeatBoss == null) return;

            QuestEntry_DefeatBoss.Begin();

            UpdateQuestMapLogo(QuestEntry_DefeatBoss);
            if (_contactAction != null)
            {
                QuestEntry_WaitForContact.onComplete.RemoveListener(_contactAction);
                _contactAction = null;
            }
            return;
        }
        public void OnBossDied()
        {
            defeatEnforcerCompleted = true;
            coros.Add(MelonCoroutines.Start(QuestReward(bossGoon)));
            Complete();

            if (bossDiedAction != null)
            {
                bossGoon.Health.onDieOrKnockedOut.RemoveListener(bossDiedAction);
                bossDiedAction = null;
            }
            return;
        }
        private void ResetGoonBoss()
        {
            if (bossGoon != null)
            {
                if (bossGoon.Behaviour.CombatBehaviour.Active)
                    bossGoon.Behaviour.CombatBehaviour.Disable_Networked(null);

                if (bossDiedAction != null)
                {
                    bossGoon.Health.onDieOrKnockedOut.RemoveListener(bossDiedAction);
                    bossDiedAction = null;
                }

                // Reset all non default stats that would carry on modified
                bossGoon.NPCData.Health.MaxHealth = 100f;
                bossGoon.Movement.MoveSpeedMultiplier = 1f;

                bossGoon.Behaviour.ScheduleManager.EnableSchedule();
                bossGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);

                bossGoon.Behaviour.CombatBehaviour.GiveUpRange = GiveUpRange;
                bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = GiveUpAfterSuccessfulHits;
                bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime = DefaultSearchTime;
            }
            return;
        }

    }

    // Quest coroutine wrapper
    public class DefeatEnforcerHelper : QuestHelperBase<Quest_DefeatEnforcer>
    {
        public DefeatEnforcerHelper(Quest_DefeatEnforcer quest) : base(quest) { }
        public IEnumerator RunBossSpawn()
        {
            Log("Boss Spawning");
            Vector3 spawnPos = _quest.QuestEntry_DefeatBoss.PoILocation.position;

            // if unspawned goon count is too low we insta despawn
            if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count == 0)
            {
                foreach (CartelGoon goon in NetworkSingleton<Cartel>.Instance.GoonPool.goons)
                {
                    if (NetworkSingleton<Cartel>.Instance.GoonPool.unspawnedGoons.Count >= 1) break;
                    if (!NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Contains(goon)) continue;

                    if (goon.IsGoonSpawned && (goon.Health.IsDead || goon.Health.IsKnockedOut))
                    {
                        goon.Despawn();
                    }
                }
            }

            CartelGoon _bossGoon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);
            _quest.QuestEntry_DefeatBoss.PoILocation.transform.SetParent(_bossGoon.transform);
            _quest.QuestEntry_DefeatBoss.PoILocation.transform.localPosition = Vector3.zero;

            _bossGoon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
            _bossGoon.Behaviour.ScheduleManager.DisableSchedule();
            bossGoon = _bossGoon;

            /*
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
             */

            _bossGoon.NPCData.Health.MaxHealth = Mathf.Round(Mathf.Lerp(500f, 1000f, _quest.questDifficultyScalar - 1f) / 10f) * 10f;
            _bossGoon.Health.Health = Mathf.Round(Mathf.Lerp(500f, 1000f, _quest.questDifficultyScalar - 1f) / 10f) * 10f;
            _bossGoon.Movement.MoveSpeedMultiplier = 0.4f;

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

            _bossGoon.Avatar.CurrentSettings.Height = 2f;
            _bossGoon.Avatar.CurrentSettings.Weight = 1f;
            _bossGoon.Avatar.SetAdditionalWeight(0.5f);
            _bossGoon.Avatar.ApplyBodySettings(_bossGoon.Avatar.CurrentSettings);

            _bossGoon.Avatar.CurrentSettings.AccessorySettings = accessorySettings;
            _bossGoon.Avatar.ApplyAccessorySettings(_bossGoon.Avatar.CurrentSettings);

            _bossGoon.Avatar.Impostor.SetAvatarSettings(_bossGoon.Avatar.CurrentSettings);

            if (_bossGoon.Avatar.onSettingsLoaded != null)
                _bossGoon.Avatar.onSettingsLoaded.Invoke();

            #endregion

            if (!_quest.hasSavedCombatVariables)
            {
                _quest.GiveUpRange = _bossGoon.Behaviour.CombatBehaviour.GiveUpRange;
                _quest.GiveUpAfterSuccessfulHits = _bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits;
                _quest.DefaultSearchTime = _bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime;
                _quest.hasSavedCombatVariables = true;
            }

            _bossGoon.Behaviour.CombatBehaviour.GiveUpRange = 70f;
            _bossGoon.Behaviour.CombatBehaviour.GiveUpAfterSuccessfulHits = 200;
            _bossGoon.Behaviour.CombatBehaviour.DefaultSearchTime = 300f;

            _quest.bossDiedAction = (UnityEngine.Events.UnityAction)_quest.OnBossDied;
            bossGoon.Health.onDieOrKnockedOut.AddListener(_quest.bossDiedAction);

            _quest.bossHasSpawned = true;
            yield break;
        }

        public IEnumerator EquipBossWeapon()
        {
            bossGoon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/PumpShotgun");
            yield return Wait05;
            if (!registered) yield break;

            if (bossGoon.Behaviour.CombatBehaviour.currentWeapon != null)
            {
                AvatarRangedWeapon wep = null;

#if MONO
                wep = bossGoon.Behaviour.CombatBehaviour.currentWeapon as AvatarRangedWeapon;
#else
                wep = bossGoon.Behaviour.CombatBehaviour.currentWeapon.TryCast<AvatarRangedWeapon>();
#endif
                if (wep != null)
                {

                    wep.MaxUseRange = Mathf.Round(25f * _quest.questDifficultyScalar);
                    wep.MinUseRange = 0.4f;
                    wep.HitChance_MaxRange = Mathf.Lerp(0.08f, 0.15f, _quest.questDifficultyScalar - 1f);
                    wep.HitChance_MinRange = Mathf.Lerp(0.65f, 0.85f, _quest.questDifficultyScalar - 1f);
                    wep.MaxFireRate = 2.6f - (_quest.questDifficultyScalar - 1f);
                    wep.CooldownDuration = 0.8f;
                    wep.Damage = 55f;
                    wep.ReloadTime = 2.3f;
                    wep.RaiseTime = 1.3f;
                    wep.ImpactForce = 28f;
                    wep.AimTime_Max = 1.2f;
                    wep.RepositionAfterHit = true;
                    wep.CanShootWhileMoving = true;
                }
            }

            if (bossGoon.Behaviour.CombatBehaviour._defaultWeapon == null && bossGoon.Behaviour.CombatBehaviour.currentWeapon != null)
                bossGoon.Behaviour.CombatBehaviour._defaultWeapon = bossGoon.Behaviour.CombatBehaviour.currentWeapon;
        }

        public IEnumerator RunRageStage()
        {
            DrinkItem drinkAct = bossGoon.transform.Find("Aux/Drink").GetComponent<DrinkItem>();
            Log("RunRage Stage");
            bool healthRegenerated = false;
            while (registered)
            {
                yield return Wait2;
                if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;

                if (!bossGoon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                    bossGoon.Behaviour.CombatBehaviour.Enable_Networked();

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
                            bossGoon.Health.Health += Mathf.RoundToInt(Mathf.Lerp(35f, 65f, _quest.questDifficultyScalar - 1f));
                        }
                        drinkAct.End();
                        healthRegenerated = true;
                    }
                    else
                    {
                        Log("DrinkAction is null");
                    }
                    Player p = PlayerManager.GetClosestPlayer(bossGoon.transform.position, out float dist);
                    yield return Wait01;
                    if (!registered) yield break;

                    bossGoon.Movement.ResumeMovement();

                    if (bossGoon.Behaviour.CombatBehaviour.Target == null)
                        bossGoon.Behaviour.CombatBehaviour.SetTarget(p.GetComponent<ICombatTargetable>().NetworkObject);
                    if (!bossGoon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                        bossGoon.Behaviour.CombatBehaviour.Enable_Networked();
                    yield return Wait01;
                    if (!registered) yield break;

                    coros.Add(MelonCoroutines.Start(EquipBossWeapon()));
                }
                yield return Wait05;
                if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;

                if (UnityEngine.Random.Range(0f, 1f) > 0.95f)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        yield return Wait05;
                        if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;
                        bossGoon.Movement.MoveSpeedMultiplier = Mathf.Lerp(bossGoon.Movement.MoveSpeedMultiplier, 2.6f, 0.33f);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        yield return Wait05;
                        if (!registered || bossGoon.Health.IsDead || bossGoon.Health.IsKnockedOut) break;
                        bossGoon.Movement.MoveSpeedMultiplier = Mathf.Lerp(bossGoon.Movement.MoveSpeedMultiplier, 0.4f, 0.33f);
                    }
                    bossGoon.Movement.MoveSpeedMultiplier = 0.4f;
                }
            }

            yield break;
        }

    }
}
