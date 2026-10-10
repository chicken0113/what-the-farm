using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class ToolSizeValidation
{
    public static void Validate()
    {
        ImportedAssetBuilder.ValidateHarvestSize();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = new GameObject("Tool size validation").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.localScale = new Vector3(26, .25f, 26);
        ground.transform.position = Vector3.down * .125f;
        var soil = ground.AddComponent<SoilSurface>();
        Physics.SyncTransforms();
        var hoe = world.CreateItem(ItemKind.Tool, 0, 10, Vector3.up * 10);
        var can = world.CreateItem(ItemKind.WateringCan, 0, 10, Vector3.up * 10);
        Near(hoe.SizeMultiplier, 1); Near(world.TillingRadiusFor(hoe), .4f);
        hoe.transform.localScale *= 2;
        Near(hoe.SizeMultiplier, 2); Near(world.TillingRadiusFor(hoe), .8f);
        var point = new Vector3(-5, 0, 0);
        if (!world.TryTill(soil, point, hoe)) throw new InvalidOperationException("Large hoe failed to till.");
        Near(soil.FindPlot(point).Radius, .8f);
        var dry = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Wet.mat");
        var plots = new FarmPlot[4];
        var positions = new[] { 0f, .6f, 1.3f, 2f };
        for (int i = 0; i < plots.Length; i++)
        {
            var center = new Vector3(positions[i], 0, 0);
            plots[i] = soil.Till(center, .28f, dry, wet);
            var seed = world.CreateItem(ItemKind.Seed, 0, 10, Vector3.up * 10);
            if (!world.TryPlant(seed, plots[i], center + Vector3.right * .1f))
                throw new InvalidOperationException("Water radius test plant failed.");
        }
        Near(world.WateringRadiusFor(can), .8f);
        if (world.TryWater(soil, Vector3.zero, can) != 2) throw new InvalidOperationException("Normal water radius mismatch.");
        Near(plots[0].WaterAmount, 25); Near(plots[1].WaterAmount, 25); Near(plots[2].WaterAmount, 0);
        can.transform.localScale *= 2;
        Near(can.SizeMultiplier, 2); Near(world.WateringRadiusFor(can), 1.6f);
        if (world.TryWater(soil, Vector3.zero, can) != 3) throw new InvalidOperationException("Large watering can did not reach neighbours.");
        Near(plots[0].WaterAmount, 50); Near(plots[2].WaterAmount, 25); Near(plots[3].WaterAmount, 0);
        can.transform.localScale *= .25f;
        Near(world.WateringRadiusFor(can), .4f);
        if (world.TryWater(soil, Vector3.zero, can) != 1) throw new InvalidOperationException("Smaller water radius mismatch.");
        Near(plots[0].WaterAmount, 75); Near(plots[1].WaterAmount, 50);
        // Growth must retain the original reference through a single growth and harvest.
        Vector3 baseline = hoe.OriginalScale;
        var home = soil.FindPlot(point);
        for (int cycle = 0; cycle < 1; cycle++)
        {
            float plantedSize = hoe.SizeMultiplier;
            if (!world.TryPlant(hoe, home, point)) throw new InvalidOperationException("Tool planting failed.");
            FleeingCrop grown = null;
            foreach (var crop in world.GetComponentsInChildren<FleeingCrop>())
                if (crop.Plot == home) grown = crop;
            home.Water(50); grown.Grow(20);
            var grownObject = grown.gameObject;
            Vector3 actualScale = grown.transform.lossyScale;
            grown.TakeHit(1000);
            hoe = grownObject.GetComponent<FarmItem>();
            if (hoe.OriginalScale != baseline || hoe.SizeMultiplier <= plantedSize)
                throw new InvalidOperationException("Harvest reset tool size reference.");
            Near(hoe.SizeMultiplier, actualScale.y / baseline.y);
            Near(world.TillingRadiusFor(hoe), .4f * hoe.SizeMultiplier);
        }
        if (!hoe.HasBeenPlanted || world.TryPlant(hoe, home, point)) throw new InvalidOperationException("Tool allowed a second planting.");
        // Nested imported models use the same root ratio as primitive tools.
        var imported = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FarmItem>("Assets/Prefabs/Game/Hoe.prefab"));
        imported.Configure(ItemKind.Tool, 0, 10);
        imported.transform.localScale *= 3;
        Near(imported.SizeMultiplier, 9); Near(world.TillingRadiusFor(imported), 3.6f);
        UnityEngine.Object.DestroyImmediate(imported.gameObject);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("Linear tool radius, multi-plant watering, smaller tools and harvest size inheritance and single planting validation passed.");
    }

    private static void Near(float actual, float expected)
    {
        if (Mathf.Abs(actual - expected) > .001f) throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
