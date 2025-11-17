using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DealerRobbery;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InfluenceOverrides;

#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.Combat;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Messaging;
using ScheduleOne.Money;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
#else
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.NPCs.Schedules;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
#endif


namespace CartelEnforcer
{

    [HarmonyPatch(typeof(Dealer), "TryRobDealer")] // This is only reached by FishNet Instance IsServer
    public static class DealerRobberyPatch
    {
        private static bool IsPlayerNearby(Dealer dealer, float maxDistance = 120f)
        {
            return Vector3.Distance(dealer.transform.position, Player.Local.transform.position) < maxDistance;
        }

        private static void Original(Dealer __instance)
        {
            // This is original source code, difference is that we allow for stealing items and persist them.
            // Per original source code SummarizeLosses is defined inside the function.
            static void SummarizeLosses(Dealer __instance, List<ItemInstance> items, float cash)
            {
                if (items.Count == 0 && cash <= 0f)
                {
                    return;
                }
                // Added this 2 pieces of code to the original source, everything else inside Original function is original source code or equivalent
                coros.Add(MelonCoroutines.Start(CartelStealsItems(items)));
                cartelCashAmount += cash;

                List<string> list = new List<string>();
                for (int i = 0; i < items.Count; i++)
                {
                    string text = items[i].Quantity.ToString() + "x ";

#if MONO
                    if (items[i] is ProductItemInstance && (items[i] as ProductItemInstance).AppliedPackaging != null)
                    {
                        text = text + (items[i] as ProductItemInstance).AppliedPackaging.Name + " of ";
                    }
                    text += items[i].Definition.Name;
                    if (items[i] is QualityItemInstance)
                    {
                        text = text + " (" + (items[i] as QualityItemInstance).Quality.ToString() + " quality)";
                    }

#else
                    // following part is edited from original source code because it crashes in il2cpp using original logic
                    // as type check fails on il2cpp we change it to try cast, replaces "as" comparison and casts to object
                    // Taxing and less performant but works.
                    ProductItemInstance tempInst = items[i].TryCast<ProductItemInstance>();
                    if (tempInst != null)
                    {
                        if (tempInst.AppliedPackaging != null)
                            text = text + tempInst.AppliedPackaging.Name + " of ";
                    }
                    text += items[i].Definition.Name;
                    QualityItemInstance tempInst2 = items[i].TryCast<QualityItemInstance>();
                    if (tempInst2 != null)
                    {
                        text = text + " (" + tempInst2.Quality.ToString() + " quality)";
                    }
#endif
                    list.Add(text);
                }
                if (cash > 0f)
                {
                    list.Add(MoneyManager.FormatAmount(cash, false, false) + " cash");
                }
                string text2 = "This is what they got:\n" + string.Join("\n", list);
                __instance.MSGConversation.SendMessage(new Message(text2, Message.ESenderType.Other, true, -1), false, true);
            }
            float num = 0f;
            foreach (ItemSlot itemSlot in __instance.Inventory.ItemSlots)
            {
#if MONO
                if (itemSlot.ItemInstance != null)
                {
                    num = Mathf.Max(num, (itemSlot.ItemInstance.Definition as StorableItemDefinition).CombatUtilityForNPCs);
                }
#else
                if (itemSlot.ItemInstance != null)
                {
                    StorableItemDefinition tempDef = itemSlot.ItemInstance.Definition.TryCast<StorableItemDefinition>();
                    if (tempDef != null)
                    {
                        num = Mathf.Max(num, tempDef.CombatUtilityForNPCs);
                    }
                    else
                    {
                        Log("[TRY ROB ORIGINAL] Evaluate Result - Temp Definition is null");
                    }
                }
#endif
            }
            float num2 = UnityEngine.Random.Range(0f, 1f);
            num2 = Mathf.Lerp(num2, 1f, num * 0.5f);
            if (num2 > 0.67f)
            {

                __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);
                return;
            }
            if (num2 > 0.25f)
            {
                __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_partially_defended"), Message.ESenderType.Other, false, -1), true, true);
                List<ItemInstance> list = new List<ItemInstance>();
                float num3 = 1f - Mathf.InverseLerp(0.25f, 0.67f, num2);
                for (int i = 0; i < __instance.Inventory.ItemSlots.Count; i++)
                {
                    if (__instance.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        float num4 = num3 * 0.8f;
                        if (UnityEngine.Random.Range(0f, 1f) < num4)
                        {
                            int num5 = Mathf.RoundToInt(__instance.Inventory.ItemSlots[i].ItemInstance.Quantity * num3);
                            list.Add(__instance.Inventory.ItemSlots[i].ItemInstance.GetCopy(num5));
                            __instance.Inventory.ItemSlots[i].ChangeQuantity(-num5, false);
                        }
                    }
                }
                __instance.TryMoveOverflowItems();
                float num6 = __instance.Cash * num3;
                __instance.ChangeCash(-num6);
                SummarizeLosses(__instance, list, num6);
                return;
            }

