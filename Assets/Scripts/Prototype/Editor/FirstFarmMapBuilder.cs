using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class FirstFarmMapBuilder
{
    private const string Kit = "Assets/Prefabs/Blockout";
    private const string Materials = "Assets/Materials/Blockout";
    private const string ScenePath = "Assets/Scenes/FirstFarm.unity";

    [MenuItem("What The Farm/Create First Farm Blockout")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Rebuild blockout", "This replaces FirstFarm.unity and the generated blockout prefabs. Save a copy of your edited map first.", "Rebuild", "Cancel"))
            return;

        Folder("Assets", "Prefabs"); Folder("Assets/Prefabs", "Blockout");
        Folder("Assets", "Materials"); Folder("Assets/Materials", "Blockout");
        Material baseline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat");
        if (baseline == null) throw new InvalidOperationException("PrototypeBaseMaterial is missing.");
        var mats = new Dictionary<string, Material>();
        void ColorMaterial(string name, Color color)
        {
            string path = $"{Materials}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(baseline);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            mats[name] = material;
        }
        ColorMaterial("Ground", new Color(.4f, .45f, .3f));
        ColorMaterial("Path", new Color(.75f, .66f, .45f));
        ColorMaterial("Untilled", new Color(.43f, .7f, .27f));
        ColorMaterial("Tilled", new Color(.18f, .1f, .06f));
        ColorMaterial("Wet", new Color(.15f, .22f, .31f));
        ColorMaterial("Wood", new Color(.45f, .27f, .12f));
        ColorMaterial("Water", new Color(.12f, .57f, .95f));
        ColorMaterial("Rock", new Color(.48f, .49f, .55f));
        ColorMaterial("Tree", new Color(.1f, .38f, .19f));
        ColorMaterial("Shop", new Color(.95f, .8f, .15f));
        ColorMaterial("Sell", new Color(.95f, .4f, .15f));
        ColorMaterial("Exit", new Color(.64f, .28f, .9f));
        ColorMaterial("Spawn", new Color(.15f, .9f, .85f));

        var prefabs = new Dictionary<string, GameObject>();
        void Save(string name, Action<GameObject> construct)
        {
            var root = new GameObject(name);
            construct(root);
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(root, $"{Kit}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
        Save("Ground_Block", root =>
        {
            Part(root, "Ground", new Vector3(0, -.125f, 0), new Vector3(4, .25f, 4), mats["Ground"]).AddComponent<SoilSurface>();
        });
        Save("Path_Block", root => Part(root, "Path", new Vector3(0, .015f, 0), new Vector3(2, .03f, 2), mats["Path"]));
        Save("Water_Block", root => Part(root, "Water (placeholder)", new Vector3(0, .025f, 0), new Vector3(3, .05f, 3), mats["Water"], false));
        Save("Rock_Block", root => Part(root, "Rock", new Vector3(0, .75f, 0), new Vector3(1.6f, 1.5f, 1.3f), mats["Rock"]));
        Save("Tree_Block", root =>
        {
            Part(root, "Trunk", new Vector3(0, 1, 0), new Vector3(.4f, 2, .4f), mats["Wood"]);
            Part(root, "Canopy", new Vector3(0, 2.5f, 0), new Vector3(2, 1.5f, 2), mats["Tree"]);
        });
        Save("Fence_Block", root =>
        {
            foreach (float x in new[] { -.9f, .9f })
                Part(root, "Post", new Vector3(x, .6f, 0), new Vector3(.15f, 1.2f, .15f), mats["Wood"]);
            Part(root, "Rail", new Vector3(0, .75f, 0), new Vector3(2, .25f, .15f), mats["Wood"]);
        });
        Save("Bridge_Block", root => Part(root, "Deck", new Vector3(0, .1f, 0), new Vector3(2, .2f, 4), mats["Wood"]));
        foreach (string name in new[] { "Shop", "Sell" })
        {
            Save(name + "_Block", root =>
            {
                Part(root, "Counter", new Vector3(0, .5f, 0), new Vector3(2, 1, 1), mats[name]);
                Part(root, "Roof", new Vector3(0, 2, 0), new Vector3(2.4f, .2f, 1.5f), mats[name]);
                foreach (float x in new[] { -.9f, .9f })
                    Part(root, "Support", new Vector3(x, 1, .4f), new Vector3(.15f, 2, .15f), mats["Wood"]);
            });
        }
        Save("RegionExit_Block", root =>
        {
            foreach (float x in new[] { -1.5f, 1.5f })
                Part(root, "Gate Post", new Vector3(x, 1.5f, 0), new Vector3(.3f, 3, .4f), mats["Exit"]);
            Part(root, "Gate Top", new Vector3(0, 3, 0), new Vector3(3.3f, .3f, .4f), mats["Exit"]);
            Part(root, "Closed Exit (placeholder)", new Vector3(0, 1, .3f), new Vector3(3, 2, .15f), mats["Exit"]);
        });
        Save("SpawnMarker_Block", root => Part(root, "Spawn marker", new Vector3(0, .03f, 0), new Vector3(1, .06f, 1), mats["Spawn"], false));

        var plotRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plotRoot.name = "FarmPlot_Block";
        plotRoot.transform.localScale = new Vector3(1.7f, .18f, 1.7f);
        plotRoot.AddComponent<FarmPlot>().Configure(mats["Untilled"], mats["Tilled"], mats["Wet"], mats["Wood"]);
        prefabs[plotRoot.name] = PrefabUtility.SaveAsPrefabAsset(plotRoot, $"{Kit}/{plotRoot.name}.prefab");
        UnityEngine.Object.DestroyImmediate(plotRoot);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var map = new GameObject("First Farm - editable blockout");
        GameObject Place(string name, Vector3 position, Vector3? scale = null, float rotation = 0)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name], scene);
            instance.transform.SetParent(map.transform);
            instance.transform.position = position;
            if (scale.HasValue) instance.transform.localScale = scale.Value;
            instance.transform.rotation = Quaternion.Euler(0, rotation, 0);
            return instance;
        }
        Place("Ground_Block", Vector3.zero, new Vector3(6.5f, 1, 6.5f));
        Place("Path_Block", new Vector3(0, 0, -7), new Vector3(11, 1, 1));
        Place("Path_Block", new Vector3(0, 0, 6), new Vector3(11, 1, 1));
        Place("Water_Block", new Vector3(-8, 0, 0), new Vector3(1, 1, 2));
        Place("Bridge_Block", new Vector3(-8, 0, 0), null, 90);
        Place("Shop_Block", new Vector3(-9, 0, 9));
        Place("Sell_Block", new Vector3(9, 0, 9));
        Place("RegionExit_Block", new Vector3(0, 0, 12));
        var spawn = Place("SpawnMarker_Block", new Vector3(0, 0, -9.5f));
        for (int row = 0; row < 4; row++)
        for (int column = 0; column < 5; column++)
            Place("FarmPlot_Block", new Vector3((column - 2) * 2.1f, .09f, -3.5f + row * 2.1f));
        foreach (Vector3 location in new[] { new Vector3(-10,0,-10), new Vector3(10,0,-10), new Vector3(-11,0,4), new Vector3(11,0,4) })
            Place("Tree_Block", location);
        Place("Rock_Block", new Vector3(8, 0, 0));
        Place("Rock_Block", new Vector3(10, 0, -3), new Vector3(1.5f, 1, 1));
        for (int index = 0; index < 13; index++)
        {
            float along = -12 + index * 2;
            Place("Fence_Block", new Vector3(along, 0, -13));
            if (Mathf.Abs(along) > 1) Place("Fence_Block", new Vector3(along, 0, 13));
            Place("Fence_Block", new Vector3(-13, 0, along), null, 90);
            Place("Fence_Block", new Vector3(13, 0, along), null, 90);
        }
        var gameplay = new GameObject("Farm Prototype").AddComponent<FarmPrototype>();
        gameplay.SetBaseMaterial(baseline);
        gameplay.UseSceneMap();
        gameplay.SetSpawnPoint(spawn.transform);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.Euler(55, -35, 0);
        RenderSettings.ambientLight = new Color(.65f, .72f, .79f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var existing in EditorBuildSettings.scenes)
            if (existing.path != ScenePath) scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log($"Blockout ready: {prefabs.Count} prefabs and {ScenePath}");
    }

    private static GameObject Part(GameObject root, string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(root.transform, false);
        part.transform.localPosition = position; part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }

    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    private static void Validate()
    {
        // Instantiate the saved asset to verify serialized materials and furrows survive prefab creation.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/FarmPlot_Block.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var plot = instance.GetComponent<FarmPlot>();
        if (plot.IsTilled || plot.Plant(null) || !plot.Till() || plot.Till())
            throw new InvalidOperationException("Farm plot prefab state validation failed.");
        if (instance.GetComponent<Renderer>().sharedMaterial == null || instance.transform.childCount != 3)
            throw new InvalidOperationException("Farm plot prefab lost materials or furrows.");
        UnityEngine.Object.DestroyImmediate(instance);
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/Water_Block.prefab").GetComponentsInChildren<Collider>().Length != 0)
            throw new InvalidOperationException("Water placeholder must not block movement.");
    }

    public static void CapturePreview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(25, 30, -30);
        camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true;
        camera.orthographicSize = 19;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.65f, .76f, .85f);
        var target = new RenderTexture(1200, 900, 24);
        camera.targetTexture = target;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        camera.Render();
        ShaderUtil.allowAsyncCompilation = asyncCompilation;
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
        pixels.Apply();
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllBytes("Logs/first-farm-preview.png", pixels.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(pixels);
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("First farm preview saved.");
    }
}
