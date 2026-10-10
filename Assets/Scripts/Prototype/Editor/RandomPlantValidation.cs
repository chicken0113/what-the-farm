using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class RandomPlantValidation
{
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var catalog = RandomPlantCatalog.Active;
        var prices = ItemPriceCatalog.Active;
        Check(catalog != null && catalog.plants.Length == 2, "Two crop models not configured");
        foreach (var plant in catalog.plants) Check(plant.model != null, "Missing crop model: " + plant.priceId);
        var priceSnapshot = EditorJsonUtility.ToJson(prices);
        var definitions = catalog.plants;
        var randomState = UnityEngine.Random.state;
        var world = new GameObject("Random crop validation").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = new Vector3(100, -.5f, 100);
        ground.transform.localScale = new Vector3(20, 1, 20); var soil = ground.AddComponent<SoilSurface>(); Physics.SyncTransforms();
        try
        {
            var seen = new HashSet<string>(); UnityEngine.Random.InitState(2384);
            for (int i = 0; i < 128; i++) seen.Add(catalog.Pick().priceId);
            Check(seen.Count == 2, "Random choice never selected both models");
            foreach (var plant in definitions)
            {
                var row = prices.Find(plant.priceId);
                Check(row != null && row.minSizePercent == 75 && row.maxSizePercent == 150, "Missing 75-150 defaults");
                for (int i = 0; i < 128; i++) { float size=ItemPriceCatalog.RollPlantSize(plant.priceId); Check(size>=.75f && size<=1.5f,"Size outside range"); }
                catalog.plants = new[] { plant };
                foreach (float size in new[] { .75f, 1.5f })
                {
                    row.minSizePercent = row.maxSizePercent = size*100; row.salePrice=100; row.growthSeconds=4;
                    var point = new Vector3(100, 0, 100);
                    var plot = soil.FindPlot(point) ?? soil.Till(point, 1, material, material);
                    var seed = world.CreateItem(ItemKind.Seed, 3, 10, Vector3.up * 20);
                    Check(world.TryPlant(seed, plot, point), "Seed planting failed");
                    var crop = world.GetComponentInChildren<FleeingCrop>();
                    Check(crop != null && Mathf.Abs(crop.RolledSize-size)<.001f, "Rolled size not retained");
                    Check(crop.GetComponentsInChildren<MeshRenderer>().Length>=1, "No model on planted crop");
                    crop.Grow(10); Check(!crop.IsMature, "Dry crop grew");
                    plot.Water(25); crop.Grow(2); Check(!crop.IsMature, "Crop ignored growth seconds");
                    row.minSizePercent=row.maxSizePercent=200; // No reroll after planting.
                    crop.Grow(2); Check(crop.IsMature && Mathf.Abs(crop.transform.localScale.x-size)<.001f, "Final size changed after roll");
                    Check(crop.Value==(int)(100*size), "Crop value is not linear or uses generation multiplier");
                    var model=crop.gameObject; var matureScale=model.transform.lossyScale;
                    crop.TakeHit(10000); var harvested=model.GetComponent<FarmItem>();
                    Check(harvested!=null && harvested.SizePriced && harvested.PriceId==plant.priceId && harvested.DisplayName==plant.displayName,
                        "Harvest lost plant identity");
                    Check(harvested.transform.lossyScale==matureScale && Mathf.Abs(harvested.SizeMultiplier-size)<.001f && harvested.Value==(int)(100*size),
                        "Harvest lost size or linear price");
                    Check(!world.TryPlant(harvested,plot,point), "Harvested plant allowed replanting");
                    harvested.MarkThrown(); var npc=new GameObject("Seller").AddComponent<NpcMerchant>(); long before=world.Gold;
                    Check(npc.TrySell(harvested) && world.Gold-before==(int)(100*size), "NPC paid wrong size-based gold");
                    UnityEngine.Object.DestroyImmediate(npc.gameObject);
                    if(model!=null) UnityEngine.Object.DestroyImmediate(model);
                    UnityEngine.Object.DestroyImmediate(seed.gameObject);
                }
                row.minSizePercent=75; row.maxSizePercent=150;
            }
            Debug.Log("RANDOM_PLANT_VALIDATION_SUCCESS: actual tomato/cabbage meshes, random selection, water gating, exact duration, 75/150% size, fixed roll, retained harvest and size-proportional NPC gold without generation multiplier.");
        }
        finally
        {
            catalog.plants=definitions; EditorJsonUtility.FromJsonOverwrite(priceSnapshot, prices); UnityEngine.Random.state=randomState;
            UnityEngine.Object.DestroyImmediate(world.gameObject); UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
