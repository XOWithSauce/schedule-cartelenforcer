using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.CartelInventory;
using static CartelEnforcer.DebugModule;
using static CartelEnforcer.InfluenceOverrides;
using static CartelEnforcer.InterceptEvent;
using static CartelEnforcer.DealerActivity;

using static UnityEngine.InputSystem.Controls.AxisControl;



#if MONO
using ScheduleOne.Cartel;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.ItemFramework;
using ScheduleOne.Map;
using ScheduleOne.Money;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Persistence.Datas;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
using ScheduleOne.UI;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.NPCs.Other;
using ScheduleOne.NPCs;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Combat;
using ScheduleOne.VoiceOver;
#else
using Il2Cpp;
using Il2CppScheduleOne.Money;
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
using Il2CppScheduleOne.NPCs.Other;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.VoiceOver;

#endif

namespace CartelEnforcer
{
    public static class CartelGathering
    {
        public static Dictionary<int, List<GatheringLocation>> gatheringLocationsByRegion = new()
        {
            // Region 5: Uptown
            { 5, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(151.39f, 2.18f, -16.93f), 5, "forest next to the barn in the Uptown region."),
                    new GatheringLocation(new Vector3(126.84f, 1.46f, 55.52f), 5, "parking lot infront of the Church in the Uptown region."),
                    new GatheringLocation(new Vector3(126.84f, 1.46f, 55.52f), 5, "alley between the Hyland towers.")
                }
            },
            // Region 4: Suburbia
            { 4, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(92.06f, 5.61f, -128.88f), 4, "park in the Suburbia region."),
                    new GatheringLocation(new Vector3(13.50f, 1.31f, -77.06f), 4, "broken RV in the Suburbia region.")
                }
            },
            // Region 3: Docks
            { 3, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(-53.69f, -1.14f, -84.46f), 3, "shipping containers in the Docks region."),
                    new GatheringLocation(new Vector3(-29.78f, -3.99f, -9.49f), 3, "canal in the Docks region."),
                    new GatheringLocation(new Vector3(-66.38f, -1.14f, 21.06f), 3, "southern wharf next to warehouse.")
                }
            },
            // Region 2: Downtown
            { 2, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(43.65f, 1.46f, 24.20f), 2, "town hall."),
                    new GatheringLocation(new Vector3(-8.86f, 1.56f, 79.54f), 2, "parking garage in the Downtown region."),
                    new GatheringLocation(new Vector3(23.46f, 1.46f, 84.16f), 2, "casino in the Downtown region.")
                }
            },
            // Region 1: Westville
            { 1, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(-139.19f, -2.57f, 71.38f), 1, "Top Tattoo in the Westville region."),
                    new GatheringLocation(new Vector3(-175.95f, -2.54f, 81.69f), 1, "brown apartment block parking lot."),
                    new GatheringLocation(new Vector3(-97.42f, -2.72f, 60.70f), 1, "small RV next to western gas mart.")
                }
            },
            // Region 0: Northtown
            { 0, new List<GatheringLocation>
                {
                    new GatheringLocation(new Vector3(-62.84f, -3.64f, 166.34f), 0, "northern wharf in the Northtown region."),
                    new GatheringLocation(new Vector3(-86.51f, -4.12f, 108.41f), 0, "western canal in the Northtown region."),
                    new GatheringLocation(new Vector3(-86.51f, -4.12f, 108.41f), 0, "area in front of Thompson's Construction"),
                    new GatheringLocation(new Vector3(-76.09f, -2.54f, 140.60f), 0, "basketball court in the Northtown region.")
                }
            }
        };

        public static bool areGoonsGathering = false;
        public static int hoursUntilNextGathering = 3;
        public static List<CartelGoon> spawnedGatherGoons = new();
        public static bool startedCombat = false;
        public static GatheringLocation currentGatheringLocation = null;
        public static GatheringLocation previousGatheringLocation = null;
        public static void OnHourPassTryGather()
        {
            coros.Add(MelonCoroutines.Start(TryStartGathering()));
        }
        public static IEnumerator TryStartGathering()
        {
            if (areGoonsGathering) yield break;

            Log("[GATHERING] Try start gathering");
            hoursUntilNextGathering = Mathf.Clamp(hoursUntilNextGathering - 1, 0, 18);
            if (hoursUntilNextGathering > 0) yield break;


            int startEarliest = 0;
            int startLatest = 0;
            if (DealerActivity.currentDealerActivity >= 0f)
            {
                startEarliest = 1200;
                startLatest = 1800;
            }
            else
            {
                startEarliest = 1000;
                startLatest = 2000;
            }

            Log("Gather time window: " + startEarliest + " - " + startLatest);
            if (TimeManager.Instance.CurrentTime >= startEarliest && TimeManager.Instance.CurrentTime <= startLatest)
            {
                if (DealerActivity.currentDealerActivity >= 0f)
                    hoursUntilNextGathering = UnityEngine.Random.Range(6, 15);
                else if (DealerActivity.currentDealerActivity < 0f && DealerActivity.currentDealerActivity > -0.5f)
                    hoursUntilNextGathering = UnityEngine.Random.Range(5, 12);
                else
                    hoursUntilNextGathering = UnityEngine.Random.Range(4, 9);

                List<GatheringLocation> candidates = new();
                List<GatheringLocation> regLocs = null;
                foreach (EMapRegion reg in Singleton<Map>.Instance.GetUnlockedRegions())
                {
                    gatheringLocationsByRegion.TryGetValue((int)reg, out regLocs);
                    if (regLocs != null)
                    {
                        foreach (GatheringLocation loc in regLocs)
                        {
                            yield return Wait01;
                            if (previousGatheringLocation != null && loc != previousGatheringLocation && !candidates.Contains(loc))
                            {
                                // check if it was prev
                                candidates.Add(loc);
                            }
                            else if (!candidates.Contains(loc)) // because previous can be null on start
                            {
                                candidates.Add(loc);
                            }
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    Log(" Failed to parse Gathering location candidates");
                    candidates.AddRange(gatheringLocationsByRegion[0]); // default to adding all pos from first region
                }

                GatheringLocation location = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                currentGatheringLocation = location;
                previousGatheringLocation = location;
                areGoonsGathering = true;
                Log("Spawning Gather at: " + location.position.ToString());
                float offsetFromCenter = 0.67f;
                Vector3 spawnPos1 = location.position + Vector3.forward * (offsetFromCenter + UnityEngine.Random.Range(-0.05f, 0.05f));
                Vector3 spawnPos2 = location.position + Vector3.right * (offsetFromCenter + UnityEngine.Random.Range(-0.05f, 0.05f));
                Vector3 spawnPos3 = location.position + Vector3.left * (offsetFromCenter + UnityEngine.Random.Range(-0.05f, 0.05f));

                if (NetworkSingleton<Cartel>.Instance.GoonPool.UnspawnedGoonCount < 3)
                {
                    do
                    {
                        yield return Wait05;
#if MONO
                        NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.FirstOrDefault().Health.Revive();
                        NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.FirstOrDefault().Despawn();
#else
                        int count = NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons.Count - 1;
                        if (count != -1)
                        {
                            NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons[count].Health.Revive();
                            NetworkSingleton<Cartel>.Instance.GoonPool.spawnedGoons[count].Despawn();
                        }
                        else
                        {
                            break;
                        }
#endif
                    } while (NetworkSingleton<Cartel>.Instance.GoonPool.UnspawnedGoonCount < 3);
                }

                spawnedGatherGoons.Add(NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos1));
                spawnedGatherGoons.Add(NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos2));
                spawnedGatherGoons.Add(NetworkSingleton<Cartel>.Instance.GoonPool.SpawnGoon(spawnPos3));

                void CombatStarted()
                {
                    if (startedCombat) return;
                    startedCombat = true;
                    Player p = Player.GetClosestPlayer(location.position, out float _);
                    foreach (CartelGoon goon in spawnedGatherGoons)
                    {
                        goon.Behaviour.CombatBehaviour.onBegin.RemoveListener((UnityEngine.Events.UnityAction)CombatStarted);
                        goon.Behaviour.CombatBehaviour.SetWeapon("Avatar/Equippables/M1911");
                        if (goon.Behaviour.CombatBehaviour.DefaultWeapon == null && goon.Behaviour.CombatBehaviour.currentWeapon != null)
                            goon.Behaviour.CombatBehaviour.DefaultWeapon = goon.Behaviour.CombatBehaviour.currentWeapon;

                        goon.AttackEntity(p.GetComponent<ICombatTargetable>());
                        
                    }
                }

                for (int i = 0; i < spawnedGatherGoons.Count; i++)
                {
                    spawnedGatherGoons[i].Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(false);
                    spawnedGatherGoons[i].Behaviour.ScheduleManager.DisableSchedule();
                    spawnedGatherGoons[i].Movement.FacePoint(location.position);
                    spawnedGatherGoons[i].Behaviour.CombatBehaviour.onBegin.AddListener((UnityEngine.Events.UnityAction)CombatStarted);
                    if (DealerActivity.currentDealerActivity < 0f)
                    {
                        // increase hp
                        spawnedGatherGoons[i].Health.MaxHealth = Mathf.Lerp(100f, 250f, -DealerActivity.currentDealerActivity);
                        spawnedGatherGoons[i].Health.Health = Mathf.Lerp(100f, 250f, -DealerActivity.currentDealerActivity);
                    }

                }
                // Fill random attendant inv slot with stolen item
                List<ItemInstance> itemsFromPool = GetFromPool(3);
                if (itemsFromPool.Count > 0)
                {
                    int lootGoblinIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                    spawnedGatherGoons[lootGoblinIndex].Inventory.Clear();
                    foreach (ItemInstance item in itemsFromPool)
                    {
                        if (spawnedGatherGoons[lootGoblinIndex].Inventory.CanItemFit(item))
                        {
                            spawnedGatherGoons[lootGoblinIndex].Inventory.InsertItem(item);
                        }
                    }
                }

                // Fill all attendants inv with stolen money
                if (cartelCashAmount > 2500f) // 1k overhead
                {
                    foreach (CartelGoon goon in spawnedGatherGoons)
                    {
                        CashInstance cashInstance = NetworkSingleton<MoneyManager>.Instance.GetCashInstance(500f);
                        if (goon.Inventory.CanItemFit(cashInstance))
                        {
                            goon.Inventory.InsertItem(cashInstance);
                            cartelCashAmount -= 500f;
                        }
                    }
                }


                // Drink / Smoke animations on random basis
                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                {
                    DrinkItem drinkAct = spawnedGatherGoons[0].transform.Find("Aux/Drink").GetComponent<DrinkItem>();
                    drinkAct.Begin();
                }

                if (UnityEngine.Random.Range(0f, 1f) > 0.2f)
                {
                    SmokeCigarette smokeAct = spawnedGatherGoons[1].transform.Find("Aux/SmokeCigarette").GetComponent<SmokeCigarette>();
                    smokeAct.Begin();
                }


                Log("[GATHERING] Gathering Spawned at: " + location.position.ToString());
                coros.Add(MelonCoroutines.Start(EvaluateCurrentGathering(location)));
            }

            yield return null;
        }
        public static IEnumerator EvaluateCurrentGathering(GatheringLocation location)
        {
            yield return Wait2;
            int elapsed = 0;
            int dead = 0;
            int annoyance = 0;
            int deltaSecondsForAnnoyance = 0;
            int deltaSecondsDecrAnnoyance = 0;

            while (registered)
            {
                dead = 0;
                // Check goon status
                foreach (CartelGoon goon in spawnedGatherGoons)
                {
                    if (goon.Health.IsDead || goon.Health.IsKnockedOut)
                        dead++;
                }

                if (elapsed > 180 || dead == 3)
                {
                    break;
                }

                if (!startedCombat)
                {
                    float distToP = Vector3.Distance(Player.Local.CenterPointTransform.position, location.position);
                    bool playerInBuilding = false;
                    if (Player.Local.CurrentProperty != null)
                        playerInBuilding = true;

                    // Check if dealer activity threshold is met
                    if (DealerActivity.currentDealerActivity < 0f)
                    {
                        // Based on the dealer activity increase the radius which goons become aggressive at
                        float distanceToAggroAt = Mathf.Lerp(8f, 18f, -DealerActivity.currentDealerActivity);

                        // Now we can check if player is nearby
                        if (distToP < distanceToAggroAt && !playerInBuilding)
                        {
                            // Player is nearby now we check that random one of the goons can see the player, very low dealer activity will trigger without los
                            Player p = Player.GetClosestPlayer(location.position, out float _);
                            int randomIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                            spawnedGatherGoons[randomIndex].Movement.FacePoint(p.CenterPointTransform.position);
                            yield return Wait05;
                            if (spawnedGatherGoons[randomIndex].Awareness.VisionCone.IsPlayerVisible(p))
                            {
                                spawnedGatherGoons[randomIndex].AttackEntity(p.GetComponent<ICombatTargetable>()); // this will trigger all of them
                            }
                            else
                            {
                                yield return Wait05;
                                spawnedGatherGoons[randomIndex].Movement.FacePoint(location.position);
                            }
                        }

                    }
                    // else 0 or higher
                    else
                    {
                        // Based on the dealer activity increase the radius which goons become annoyed at
                        float distanceToGetAnnoyedAt = Mathf.Lerp(9f, 5f, DealerActivity.currentDealerActivity);
                        float maxAnnoyance = Mathf.RoundToInt(Mathf.Lerp(3f, 6f, DealerActivity.currentDealerActivity));
                        if (distToP < distanceToGetAnnoyedAt && !playerInBuilding)
                        {
                            deltaSecondsForAnnoyance += 2;
                            if (deltaSecondsForAnnoyance >= 4)
                            {
                                annoyance++;
                                deltaSecondsForAnnoyance = 0;
                            }

                            if (annoyance == 1)
                            {
                                Player p = Player.GetClosestPlayer(location.position, out float _);
                                int randomIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                                spawnedGatherGoons[randomIndex].Movement.FacePoint(p.CenterPointTransform.position, 1.4f);
                                spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.Annoyed, true);
                            }
                            else if (annoyance > 1)
                            {
                                Player p = Player.GetClosestPlayer(location.position, out float _);
                                int randomIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                                spawnedGatherGoons[randomIndex].Movement.FacePoint(p.CenterPointTransform.position, 1.4f);
                                spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.Angry, true);
                                spawnedGatherGoons[randomIndex].Avatar.EmotionManager.AddEmotionOverride("Annoyed", "product_rejected", 6f, 1);
                                if (annoyance >= maxAnnoyance)
                                {
                                    spawnedGatherGoons[randomIndex].AttackEntity(p.GetComponent<ICombatTargetable>());
                                }
                            }
                        }
                        else // Not in range decrease annoyance overtime
                        {
                            deltaSecondsDecrAnnoyance += 2;
                            if (deltaSecondsDecrAnnoyance >= 8)
                            {
                                annoyance = Mathf.Clamp(annoyance - 1, 0, 8);
                                deltaSecondsDecrAnnoyance = 0;
                            }
                            int randomIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);

                            // Only run the interaction if player is nearby otherwise it just wastes memory??
                            if (distToP < 30f)
                            {
                                // Some extra shit just random voicelines to make it feel like they actually talk..
                                if (UnityEngine.Random.Range(0f, 1f) > 0.8f)
                                {
                                    int randomLookAtIndex;
                                    do
                                    {
                                        yield return Wait01;
                                        randomLookAtIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                                    } while (randomIndex == randomLookAtIndex);

                                    spawnedGatherGoons[randomIndex].Movement.FacePoint(spawnedGatherGoons[randomLookAtIndex].CenterPoint);
                                    switch (UnityEngine.Random.Range(0, 4))
                                    {
                                        case 0:
                                            spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.Acknowledge, true);
                                            break;

                                        case 1:
                                            spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.No, true);
                                            break;

                                        case 2:
                                            spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.Think, true);
                                            break;

                                        case 3:
                                            spawnedGatherGoons[randomIndex].PlayVO(EVOLineType.Question, true);
                                            break;
                                    } // talk
                                    yield return Wait05;
                                    switch (UnityEngine.Random.Range(0, 3))
                                    {
                                        case 0:
                                            spawnedGatherGoons[randomLookAtIndex].PlayVO(EVOLineType.Thanks, true);
                                            break;

                                        case 1:
                                            spawnedGatherGoons[randomLookAtIndex].PlayVO(EVOLineType.Surprised, true);
                                            break;

                                        case 2:
                                            spawnedGatherGoons[randomLookAtIndex].PlayVO(EVOLineType.Command, true);
                                            break;
                                    } // other one responds
                                }
                                else
                                {
                                    spawnedGatherGoons[randomIndex].Movement.FacePoint(location.position);
                                }
                            }
                            else
                            {
                                spawnedGatherGoons[randomIndex].Movement.FacePoint(location.position);
                            }

                        }
                    }
                }

                yield return Wait2;
                elapsed += 2;
            }

            foreach (CartelGoon goon in spawnedGatherGoons)
            {
                yield return Wait05;
                goon.Behaviour.ScheduleManager.ActionList[0].gameObject.SetActive(true);
                goon.Behaviour.ScheduleManager.EnableSchedule();
            }

            bool gatheringDefeated = (dead == 3 && elapsed < 180);
            coros.Add(MelonCoroutines.Start(EndGatherEvent(gatheringDefeated, location)));

            yield return null;
        }
        public static IEnumerator EndGatherEvent(bool defeated, GatheringLocation location)
        {
            EMapRegion region = EMapRegion.Northtown;
            switch (location.region)
            {
                case 0:
                    region = EMapRegion.Northtown;
                    break;
                case 1:
                    region = EMapRegion.Westville;
                    break;
                case 2:
                    region = EMapRegion.Downtown;
                    break;
                case 3:
                    region = EMapRegion.Docks;
                    break;
                case 4:
                    region = EMapRegion.Suburbia;
                    break;
                case 5:
                    region = EMapRegion.Uptown;
                    break;
            }

            if (defeated)
            {
                if (ShouldChangeInfluence(region))
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.100f);
            }
            else
            {
                if (region != EMapRegion.Northtown)
                {
                    if (NetworkSingleton<Cartel>.Instance.Influence.GetRegionData(region).Influence < 0.400f)
                    {
                        if (ShouldChangeInfluence(region))
                            NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.025f);
                    }
                }
            }

            yield return Wait30;
            foreach (CartelGoon goon in spawnedGatherGoons)
            {
                yield return Wait05;
                goon.Health.MaxHealth = 100f;
                goon.Health.Revive();
                if (goon.IsGoonSpawned)
                    goon.Despawn();
            }

            startedCombat = false;
            areGoonsGathering = false;
            spawnedGatherGoons.Clear();
            currentGatheringLocation = null;
        }

    }

    public class GatheringLocation
    {
        public GatheringLocation(Vector3 pos, int region, string desc) 
        {
            this.position = pos;
            this.region = region;
            this.description = desc;
        }
        public Vector3 position;
        public int region;
        public string description;
    }
}