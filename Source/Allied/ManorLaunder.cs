using System.Collections;
using UnityEngine;

using static CartelEnforcer.AlliedExtension;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.CartelEnforcer;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Messaging;
using ScheduleOne.Money;
using ScheduleOne.Property;
using ScheduleOne.Storage;
using ScheduleOne.Vehicles;
using ScheduleOne.GameTime;
using ScheduleOne.NPCs.CharacterClasses;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs.CharacterClasses;
#endif

namespace CartelEnforcer
{
    public static class ManorLaunder
    {

        public readonly static string MsgOption1Text = "Can you clean some cash for me?";

        public readonly static string MsgOption1Resp = "Sure. Go buy a Veeper and fill it with cash and gold bars. I won't pay for anything else. Drive the Veeper to the front gate of my Manor in the Uptown region and leave it there. I'm expecting it to be done by the end of the day.";

        public readonly static string MsgRequestPending = "I'm still waiting for you to deliver the Veeper to the Manor. Fill it with cash and / or gold. I'll let you know when the cleaning is done.";
        public readonly static string MsgLaunderPending = "Your previous contract is still being processed.";

        public readonly static int baseWeeklyLaunderCap = 125000;

        public static MSGConversation msg = null;
        public static SendableMessage sendableOption;
        public static bool isLaunderRequestActive = false;

        public static void ResetManorLaunder()
        {
            msg = null;
            sendableOption = null;
            isLaunderRequestActive = false;
        }
        public static void SetupMsgConversation()
        {
            Thomas thomas = UnityEngine.Object.FindObjectOfType<Thomas>(true);
            msg = thomas.MSGConversation;
            sendableOption = msg.CreateSendableMessage(MsgOption1Text);

            void RespondMessage()
            {
                ModCoroutines.Start(DelayedResponse());
            }
#if MONO
            sendableOption.onSent += RespondMessage;
#else
            sendableOption.onSent += (Il2CppSystem.Action)RespondMessage;
#endif

            bool UnlockCheck(SendableMessage message)
            {
                return alliedQuests.ManorLaunderingState.manorLaunderingUnlocked;
            }

#if MONO
            sendableOption.ShouldShowCheck = UnlockCheck;
#else
            SendableMessage.BoolCheck boolCheckInst = (SendableMessage.BoolCheck)UnlockCheck;
            sendableOption.ShouldShowCheck = boolCheckInst;
#endif
        }

        public static IEnumerator DelayedResponse()
        {
            if (UnityEngine.Random.Range(0, 100) > 30)
                yield return Wait2;
            else
                yield return Wait5;

            if (isLaunderRequestActive)
            {
                msg.SendMessage(new(MsgRequestPending, Message.ESenderType.Other), true, false);
            }
            else if (alliedQuests.ManorLaunderingState.daysUntilComplete != -1)
            {
                string msgTemplate = MsgLaunderPending + $" It will be done in {alliedQuests.ManorLaunderingState.daysUntilComplete}";
                msgTemplate = msgTemplate + (alliedQuests.ManorLaunderingState.daysUntilComplete == 1 ? " day." : " days.");
                msg.SendMessage(new(msgTemplate, Message.ESenderType.Other), true, false);
            }
            else
            {
                string msgTemplate = MsgOption1Resp + $" You can clean up to ${alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap} this week.";
                msg.SendMessage(new(msgTemplate, Message.ESenderType.Other), true, false);
                isLaunderRequestActive = true;
            }
        }

