using System.Collections;
using UnityEngine;
using static CartelEnforcer.DebugModule;

#if MONO
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
#else
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
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
        public List<SerializeStolenItems> items;
    }

    public static class CartelInventory
    {

        public static List<QualityItemInstance> cartelStolenItems = new();
        public static readonly object cartelItemLock = new object(); // for above list

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
                                        realQty = 5;
                                        break;
                                    case "brick":
                                        realQty = 20;
                                        break;
                                    default:
                                        realQty = 1;
                                        break;
                                }
                            }
                        }

                        if (foundIdx >= 0) // Exists in already stolen items
                        {
                            Log($"[CARTEL INV]    EXISTS ADD: {inst.Name}x{inst.Quantity * realQty}");
                            cartelStolenItems[foundIdx].Quantity += inst.Quantity * realQty;
                        }
                        else // not exist
                        {
                            Log($"[CARTEL INV]    ADD: {items[i].Name}x{inst.Quantity * realQty}");
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
                                        realQty = 5;
                                        break;
                                    case "brick":
                                        realQty = 20;
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

        // From pool max 20 unpackaged items per slot, saves quality
        public static List<ItemInstance> GetFromPool(int maxEmptySlotsToFill)
        {
            List<ItemInstance> fromPool = new();
#if IL2CPP
            QualityItemInstance qtInst;
#endif
            lock (cartelItemLock)
            {
                int itemsToPick = Mathf.Min(maxEmptySlotsToFill, cartelStolenItems.Count);
                int randomIndex;
                int minQty;
                for (int i = 0; i < itemsToPick; i++)
                {
                    if (cartelStolenItems.Count == 0) break;
                    randomIndex = UnityEngine.Random.Range(0, cartelStolenItems.Count);
                    minQty = Mathf.Min(cartelStolenItems[randomIndex].Quantity, 20);

#if MONO
                    ItemDefinition def = ScheduleOne.Registry.GetItem(cartelStolenItems[randomIndex].ID);
#else
                    ItemDefinition def = Il2CppScheduleOne.Registry.GetItem(cartelStolenItems[randomIndex].ID);
#endif
                    ItemInstance item = def.GetDefaultInstance(minQty);
#if MONO
                    if (item is QualityItemInstance inst)
                        inst.Quality = cartelStolenItems[randomIndex].Quality;
#else
                    qtInst = item.TryCast<QualityItemInstance>();
                    if (qtInst != null)
                        qtInst.Quality = cartelStolenItems[randomIndex].Quality;
#endif

                    fromPool.Add(item);

                    if (minQty >= cartelStolenItems[randomIndex].Quantity)
                        cartelStolenItems.RemoveAt(randomIndex);
                    else
                        cartelStolenItems[randomIndex].Quantity -= minQty;
                }
            }
            return fromPool;
        }
    }
}
