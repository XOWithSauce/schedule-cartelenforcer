using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DealerActivity;
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
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.Levelling;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.Levelling;
#endif

namespace CartelEnforcer
{
    public static class InterceptEvent
    {

        // UI Elements to save Sprites and Colors for changing Quest icon when intercepted
        public static Color questIconBack;
        public static Sprite handshake;
        public static Sprite benziesLogo;

        private static CartelDealer[] allCartelDealers;

        // Track current intercepted contract GUID
        public static List<string> contractGuids = new();

        public static bool interceptingDeal = false;
        public static CartelDealer interceptor = null;

        public static IEnumerator EvaluateCartelIntercepts()
        {
            yield return Wait5;
            InterceptEvent.allCartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            Log("Starting Cartel Intercepts Evaluation");
            float frequency = 120f;
            if (currentConfig.activityFrequency > 0.0f)
                frequency = Mathf.Lerp(frequency, 60f, currentConfig.activityFrequency);
            else if (currentConfig.activityFrequency < 0.0f)
                frequency = Mathf.Lerp(frequency, 240f, -currentConfig.activityFrequency);

            WaitForSeconds WaitRandom1 = new WaitForSeconds(UnityEngine.Random.Range(frequency, frequency * 1.5f));
            WaitForSeconds WaitRandom2 = new WaitForSeconds(UnityEngine.Random.Range(frequency * 1.3f, frequency * 2f));
            WaitForSeconds WaitRandom3 = new WaitForSeconds(UnityEngine.Random.Range(frequency * 2f, frequency * 3f));

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

                // from 4pm to 4am only
                if (!(TimeManager.Instance.CurrentTime >= 1620 || TimeManager.Instance.CurrentTime <= 420))
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
            int occupiedDealersCount = 0;
            foreach (CartelDealer d in InterceptEvent.allCartelDealers)
            {
                bool hasActive = false;
                foreach (Contract c in d.ActiveContracts)
                {
                    hasActive = true;
                    if (!occupied.Contains(c.GUID.ToString()))
                        occupied.Add(c.GUID.ToString());
                }

                if (hasActive) 
                    occupiedDealersCount++;
            }

            // Since the prerequirement here is that the chosen dealer out of all dealers has no active contract, if the occupied dealer count is the all cartel dealer count then we can just skip running the function totally to avoid unnecessary computation
            if (InterceptEvent.allCartelDealers.Length == occupiedDealersCount)
                yield break;

            Log($"[INTERCEPT]    Check Contracts from total of: {NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount} contracts");
            List<Contract> validContracts = new();
            int i = 0;
            do
            {
                yield return Wait025;
                if (!registered) yield break;

                if (i >= NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount)
                {
                    Log("[INTERCEPT]    - Check Ended");
                    break; // Safe transform parse
                }
                Transform trContract = NetworkSingleton<QuestManager>.Instance.ContractContainer.GetChild(i);
                i++;

                if (trContract != null)
                {
                    Contract contract = trContract.GetComponent<Contract>();
                    if (contract != null)
                    {
                        if (contract.Dealer != null) continue; // Not player
                        if (contract.Customer == null) continue; // broken??
                        if (contract.State != EQuestState.Active) continue;
                        if (!contract.hudUIExists) continue; // if not UI exists not valid should simplify the process
                        if (contract.hudUI == null) continue; // Check hud ui state
                        if (contract.hudUI.gameObject == null) continue;
                        if (contract.hudUI.gameObject.activeSelf == false) continue;
                        if (occupied.Contains(contract.GUID.ToString())) continue; // Not cartel dealer
                        if (contract.GetMinsUntilExpiry() > 300) continue; // Only take contracts with less than 5h left
                        if (contract.GetMinsUntilExpiry() < 90) continue; // Only take contracts with More than 1h 30min left (30min) reserved for max wait sleep
                        if (contractGuids.Contains(contract.GUID.ToString())) continue; // Only take contracts currently not intercepted

                        if (!validContracts.Contains(contract))
                            validContracts.Add(contract);
                    }
                }
                
            } while (i < NetworkSingleton<QuestManager>.Instance.ContractContainer.childCount && registered);
            Log("[INTERCEPT]    Check Contracts done");

            if (validContracts.Count == 0)
            {
                Log("[INTERCEPT]    No Valid Contracts Amount");
                yield break; // No Valid Contracts this time
            }

            Customer customer = null;
            Contract randomContract = null;
            Log("[INTERCEPT]     Take contract ");
            do
            {
                yield return Wait01;
                if (!registered) yield break;
                if (validContracts.Count == 0) break;
                int randomIndex = UnityEngine.Random.Range(0, validContracts.Count);

                randomContract = validContracts[randomIndex];

                if (randomContract == null || (randomContract != null && randomContract.State == EQuestState.Completed))
                {
                    Log("[INTERCEPT]    Contract got completed before intercept initialized");
                    validContracts.RemoveAt(randomIndex);
                    continue;
                }

                customer = randomContract.Customer.GetComponent<Customer>();
                if (customer.NPC.Health.IsDead || customer.NPC.Health.IsKnockedOut)
                {
                    Log("[INTERCEPT]    Contract NPC is dead or knocked out");
                    validContracts.RemoveAt(randomIndex);
                    continue;
                }

                // Else current iteration picked contract is valid and associated customer is alive
                break;

            } while (registered && validContracts.Count != 0);

            if (customer == null || randomContract == null) yield break; // just a sanity check because sometimes with quantum theory double slit experiment proves that something can be unexpected

            CartelDealer selected = null;

            Log("[INTERCEPT]    Dealer Parsing started");

            EMapRegion region = EMapRegion.Northtown;
            for (int j = 0; j < Singleton<Map>.Instance.Regions.Length; j++)
            {
                if (Singleton<Map>.Instance.Regions[j].RegionBounds.IsPointInsidePolygon(customer.NPC.CenterPointTransform.position))
                {
                    region = Singleton<Map>.Instance.Regions[j].Region;
                }
            }

            if (region == EMapRegion.Northtown)
            {
                selected = NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities(EMapRegion.Westville).CartelDealer;
            }
            else
            {
                selected = NetworkSingleton<Cartel>.Instance.Activities.GetRegionalActivities(region).CartelDealer;
            }

            
            // Ensure Cartel Dealer has no active contract and is not dead or knocked out OR they are occupied by base game CartelCustomerDeals region activity
            if ((selected.ActiveContracts != null && selected.ActiveContracts.Count >= 1) || (selected.Health.IsDead || selected.Health.IsKnockedOut))
            {
                // How to manage the state between cartel dealer intercepting player dealer contracts, player pending and also intercepting active? This causes the more frequent kind to be always preferred leading to the intercept deals rarely happening. Config for dealer.json needs to be tweaked
                Log("[INTERCEPT]    Dealer has active contracts or is dead or is occupied, check if any nearby.");
                bool foundReplacement = false;
                // Alternatively we check if any of the dealers would be nearby (60units max), this way even higher values for the dealer config chance will still allow the intercept event to work
                foreach (CartelDealer otherDealer in allCartelDealers)
                {
                    yield return Wait01;
                    if (!registered) yield break;
                    if (otherDealer == selected) continue; // skip the one who is not elgible
                    if (otherDealer.Health.IsDead || otherDealer.Health.IsKnockedOut) continue; // Dead or knocked out not elgible
                    if (otherDealer.ActiveContracts != null && otherDealer.ActiveContracts.Count >= 1) continue;

                    if (Vector3.Distance(otherDealer.CenterPoint, customer.NPC.CenterPoint) < 80f)
                    {
                        foundReplacement = true;
                        selected = otherDealer;
                        break;
                    }
                }
                if (!foundReplacement) yield break;
            }

            Log("[INTERCEPT]    Dealer Parsing completed");

            string cGuid = randomContract.GUID.ToString();
            if (!contractGuids.Contains(cGuid))
                contractGuids.Add(cGuid);

            NPCEvent_StayInBuilding event1 = null;
            if (selected.Behaviour.ScheduleManager.ActionList != null)
            {
                foreach (NPCAction action in selected.Behaviour.ScheduleManager.ActionList)
                {
#if MONO
                    if (action is NPCEvent_StayInBuilding ev1)
                        event1 = ev1;
#else
                    NPCEvent_StayInBuilding ev1_temp = action.TryCast<NPCEvent_StayInBuilding>();
                    if (ev1_temp != null)
                    {
                        event1 = ev1_temp;
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
            interceptor = selected;
            coros.Add(MelonCoroutines.Start(QuestUIEffect(randomContract)));
            coros.Add(MelonCoroutines.Start(BeginIntercept(selected, randomContract, customer, region, event1, cGuid)));
            yield return null;
        }

        public static IEnumerator BeginIntercept(CartelDealer dealer, Contract contract, Customer customer, EMapRegion region, NPCEvent_StayInBuilding ev1, string cGuid)
        {
            yield return Wait30; // Cartel dealer is kinda fast so have to wait a bit
            if (!registered) yield break;
            bool changeInfluence = ShouldChangeInfluence(region);

            if (customer.CurrentContract == null && contract != null && contract.State == EQuestState.Completed) // If player managed to complete it within that timeframe
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.interceptSuccess);
                if (contractGuids.Contains(cGuid))
                    contractGuids.Remove(cGuid);
                interceptingDeal = false;
                interceptor = null;
                yield break;
            }

            int originalXP = contract.CompletionXP;

            contract.CompletionXP = 0;
            contract.BopHUDUI();
            for (int i = 0; i < dealer.Inventory.ItemSlots.Count; i++)
            {
                if (dealer.Inventory.ItemSlots[i].ItemInstance == null)
                {
                    List<ItemInstance> fromPool = GetFromPool(1);
                    if (fromPool.Count > 0)
                        dealer.Inventory.ItemSlots[i].ItemInstance = fromPool[0];
                }
            }

            // Store the events states to reset
            int currentStayInsideStart = ev1.StartTime;
            int currentStayInsideEnd = ev1.EndTime;
            int currentStayInsideDur = ev1.Duration;

            bool runOnce = false;

#if MONO
            UnityEngine.Events.UnityAction<EQuestState> cb = null;
            cb = new UnityEngine.Events.UnityAction<EQuestState>(OnQuestEndEvaluateResult);
#else
            Action<EQuestState> cb = null;
            cb = new Action<EQuestState>(OnQuestEndEvaluateResult);
#endif
            void OnQuestEndEvaluateResult(EQuestState state)
            {
                if (runOnce) return;
                runOnce = true;

                contract.onQuestEnd.RemoveListener(cb);

                Log("[INTERCEPT]    EVALUATE RESULT: " + state);
                float cartelDealerDist = Vector3.Distance(dealer.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                float playerDist = Vector3.Distance(Player.Local.CenterPointTransform.position, customer.NPC.CenterPointTransform.position);
                if (cartelDealerDist < playerDist && cartelDealerDist < 4f && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Cartel Succesfully Intercepted Deal");
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.interceptFail);
                    customer.NPC.RelationData.ChangeRelationship(-0.10f, true);
                }
                else if (playerDist < 4f && state == EQuestState.Completed)
                {
                    Log("[INTERCEPT]    Player Stopped Cartel Intercept");
                    NetworkSingleton<LevelManager>.Instance.AddXP(originalXP);
                    if (changeInfluence)
                        NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.interceptSuccess);
                    customer.NPC.RelationData.ChangeRelationship(0.10f, true);
                }
                else if (state == EQuestState.Failed && (dealer.Health.IsDead || dealer.Health.IsKnockedOut))
                {
                    coros.Add(MelonCoroutines.Start(AssignContractSoon(customer, contract, originalXP)));
                }
                if (contractGuids.Contains(cGuid))
                    contractGuids.Remove(cGuid);

                if (ev1 != null)
                {
                    Log("Re-enable event");
                    ev1.enabled = true;
                    ev1.IsActive = true;
                    ev1.StartTime = currentStayInsideStart;
                    ev1.EndTime = currentStayInsideEnd;
                    ev1.Duration = currentStayInsideDur;
                    ev1.HasStarted = true;
                    ev1.Resume();
                }
                interceptingDeal = false;
                interceptor = null;
            }

            contract.onQuestEnd.AddListener(cb);
            if (!dealer.IsAcceptingDeals)
                dealer.SetIsAcceptingDeals(true);
            dealer.AddContract(contract);
            dealer.SetIsAcceptingDeals(false);

            if (ev1 != null)
            {
                ev1.StartTime = 420;
                ev1.EndTime = 1620;
                ev1.Duration = 720;
                ev1.End();
                ev1.HasStarted = false;
                ev1.enabled = false;
            }
            Log("[INTERCEPT] Dealer Check attend start");
            dealer.CheckAttendStart();

            // Because it seems that this gets reset every once in a while
            dealer.Movement.MoveSpeedMultiplier = dealerConfig.CartelDealerMoveSpeedMultiplier;

            // Set the dealer to null because player wont be able to complete the deal otherwise, locked because its reserved for "Rival Dealer"
            // The dealer will have the contract too and try to complete it, but this way player can do it too
            if (customer.CurrentContract != null)
            {
                if (customer.CurrentContract.Dealer != null)
                    customer.CurrentContract.Dealer = null;
            }
            yield return null;
        }