            __instance.MSGConversation.SendMessage(new Message(__instance.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_loss"), Message.ESenderType.Other, false, -1), true, true);
            List<ItemInstance> list2 = new List<ItemInstance>();
            foreach (ItemSlot itemSlot2 in __instance.Inventory.ItemSlots)
            {
                if (itemSlot2.ItemInstance != null)
                {
                    list2.Add(itemSlot2.ItemInstance.GetCopy(itemSlot2.ItemInstance.Quantity));
                }
            }
            __instance.Inventory.Clear();
            foreach (ItemSlot itemSlot3 in __instance.overflowSlots)
            {
                if (itemSlot3.ItemInstance != null)
                {
                    list2.Add(itemSlot3.ItemInstance.GetCopy(itemSlot3.ItemInstance.Quantity));
                    itemSlot3.ClearStoredInstance(false);
                }
            }
            float cash = __instance.Cash;
            __instance.ChangeCash(-cash);
            SummarizeLosses(__instance, list2, cash);
        }

        [HarmonyPrefix]
        public static bool Prefix(Dealer __instance)
        {
            // Check dealer unlock and recruited
            if (__instance.RelationData.Unlocked && __instance.IsRecruited)
            {
                // Check not in building, dead or knocked out
                if (!__instance.Health.IsDead && !__instance.Health.IsKnockedOut && !__instance.isInBuilding)
                {
                    Log("[TRY ROB] Started");
                    if (IsPlayerNearby(__instance) && currentConfig.realRobberyEnabled)
                    {
                        Log("[TRY ROB]    Run Custom");
                        coros.Add(MelonCoroutines.Start(RobberyCombatCoroutine(__instance)));
                    }
                    else if (currentConfig.defaultRobberyEnabled)
                    {
                        Log("[TRY ROB]    Run original");
                        Original(__instance);
                    }
                }
            }
            return false;
        }

    }

