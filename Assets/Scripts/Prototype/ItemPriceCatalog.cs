using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [CreateAssetMenu(menuName = "What The Farm/Item Prices")]
    public sealed class ItemPriceCatalog : ScriptableObject
    {
        public const string ResourceName = "ItemPrices";
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string label;
            [Min(0)] public int purchasePrice;
            [Min(0)] public int salePrice;
        }
        public List<Entry> entries = new();
        [Min(1)] public float generationMultiplier = 1.8f;
        public static ItemPriceCatalog Active => Resources.Load<ItemPriceCatalog>(ResourceName);
        public Entry Find(string id) => entries.Find(row => row != null && row.id == id);
        public static string DefaultId(FarmItem item)
        {
            if (item.Kind == ItemKind.Corpse)
                return item.GetComponent<PlantableCorpse>()?.IsPlayerBody == true ? "player-body" : "npc-body";
            if (item.Kind == ItemKind.Tool && item.GetComponent<GrowableTool>() != null)
                return item.HasBeenPlanted ? "shovel" : "shovel-head";
            return item.Kind.ToString().ToLowerInvariant();
        }
        public static int Purchase(FarmItem item)
        {
            var row = Active?.Find(item.PriceId);
            return row != null ? Mathf.Max(0, row.purchasePrice) : Mathf.Max(0, item.BaseValue);
        }
        public static int Sale(FarmItem item)
            => Sale(item.PriceId, item.Generation, item.BaseValue);
        public static int Sale(string id, int generation, int fallbackValue)
        {
            var catalog = Active;
            var row = catalog?.Find(id);
            float value = row != null ? Mathf.Max(0, row.salePrice) : Mathf.Max(0, fallbackValue);
            if (value == 0) return 0;
            double result = value * Math.Pow(Math.Max(1, catalog != null ? catalog.generationMultiplier : 1.8f), generation);
            return (int)Math.Min(int.MaxValue, Math.Round(result, MidpointRounding.ToEven));
        }
    }
}
