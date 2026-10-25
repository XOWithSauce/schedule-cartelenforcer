using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelGathering;
using static CartelEnforcer.EndGameQuest;
using static CartelEnforcer.ManorLaunder;


#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.Dialogue;
#else
using ECartelStatus = Il2Cpp.ECartelStatus;
using Il2CppScheduleOne.Cartel;
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

        #region Startable Mod Events

        public class RobberyTarget : ConsoleCommandBase
        {
            public override string Name => "robbery";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                    ModCoroutines.Start(OnInputStartRob());
                return;
            }
        }

        public class DriveByTarget : ConsoleCommandBase
        {
            public override string Name => "driveby";
            public override CommandSupport SupportedMethods => CommandSupport.Start;
            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                    ModCoroutines.Start(OnInputStartDriveBy());
                return;
            }
        }

        public class MiniQuestTarget : ConsoleCommandBase
        {
            public override string Name => "miniquest";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                    ModCoroutines.Start(OnInputGiveMiniQuest());
                return;
            }
        }

        public class InterceptDealTarget : ConsoleCommandBase
        {
            public override string Name => "intercept";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                    ModCoroutines.Start(OnInputInterceptContract());
                return;
            }
        }

        public class GatheringTarget : ConsoleCommandBase
        {
            public override string Name => "gathering";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                {
                    hoursUntilNextGathering = 1;
                    ModCoroutines.Start(TryStartGathering());
                }
                    
                return;
            }
        }

        public class SabotageTarget : ConsoleCommandBase
        {
            public override string Name => "sabotage";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                    ModCoroutines.Start(OnInputStartSabotage());
                return;
            }
        }

        public class StealBackCustomerTarget : ConsoleCommandBase
        {
            public override string Name => "stealback";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                {
                    LogRelease("Starting steal back customer");
                    ModCoroutines.Start(OnInputStealNearestCustomer());
                }
                return;
            }
        }

        public class ManorLaunderProcessTarget : ConsoleCommandBase
        {
            public override string Name => "manorlaunder";
            public override CommandSupport SupportedMethods => CommandSupport.Start;
            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                    LogRelease("Change Cartel Status to TRUCED before running this command!");
                else
                {
                    LogRelease("Starting manor laundering");
                    LogRelease("Park a VEEPER vehicle with cash/gold in storage to the manor gate BEFORE running this command!");
                    ModCoroutines.Start(GenerateLaunderProcess());
                }
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
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                    LogRelease("Change Cartel Status to TRUCED before running this command!");
                else
                {
                    ModCoroutines.Start(OnInputGenerateAlliedSupplyQuest());
                }
                return;
            }
        }

        public class AlliedIntroTarget : ConsoleCommandBase
        {
            public override string Name => "alliedintro";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                    LogRelease("Change Cartel Status to TRUCED before running this command!");
                else
                {
                    ModCoroutines.Start(OnInputGenerateAlliedIntroQuest());
                }
                return;
            }
        }

        public class AlliedTrueBrothersTarget : ConsoleCommandBase
        {
            public override string Name => "truebrothers";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                    LogRelease("Change Cartel Status to TRUCED before running this command!");
                else
                {
                    LogRelease("Starting true brothers quest");
                    ModCoroutines.Start(SummonConversateGoon());
                }
                return;
            }
        }
        public class AlliedLogisticsTarget : ConsoleCommandBase
        {
            public override string Name => "cartellogistics";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                    LogRelease("Change Cartel Status to TRUCED before running this command!");
                else
                {
                    LogRelease("Starting cartel logistics quest");
                    ModCoroutines.Start(SetupCartelLogisticsQuest());
                }
                return;
            }
        }

        public class UnexpectedAlliancesTarget : ConsoleCommandBase
        {
            public override string Name => "unexpectedalliances";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                else
                {
                    hasGeneratedDefeatEnforcerQuest = true;
                    EndGameQuest.fixer = UnityEngine.Object.FindObjectOfType<Fixer>(true);
                    ModCoroutines.Start(GenerateQuestState());
                }
                return;
            }
        }

        public class InfiltrateManorTarget : ConsoleCommandBase
        {
            public override string Name => "infiltratemanor";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                {
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                    return;
                }
                hasGeneratedManorQuest = true;
                NetworkSingleton<TimeManager>.Instance.SetTime(1816);
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
                if (choiceIndex != -1)
                {
                    var oldChoices = controller.Choices;
                    oldChoices.RemoveAt(choiceIndex);
                    controller.Choices = oldChoices;
                }
                ModCoroutines.Start(GenerateManorQuestState());
                return;
            }
        }

        public class FourWheelsTarget : ConsoleCommandBase
        {
            public override string Name => "fourwheels";
            public override CommandSupport SupportedMethods => CommandSupport.Start;

            public override void Start()
            {
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                {
                    LogRelease("Change Cartel Status to HOSTILE before running this command!");
                }
                else
                {
                    hasGeneratedCarQuest = true;
                    NetworkSingleton<TimeManager>.Instance.SetTime(2050);
                    ModCoroutines.Start(GenerateCarQuestState());
                }
                return;
            }
        }

        #endregion

    }
}