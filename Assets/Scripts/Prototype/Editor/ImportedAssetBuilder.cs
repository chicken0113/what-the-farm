using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class ImportedAssetBuilder
{
    private const string Output = "Assets/Prefabs/Game";
    private const string Mats = "Assets/Materials/Imported";
    private static readonly Dictionary<Material, Material> Converted = new();

    [MenuItem("What The Farm/Apply Imported Assets")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Folder("Assets/Prefabs", "Game"); Folder("Assets/Materials", "Imported");
        Converted.Clear();
        ReplaceVisual("Assets/Prefabs/Blockout/Tree_Block.prefab", "Assets/SimpleNaturePack/Prefabs/Tree_01.prefab", 4f);
        ReplaceVisual("Assets/Prefabs/Blockout/Rock_Block.prefab", "Assets/SimpleNaturePack/Prefabs/Rock_01.prefab", 1.3f);
        ReplaceVisual("Assets/Prefabs/Blockout/BuyerNPC.prefab", "Assets/Floreswa/Prefabs/male01_1.prefab", 1.8f, true);
        var hoe = CreateItem("Hoe", "Assets/Low Poly Weapon Series V 4/Prefabs/Low Poly Series V 4 New/Close Combat/Shovel_A.prefab", ItemKind.Tool, .9f);
        var stone = CreateItem("Stone", "Assets/SimpleNaturePack/Prefabs/Rock_02.prefab", ItemKind.Curio, .5f);
        foreach (string path in new[] { "Assets/Scenes/FirstFarm.unity", "Assets/Scenes/FarmPrototype.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var world in root.GetComponentsInChildren<FarmPrototype>(true))
                {
                    world.SetItemPrefabs(new[] {
                        new FarmPrototype.ItemPrefabBinding { kind = ItemKind.Tool, prefab = hoe },
                        new FarmPrototype.ItemPrefabBinding { kind = ItemKind.Curio, prefab = stone }
                    });
                    EditorUtility.SetDirty(world);
                }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Validate(hoe, stone);
        PlantGrowthBuilder.Validate();
        NpcMerchantBuilder.Validate();
        NpcMerchantBuilder.ValidateStockRefill();
        FirstFarmMapBuilder.CapturePreview();
        Debug.Log("Imported assets applied and validation passed.");
    }

    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject Visual(GameObject root, string source, float height, bool removeColliders)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (asset == null) throw new InvalidOperationException("Missing imported prefab: " + source);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Visual";
        model.transform.SetParent(root.transform, false);
        if (source.EndsWith("Shovel_A.prefab")) model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        if (source.Contains("Floreswa/")) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
        Bounds bounds = BoundsOf(model);
        model.transform.localScale *= height / Mathf.Max(.001f, bounds.size.y);
        bounds = BoundsOf(model);
        model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - root.transform.position;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = Convert(materials[i]);
            renderer.sharedMaterials = materials;
        }
        if (removeColliders)
            foreach (var collider in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        return model;
    }

    private static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderer.");
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Material Convert(Material source)
    {
        if (source == null || source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
        if (Converted.TryGetValue(source, out var cached)) return cached;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path = Mats + "/" + source.name + "_" + guid + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            if (source.HasProperty("_MainTex"))
            {
                material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            }
            material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0);
            material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
            AssetDatabase.CreateAsset(material, path);
        }
        Converted[source] = material;
        return material;
    }

    private static void ReplaceVisual(string target, string source, float height, bool npc = false)
    {
        var root = PrefabUtility.LoadPrefabContents(target);
        try
        {
            while (root.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            Visual(root, source, height, npc);
            PrefabUtility.SaveAsPrefabAsset(root, target);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static FarmItem CreateItem(string name, string source, ItemKind kind, float height)
    {
        var root = new GameObject(name);
        var model = Visual(root, source, height, true);
        // A single box makes imported models safe for dynamic throwing and planting.
        Bounds bounds = BoundsOf(model);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = bounds.center; collider.size = bounds.size;
        root.AddComponent<Rigidbody>().mass = .5f;
        var item = root.AddComponent<FarmItem>();
        item.Configure(kind, 0, kind == ItemKind.Tool ? 16 : 6);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<FarmItem>();
    }

    private static void Validate(FarmItem hoe, FarmItem stone)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        var world = UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
        var soil = UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
        var dry = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Wet.mat");
        foreach (var prefab in new[] { hoe, stone })
        {
            var size = prefab.GetComponent<BoxCollider>().size;
            if (Mathf.Max(size.x, size.y, size.z) > 1.2f)
                throw new InvalidOperationException("Imported held item is oversized.");
            Vector3 point = new Vector3(prefab.Kind == ItemKind.Tool ? -3 : 3, 0, -3);
            var plot = soil.Till(point, .8f, dry, wet);
            var item = world.CreateItem(prefab.Kind, 0, 10, point + Vector3.up);
            if (item.GetComponentInChildren<MeshFilter>() == null || !world.TryPlant(item, plot, point))
                throw new InvalidOperationException("Imported item cannot be planted.");
            var crop = world.GetComponentsInChildren<FleeingCrop>()[^1];
            if (Mathf.Abs(BoundsOf(crop.gameObject).min.y) > .01f) throw new InvalidOperationException("Imported plant floats.");
            plot.Water(50); crop.Grow(10);
            var grownObject = crop.gameObject;
            var grownScale = crop.transform.lossyScale;
            var grownSize = BoundsOf(grownObject).size;
            crop.TakeHit(999);
            var drop = grownObject.GetComponent<FarmItem>();
            if (drop == null || drop.Kind != prefab.Kind || drop.transform.lossyScale != grownScale ||
                Vector3.Distance(BoundsOf(drop.gameObject).size, grownSize) > .001f)
                throw new InvalidOperationException("Imported harvested model lost its grown size.");
            if (!world.TryPlant(drop, plot, point)) throw new InvalidOperationException("Harvested model could not be replanted.");
            var replanted = world.GetComponentsInChildren<FleeingCrop>()[^1];
            if (Vector3.Distance(replanted.transform.lossyScale, grownScale) > .001f)
                throw new InvalidOperationException("Imported replant lost harvested size.");
            plot.Water(50); replanted.Grow(20);
        }
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (var material in renderer.sharedMaterials)
                if (material == null || !material.shader.name.StartsWith("Universal Render Pipeline/"))
                    throw new InvalidOperationException("Scene material is not URP compatible: " + renderer.name);
        Debug.Log("Imported model planting, growth, harvest and URP validation passed.");
        // Discard temporary validation plants.
        EditorSceneManager.OpenScene(scene.path);
    }

    public static void ValidateHarvestSize()
    {
        PlantGrowthBuilder.Validate();
        Validate(AssetDatabase.LoadAssetAtPath<FarmItem>(Output + "/Hoe.prefab"),
            AssetDatabase.LoadAssetAtPath<FarmItem>(Output + "/Stone.prefab"));
        NpcMerchantBuilder.Validate();
        Debug.Log("Harvest size validation passed.");
    }
}
