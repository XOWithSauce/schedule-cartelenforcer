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
using ScheduleOne.NPCs.Other;
using ScheduleOne.NPCs;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Combat;
#else
using Il2Cpp;
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
#endif

namespace CartelEnforcer
{
    public static class CartelGathering
    {
        public static List<GatheringLocation> gatheringLocations = new()
        {
            new GatheringLocation(new Vector3(151.39f, 2.18f, -16.93f), 5), // region 5 uptown
            new GatheringLocation(new Vector3(92.06f, 5.61f, -128.88f), 4), // region 4 suburbia
            new GatheringLocation(new Vector3(13.50f, 1.31f, -77.06f), 2),  // region 2 downtown
            new GatheringLocation(new Vector3(-53.69f, -1.14f, -84.46f), 3), // region 3 docks
            new GatheringLocation(new Vector3(-29.78f, -3.99f, -9.49f), 3),  // region 3 docks
            new GatheringLocation(new Vector3(-66.38f, -1.14f, 21.06f), 3),  // region 3 docks
            new GatheringLocation(new Vector3(-139.19f, -2.57f, 71.38f), 1), // region 1 westville
            new GatheringLocation(new Vector3(-175.95f, -2.54f, 81.69f), 1), // region 1 westville
            new GatheringLocation(new Vector3(-8.86f, 1.56f, 79.54f), 2),   // region 2 downtown
            new GatheringLocation(new Vector3(-62.84f, -3.64f, 166.34f), 1)  // region 1 westville
        };

        public static bool areGoonsGathering = false;
        public static int hoursUntilNextGathering = 3;
        public static List<CartelGoon> spawnedGatherGoons = new();
        public static bool startedCombat = false;

