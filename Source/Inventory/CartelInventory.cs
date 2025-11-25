using System.Collections;
using UnityEngine;

using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.Product.Packaging;
using ScheduleOne.ObjectScripts;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
#else
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
#endif

namespace CartelEnforcer
{
    [Serializable]
    public class SerializeStolenItems
    {
        public string ID;
        public int Quality;
        public int Quantity;
    }
    [Serializable]
    public class StolenItemsList
    {
        public float balance = 0f;
        public List<SerializeStolenItems> items;
    }

    public static class CartelInventory
    {

        public static List<QualityItemInstance> cartelStolenItems = new();
        public static float cartelCashAmount = 0f;
        public static readonly object cartelItemLock = new object(); // for above list

        public static PackagingDefinition jarPackaging = null;
        public static PackagingDefinition brickPackaging = null;

        public static readonly int brickQuantity = 20;
        public static readonly int jarQuantity = 5;

        public static void PreparePackagingRefs()
        {
            // parse needed nob for bricks definition
            NetworkManager netManager = UnityEngine.Object.FindObjectOfType<NetworkManager>(true);
            PrefabObjects spawnablePrefabs = netManager.SpawnablePrefabs;

            NetworkObject nobBrickPress = null;

            for (int i = 0; i < spawnablePrefabs.GetObjectCount(); i++)
            {
                NetworkObject prefab = spawnablePrefabs.GetObject(true, i);
                if (prefab?.gameObject?.name == "BrickPress")
                {
                    nobBrickPress = prefab;
                }
            }

            BrickPress brickPressComp = nobBrickPress.GetComponent<BrickPress>();
            if (brickPressComp != null && brickPressComp.BrickPackaging != null)
            {
                Log("Assigned brick packaging");
                brickPackaging = brickPressComp.BrickPackaging;
            }

            // jars definition
            Func<string, ItemDefinition> GetItem;

#if MONO
            GetItem = ScheduleOne.Registry.GetItem;
#else
            GetItem = Il2CppScheduleOne.Registry.GetItem;
#endif

            ItemDefinition def = GetItem("jar");
            if (def == null)
            {
                Log("Failed to find Jar definition");
                return;
            }
            else
            {
#if MONO
                if (def is PackagingDefinition jarDef)
                {
                    Log("Assigned jar packaging");
                    jarPackaging = jarDef;
                }
#else
                PackagingDefinition temp = def.TryCast<PackagingDefinition>();
                if (temp != null)
                {
                    jarPackaging = temp;
                }
#endif
            }


        }

        public static IEnumerator CartelStealsItems(List<ItemInstance> items, Action cb = null)
        {
            lock (cartelItemLock)
            {
#if MONO
                for (int i = 0; i < items.Count; i++)
                {
                    int realQty = 1;
                    if (items[i] is QualityItemInstance inst)
                    {
                        // Search for existing 
                        int foundIdx = -1;
                        if (cartelStolenItems.Count > 0)
                        {
                            for (int j = 0; j < cartelStolenItems.Count; j++)
                            {
                                if (cartelStolenItems[j].ID == inst.ID && cartelStolenItems[j].Quality == inst.Quality)
                                {
                                    foundIdx = j;
                                    break;
                                }
                            }
                        }
                        // Is packaging, jars + 5qty, brick +20
                        if (items[i] is ProductItemInstance packin)
                        {
                            if (packin != null && packin.PackagingID != null)
                            {
                                switch (packin.PackagingID)
                                {
                                    case "jar":
                                        realQty = jarQuantity;
                                        break;
                                    case "brick":
                                        realQty = brickQuantity;
                                        break;
                                    default:
                                        realQty = 1;
                                        break;
                                }
                            }
                        }

                        if (foundIdx >= 0) // Exists in already stolen items
                        {
                            Log($"[CARTEL INV]    EXISTS ADD: {inst.ID} x {inst.Quantity * realQty}");
                            cartelStolenItems[foundIdx].Quantity += inst.Quantity * realQty;
                        }
                        else // not exist
                        {
                            Log($"[CARTEL INV]    ADD: {items[i].ID} x {inst.Quantity * realQty}");
                            inst.Quantity = inst.Quantity * realQty;
                            cartelStolenItems.Add(inst);
                        }
                    }
                }
#else
                for (int i = 0; i < items.Count; i++)
                {
                    int realQty = 1;

                    QualityItemInstance tempQt = items[i].TryCast<QualityItemInstance>();

                    if (tempQt != null)
                    {
                        // Search for existing 
                        int foundIdx = -1;
                        if (cartelStolenItems.Count > 0)
                        {
                            for (int j = 0; j < cartelStolenItems.Count; j++)
                            {
                                if (cartelStolenItems[j].ID == tempQt.ID && cartelStolenItems[j].Quality == tempQt.Quality)
                                {
                                    foundIdx = j;
                                    Log($"[CARTEL INV]    Item Already exists, append");
                                    break;
                                }
                            }
                        }
                        ProductItemInstance pTemp = items[i].TryCast<ProductItemInstance>();
                        
                        // Is packaging, jars + 5qty, brick +20
                        if (pTemp != null)
                        {
                            if (pTemp.PackagingID != null)
                            {
                                switch (pTemp.PackagingID)
                                {
                                    case "jar":
                                        realQty = jarQuantity;
                                        break;
                                    case "brick":
                                        realQty = brickQuantity;
                                        break;
                                    default:
                                        realQty = 1;
                                        break;
                                }
                                Log($"[CARTEL INV]   Real Qty: {pTemp.ID} = {realQty}");
                            }
                        }

                        if (foundIdx >= 0) // Exists in already stolen items
                        {
                            Log($"[CARTEL INV]    EXISTS ADD: {items[i].Name}x{tempQt.Quantity * realQty}");
                            cartelStolenItems[foundIdx].Quantity += tempQt.Quantity * realQty;
                        }
                        else // not exist
                        {
                            Log($"[CARTEL INV]    ADD: {items[i].Name}x{tempQt.Quantity * realQty}");
                            tempQt.Quantity = tempQt.Quantity * realQty;
                            cartelStolenItems.Add(tempQt);
                        }
                    }
                }
#endif
            }
            if (cb != null)
                cb();
            yield return null;
        }