    public static class DealerRobbery
    {
        public static IEnumerator RobberyCombatCoroutine(Dealer dealer)
        {
            yield return Wait2;
            if (!registered) yield break;
            EMapRegion region = EMapRegion.Northtown;
            for (int i = 0; i < Singleton<Map>.Instance.Regions.Length; i++)
            {
                if (Singleton<Map>.Instance.Regions[i].RegionBounds.IsPointInsidePolygon(dealer.CenterPointTransform.position))
                {
                    region = Singleton<Map>.Instance.Regions[i].Region;
                }
            }

            Vector3 spawnPos = Vector3.zero;
            int maxAttempts = 6;
            Vector3 randomPoint;
            float randomRadius;
            Vector3 randomDirection;
            int j = 0;
            do
            {
                yield return Wait05;
                if (!registered) yield break;

                Log("[TRY ROB]    Finding Spawn Robber Position");
                randomDirection = UnityEngine.Random.onUnitSphere;
                randomDirection.y = 0f;
                randomDirection.Normalize();
                randomRadius = UnityEngine.Random.Range(8f, 16f);
                randomPoint = dealer.transform.position + randomDirection * randomRadius;
                dealer.Movement.GetClosestReachablePoint(targetPosition: randomPoint, out spawnPos);
                j++;
            } while (spawnPos == Vector3.zero && j <= maxAttempts); // Because GetClosestReachablePoint can return V3.Zero as default (unreachable)

            Log("[TRY ROB]    Position for robber: " + spawnPos.ToString());

            if (spawnPos == Vector3.zero)
            {
                Log("[TRY ROB]    Failed to find valid spawn position for robber");
                yield break;
            }

            CartelGoon goon = NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos);
            if (goon == null)
            {
                Log("[TRY ROB]    Failed to spawn goon. Robbery failed.");
                yield break;
            }
            Log("[TRY ROB]    Send Message");
            string text = "";
            switch (UnityEngine.Random.Range(0, 5))
            {
                case 0:
                    text = "HELP BOSS!! Benzies are trying to ROB ME!!";
                    break;
                case 1:
                    text = "BOSS!! I'm getting robbed!";
                    break;
                case 2:
                    text = "I'm being jumped, come back me up!";
                    break;
                case 3:
                    text = "Benzies set me up!! Come help quick!";
                    break;
                case 4:
                    text = "Help, boss! I'm getting ambushed!";
                    break;
                default:
                    text = "HELP BOSS!! Benzies are trying to ROB ME!!";
                    break;
            }

            dealer.MSGConversation.SendMessage(new Message(text, Message.ESenderType.Other, false, -1), true, true);
            goon.Behaviour.CombatBehaviour.DefaultWeapon = null;
            if (UnityEngine.Random.Range(0f, 1f) > 0.7f && AmbushOverrides.RangedWeapons != null && AmbushOverrides.RangedWeapons.Length > 0)
            {
                goon.Behaviour.CombatBehaviour.DefaultWeapon = AmbushOverrides.RangedWeapons[UnityEngine.Random.Range(0, AmbushOverrides.RangedWeapons.Length)];
            }
            else if (AmbushOverrides.MeleeWeapons != null && AmbushOverrides.MeleeWeapons.Length > 0)
            {
                goon.Behaviour.CombatBehaviour.DefaultWeapon = AmbushOverrides.MeleeWeapons[UnityEngine.Random.Range(0, AmbushOverrides.MeleeWeapons.Length)];
            }
            Log("[TRY ROB]    Warp to spawn");
            goon.Movement.Warp(spawnPos);
            yield return Wait05;
            if (!registered) yield break;
            Log("[TRY ROB]    Set combat target");
            dealer.Behaviour.CombatBehaviour.SetTarget(goon.GetComponent<ICombatTargetable>().NetworkObject); 
            dealer.Behaviour.CombatBehaviour.Enable_Networked(null);