        public static IEnumerator GenerateLaunderProcess()
        {
            yield return Wait10;
            // Check van presence at manor gate
            Log("Generate manor launder process start");
            ManorGate gate = null;
            for (int i = 0; i < Property.Properties.Count; i++)
            {
                if (Property.Properties[i].propertyCode == "manor")
                {
                    gate = Property.Properties[i].gameObject.GetComponentInChildren<ManorGate>();
                    if (gate == null)
                    {
                        // try again with transform child direct
                        Transform manorGateTr = Property.Properties[i].transform.Find("Manor Gate");
                        if (manorGateTr != null)
                        {
                            ManorGate gate2 = manorGateTr.gameObject.GetComponent<ManorGate>();
                            if (gate2 != null)
                                gate = gate2;
                        }
                    }
                    break;
                }
            }

            if (gate == null)
            {
                Log("Failed to find Manor Gate instance");
                yield break;
            }

            LandVehicle nearestVehicle = null;
            if (NetworkSingleton<VehicleManager>.Instance.PlayerOwnedVehicles != null)
            {
                foreach (LandVehicle veh in NetworkSingleton<VehicleManager>.Instance.PlayerOwnedVehicles)
                {
                    if (veh.vehicleCode == "veeper") // todo distance check from gate?
                    {
                        float distanceFromGate = Vector3.Distance(gate.transform.position, veh.transform.position);
                        Log($"Veeper distance : {distanceFromGate}");
                        nearestVehicle = veh;
                        break;
                    }
                }
            }

            if (nearestVehicle == null)
            {
                Log("Failed to find a vehicle at manor gate");
                yield break;
            }

            // parse van contents for cash and gold
            StorageEntity storage = nearestVehicle.Storage;

            // iter over itemslots, cast for money instances, check for item gold bars???
            // calculate value
            float goldBarValue = UnityEngine.Random.Range(7500f, 12000f);
            float vanStorageValue = 0f;
            if (storage != null)
            {
#if MONO
                List<ItemInstance> storageItems = storage.GetAllItems();
#else
                Il2CppSystem.Collections.Generic.List<ItemInstance> storageItems = storage.GetAllItems();
#endif
                foreach (ItemInstance item in storageItems)
                {
                    if (item.ID == "cash")
                    {
                        CashInstance cashInst = null;
#if MONO
                        cashInst = item as CashInstance;
#else
                        cashInst = item.TryCast<CashInstance>();
#endif
                        vanStorageValue += cashInst.Balance;
                    }
                    else if (item.ID == "goldbar")
                    {
                        vanStorageValue += goldBarValue * item.Quantity;
                    }
                }
                storage.ClearContents();
            }

            if (vanStorageValue > 0f)
            {
                alliedQuests.ManorLaunderingState.totalLaunderValue = Mathf.RoundToInt(vanStorageValue);
                alliedQuests.ManorLaunderingState.daysUntilComplete = 3;
                Log($"Launder process started with: {alliedQuests.ManorLaunderingState.totalLaunderValue}");
                string msgText = $"The van contents are worth ${alliedQuests.ManorLaunderingState.totalLaunderValue}. Move your van away from the manor gate and wait 3 days for the transaction to complete.";
                msg.SendMessage(new(msgText, Message.ESenderType.Other), true, false);
            }


            isLaunderRequestActive = false;
            
            yield break;
        }

        public static IEnumerator LaunderOperationCompleted()
        {
            // bank transfer
            yield return Wait30;
            int subtotal = 0;

            if (alliedQuests.ManorLaunderingState.totalLaunderValue > alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap)
                subtotal = alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap;
            else
                subtotal = alliedQuests.ManorLaunderingState.totalLaunderValue;

            alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap = Mathf.Clamp(alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap - subtotal, 0, int.MaxValue);

            NetworkSingleton<MoneyManager>.Instance.CreateOnlineTransaction("Business Investment", (float)subtotal, 1f, string.Empty);
            string msgText = $"Your Hyland Business contract price was set at ${subtotal}. You can make new requests for a total of ${alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap} until next monday.";
            alliedQuests.ManorLaunderingState.daysUntilComplete = -1;
            yield break;
        }

        public static void OnDayPassProcessLaunder()
        {
            if (!alliedQuests.ManorLaunderingState.manorLaunderingUnlocked)
                return;
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Truced)
                return;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Truced)
                return;
#endif

            if (TimeManager.Instance.CurrentDay == EDay.Monday)
                alliedQuests.ManorLaunderingState.manorWeeklyLaunderCap = baseWeeklyLaunderCap;

            if (isLaunderRequestActive && alliedQuests.ManorLaunderingState.daysUntilComplete == -1)
            {
                // check for van presence at the gate
                ModCoroutines.Start(GenerateLaunderProcess());
            }
            else if (alliedQuests.ManorLaunderingState.daysUntilComplete >= 1)
            {

                alliedQuests.ManorLaunderingState.daysUntilComplete -= 1;

                if (alliedQuests.ManorLaunderingState.daysUntilComplete == 0)
                    ModCoroutines.Start(LaunderOperationCompleted());
            }

            return;
        }
    }
}