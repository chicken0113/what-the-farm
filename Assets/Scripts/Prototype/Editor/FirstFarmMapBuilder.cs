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
        Save("Water_Block", root => Part(root, "Water (placeholder)", new Vector3(0, .025f, 0), new Vector3(3, .05f, 3), mats["Water"]));
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
        prefabs["BuyerNPC"] = NpcMerchantBuilder.CreatePrefab();

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
        var buyer = Place("BuyerNPC", new Vector3(9, 0, 7.5f)).GetComponent<NpcMerchant>();
        Place("RegionExit_Block", new Vector3(0, 0, 12));
        var spawn = Place("SpawnMarker_Block", new Vector3(0, 0, -9.5f));
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
        buyer.BindWorld(gameplay);
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
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/Ground_Block.prefab")
            .GetComponentInChildren<SoilSurface>() == null)
            throw new InvalidOperationException("Ground must support local tilling.");
    }

    public static void MigrateGround()
    {
        var water = PrefabUtility.LoadPrefabContents($"{Kit}/Water_Block.prefab");
        var surface = water.GetComponentInChildren<MeshRenderer>().gameObject;
        if (surface.GetComponent<Collider>() == null) surface.AddComponent<BoxCollider>();
        PrefabUtility.SaveAsPrefabAsset(water, $"{Kit}/Water_Block.prefab");
        PrefabUtility.UnloadPrefabContents(water);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var plot in root.GetComponentsInChildren<FarmPlot>(true))
                UnityEngine.Object.DestroyImmediate(plot.gameObject);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.DeleteAsset($"{Kit}/FarmPlot_Block.prefab");
        AssetDatabase.SaveAssets();
        ValidateGround();
    }

    public static void ValidateGround()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.transform.position = new Vector3(100, -.125f, 100);
        root.transform.localScale = new Vector3(26, .25f, 26);
        var soil = root.AddComponent<SoilSurface>();
        var dry = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Wet.mat");
        Physics.SyncTransforms();
        var center = new Vector3(100, 0, 100);
        if (soil.FindPlot(center) != null) throw new InvalidOperationException("Untilled ground accepted.");
        var first = soil.Till(center, .8f, dry, wet);
        if (first == null || !first.IsTilled || first.IsWatered || first.Plant(null) ||
            soil.FindPlot(center + Vector3.right * .5f) != first ||
            soil.FindPlot(center + Vector3.right * 2) != null ||
            soil.Till(center, .8f, dry, wet) != null)
            throw new InvalidOperationException("Local tilling validation failed.");
        var second = soil.Till(center + Vector3.right * 3, .8f, dry, wet);
        var crop = new GameObject("Validation crop").AddComponent<FleeingCrop>();
        if (!first.Plant(crop)) throw new InvalidOperationException("Planting failed.");
        first.Water();
        if (!first.IsWatered || second.IsWatered || second.IsOccupied)
            throw new InvalidOperationException("Soil states leaked between areas.");
        first.Clear(crop);
        if (first.IsOccupied || first.IsWatered || !first.IsTilled)
            throw new InvalidOperationException("Harvest soil reset failed.");
        var edge = soil.Till(center + Vector3.right * 12.8f, .8f, dry, wet);
        foreach (var vertex in edge.GetComponent<MeshFilter>().sharedMesh.vertices)
            if (edge.transform.TransformPoint(vertex).x > 113.001f)
                throw new InvalidOperationException("Tilled soil extends beyond ground.");
        UnityEngine.Object.DestroyImmediate(crop.gameObject);
        UnityEngine.Object.DestroyImmediate(root);
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.position = new Vector3(100, 0, 100);
        Physics.SyncTransforms();
        var planeArea = plane.AddComponent<SoilSurface>().Till(center + Vector3.right * 4.8f, .8f, dry, wet);
        foreach (var vertex in planeArea.GetComponent<MeshFilter>().sharedMesh.vertices)
            if (planeArea.transform.TransformPoint(vertex).x > 105.001f)
                throw new InvalidOperationException("Plane soil extends beyond ground.");
        UnityEngine.Object.DestroyImmediate(plane);
        Debug.Log("Local ground tilling validation passed.");
    }

    public static void CaptureTillingPreview()
    {
        ValidateGround();
        EditorSceneManager.OpenScene(ScenePath);
        var soil = UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
        var dry = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Wet.mat");
        soil.Till(new Vector3(-2, 0, -2), .8f, dry, wet);
        soil.Till(new Vector3(0, 0, -2), .8f, dry, wet);
        soil.Till(new Vector3(2, 0, -2), 1.2f, dry, wet);
        CapturePreviewImage();
    }

    public static void ValidatePlantingGrowth()
    {
        ValidateGround();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(100, -.125f, 100);
        ground.transform.localScale = new Vector3(26, .25f, 26);
        var soil = ground.AddComponent<SoilSurface>();
        var dry = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Tilled.mat");
        var wet = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Wet.mat");
        var origin = new Vector3(100, 0, 100);
        Physics.SyncTransforms();
        var plot = soil.Till(origin, .8f, dry, wet);
        var world = new GameObject("Validation world").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var source = new GameObject("Plantable source").AddComponent<FarmItem>();
        FleeingCrop Plant(ItemKind kind, FarmPlot area, Vector3 point)
        {
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            source = world.CreateItem(kind, 0, 10, origin + Vector3.right * 20);
            var sourceSize = source.transform.lossyScale;
            var sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var sourceColor = source.GetComponent<Renderer>().sharedMaterial.color;
            source.GetComponent<Collider>().enabled = false;
            source.GetComponent<Rigidbody>().isKinematic = true;
            if (!world.TryPlant(source, area, point)) throw new InvalidOperationException("Free planting rejected.");
            var all = world.GetComponentsInChildren<FleeingCrop>();
            var crop = all[all.Length - 1];
            if (Mathf.Abs(crop.transform.position.x - point.x) > .001f ||
                Mathf.Abs(crop.transform.position.z - point.z) > .001f ||
                Mathf.Abs(crop.GetComponent<Renderer>().bounds.min.y - point.y) > .001f)
                throw new InvalidOperationException("Plant moved away from aim point or floated above soil.");
            if (Vector3.Distance(crop.transform.lossyScale, sourceSize) > .001f ||
                crop.GetComponent<MeshFilter>().sharedMesh != sourceMesh ||
                crop.GetComponent<FarmItem>() != null || !crop.GetComponent<Collider>().enabled ||
                crop.GetComponent<Renderer>().sharedMaterial.color != sourceColor)
                throw new InvalidOperationException("Plant lost its original shape or size.");
            return crop;
        }
        var growing = Plant(ItemKind.Seed, plot, origin + Vector3.left * .4f);
        Vector3 initialSize = growing.transform.localScale;
        var far = Plant(ItemKind.Seed, plot, origin + Vector3.right * .6f);
        var victims = new List<FleeingCrop>();
        foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            victims.Add(Plant(kind, plot, origin + Vector3.left * .2f));
        if (world.TryPlant(source, plot, origin + Vector3.right * 2))
            throw new InvalidOperationException("Planting outside tilled range accepted.");

        var scenery = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scenery.transform.position = growing.transform.position;
        var loose = world.CreateItem(ItemKind.Seed, 0, 10, growing.transform.position);
        var freeCrop = GameObject.CreatePrimitive(PrimitiveType.Capsule).AddComponent<FleeingCrop>();
        freeCrop.transform.position = growing.transform.position;
        var maturePlot = soil.Till(origin + Vector3.right * 4, .8f, dry, wet);
        var mature = Plant(ItemKind.Produce, maturePlot, maturePlot.transform.position);
        maturePlot.Water(); mature.Grow(10);
        mature.transform.position = growing.transform.position;
        if (mature.IsPlanted || !mature.IsMature) throw new InvalidOperationException("Mature crop remains rooted.");
        growing.Grow(3);
        if (victims.Exists(crop => crop == null)) throw new InvalidOperationException("Dry plant destroyed neighbours.");
        plot.Water(); growing.Grow(3);
        if (Vector3.Distance(growing.transform.localScale, initialSize * 1.75f) > .001f ||
            Mathf.Abs(growing.GetComponent<Renderer>().bounds.min.y) > .001f)
            throw new InvalidOperationException("Growth lost original proportions or soil alignment.");
        if (victims.Exists(crop => crop != null) || !growing.IsPlanted || !far.IsPlanted)
            throw new InvalidOperationException($"Growth overlap failed: remaining={victims.FindAll(crop => crop != null).Count}, grower={growing.IsPlanted}, distant={far.IsPlanted}.");
        if (scenery == null || loose == null || freeCrop == null || mature == null || ground == null)
            throw new InvalidOperationException("Growth destroyed a protected object.");
        if (!plot.IsOccupied || !plot.IsWatered)
            throw new InvalidOperationException("Destroying a neighbour reset surviving plants' soil.");

        var adjoining = soil.Till(origin + Vector3.forward * 1.7f, .8f, dry, wet);
        var edgeGrower = Plant(ItemKind.Seed, plot, origin + Vector3.forward * .7f);
        var edgeVictim = Plant(ItemKind.Seed, adjoining, origin + Vector3.forward * 1.0f);
        edgeGrower.Grow(3);
        if (edgeVictim != null) throw new InvalidOperationException("Growth missed a neighbour in another soil area.");

        var farmer = new GameObject("Aim validation").AddComponent<LocalFarmer>();
        var camera = new GameObject("Aim camera").AddComponent<Camera>();
        camera.transform.position = far.transform.position + Vector3.up * 2;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        farmer.Configure(world, camera, 12);
        Physics.SyncTransforms();
        if (!farmer.TryLookSoil(out RaycastHit aim) || Vector3.Distance(aim.point, origin + Vector3.right * .6f) > .001f)
            throw new InvalidOperationException("Existing crop blocks planting aim.");
        var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstruction.transform.position = origin + new Vector3(.6f, .7f, 0);
        Physics.SyncTransforms();
        if (farmer.TryLookSoil(out _)) throw new InvalidOperationException("Planting aim passes through solid scenery.");
        var compound = new GameObject("Compound item").AddComponent<FarmItem>();
        compound.Configure(ItemKind.Tool, 0, 10);
        compound.transform.localScale = new Vector3(.7f, 1.2f, .8f);
        foreach (float x in new[] { -.3f, .3f })
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(compound.transform, false);
            part.transform.localPosition = new Vector3(x, .5f, 0);
            part.transform.localScale = new Vector3(.2f, 1, .3f);
            part.GetComponent<Renderer>().sharedMaterial = dry;
            part.GetComponent<Collider>().enabled = false;
        }
        var compoundPlot = soil.Till(origin + Vector3.left * 4, .8f, dry, wet);
        if (!world.TryPlant(compound, compoundPlot, compoundPlot.transform.position))
            throw new InvalidOperationException("Compound model planting failed.");
        var planted = world.GetComponentsInChildren<FleeingCrop>();
        var compoundCrop = planted[planted.Length - 1];
        if (compoundCrop.GetComponentsInChildren<Renderer>().Length != 2 ||
            compoundCrop.GetComponentsInChildren<Collider>().Length != 2 ||
            Vector3.Distance(compoundCrop.transform.lossyScale, compound.transform.lossyScale) > .001f)
            throw new InvalidOperationException("Compound model or size was lost.");
        compoundPlot.Water(); compoundCrop.Grow(1);
        foreach (Renderer renderer in compoundCrop.GetComponentsInChildren<Renderer>())
            if (Mathf.Abs(renderer.bounds.min.y) > .001f)
                throw new InvalidOperationException("Compound model floats during growth.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("Original shape/size, free planting and protected growth destruction validation passed.");
    }

    public static void CapturePreview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        CapturePreviewImage();
    }

    private static void CapturePreviewImage()
    {
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
