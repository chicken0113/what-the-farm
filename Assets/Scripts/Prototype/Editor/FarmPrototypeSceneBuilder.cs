using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class FarmPrototypeSceneBuilder
{
    [MenuItem("What The Farm/Create Prototype Scene")]
    public static void Build()
    {
        const string path = "Assets/Scenes/FarmPrototype.unity";
        const string materialPath = "Assets/Settings/PrototypeBaseMaterial.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("URP Lit shader was not found.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prototype = new GameObject("Farm Prototype").AddComponent<FarmPrototype>();
        prototype.SetBaseMaterial(material);
        EditorSceneManager.SaveScene(scene, path);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {path}");
    }
}