            goon.Health.MaxHealth = 160f;
            goon.Health.Health = 160f;
#if MONO
            goon.AttackEntity(dealer);
#else
            goon.AttackEntity(dealer.NetworkObject.GetComponent<ICombatTargetable>());
#endif
            coros.Add(MelonCoroutines.Start(StateRobberyCombat(dealer, goon, region)));
        }
        public static IEnumerator StateRobberyCombat(Dealer dealer, CartelGoon goon, EMapRegion region)
        {
            bool changeInfluence = ShouldChangeInfluence(region);

            // While Both dealer and spawned goon are alive and conscious evaluate every sec, max timeout is 1 minute
            int maxWaitSec = 60;
            float elapsed = 0f;
            float currDistance = 159f;
            while (!dealer.Health.IsDead &&
                !dealer.Health.IsKnockedOut &&
                !goon.Health.IsDead &&
                !goon.Health.IsKnockedOut &&
                currDistance <= 160f &&
                elapsed < maxWaitSec)
            {
                yield return Wait05;
                if (!registered) yield break;
                currDistance = Vector3.Distance(Player.Local.CenterPointTransform.position, dealer.CenterPointTransform.position);
                elapsed += 0.5f;
                //Log($"In Combat:\n    Dealer:{dealer.Health.Health}\n    Goon:{goon.Health.Health}");
            }

            if (dealer.Health.IsDead || !dealer.IsConscious || dealer.Health.IsKnockedOut)
            {
                // Dealer is dead Partial rob first start with getting goon to the body
                Log("[TRY ROB]    Dealer was defeated! Initiating partial robbery.");
                goon.Behaviour.ScheduleManager.DisableSchedule();
                goon.Behaviour.activeBehaviour = null;

                // Event has to be disabled here otherwise the stay inside action will override. We pass this to the escape logic function too to re-enable on success
                NPCEvent_CartelGoonExit stayInside = null;

#if MONO
                if (goon.Behaviour.ScheduleManager.ActionList != null && goon.Behaviour.ScheduleManager.ActionList[0] != null && goon.Behaviour.ScheduleManager.ActionList[0] is NPCEvent_CartelGoonExit ev)
                {
                    stayInside = ev;

                    stayInside.End();
                    stayInside.gameObject.SetActive(false);
                }
#else
                NPCEvent_CartelGoonExit temp1;
                if (goon.Behaviour.ScheduleManager.ActionList != null)
                {
                    for (int k = 0; k < goon.Behaviour.ScheduleManager.ActionList.Count; k++)
                    {
                        temp1 = goon.Behaviour.ScheduleManager.ActionList[k].TryCast<NPCEvent_CartelGoonExit>();
                        if (temp1 != null)
                            stayInside = temp1;
                    }
                }
                if (stayInside != null)
                {
                    stayInside.End();
                    stayInside.gameObject.SetActive(false);
                }
#endif
                yield return Wait2; // wait ragdoll
                if (!registered) yield break;
                if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                {
                    coros.Add(MelonCoroutines.Start(EndBodyLootPrematurely(goon, stayInside)));
                    yield break;
                }

                goon.Movement.FacePoint(dealer.CenterPointTransform.position, lerpTime: 0.3f);
                goon.Movement.SetDestination(dealer.CenterPoint);

                float distanceToBody = 10f;
                int n = 0;
                // While not in range and traverse lasted less than 6 sec, continue
                while (distanceToBody > 2f && n < 24)
                {
                    n++;
                    yield return Wait025; // wait traverse
                    if (!registered) yield break;
                    if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                    {
                        coros.Add(MelonCoroutines.Start(EndBodyLootPrematurely(goon, stayInside)));
                        yield break;
                    }

                    distanceToBody = Vector3.Distance(goon.CenterPoint, dealer.CenterPoint);
                }
                goon.Avatar.Animation.SetCrouched(true);
                goon.Movement.Stop();
                int availableSlots = 0;
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
                    if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                    {
                        availableSlots++;
                    }
                }

                // Rob default items first from dealer
                ItemInstance temp;
                int takenSlots = 0;
                for (int i = 0; i < dealer.Inventory.ItemSlots.Count; i++)
                {
                    if (takenSlots >= availableSlots) break; // No more space in goon inventory
                    yield return Wait025;
                    if (!registered) yield break;
                    if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                    {
                        coros.Add(MelonCoroutines.Start(EndBodyLootPrematurely(goon, stayInside)));
                        yield break;
                    }

                    if (dealer.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        takenSlots++;
                        int qtyRobbed = Mathf.Max(1, Mathf.RoundToInt(dealer.Inventory.ItemSlots[i].ItemInstance.Quantity * 0.6f));
                        temp = dealer.Inventory.ItemSlots[i].ItemInstance.GetCopy(qtyRobbed); // this temp item trick maybe not functional relies on GetCopy
                        dealer.Inventory.ItemSlots[i].ChangeQuantity(-qtyRobbed, false); // is this networked
                        goon.Inventory.InsertItem(temp, true);
                    }
                }

                // Based on source code this should be done
                dealer.TryMoveOverflowItems();

                goon.SetAnimationTrigger("GrabItem");
                yield return Wait05;
                if (!registered) yield break;
                if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                {
                    coros.Add(MelonCoroutines.Start(EndBodyLootPrematurely(goon, stayInside)));
                    yield break;
                }

                if (takenSlots < availableSlots && dealer.Cash > 1f)
                {
                    // Also take cash if there is still available space
                    float qtyCashLoss = dealer.Cash * 0.6f;

                    float clamp = Mathf.Clamp(qtyCashLoss, 1f, 2000f);// For cash stack size max

                    dealer.ChangeCash(-clamp);

                    CashInstance cashInstance = NetworkSingleton<MoneyManager>.Instance.GetCashInstance(clamp);
                    // Now insert cash stack
                    for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                    {
                        yield return Wait025;
                        if (!registered) yield break;
                        if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
                        {
                            coros.Add(MelonCoroutines.Start(EndBodyLootPrematurely(goon, stayInside)));
                            yield break;
                        }

                        if (goon.Inventory.ItemSlots[i].ItemInstance == null)
                        {
                            Log($"[TRY ROB]    Inserting Cash to Slot {i}");
                            goon.Inventory.ItemSlots[i].InsertItem(cashInstance);
                            break;
                        }
                    }
                }
                Log("[TRY ROB    Finished Body Intercept]");
                goon.Avatar.Animation.SetCrouched(false);
                coros.Add(MelonCoroutines.Start(NavigateGoonEsacpe(goon, region, changeInfluence, stayInside)));
            }
            else if (goon.Health.IsDead || !goon.IsConscious || goon.Health.IsKnockedOut)
            {
                // Goon is dead or knocked out,defended robbery
                Log("[TRY ROB]    Goon was defeated! Robbery attempt defended.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);

                dealer.Behaviour.CombatBehaviour.End();

                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.robberyGoonDead);
            }
            else if (Vector3.Distance(Player.Local.CenterPointTransform.position, goon.CenterPointTransform.position) > 160f)
            {
                // Player is out of range
                Log("[TRY ROB]    Player outside of range. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);

                dealer.Behaviour.CombatBehaviour.Disable_Networked(null);
                goon.Behaviour.CombatBehaviour.Disable_Networked(null);

                coros.Add(MelonCoroutines.Start(DespawnSoon(goon, true)));
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.robberyPlayerEscape);
            }
            else if (elapsed >= 60)
            {
                Log("[TRY ROB]    State Timed Out. Dealer defends robbery.");
                dealer.MSGConversation.SendMessage(new Message(dealer.DialogueHandler.Database.GetLine(EDialogueModule.Dealer, "dealer_rob_defended"), Message.ESenderType.Other, false, -1), true, true);

                dealer.Behaviour.CombatBehaviour.Disable_Networked(null);
                goon.Behaviour.CombatBehaviour.Disable_Networked(null);

                coros.Add(MelonCoroutines.Start(DespawnSoon(goon, true)));
            }
        }

