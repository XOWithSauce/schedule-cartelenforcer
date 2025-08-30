using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InfluenceOverrides;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using ScheduleOne.UI;
using ScheduleOne.UI.Phone.Messages;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Messages;
#endif

namespace CartelEnforcer
{
    public static class InterceptEvent
    {

        // UI Elements to save Sprites and Colors for changing Quest icon when intercepted
        public static Color questIconBack;
        public static Sprite handshake;
        public static Sprite benziesLogo;

        public static CartelDealer[] allCartelDealers;

        // Track current intercepted contract GUID
        public static List<string> contractGuids = new();

        public static bool interceptingDeal = false;

        public static IEnumerator EvaluateCartelIntercepts()
        {
            yield return Wait5;
            allCartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            Log("Starting Cartel Intercepts Evaluation");
            float frequency = 90f;
            if (currentConfig.activityFrequency >= 0.0f)
                frequency = Mathf.Lerp(frequency, 60f, currentConfig.activityFrequency);
            else
                frequency = Mathf.Lerp(frequency, 480f, -currentConfig.activityFrequency);

            WaitForSeconds WaitRandom1 = new WaitForSeconds(UnityEngine.Random.Range(frequency, frequency * 2f));
            WaitForSeconds WaitRandom2 = new WaitForSeconds(UnityEngine.Random.Range(frequency * 1.2f, frequency * 2f));
            WaitForSeconds WaitRandom3 = new WaitForSeconds(UnityEngine.Random.Range(frequency * 1.5f, frequency * 2.5f));

            while (registered)
            {
                switch (UnityEngine.Random.Range(1, 4))
                {
                    case 1:
                        yield return WaitRandom1; break;
                    case 2:
                        yield return WaitRandom2; break;
                    case 3:
                        yield return WaitRandom3; break;
                }
                if (!registered) yield break;

                // from 6pm to 4am only
                if (!(TimeManager.Instance.CurrentTime >= 1800 || TimeManager.Instance.CurrentTime <= 400))
                    continue;

#if MONO
                // Only when hostile
                if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile)
                    continue;
#else

                if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile)
                    continue;
#endif


