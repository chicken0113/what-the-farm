using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public static class FarmTravel
    {
        private static FarmItem[] items;
        private static long gold;
        private static int selected;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { items = null; gold = 0; selected = 0; }
        public static void Capture(FarmPrototype world, LocalFarmer player)
        {
            items = player.CaptureInventory(); gold = world.Gold; selected = player.SelectedSlot;
            foreach (var item in items)
            {
                if (item == null) continue;
                item.transform.SetParent(null, true);
                Object.DontDestroyOnLoad(item.gameObject);
                item.gameObject.SetActive(false);
            }
        }
        public static void Restore(FarmPrototype world, LocalFarmer player)
        {
            if (items == null) return;
            var carried = items; items = null;
            world.RestoreGold(gold); player.RestoreInventory(carried, selected);
        }
    }
}