        // From stolen items return maximum packaged amount that slot can hold
        // To prevent mass surplus of items e.g. 20 bricks max from the pool
        // the function lerps linearly with the percentage of stolen items
        // so that the inventory avoids clearing itself out whenever
        // item quantities exceed jar and brick quantity
        public static List<ItemInstance> GetFromPool(int maxEmptySlotsToFill)
        {
            List<ItemInstance> fromPool = new();
#if IL2CPP
            QualityItemInstance qtInst;
#endif
            lock (cartelItemLock)
            {
                int slotsToFill = Mathf.Min(maxEmptySlotsToFill, cartelStolenItems.Count);
                int randomIndex;
                int takenQty;

                for (int i = 0; i < slotsToFill; i++)
                {
                    if (cartelStolenItems.Count == 0) break;
                    randomIndex = UnityEngine.Random.Range(0, cartelStolenItems.Count);
#if MONO
                    ItemDefinition def = ScheduleOne.Registry.GetItem(cartelStolenItems[randomIndex].ID);
#else
                    ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(cartelStolenItems[randomIndex].ID);
#endif
                    // Quotient from division by known packaging quantities jars bricks
                    // Max slot qty of 20 with mathfmin
                    int brickQuotient = Mathf.Min(Mathf.FloorToInt(cartelStolenItems[randomIndex].Quantity / brickQuantity), 20);
                    int jarQuotient = Mathf.Min(Mathf.FloorToInt(cartelStolenItems[randomIndex].Quantity / jarQuantity), 20);

                    ItemInstance item = null;
                    PackagingDefinition packagingDef = null;

                    // Start packing with bricks when inventory has this item quantity of atleast 100
                    if (brickQuotient > 5)
                    {
                        takenQty = brickQuantity * brickQuotient;
                        float percentageOfTotal = takenQty / cartelStolenItems[randomIndex].Quantity;
                        int fixedQuotient = Mathf.RoundToInt(
                            Mathf.Lerp(
                                (float)brickQuotient,
                                Mathf.Clamp((float)brickQuotient / 2f, 1f, 20f), // half of what quotient originally allowed
                                percentageOfTotal
                                )
                            );
                        takenQty = brickQuantity * fixedQuotient;
                        item = def.GetDefaultInstance(fixedQuotient);
                        packagingDef = CartelInventory.brickPackaging;
                    }
                    // Else item quantity under 100 packs to jars until 5
                    else if (jarQuotient > 1) 
                    {
                        takenQty = jarQuantity * jarQuotient;
                        float percentageOfTotal = takenQty / cartelStolenItems[randomIndex].Quantity;
                        int fixedQuotient = Mathf.RoundToInt(
                            Mathf.Lerp(
                                (float)jarQuotient,
                                Mathf.Clamp((float)jarQuotient / 2f, 1f, 20f), // half of what quotient originally allowed
                                percentageOfTotal
                                )
                            );
                        takenQty = jarQuantity * fixedQuotient;
                        item = def.GetDefaultInstance(fixedQuotient);
                        packagingDef = CartelInventory.jarPackaging;
                    }
                    // Else raw quantity 1 unpackaged
                    else
                    {
                        takenQty = Mathf.Min(cartelStolenItems[randomIndex].Quantity, 1);
                        item = def.GetDefaultInstance(takenQty);
                    }


                    // Apply packaging if it was selected
                    if (packagingDef != null)
                    {
#if MONO
                        if (item is ProductItemInstance product)
                        {
                            product.SetPackaging(packagingDef);
                        }
#else
                        ProductItemInstance temp = item.TryCast<ProductItemInstance>();
                        if (temp != null)
                        {
                            temp.SetPackaging(packagingDef);
                        }
#endif
                    }

                    // Change quality
#if MONO
                    if (item is QualityItemInstance inst)
                        inst.Quality = cartelStolenItems[randomIndex].Quality;
#else
                    qtInst = item.TryCast<QualityItemInstance>();
                    if (qtInst != null)
                        qtInst.Quality = cartelStolenItems[randomIndex].Quality;
#endif

                    fromPool.Add(item);

                    if (takenQty >= cartelStolenItems[randomIndex].Quantity)
                        cartelStolenItems.RemoveAt(randomIndex);
                    else
                        cartelStolenItems[randomIndex].Quantity -= takenQty;
                }
            }
            return fromPool;
        }
    }
}
