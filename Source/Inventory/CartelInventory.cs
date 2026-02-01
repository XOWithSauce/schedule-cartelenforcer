using System.Collections;
using UnityEngine;

using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.Quests;
using ScheduleOne.Cartel;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.Product.Packaging;
using ScheduleOne.ObjectScripts;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
#else
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.Cartel;
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

        // From stolen items return amount of ordered by exact match higher or equal quality autopackaged in jars
        public static List<ItemInstance> GetFromPool(string id, EQuality quality, int qty, out int returnedQty)
        {
            if (cartelStolenItems.Count == 0)
            {
                returnedQty = 0;
                return new();
            }

            List<ItemInstance> fromPool = new();

#if IL2CPP
            QualityItemInstance qtInst;
#endif
            lock (cartelItemLock)
            {
#if MONO
                ItemDefinition def = ScheduleOne.Registry.GetItem(id);
#else
                ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(id);
#endif
                // So find first which meets quality >= required quality
                QualityItemInstance inst = cartelStolenItems.First(item => item.ID == id &&  item.Quality >= quality);
                if (inst == null || inst.Quantity <= 5)
                {
                    returnedQty = 0;
                    return fromPool;
                }
                int listIndex = cartelStolenItems.IndexOf(inst);
                // Quotient from division by known packaging quantities jars 
                // Max slot qty of 20 with mathfmin
                int jarQuotient = Mathf.FloorToInt((float)inst.Quantity / (float)jarQuantity);
                int jarReqQuotient = Mathf.CeilToInt((float)qty / (float)jarQuantity);
                PackagingDefinition packagingDef = CartelInventory.jarPackaging;

                int slotsNeeded = 0;
                int quotientLeft = 0;
                if (jarReqQuotient > jarQuotient)
                {
                    returnedQty = jarQuotient * jarQuantity;
                    quotientLeft = jarQuotient;
                    slotsNeeded = Mathf.CeilToInt((float)jarQuotient / 20);
                }
                else
                {
                    returnedQty = jarReqQuotient * jarQuantity;
                    quotientLeft = jarReqQuotient;
                    slotsNeeded = Mathf.CeilToInt((float)jarReqQuotient / 20);
                }

                for (int i = 0; i < slotsNeeded; i++)
                {
                    ItemInstance item = null;
                    // use jarquotient
                    if (quotientLeft > 20)
                    {
                        item = def.GetDefaultInstance(20);
                        quotientLeft -= 20;
                    }
                    else
                    {
                        item = def.GetDefaultInstance(quotientLeft);
                    }

                    // Apply packaging
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
                    if (item is QualityItemInstance qtInst)
                        qtInst.Quality = quality;
#else
                    qtInst = item.TryCast<QualityItemInstance>();
                    if (qtInst != null)
                        qtInst.Quality = quality;
#endif
                    fromPool.Add(item);
                }

                if (returnedQty >= inst.Quantity)
                    cartelStolenItems.Remove(inst);
                else
                    cartelStolenItems[listIndex].Quantity -= returnedQty;
            }
            return fromPool;
        }

        public static List<ItemInstance> MakeItem(string id, EQuality quality, int qty)
        {
            List<ItemInstance> fromPool = new();

#if IL2CPP
            QualityItemInstance qtInst;
#endif
#if MONO
            ItemDefinition def = ScheduleOne.Registry.GetItem(id);
#else
            ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(id);
#endif
            int jarReqQuotient = Mathf.CeilToInt((float)qty / (float)jarQuantity);
            PackagingDefinition packagingDef = CartelInventory.jarPackaging;

            int slotsNeeded = 0;
            int quotientLeft = jarReqQuotient;
            slotsNeeded = Mathf.CeilToInt((float)jarReqQuotient / 20);

            for (int i = 0; i < slotsNeeded; i++)
            {
                ItemInstance item = null;
                if (quotientLeft > 20)
                {
                    item = def.GetDefaultInstance(20);
                    quotientLeft -= 20;
                }
                else
                {
                    item = def.GetDefaultInstance(quotientLeft);
                }

                // Apply packaging
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
                if (item is QualityItemInstance qtInst)
                    qtInst.Quality = quality;
#else
                qtInst = item.TryCast<QualityItemInstance>();
                if (qtInst != null)
                    qtInst.Quality = quality;
#endif
                fromPool.Add(item);
            }
            return fromPool;
        }

        public static bool ExistsInInventory(string id, EQuality quality, out int qty)
        {
            // Exists first of equal or higher quality ret quantity
            bool exists = false;
            lock (cartelItemLock)
            {
                exists = cartelStolenItems.Any(item => item.ID == id && item.Quality >= quality);
                if (exists)
                    qty = cartelStolenItems.First(item => item.ID == id && item.Quality >= quality).Quantity;
                else
                    qty = 0;
            }
            return exists;
        }

        public static void FulfillContractItems(Contract contract, CartelDealer dealer)
        {
            // todo logic from intercept, then reuse in intercept + dealer activity
            List<ItemInstance> fromPool = new();
            foreach (ProductList.Entry entry in contract.ProductList.entries)
            {
                bool entrySatisfied = false;
                Log($"Intercept entry: {entry.ProductID} - {entry.Quality} - {entry.Quantity}");
                int inventoryRemainder = -1;
                if (ExistsInInventory(entry.ProductID, entry.Quality, out int qty))
                {
                    Log("Item exists in inventory");
                    fromPool.AddRange(GetFromPool(entry.ProductID, entry.Quality, entry.Quantity, out int returned));
                    if (returned < entry.Quantity)
                        inventoryRemainder = entry.Quantity - returned;
                    else
                        entrySatisfied = true;
                    Log("Added dealer items from stolen inventory");
                }

                if (!entrySatisfied || inventoryRemainder > 0)
                {
                    Log("Entry not satisfied");
                    // magic generate items from thin air
                    if (inventoryRemainder > 0)
                        fromPool.AddRange(MakeItem(entry.ProductID, entry.Quality, inventoryRemainder));
                    else
                        fromPool.AddRange(MakeItem(entry.ProductID, entry.Quality, entry.Quantity));
                    Log("Added dealer items with magic");
                }
            }
            Log("Insert from pool");
            // From pool now contains the item instances needed to make the deal
            // These might include player stolen items but wont be tracked for accidental clear
            // since they can be considered as already spent items due to being reserved for the deal
            for (int i = 0; i < fromPool.Count; i++)
            {
                if (fromPool[i] != null && dealer.Inventory.CanItemFit(fromPool[i]))
                {
                    dealer.Inventory.InsertItem(fromPool[i], true);
                }
            }
        }


    }

}