        public static IEnumerator EndBodyLootPrematurely(CartelGoon goon, NPCEvent_CartelGoonExit stayInside)
        {
            yield return Wait60;
            if (!registered) yield break;

            goon.Behaviour.ScheduleManager.EnableSchedule();

            if (stayInside != null)
            {
                stayInside.gameObject.SetActive(true);
                stayInside.Resume();
            }

            if (goon.IsGoonSpawned)
            {
                goon.Behaviour.CombatBehaviour.Disable_Networked(null);
                goon.Despawn();
            }
            goon.Health.MaxHealth = 100f;
            goon.Health.Health = 100f;
            goon.Health.Revive();

            yield return null;
        }

        public static IEnumerator DespawnSoon(CartelGoon goon, bool instant = false)
        {
            Log("[TRY ROB]    Despawned Goon");
            if (!instant)
                yield return Wait60;
            if (!registered) yield break;

            goon.Despawn();
            goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            goon.Health.MaxHealth = 100f;
            goon.Health.Health = 100f;
            goon.Health.Revive();

            yield return null;
        }
        public static IEnumerator NavigateGoonEsacpe(CartelGoon goon, EMapRegion region, bool changeInfluence, NPCEvent_CartelGoonExit stayInside)
        {
            if (!registered) yield break;
            Log("[TRY ROB]    Start Escape");
            // After succesful robbery, navigate goon towards nearest CartelDealer apartment door
            CartelDealer[] cartelDealers = UnityEngine.Object.FindObjectsOfType<CartelDealer>(true);
            float distance = 150f;
            float distCalculated = 0f;
            NPCEnterableBuilding building = null;
#if MONO
            ScheduleOne.Doors.StaticDoor door = null;
#else
            Il2CppScheduleOne.Doors.StaticDoor door = null;
#endif
            Vector3 destination = Vector3.zero;

            foreach (CartelDealer d in cartelDealers)
            {
                if (d.isInBuilding && d.CurrentBuilding != null)
                {
                    building = d.CurrentBuilding;
                    door = building.GetClosestDoor(goon.CenterPointTransform.position, false);
                    distCalculated = Vector3.Distance(door.AccessPoint.position, goon.CenterPointTransform.position);
                    if (distCalculated < distance)
                    {
                        destination = door.AccessPoint.position;
                        distance = distCalculated;
                    }
                }
            }

            if (stayInside != null)
            {
                stayInside.Door = door;
                stayInside.Building = building;
            }


            Log($"[TRY ROB]    Escaping to: {destination}");
            Log($"[TRY ROB]    Distance: {distance}");
            goon.Behaviour.ScheduleManager.EnableSchedule();
            goon.Behaviour.CombatBehaviour.Disable_Networked(null);
            goon.Behaviour.GetBehaviour("Follow Schedule").Enable();

            goon.Movement.GetClosestReachablePoint(destination, out Vector3 closest);
            coros.Add(MelonCoroutines.Start(ApplyAdrenalineRush(goon)));

            if (destination == Vector3.zero || !goon.Movement.CanGetTo(closest)) // If the destination look up fails or cant traverse to
            {
                goon.Behaviour.FleeBehaviour.SetEntityToFlee(Player.GetClosestPlayer(goon.CenterPointTransform.position, out float _).NetworkObject);
                goon.Behaviour.FleeBehaviour.Begin_Networked(null);
            }
            else
            {
                goon.Movement.SetDestination(closest);
            }

            // While not dead or escape has elapsed under 60 seconds
            float elapsedNav = 0f;
            float remainingDist = 100f;
            float currDist = 0f;
            while (elapsedNav < 60f &&
                goon.IsConscious &&
                goon.IsGoonSpawned &&
                !goon.Health.IsDead &&
                !goon.Health.IsKnockedOut &&
                !goon.isInBuilding &&
                Vector3.Distance(closest, goon.CenterPointTransform.position) > 3f)
            {
                yield return Wait05;
                if (!registered) yield break;
                currDist = Vector3.Distance(closest, goon.CenterPointTransform.position);
                if (currDist < remainingDist)
                    remainingDist = currDist;
                elapsedNav += 0.5f;
            }

            if (!goon.Health.IsDead && !goon.Health.IsKnockedOut && remainingDist < 5f)
            {
                // The goon successfully escaped.
                Log("[TRY ROB]    Goon Escaped to Cartel Dealer!");
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.robberyGoonEscapeSuccess);


                // Parse inventory after escape
                List<ItemInstance> list = new List<ItemInstance>();

#if IL2CPP
                CashInstance temp = null;
#endif
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
#if MONO
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null && goon.Inventory.ItemSlots[i].ItemInstance is CashInstance inst)
                    {
                        cartelCashAmount += inst.Balance;
                        continue;
                    }
#else
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        temp = goon.Inventory.ItemSlots[i].ItemInstance.TryCast<CashInstance>();
                        if (temp != null)
                            cartelCashAmount += temp.Balance;
                        temp = null;
                        continue;
                    }