        public static IEnumerator AssignContractSoon(Customer customer, Contract contract, int XP)
        {
            yield return Wait01;
            if (!registered) yield break;

            if (contract.State != EQuestState.Active)
                contract.SetQuestState(EQuestState.Active);
            customer.AssignContract(contract);
            customer.CurrentContract.CompletionXP = XP;
            customer.ConfigureDealSignal(null, NetworkSingleton<TimeManager>.Instance.CurrentTime, true);
            customer.DealSignal.SetContract(contract);
            customer.DealSignal.IsActive = true;
            customer.UpdateDealAttendance();
            customer.SetIsAwaitingDelivery(true);
            contract.SetIsTracked(true);
            if (customer.CurrentContract.hudUI == null || customer.CurrentContract.hudUI?.gameObject == null)
            {
                customer.CurrentContract.SetupHUDUI();
            }

            if (!Contract.Contracts.Contains(contract))
                Contract.Contracts.Add(contract);
            if (contract.DeliveryLocation != null && !contract.DeliveryLocation.ScheduledContracts.Contains(contract))
                contract.DeliveryLocation.ScheduledContracts.Add(contract);
            yield return Wait05;
            yield return Wait01;
            if (!registered) yield break;

            if (!customer.CurrentContract.hudUI.gameObject.activeSelf)
                customer.CurrentContract.hudUI.gameObject.SetActive(true);

            customer.HasChanged = true;
            contract.HasChanged = true;
            customer.CurrentContract.hudUI.FadeIn();
            NetworkSingleton<TimeManager>.Instance.onMinutePass += new Action(contract.MinPass); 
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