                if (!interceptingDeal)
                {
                    coros.Add(MelonCoroutines.Start(StartInterceptDeal()));
                }
            }
        }

        public static IEnumerator StartInterceptDeal()
        {
            Log("[INTERCEPT] Started Checking Intercept Deal Validity");
            List<string> occupied = new();
            foreach (CartelDealer d in allCartelDealers)
            {
                foreach (Contract c in d.ActiveContracts)
                {
                    if (!occupied.Contains(c.GUID.ToString()))
                        occupied.Add(c.GUID.ToString());
                }
            }
            Log($"[INTERCEPT]    Check Contracts from total of: {NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount} contracts");

            List<Contract> validContracts = new();

            int i = 0;
            do
            {
                if (i >= NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount)
                {
                    Log("[INTERCEPT]    - Check Ended");
                    break; // Safe transform parse
                }
                Transform trContract = NetworkSingleton<QuestManager>.Instance.ContractContainer.GetChild(i);
                if (trContract != null)
                {
                    Contract contract = trContract.GetComponent<Contract>();
                    if (contract != null)
                    {
                        bool isValid = true;
                        if (contract.Dealer != null) isValid = false; // Not player
                        if (contract.Customer == null) isValid = false; // broken??
                        if (occupied.Contains(contract.GUID.ToString())) isValid = false; // Not cartel dealer
                        if (contract.GetMinsUntilExpiry() > 300) isValid = false; // Only take contracts with less than 5h left
                        if (contract.GetMinsUntilExpiry() < 90) isValid = false; // Only take contracts with More than 1h 30min left (30min) reserved for max wait sleep
                        if (contractGuids.Contains(contract.GUID.ToString())) isValid = false; // Only take contracts currently not intercepted

                        if (isValid)
                        {
                            if (!validContracts.Contains(contract))
                                validContracts.Add(contract);
                        }
                    }
                }
                i++;
            } while (i < NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount);

            if (validContracts.Count == 0)
            {
                Log("[INTERCEPT]    No Valid Contracts Amount");
                yield break; // No Valid Contracts this time
            }

            // Check that the contract exists in UI since we need it later in functions
            QuestHUDUI[] current = UnityEngine.Object.FindObjectsOfType<QuestHUDUI>();
            if (current.Length == 0)
            {
                Log($"[INTERCEPT]    No HUD Elements to parse");
                yield break;
            }
            List<Contract> ctrsToRemove = new();
            Log($"[INTERCEPT]    Match Contract to HUD Elements from total of: {current.Length} elements");
            foreach (Contract contract in validContracts)
            {
                bool exists = false;
                foreach (QuestHUDUI item in current)
                {
                    if (!registered) yield break;
                    if (item.Quest == contract)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    ctrsToRemove.Add(contract);
            }
            Log("[INTERCEPT]    Matching done");
            // Remove the ones which dont exist in UI since that causes an error
            if (ctrsToRemove.Count > 0)
            {
                foreach (Contract ctr in ctrsToRemove)
                    validContracts.Remove(ctr);
            }
            if (validContracts.Count == 0)
            {
                Log("[INTERCEPT]    No Valid Contracts After Removing Non-UI supported");
                yield break; // No Valid Contracts this time
            }
            
            Contract randomContract = validContracts[UnityEngine.Random.Range(0, validContracts.Count)];
            Customer customer = randomContract.Customer.GetComponent<Customer>();
            CartelDealer selected = null;

            EMapRegion region = EMapRegion.Northtown;
            for (int j = 0; j < Singleton<Map>.Instance.Regions.Length; j++)
            {
                if (Singleton<Map>.Instance.Regions[j].RegionBounds.IsPointInsidePolygon(customer.NPC.CenterPointTransform.position))
                {
                    region = Singleton<Map>.Instance.Regions[j].Region;
                }
            }
            selected = NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities(region).CartelDealer;
            if (selected == null)
            {
                //Log("[INTERCEPT]    Selected Cartel Dealer is null"); // this happens in the first game region
                selected = NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities(EMapRegion.Westville).CartelDealer;
            }

            // Ensure Cartel Dealer is not dead or knocked out
            if (selected.Health.IsDead || selected.Health.IsKnockedOut) yield break;
            // Ensure Cartel Dealer has no active contract
            if (selected.ActiveContracts != null && selected.ActiveContracts.Count >= 1) yield break;
            // Ensure NPC is not dead or knocked out
            if (customer.NPC.Health.IsDead || customer.NPC.Health.IsKnockedOut) yield break;
            // Ensure Player is not nearby
            float distanceToPlayer = Vector3.Distance(customer.NPC.CenterPointTransform.position, Player.Local.CenterPointTransform.position);
            if (distanceToPlayer < 40f) yield break;

            string cGuid = randomContract.GUID.ToString();
            contractGuids.Add(cGuid);

            NPCEvent_StayInBuilding event1 = null;
            NPCSignal_HandleDeal event2 = null;
            if (selected.Behaviour.ScheduleManager.ActionList != null)
            {
                foreach (NPCAction action in selected.Behaviour.ScheduleManager.ActionList)
                {
#if MONO
                    if (action is NPCEvent_StayInBuilding ev1)
                        event1 = ev1;

                    else if (action is NPCSignal_HandleDeal ev2)
                        event2 = ev2;
#else
                    NPCEvent_StayInBuilding ev1_temp = action.TryCast<NPCEvent_StayInBuilding>();
                    if (ev1_temp != null)
                    {
                        event1 = ev1_temp;
                    }
                    else
                    {
                        NPCSignal_HandleDeal ev2_temp = action.TryCast<NPCSignal_HandleDeal>();
                        if (ev2_temp != null)
                        {
                            event2 = ev2_temp;
                        }
                    }
#endif

                }
            }

            string text = "";
            switch (UnityEngine.Random.Range(0, 7))
            {
                case 0:
                    text = "What's taking so long? I will just find another dealer...";
                    break;
                case 1:
                    text = "Nevermind! I made a deal with someone else.";
                    break;
                case 2:
                    text = "Are you coming or not? I might just buy from someone else.";
                    break;
                case 3:
                    text = "Yo where are you?! I've been waiting at our spot. I'll message another dealer then...";
                    break;
                case 4:
                    text = "This isn't working out. I'm taking my business elsewhere.";
                    break;
                case 5:
                    text = "I'm not waiting around all day. Don't bother texting me back.";
                    break;
                case 6:
                    text = "You snooze, you lose. Found another dealer to sell me my shit.";
                    break;
                case 7:
                    text = "I'll hustle with someone else if you ghost me like this...";
                    break;
            }

            Log("[INTERCEPT]    Starting Intercept Deal");
            customer.NPC.MSGConversation.SendMessage(new Message(text, Message.ESenderType.Other, true, -1), true, true);
            interceptingDeal = true;

            coros.Add(MelonCoroutines.Start(QuestUIEffect(randomContract)));
            coros.Add(MelonCoroutines.Start(BeginIntercept(selected, randomContract, customer, region, event1, event2, cGuid)));
            yield return null;
        }

        public static IEnumerator BeginIntercept(CartelDealer dealer, Contract contract, Customer customer, EMapRegion region, NPCEvent_StayInBuilding ev1, NPCSignal_HandleDeal ev2, string cGuid)
        {
            yield return Wait30; // Cartel dealer is kinda fast so have to wait a bit
            if (!registered) yield break;
            bool changeInfluence = ShouldChangeInfluence(region);

            if (customer.CurrentContract == null) // If player managed to complete it within that timeframe
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.100f);
                contractGuids.Remove(cGuid);
                interceptingDeal = false;
                yield break;
            }

            contract.BopHUDUI();
            contract.CompletionXP = Mathf.RoundToInt(contract.CompletionXP * 0.5f);
            contract.completedContractsIncremented = false;

            for (int i = 0; i < dealer.Inventory.ItemSlots.Count; i++)
            {
                if (dealer.Inventory.ItemSlots[i].ItemInstance == null)
                {
                    List<ItemInstance> fromPool = GetFromPool(1);
                    if (fromPool.Count > 0)
                        dealer.Inventory.ItemSlots[i].ItemInstance = fromPool[0];
                }
            }

            void OnQuestEndEvaluateResult(EQuestState state)
            {
                Log("[INTERCEPT]    EVALUATE RESULT: " + state);
                float cartelDealerDist = Vector3.Distance(dealer.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                float playerDist = Vector3.Distance(Player.Local.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                if (cartelDealerDist < playerDist && cartelDealerDist < 5f && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Cartel Succesfully Intercepted Deal");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.100f);
                    customer.NPC.RelationData.ChangeRelationship(-1f, true);
                }
                // Now this one doesnt work in il2cpp for some reason the contract insta fails when cartel dealer is killed, for mono it doesnt fail allows player to complete the deal correctly
                else if (playerDist < 5f && (dealer.Health.IsDead || dealer.Health.IsKnockedOut) && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Player Stopped Cartel Intercept & killed dealer");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.100f);
                    customer.NPC.RelationData.ChangeRelationship(0.25f, true);

                }
                else if (playerDist < 5f && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Player Stopped Cartel Intercept");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.050f);
                    customer.NPC.RelationData.ChangeRelationship(0.25f, true);
                }
                if (contractGuids.Contains(cGuid))
                    contractGuids.Remove(cGuid);

                ev2.End();
                ev2.HasStarted = false;
                ev2.gameObject.SetActive(false);
                ev1.enabled = true;
                ev1.IsActive = true;
                ev1.StartTime = 358;
                ev1.HasStarted = true;
                ev1.Resume();

                interceptingDeal = false;
            }
#if MONO
            contract.onQuestEnd.AddListener(new UnityEngine.Events.UnityAction<EQuestState>(OnQuestEndEvaluateResult));
#else
            contract.onQuestEnd.AddListener(new Action<EQuestState>(OnQuestEndEvaluateResult));
#endif

            dealer.SetIsAcceptingDeals(true);
            dealer.AddContract(contract);

            //List<QuestEntryData> list = new List<QuestEntryData>();
            //for (int i = 0; i < contract.Entries.Count; i++)
            //{
            //    list.Add(contract.Entries[i].GetSaveData());
            //}

            //ContractInfo data = new ContractInfo(contract.Payment, contract.ProductList, contract.DeliveryLocation.GUID.ToString(), contract.DeliveryWindow, true, contract.Expiry.time, contract.PickupScheduleIndex, false);

            //NetworkSingleton<QuestManager>.Instance.CreateContract_Networked(null, "Intercept Cartel Deal", "1", contract.GUID.ToString(), true, customer.NetworkObject, data, contract.Expiry, contract.AcceptTime, dealer.NetworkObject);

            // But the logic is still missing receiver because it needs to know to overwrite the cartel dealer stay inside event and reset state
            // how to differentiate contracts?? Maybe PickupSchedule Index can be custom??
            // The patching needs something custom for the rpc logic create contract networked function
            // basically create the contract + do these below events


            if (ev1 != null)
            {
                ev1.StartTime = 420;
                ev1.EndTime = 1800;
                ev1.End();
                ev1.HasStarted = false;
                ev1.enabled = false;
            }
            if (ev2 != null)
            {
                ev2.Started();
                ev2.HasStarted = true;
                ev2.gameObject.SetActive(true);
            }
            dealer.CheckAttendStart();
            // Set the dealer to null because player wont be able to complete the deal otherwise, locked because its reserved for "Rival Dealer"
            // The dealer will have the contract too and try to complete it, but this way player can do it too
            if (customer.CurrentContract != null)
                if (customer.CurrentContract.Dealer != null)
                    customer.CurrentContract.Dealer = null;

            yield return null;
        }


        public static IEnumerator FetchUIElementsInit()
        {
            yield return Wait5;
            if (!registered) yield break;

            RectTransform rt = PlayerSingleton<MessagesApp>.Instance.conversationEntryContainer;
            if (rt == null)
            {
                Log("Conversation entry container is null");
                yield break;
            }

            // Now we build a mount everest here because otherwise il2cpp thinks we are dealing with system objects, this is safe code for mono too so we prefer the mount everest over simplified code
            for (int i = 0; i < rt.childCount; i++)
            {
                if (i > rt.childCount) break;
                Transform msgItem = rt.GetChild(i);
                if (msgItem != null)
                {
                    Transform nameTr = msgItem.Find("Name");
                    if (nameTr != null && nameTr.gameObject != null)
                    {
                        Text text = nameTr.gameObject.GetComponent<Text>();
                        if (text != null && text.text == "Thomas Benzies")
                        {
                            Transform iconMask = msgItem.Find("IconMask");
                            if (iconMask != null)
                            {
                                Transform icon = iconMask.Find("Icon");
                                if (icon != null && icon.gameObject != null)
                                {
                                    benziesLogo = icon.gameObject.GetComponent<Image>().sprite;
                                    Log("Benzies Logo Assigned");
                                }
                            }
                        }
                        else
                            continue;
                    }
                    else
                        Log("NameTr is null");
                }
                else
                    Log("Msg Item is Null");
            }
            Log("Fetched Benzies Logo UI Element");
            // the below code is alternative to the mount everest code above
            //foreach (Transform tr in rt.childCount)
            //{
            //    if (tr.Find("Name").GetComponent<Text>().text != "Thomas Benzies")
            //        continue;
            //    benziesLogo = tr.Find("IconMask").Find("Icon").GetComponent<Image>().sprite;
            //}
            yield return null;
        }

        public static IEnumerator QuestUIEffect(Contract contract)
        {
            Log("[INTERCEPT]    Quest UI Effect");

            // Ugly code here but we need to actually check each transform for null
            if (contract.hudUI == null || contract.hudUI.MainLabel == null || contract.hudUI.MainLabel.transform == null)
            {
                Log("[INTERCEPT] foundItem got nulled, break");
                yield break;
            }

            Transform iconContainer = contract.hudUI.MainLabel.transform.Find("IconContainer");
            if (iconContainer == null)
            {
                Log("[INTERCEPT] iconContainer got nulled, break");
                yield break;
            }
            Transform contractIcon = iconContainer.Find("ContractIcon(Clone)");
            if (contractIcon == null)
            {
                Log("[INTERCEPT] contractIcon got nulled, break");
                yield break;
            }

            Transform backgroundTr = contractIcon.Find("Background");
            if (backgroundTr == null)
            {
                Log("[INTERCEPT] backgroundTr got nulled, break");
                yield break;
            }
            Image background = backgroundTr.GetComponent<Image>();
            questIconBack = background.color;

            coros.Add(MelonCoroutines.Start(LerpQuestColor(background)));

            Transform fillImgTr = contractIcon.Find("Fill");
            if (fillImgTr == null)
            {
                Log("[INTERCEPT] backgroundTr got nulled, break");
                yield break;
            }
            Image fillImg = fillImgTr.GetComponent<Image>();
            handshake = fillImg.sprite;

            contract.hudUI.BopIcon();
            fillImg.overrideSprite = benziesLogo;

            coros.Add(MelonCoroutines.Start(ResetQuestUIEffect(fillImg, background)));
            yield return null;
        }

        public static IEnumerator ResetQuestUIEffect(Image fillImg, Image background)
        {
            while (interceptingDeal && registered) { yield return Wait5; }
            if (background != null)
                background.color = questIconBack;
            if (fillImg)
                fillImg.overrideSprite = handshake;
            Log("[INTERCEPT] Reset QuestUI");
            yield return null;
        }
        public static IEnumerator LerpQuestColor(Image background)
        {
            Color startColor = background.color;
            Color endColor = Color.white;
            float duration = 2.0f;
            float timer = 0f;

            while (timer < duration && registered)
            {
                float t = timer / duration;
                background.color = Color.Lerp(startColor, endColor, t);
                timer += Time.deltaTime;
                yield return Wait01;
                if (!registered) yield break;
            }
            yield return null;
        }
    }
}