#endif
                    // Not cash Instance, can still be product etc.

                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        int qty = Mathf.Min(goon.Inventory.ItemSlots[i].ItemInstance.Quantity, 20);
                        list.Add(goon.Inventory.ItemSlots[i].ItemInstance.GetCopy(qty));
                    }
                }
#if MONO
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, () => { goon.Inventory.Clear(); })));
#else
                void Callback()
                {
                    if (goon != null)
                        goon.Inventory.Clear();
                }
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, Callback)));
#endif
                if (goon.IsGoonSpawned)
                    goon.Despawn();
                if (goon.Behaviour.CombatBehaviour.isActiveAndEnabled)
                    goon.Behaviour.CombatBehaviour.Disable_Networked(null);

                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }

                goon.Health.MaxHealth = 100f;
                goon.Health.Health = 100f;

            }
            else if (goon.Health.IsDead || goon.Health.IsKnockedOut)
            {
                if (changeInfluence)
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, influenceConfig.robberyGoonEscapeDead);
                // The goon was defeated (dead or knocked out).
                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }
                coros.Add(MelonCoroutines.Start(DespawnSoon(goon)));
            }
            else if (elapsedNav >= 60f && goon.IsGoonSpawned)
            {
                // The escape attempt timed out.
                Log("[TRY ROB]    Despawned escaping goon due to timeout");

                // Parse inventory after escape
                List<ItemInstance> list = new List<ItemInstance>();

#if IL2CPP
                CashInstance temp = null;
#endif
                for (int i = 0; i < goon.Inventory.ItemSlots.Count; i++)
                {
#if MONO
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null && goon.Inventory.ItemSlots[i].ItemInstance is CashInstance inst)
                    {
                        cartelCashAmount += inst.Balance;
                        continue;
                    }
#else
                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        temp = goon.Inventory.ItemSlots[i].ItemInstance.TryCast<CashInstance>();
                        if (temp != null)
                            cartelCashAmount += temp.Balance;
                        temp = null;
                        continue;
                    }
