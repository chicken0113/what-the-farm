using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class ItemPriceValidation
{
    public static void RunAndPlay()
    {
        Run();
        UnityMigrationBatchCheck.PlayExisting();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var catalog = ItemPriceWindow.EnsureCatalog();
        var snapshot = EditorJsonUtility.ToJson(catalog);
        var root = new GameObject("Price validation");
        try
        {
            foreach (var id in new[] { "seed", "produce", "tool", "shovel-head", "shovel", "wateringcan", "curio", "npc-body", "player-body" })
                Check(catalog.Find(id) != null, "Missing price row: " + id);
            var item = root.AddComponent<FarmItem>();
            var row = catalog.Find("curio"); row.purchasePrice = 127; row.salePrice = 19; catalog.generationMultiplier = 2;
            item.Configure(ItemKind.Curio, 2, 6);
            Check(item.PurchasePrice == 127 && item.Value == 76, "Prices do not use catalog/generation");
            row.salePrice = 23;
            Check(item.Value == 92, "Price edit did not update live value");
            var custom = new ItemPriceCatalog.Entry { id = "test-item", label = "Test", purchasePrice = 37, salePrice = 41 };
            catalog.entries.Add(custom); item.SetPriceId(custom.id);
            Check(item.PurchasePrice == 37 && item.Value == 164, "Custom item pricing failed");
            item.SetPriceId(null); root.AddComponent<GrowableTool>(); item.Configure(ItemKind.Tool, 0, 16);
            Check(item.PriceId == "shovel-head", "Raw shovel not distinguished");
            item.MarkPlanted(); Check(item.PriceId == "shovel" && item.Value == catalog.Find("shovel").salePrice, "Grown shovel price missing");
            row.salePrice = 0; item.Configure(ItemKind.Curio, 0, 6);
            Check(item.Value == 0, "Zero sale price failed");
            NpcMerchantBuilder.Validate(); // Includes a zero-price curio and positive-price corpse sale.
            EditorJsonUtility.FromJsonOverwrite(snapshot, catalog);
            NpcMerchantBuilder.Validate();
            NpcMerchantBuilder.ValidateStockRefill();
            var bodyRoot = new GameObject("Player body pricing");
            var owner = new GameObject("Body owner");
            try
            {
                bodyRoot.AddComponent<PlantableCorpse>().BindPlayer(owner.AddComponent<LocalFarmer>(), 4, .5f);
                var bodyItem = bodyRoot.GetComponent<FarmItem>();
                Check(bodyItem.PriceId == "player-body" && bodyItem.Value == catalog.Find("player-body").salePrice,
                    "Player body uses NPC price");
            }
            finally { UnityEngine.Object.DestroyImmediate(bodyRoot); UnityEngine.Object.DestroyImmediate(owner); }
            Debug.Log("ITEM_PRICE_VALIDATION_SUCCESS: complete catalog, live prices, custom IDs, shovel states, generations, all-kind sales and refill.");
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(snapshot, catalog);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
