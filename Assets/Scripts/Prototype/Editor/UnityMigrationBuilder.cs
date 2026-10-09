using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class UnityMigrationBuilder
{
    const string Farm = "Assets/LowPolyFarmLite/Prefabs/";
    const string Game = "Assets/Prefabs/Game/";
    const string Mats = "Assets/Materials/Migration/";
    static readonly Dictionary<Material, Material> Materials = new();
    public static void Install()
    {
        if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Could not save the open scene.");
        Directory.CreateDirectory("Library/WhatTheFarmBackups");
        if (!File.Exists("Library/WhatTheFarmBackups/FirstFarm-before-migration.unity")) File.Copy("Assets/Scenes/FirstFarm.unity", "Library/WhatTheFarmBackups/FirstFarm-before-migration.unity");
        Folder("Assets/Materials", "Migration");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(8).stringValue) && layers.GetArrayElementAtIndex(8).stringValue != "LooseItem") throw new InvalidOperationException("Layer 8 is already in use");
        if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(9).stringValue) && layers.GetArrayElementAtIndex(9).stringValue != "FarmCharacter") throw new InvalidOperationException("Layer 9 is already in use");
        layers.GetArrayElementAtIndex(8).stringValue = "LooseItem";
        layers.GetArrayElementAtIndex(9).stringValue = "FarmCharacter";
        tags.ApplyModifiedPropertiesWithoutUndo();
        Materials.Clear();
        var hoe = Item("Hoe", "Assets/Low Poly Weapon Series V 4/Prefabs/Low Poly Series V 4 New/Close Combat/Shovel_A.prefab", ItemKind.Tool, .27f, true);
        var can = Item("WateringCan", Farm+"WateringCan_01.prefab", ItemKind.WateringCan, .168f);
        var stone = Item("Stone", Farm+"Rock_04.prefab", ItemKind.Curio, .15f);
        var produce = Item("Produce", Farm+"Cabbage_01.prefab", ItemKind.Produce, .195f);
        var bindings = new[] {
            new FarmPrototype.ItemPrefabBinding {kind=ItemKind.Tool,prefab=hoe},
            new FarmPrototype.ItemPrefabBinding {kind=ItemKind.WateringCan,prefab=can},
            new FarmPrototype.ItemPrefabBinding {kind=ItemKind.Curio,prefab=stone},
            new FarmPrototype.ItemPrefabBinding {kind=ItemKind.Produce,prefab=produce}
        };
        ReplaceVisual("Assets/Prefabs/Blockout/Tree_Block.prefab", Farm+"Tree_04.prefab", 4);
        ReplaceVisual("Assets/Prefabs/Blockout/Rock_Block.prefab", Farm+"Rock_04.prefab", 1.3f);
        var guardian = GuardianPrefab();
        foreach (var path in new[] {"Assets/Scenes/FirstFarm.unity", "Assets/Scenes/FarmPrototype.unity"})
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var world in root.GetComponentsInChildren<FarmPrototype>(true))
                {
                    world.SetItemPrefabs(bindings);
                    EditorUtility.SetDirty(world);
                    if (!path.EndsWith("FirstFarm.unity")) { world.SetArenaHalfSize(12.2f); continue; }
                    world.SetArenaHalfSize(49.2f);
                    var stage = world.GetComponent<FarmFirstStage>();
                    if (stage != null) continue; // Keep designer changes after the initial migration.
                    var ground = UnityEngine.Object.FindFirstObjectByType<SoilSurface>();
                    if (ground == null) throw new InvalidOperationException("FirstFarm soil is missing");
                    Vector3 size = ground.GetComponent<Collider>().bounds.size;
                    Transform groundRoot = ground.transform.parent != null ? ground.transform.parent : ground.transform;
                    Vector3 scale = groundRoot.localScale;
                    groundRoot.localScale = new Vector3(scale.x*100/size.x, scale.y, scale.z*100/size.z);
                    foreach (var candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                        if (candidate != null && candidate.name.StartsWith("Fence_Block")) UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                    var oldArea = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    oldArea.name = "Original Farm Area - Visual Only";
                    oldArea.transform.position = new Vector3(0, .0025f, 0);
                    oldArea.transform.localScale = new Vector3(26, .005f, 26);
                    UnityEngine.Object.DestroyImmediate(oldArea.GetComponent<Collider>());
                    oldArea.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Untilled.mat");
                    stage = world.gameObject.AddComponent<FarmFirstStage>(); stage.Configure(guardian);
                    var gate = Array.Find(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None), t => t.name.StartsWith("RegionExit_Block"));
                    if (gate == null) throw new InvalidOperationException("FirstFarm exit is missing");
                    gate.position = new Vector3(0, 0, 30);
                    gate.gameObject.AddComponent<FarmStageExit>().Configure(stage);
                }
            EditorSceneManager.SaveScene(scene);
        }
        const string next = "Assets/Scenes/StageTwo.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(next) == null)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var soilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blockout/Ground_Block.prefab");
            var ground = (GameObject)PrefabUtility.InstantiatePrefab(soilPrefab);
            ground.transform.localScale = new Vector3(15, 1, 15);
            var world = new GameObject("Farm Prototype").AddComponent<FarmPrototype>();
            world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
            world.UseSceneMap(); world.SetSupplySpawning(false); world.SetArenaHalfSize(29.2f); world.SetItemPrefabs(bindings);
            var spawn = new GameObject("Spawn"); world.SetSpawnPoint(spawn.transform);
            PlantGrowthBuilder.ConfigureWorld(world);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.5f; light.transform.rotation=Quaternion.Euler(55,-35,0);
            PlantGrowthBuilder.ConfigureGround(ground.GetComponentInChildren<SoilSurface>(), light);
            RenderSettings.ambientLight = new Color(.65f,.72f,.79f);
            EditorSceneManager.SaveScene(scene, next);
        }
        var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!build.Exists(s => s.path == next)) build.Add(new EditorBuildSettingsScene(next,true));
        EditorBuildSettings.scenes = build.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        File.WriteAllLines("Library/WhatTheFarmMigration.dependencies", AssetDatabase.GetDependencies(new[]{"Assets/Scenes/FirstFarm.unity", "Assets/Scenes/StageTwo.unity", "Assets/Prefabs/Game/Produce.prefab"}, true));
        var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/LowPolyFarmLite","Assets/Easy3D"});
        File.WriteAllText("Library/WhatTheFarmMigration.assets", "New pack animation clips: "+clips.Length+"\nLowPolyFarmLite static prefabs: "+AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/LowPolyFarmLite"}).Length+"\nEasy3D static prefabs: "+AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Easy3D"}).Length);
        Debug.Log("UNITY_MIGRATION_INSTALL_SUCCESS");
    }
    static void Folder(string parent,string name) { if (!AssetDatabase.IsValidFolder(parent+"/"+name)) AssetDatabase.CreateFolder(parent,name); }
    static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("No model renderers: "+model.name);
        var bounds = renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static Material Convert(Material source)
    {
        if (source == null) return AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat");
        if (source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
        if (Materials.TryGetValue(source,out var cached)) return cached;
        string path=Mats+source.name+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var serialized = new SerializedObject(source);
            var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            for(int i=0;i<textures.arraySize;i++)
            {
                var pair = textures.GetArrayElementAtIndex(i);
                string key=pair.FindPropertyRelative("first").stringValue;
                if (key!="_MainTex" && key!="_BaseMap" && key!="_BaseColorMap") continue;
                var texture=pair.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if(texture != null) material.SetTexture("_BaseMap",texture);
            }
            material.SetColor("_BaseColor",source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            material.SetFloat("_Smoothness",.1f);
            AssetDatabase.CreateAsset(material,path);
        }
        Materials[source]=material; return material;
    }
    static GameObject Visual(GameObject root,string source,float height,bool rotate=false)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if(prefab == null) throw new InvalidOperationException("Missing model: "+source);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name="Visual"; model.transform.SetParent(root.transform,false);
        if(rotate) model.transform.localRotation=Quaternion.Euler(-90,0,0);
        var bounds=BoundsOf(model);
        model.transform.localScale *= height/Mathf.Max(.001f,bounds.size.y);
        bounds=BoundsOf(model);
        model.transform.localPosition -= bounds.center-root.transform.position-Vector3.up*bounds.extents.y;
        foreach(var collider in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        { var mats=renderer.sharedMaterials; for(int i=0;i<mats.Length;i++) mats[i]=Convert(mats[i]); renderer.sharedMaterials=mats; }
        return model;
    }
    static FarmItem Item(string name,string source,ItemKind kind,float height,bool rotate=false)
    {
        var root=new GameObject(name);
        var model=Visual(root,source,height,rotate);
        var bounds=BoundsOf(model);
        var collider=root.AddComponent<BoxCollider>(); collider.center=bounds.center-root.transform.position; collider.size=bounds.size;
        root.AddComponent<Rigidbody>().mass=.5f;
        var item=root.AddComponent<FarmItem>(); item.Configure(kind,0,kind==ItemKind.Tool?16:kind==ItemKind.WateringCan?14:kind==ItemKind.Curio?6:10);
        // Existing references keep their GUID when their prefab is updated.
        var saved=PrefabUtility.SaveAsPrefabAsset(root,Game+name+".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return saved.GetComponent<FarmItem>();
    }
    static void ReplaceVisual(string target,string source,float height)
    {
        var root=PrefabUtility.LoadPrefabContents(target);
        try
        {
            var old=root.transform.Find("Visual"); if(old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var model = Visual(root,source,height);
            var bounds = BoundsOf(model);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            PrefabUtility.SaveAsPrefabAsset(root,target);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static FarmGuardian GuardianPrefab()
    {
        string path=Game+"Guardian.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(existing != null) return existing.GetComponent<FarmGuardian>();
        var root=new GameObject("Guardian"); root.layer=9;
        var controller=root.AddComponent<CharacterController>(); controller.radius=.55f; controller.height=1.8f; controller.center=Vector3.up*.9f;
        var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Visual"; body.transform.SetParent(root.transform,false);
        body.transform.localPosition=Vector3.up*.9f; body.transform.localScale=new Vector3(1.1f,.9f,1.1f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=new Color(.65f,.035f,.025f);
        AssetDatabase.CreateAsset(material,Mats+"Guardian.mat"); body.GetComponent<Renderer>().sharedMaterial=material;
        root.AddComponent<FarmGuardian>();
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<FarmGuardian>();
    }
}
