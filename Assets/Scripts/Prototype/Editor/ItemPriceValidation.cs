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
            CheckGrowth(catalog);
            Debug.Log("ITEM_PRICE_VALIDATION_SUCCESS: complete catalog, live prices, custom IDs, shovel states, generations, all-kind sales and refill.");
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(snapshot, catalog);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static void CheckGrowth(ItemPriceCatalog catalog)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var farmRoot = new GameObject("Growth speed validation");
        var sourceRoot = new GameObject("Growth source");
        var profile = ScriptableObject.CreateInstance<PlantGrowthProfile>();
        var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        try
        {
            ground.transform.position = new Vector3(400, -.5f, 400); ground.transform.localScale = new Vector3(5, 1, 5);
            var soil = ground.AddComponent<SoilSurface>(); Physics.SyncTransforms();
            var plot = soil.Till(new Vector3(400, 0, 400), 1, material, material);
            var world = farmRoot.AddComponent<FarmPrototype>();
            var source = sourceRoot.AddComponent<FarmItem>(); source.Configure(ItemKind.Seed, 2, 10);
            profile.growthSeconds = 4; profile.secondsPerGeneration = 1; profile.sizePerGeneration = 0;
            profile.useLightCondition = profile.useWaterCondition = profile.useSoilCondition = false;
            source.SetGrowthProfile(profile);
            var plant = GameObject.CreatePrimitive(PrimitiveType.Cube); plant.transform.SetParent(farmRoot.transform);
            plant.GetComponent<Renderer>().sharedMaterial = new Material(material);
            var crop = plant.AddComponent<FleeingCrop>(); Check(plot.Plant(crop), "Growth plot setup failed");
            crop.Configure(world, source, plot, 0);
            var row = catalog.Find("seed"); row.growthSeconds = 8;
            crop.Grow(100); Check(!crop.IsMature, "Speed control bypassed watering requirement");
            plot.Water(25);
            crop.Grow(7); Check(!crop.IsMature && Mathf.Abs(plant.transform.localScale.x - 1.875f) < .001f,
                "Eight-second duration ignored or generation changed duration");
            row.growthSeconds = 4; crop.Grow(.4f); Check(!crop.IsMature, "Live duration edit completed early");
            crop.Grow(.11f); Check(crop.IsMature && Mathf.Abs(plant.transform.localScale.x - 2) < .001f,
                "Duration edit lost progress or changed final size");
            Check(ItemPriceCatalog.GrowthSeconds("missing", 7) == 7, "Unknown item lost profile duration fallback");
            row.growthSeconds = 0; Check(ItemPriceCatalog.GrowthSeconds("seed", 4) == .1f, "Minimum duration not enforced");
            Debug.Log("ITEM_GROWTH_SECONDS_VALIDATION_SUCCESS: water gate, exact seconds independent of generation, live edit preserving progress and final size.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(farmRoot); UnityEngine.Object.DestroyImmediate(sourceRoot);
            UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
