using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class PlantGrowthBuilder
{
    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    public static void ConfigureWorld(FarmPrototype world)
    {
        Folder("Assets", "Data"); Folder("Assets/Data", "Growth"); Folder("Assets/Data", "Soils");
        SoilType Soil(string name)
        {
            string path = $"Assets/Data/Soils/{name}.asset";
            var soil = AssetDatabase.LoadAssetAtPath<SoilType>(path);
            if (soil == null) { soil = ScriptableObject.CreateInstance<SoilType>(); soil.name = name; AssetDatabase.CreateAsset(soil, path); }
            return soil;
        }
        var loam = Soil("Loam"); var clay = Soil("Clay"); var sand = Soil("Sand");
        var paddy = Soil("Paddy"); Soil("Jungle");
        PlantGrowthProfile Profile(string name, float minLight, float maxLight, float lightBonus,
            float minWater, float maxWater, float waterBonus, SoilType soil, float soilBonus)
        {
            string path = $"Assets/Data/Growth/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PlantGrowthProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<PlantGrowthProfile>();
            profile.name = name;
            profile.minLight = minLight; profile.maxLight = maxLight; profile.lightBonusPercent = lightBonus;
            profile.minWater = minWater; profile.maxWater = maxWater; profile.waterBonusPercent = waterBonus;
            profile.preferredSoils = soil != null ? new[] { soil } : Array.Empty<SoilType>();
            profile.soilBonusPercent = soilBonus;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
        var fallback = Profile("Default", 0, 100, 0, 0, 100, 0, null, 0);
        var bindings = new[]
        {
            new FarmPrototype.GrowthBinding { kind = ItemKind.Seed, profile = Profile("Seed", 60,100,25,40,80,25,loam,20) },
            new FarmPrototype.GrowthBinding { kind = ItemKind.Produce, profile = Profile("Produce", 50,100,20,40,90,20,loam,20) },
            new FarmPrototype.GrowthBinding { kind = ItemKind.Tool, profile = Profile("Hoe", 0,40,15,20,60,20,clay,30) },
            new FarmPrototype.GrowthBinding { kind = ItemKind.WateringCan, profile = Profile("WateringCan", 20,70,20,70,100,35,paddy,25) },
            new FarmPrototype.GrowthBinding { kind = ItemKind.Curio, profile = Profile("Stone", 60,100,10,0,30,15,sand,35) }
        };
        world.SetGrowthDefaults(fallback, loam, bindings);
        EditorUtility.SetDirty(world);
        AssetDatabase.SaveAssets();
    }

    public static void ConfigureGround(SoilSurface ground, Light sunlight = null)
    {
        ground.SetEnvironment(AssetDatabase.LoadAssetAtPath<SoilType>("Assets/Data/Soils/Loam.asset"), 80, 0, sunlight);
        EditorUtility.SetDirty(ground);
    }

    public static void Install()
    {
        foreach (string path in new[] { "Assets/Scenes/FirstFarm.unity", "Assets/Scenes/FarmPrototype.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            var world = UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
            ConfigureWorld(world);
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            foreach (var ground in UnityEngine.Object.FindObjectsByType<SoilSurface>(FindObjectsSortMode.None))
                ConfigureGround(ground, sun);
            EditorSceneManager.SaveScene(scene);
        }
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Blockout/Ground_Block.prefab");
        ConfigureGround(prefab.GetComponentInChildren<SoilSurface>());
        PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Blockout/Ground_Block.prefab");
        PrefabUtility.UnloadPrefabContents(prefab);
        Validate();
        Debug.Log("Object growth profiles and soil environments installed.");
    }

    public static void Validate()
    {
        FirstFarmMapBuilder.ValidatePlantingGrowth();
        var loam = AssetDatabase.LoadAssetAtPath<SoilType>("Assets/Data/Soils/Loam.asset");
        var sand = AssetDatabase.LoadAssetAtPath<SoilType>("Assets/Data/Soils/Sand.asset");
        var profile = ScriptableObject.CreateInstance<PlantGrowthProfile>();
        profile.minLight = 60; profile.maxLight = 100; profile.lightBonusPercent = 20;
        profile.minWater = 30; profile.maxWater = 60; profile.waterBonusPercent = 30;
        profile.preferredSoils = new[] { loam }; profile.soilBonusPercent = 40;
        if (profile.Evaluate(0, 0, sand) != 100 || profile.Evaluate(80, 0, sand) != 120 ||
            profile.Evaluate(0, 50, sand) != 130 || profile.Evaluate(0, 0, loam) != 140 ||
            profile.Evaluate(80, 50, loam) != 190 || profile.Evaluate(60, 30, loam) != 190)
            throw new InvalidOperationException("Growth bonuses do not add correctly.");
        var world = new GameObject("Growth environment validation").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(100, -.125f, 100);
        ground.transform.localScale = new Vector3(26, .25f, 26);
        var surface = ground.AddComponent<SoilSurface>();
        surface.SetEnvironment(sand, 80);
        Physics.SyncTransforms();
        var dry = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Wet.mat");
        var area = surface.Till(new Vector3(100, 0, 100), .8f, dry, wet);
        var source = world.CreateItem(ItemKind.Seed, 0, 10, Vector3.one * 150);
        source.SetGrowthProfile(profile);
        if (!world.TryPlant(source, area, area.transform.position)) throw new InvalidOperationException("Profile planting failed.");
        var crop = world.GetComponentInChildren<FleeingCrop>();
        Vector3 initialScale = crop.transform.localScale;
        crop.Grow(2);
        if (crop.transform.localScale != initialScale) throw new InvalidOperationException("Required watering was bypassed.");
        area.Water(25); area.Water(25);
        if (area.WaterAmount != 50) throw new InvalidOperationException("Water doses do not accumulate.");
        crop.Grow(2);
        if (crop.IsMature || crop.GrowthRatePercent != 150 ||
            Vector3.Distance(crop.transform.localScale, initialScale * 2) > .001f)
            throw new InvalidOperationException("150% bonus changed growth time or intermediate size.");
        surface.SetEnvironment(sand, 0);
        crop.Grow(1);
        if (crop.GrowthRatePercent != 130) throw new InvalidOperationException("Light change was not reflected.");
        surface.SetEnvironment(sand, 80);
        crop.Grow(1);
        if (!crop.IsMature || Vector3.Distance(crop.transform.localScale, initialScale * 3) > .001f)
            throw new InvalidOperationException("150% did not produce 1.5 times the normal mature size.");
        source.SetGrowthProfile(null);
        var grownObject = crop.gameObject;
        var grownScale = crop.transform.lossyScale;
        var grownMesh = crop.GetComponent<MeshFilter>().sharedMesh;
        var grownBounds = crop.GetComponent<Renderer>().bounds.size;
        crop.TakeHit(100);
        var drop = grownObject.GetComponent<FarmItem>();
        if (drop == null || drop.Kind != ItemKind.Produce ||
            drop.transform.lossyScale != grownScale || drop.GetComponent<MeshFilter>().sharedMesh != grownMesh ||
            Vector3.Distance(drop.GetComponent<Renderer>().bounds.size, grownBounds) > .001f ||
            drop.GetComponent<Rigidbody>().isKinematic || !drop.GetComponent<Rigidbody>().useGravity)
            throw new InvalidOperationException("Harvest changed grown size/model or did not create a pickup.");
        if (!world.TryPlant(drop, area, area.transform.position) ||
            Vector3.Distance(world.GetComponentInChildren<FleeingCrop>().transform.lossyScale, grownScale) > .001f)
            throw new InvalidOperationException("Replanting lost harvested size.");
        var replanted = world.GetComponentInChildren<FleeingCrop>();
        replanted.Grow(20);
        UnityEngine.Object.DestroyImmediate(replanted.gameObject);
        var harvested = world.GetComponentsInChildren<FarmItem>();
        if (harvested[harvested.Length - 1].GrowthProfile != profile)
            throw new InvalidOperationException("Harvest lost the object's growth profile.");
        var second = surface.Till(new Vector3(103, 0, 100), .8f, dry, wet);
        var stone = world.CreateItem(ItemKind.Curio, 0, 10, Vector3.one * 150);
        stone.SetGrowthProfile(profile); profile.requireWaterToStart = false;
        if (!world.TryPlant(stone, second, second.transform.position)) throw new InvalidOperationException("Generic item profile failed.");
        var crops = world.GetComponentsInChildren<FleeingCrop>();
        var stoneCrop = crops[crops.Length - 1];
        stoneCrop.Grow(5);
        if (!stoneCrop.IsMature) throw new InvalidOperationException("Optional watering gate did not work.");
        var sun = new GameObject("Growth light validation").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.Euler(90, 0, 0);
        surface.SetEnvironment(sand, 80, 0, sun);
        Physics.SyncTransforms();
        if (Mathf.Abs(surface.GetLight(area.transform.position) - 80) > .01f)
            throw new InvalidOperationException("Directional light intensity was not applied.");
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.position = area.transform.position + Vector3.up * 4;
        roof.transform.localScale = new Vector3(4, .5f, 4);
        Physics.SyncTransforms();
        if (surface.GetLight(area.transform.position) != 20)
            throw new InvalidOperationException("Shelter did not reduce growth light.");
        UnityEngine.Object.DestroyImmediate(roof);
        sun.enabled = false;
        if (surface.GetLight(area.transform.position) != 20)
            throw new InvalidOperationException("Disabled sun still supplies full growth light.");
        UnityEngine.Object.DestroyImmediate(sun.gameObject);
        UnityEngine.Object.DestroyImmediate(world.gameObject);
        UnityEngine.Object.DestroyImmediate(ground);
        UnityEngine.Object.DestroyImmediate(profile);
        Debug.Log("Growth environment, harvest size/model preservation and replant size validation passed.");
    }
}