        public static void OnHourPassTryGather()
        {
            coros.Add(MelonCoroutines.Start(TryStartGathering()));
        }
        public static IEnumerator TryStartGathering()
        {
            if (areGoonsGathering) yield break;

            // Only when hostile
#if MONO
            if (NetworkSingleton<Cartel>.Instance.Status != ECartelStatus.Hostile) yield break;
#else
            if (NetworkSingleton<Cartel>.Instance.Status != Il2Cpp.ECartelStatus.Hostile) yield break;

#endif

            Log("[GATHERING] Try start gathering");
            hoursUntilNextGathering = Mathf.Clamp(hoursUntilNextGathering - 1, 0, 36);
            if (hoursUntilNextGathering > 0) yield break;


            int startEarliest = 0;
            int startLatest = 0;
            if (DealerActivity.currentDealerActivity >= 0f)
            {
                hoursUntilNextGathering = UnityEngine.Random.Range(12, 36);
                startEarliest = 1200;
                startLatest = 1600;
            }
            else
            {
                hoursUntilNextGathering = UnityEngine.Random.Range(6, 12);
                startEarliest = 1000;
                startLatest = 2000;
            }

            Log("Gather time window: " + startEarliest + " - " + startLatest);
            if (TimeManager.Instance.CurrentTime >= startEarliest && TimeManager.Instance.CurrentTime <= startLatest)
            {
                areGoonsGathering = true;
                GatheringLocation location = gatheringLocations[UnityEngine.Random.Range(0, gatheringLocations.Count)];
                Log("Spawning Gather at: " + location.position.ToString());
                float offsetFromCenter = 0.62f;
                Vector3 spawnPos1 = location.position + Vector3.forward * offsetFromCenter;
                Vector3 spawnPos2 = location.position + Vector3.right * offsetFromCenter;
                Vector3 spawnPos3 = location.position + Vector3.left * offsetFromCenter;

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

                DrinkItem drinkAct = spawnedGatherGoons[0].transform.Find("Aux/Drink").GetComponent<DrinkItem>();
                drinkAct.Begin();

                SmokeCigarette smokeAct = spawnedGatherGoons[1].transform.Find("Aux/SmokeCigarette").GetComponent<SmokeCigarette>();
                smokeAct.Begin();

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

                if (elapsed > 120 || dead == 3)
                {
                    break;
                }

                if (!startedCombat)
                {
                    // Check if dealer activity threshold is met
                    if (DealerActivity.currentDealerActivity < 0f)
                    {
                        // Based on the dealer activity increase the radius which goons become aggressive at
                        float distanceToAggroAt = Mathf.Lerp(6f, 18f, -DealerActivity.currentDealerActivity);

                        // Now we can check if player is nearby
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, location.position) < distanceToAggroAt)
                        {
                            // Player is nearby now we check that random one of the goons can see the player, very low dealer activity will trigger without los
                            bool ignoreLos = false;
                            if (DealerActivity.currentDealerActivity < -0.5f)
                                ignoreLos = true;

                            Player p = Player.GetClosestPlayer(location.position, out float _);
                            if (spawnedGatherGoons[UnityEngine.Random.Range(0, spawnedGatherGoons.Count)].Awareness.VisionCone.IsPointWithinSight(Player.Local.CenterPointTransform.position, ignoreLos))
                            {
                                spawnedGatherGoons[0].AttackEntity(p.GetComponent<ICombatTargetable>()); // this will trigger all of them
                            }
                        }

                    }
                    // else 0 or higher
                    else
                    {
                        // Based on the dealer activity increase the radius which goons become annoyed at
                        float distanceToGetAnnoyedAt = Mathf.Lerp(8f, 4f, DealerActivity.currentDealerActivity);
                        int maxAnnoyance = Mathf.RoundToInt(Mathf.Lerp(3f, 6f, DealerActivity.currentDealerActivity));
                        // Now we can check if player is nearby
                        if (Vector3.Distance(Player.Local.CenterPointTransform.position, location.position) < distanceToGetAnnoyedAt)
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
#if MONO
                                spawnedGatherGoons[randomIndex].PlayVO(ScheduleOne.VoiceOver.EVOLineType.Annoyed, true);
#else
                                spawnedGatherGoons[randomIndex].PlayVO(Il2CppScheduleOne.VoiceOver.EVOLineType.Annoyed, true);
#endif
                            }
                            else if (annoyance > 1)
                            {
                                Player p = Player.GetClosestPlayer(location.position, out float _);
                                int randomIndex = UnityEngine.Random.Range(0, spawnedGatherGoons.Count);
                                spawnedGatherGoons[randomIndex].Movement.FacePoint(p.CenterPointTransform.position, 1.4f);
#if MONO
                                spawnedGatherGoons[randomIndex].PlayVO(ScheduleOne.VoiceOver.EVOLineType.Angry, true);
#else
                                spawnedGatherGoons[randomIndex].PlayVO(Il2CppScheduleOne.VoiceOver.EVOLineType.Angry, true);
#endif
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
                            spawnedGatherGoons[randomIndex].Movement.FacePoint(location.position);
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

            bool gatheringDefeated = (dead == 3 && elapsed < 120);
            coros.Add(MelonCoroutines.Start(EndGatherEvent(gatheringDefeated, location)));

            yield return null;
        }
        public static IEnumerator EndGatherEvent(bool defeated, GatheringLocation location)
        {
            EMapRegion region = EMapRegion.Northtown;
            switch (location.region)
            {
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
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, -0.025f);
            }
            else
            {
                if (ShouldChangeInfluence(region))
                    NetworkSingleton<Cartel>.Instance.Influence.ChangeInfluence(region, 0.005f);
            }

            yield return Wait30;
            foreach (CartelGoon goon in spawnedGatherGoons)
            {
                yield return Wait05;
                goon.Health.MaxHealth = 100f;
                goon.Health.Revive();
                goon.Despawn();
            }

            startedCombat = false;
            areGoonsGathering = false;
            spawnedGatherGoons.Clear();

        }

    }

    public class GatheringLocation
    {
        public GatheringLocation(Vector3 pos, int region) 
        {
            this.position = pos;
            this.region = region;
        }
        public Vector3 position;
        public int region;
    }
}