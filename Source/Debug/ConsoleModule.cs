using MelonLoader;

using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelGathering;
using static CartelEnforcer.EndGameQuest;

#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.Dialogue;
#else
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.Dialogue;
#endif

namespace CartelEnforcer
{
    public static class ConsoleModule
    {
        [Flags]
        public enum CommandSupport
        {
            None = 0,
            List = 1 << 0,
            Start = 1 << 1,
        }

        public abstract class ConsoleCommandBase
        {
            public virtual string Name { get; }
            public virtual CommandSupport SupportedMethods { get; }
            public virtual void List() => Log("Not implemented");
            public virtual void Start() => Log("Not implemented");
        }

        // TODO:
        // Stolen Items
        // Event Cooldowns table
        // anything else?

        #region Startable Mod Events

        public class RobberyTarget : ConsoleCommandBase
        {
            public override string Name => "robbery";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputStartRob()));
                return;
            }
        }

        public class DriveByTarget : ConsoleCommandBase
        {
            public override string Name => "driveby";
            public override CommandSupport SupportedMethods => CommandSupport.Start;
            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputStartDriveBy()));
                return;
            }
        }

        public class MiniQuestTarget : ConsoleCommandBase
        {
            public override string Name => "miniquest";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputGiveMiniQuest()));
                return;
            }
        }

        public class InterceptDealTarget : ConsoleCommandBase
        {
            public override string Name => "intercept";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputInterceptContract()));
                return;
            }
        }

        public class GatheringTarget : ConsoleCommandBase
        {
            public override string Name => "gathering";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                hoursUntilNextGathering = 1;
                coros.Add(MelonCoroutines.Start(TryStartGathering()));
                return;
            }
        }

        public class SabotageTarget : ConsoleCommandBase
        {
            public override string Name => "sabotage";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputStartSabotage()));
                return;
            }
        }

        public class StealBackCustomerTarget : ConsoleCommandBase
        {
            public override string Name => "stealback";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputStealNearestCustomer()));
                return;
            }
        }

        #endregion

        #region Startable Quests

        public class AlliedSuppliesTarget : ConsoleCommandBase
        {
            public override string Name => "alliedsupplies";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputGenerateAlliedSupplyQuest()));
                return;
            }
        }

        public class AlliedIntroTarget : ConsoleCommandBase
        {
            public override string Name => "alliedintro";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(OnInputGenerateAlliedIntroQuest()));
                return;
            }
        }

        public class AlliedTrueBrothersTarget : ConsoleCommandBase
        {
            public override string Name => "truebrothers";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                coros.Add(MelonCoroutines.Start(SummonConversateGoon()));
                return;
            }
        }

        public class UnexpectedAlliancesTarget : ConsoleCommandBase
        {
            public override string Name => "unexpectedalliances";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                hasGeneratedDefeatEnforcerQuest = true;
                EndGameQuest.fixer = UnityEngine.Object.FindObjectOfType<Fixer>(true);
                coros.Add(MelonCoroutines.Start(GenerateQuestState()));
                return;
            }
        }

        public class InfiltrateManorTarget : ConsoleCommandBase
        {
            public override string Name => "infiltratemanor";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                hasGeneratedManorQuest = true;
                NetworkSingleton<TimeManager>.Instance.SetTimeAndSync(1815);
#if MONO
                NPC npc = UnityEngine.Object.FindObjectOfType<ScheduleOne.NPCs.CharacterClasses.Ray>(true);
#else
                NPC npc = UnityEngine.Object.FindObjectOfType<Il2CppScheduleOne.NPCs.CharacterClasses.Ray>(true);
#endif
                DialogueController controller = npc.DialogueHandler.gameObject.GetComponent<DialogueController>();
                int choiceIndex = -1;
                for (int i = 0; i < controller.Choices.Count; i++)
                {
                    if (controller.Choices[i].ChoiceText.Contains("What can you tell me about the owner of that manor"))
                    {
                        choiceIndex = i;
                        break;
                    }
                }
                var oldChoices = controller.Choices;
                oldChoices.RemoveAt(choiceIndex);
                controller.Choices = oldChoices;
                coros.Add(MelonCoroutines.Start(GenerateManorQuestState()));
                return;
            }
        }

        public class FourWheelsTarget : ConsoleCommandBase
        {
            public override string Name => "fourwheels";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                hasGeneratedCarQuest = true;
                NetworkSingleton<TimeManager>.Instance.SetTimeAndSync(1700);
                coros.Add(MelonCoroutines.Start(GenerateCarQuestState()));
                return;
            }
        }


        #endregion



    }
}