#endif
                    // Not cash Instance, can still be product etc.

                    if (goon.Inventory.ItemSlots[i].ItemInstance != null)
                    {
                        int qty = Mathf.Min(goon.Inventory.ItemSlots[i].ItemInstance.Quantity, 20);
                        list.Add(goon.Inventory.ItemSlots[i].ItemInstance.GetCopy(qty));
                    }
                }
#if MONO
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, () => { goon.Inventory.Clear(); })));
#else
                void Callback()
                {
                    if (goon != null)
                        goon.Inventory.Clear();
                }
                if (list.Count > 0)
                    coros.Add(MelonCoroutines.Start(CartelStealsItems(list, Callback)));
#endif

                if (stayInside != null)
                {
                    stayInside.gameObject.SetActive(true);
                    stayInside.Resume();
                }

                goon.Behaviour.CombatBehaviour.Disable_Networked(null);

                goon.Health.MaxHealth = 100f;
                goon.Health.Health = 100f;
                if (goon.IsGoonSpawned)
                    goon.Despawn();

                Log("[TRY ROB] End");
            }
            yield return null;
        }

        // After combat goon gets adrenaline rush, getting little health regen instantly and increasing speed for 15sec ...
        public static IEnumerator ApplyAdrenalineRush(CartelGoon goon)
        {
            float origWalk = goon.Movement.WalkSpeed;
            float origRun = goon.Movement.RunSpeed;
            goon.Movement.WalkSpeed = goon.Movement.WalkSpeed * 3.5f;
            goon.Movement.RunSpeed = goon.Movement.RunSpeed * 2.5f;
            goon.Movement.MoveSpeedMultiplier = 1.4f;
            goon.Health.Health = Mathf.Round(Mathf.Lerp(goon.Health.Health, goon.Health.MaxHealth, 0.15f));

            for (int i = 0; i < 50; i++)
            {
                yield return Wait05;
                if (!registered) yield break;
                goon.Movement.WalkSpeed = Mathf.Lerp(goon.Movement.WalkSpeed, origWalk, 0.025f);
                goon.Movement.RunSpeed = Mathf.Lerp(goon.Movement.RunSpeed, origRun, 0.025f);
                goon.Movement.MoveSpeedMultiplier = Mathf.Lerp(goon.Movement.MoveSpeedMultiplier, 1f, 0.025f);
            }

            goon.Movement.WalkSpeed = origWalk;
            goon.Movement.RunSpeed = origRun;
            goon.Movement.MoveSpeedMultiplier = 1f;
        }
    }
}
