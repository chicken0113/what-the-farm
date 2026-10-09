using System;
using UnityEditor;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class NpcFirearmBuilder
{
    const string Source = "Assets/Low Poly Weapon Series V 4/Prefabs/Low Poly Series V 4 New/Weapons/WWII_SMG_B.prefab";
    const string Output = "Assets/Prefabs/Game/MerchantSMG.prefab";
    [MenuItem("What The Farm/Configure Merchant Firearm")]
    public static void Install()
    {
        var root = new GameObject("Merchant SMG");
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (prefab == null) throw new InvalidOperationException("Existing SMG model not found.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab); visual.name = "Gun Model";
            visual.transform.SetParent(root.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale *= .7f / Mathf.Max(.001f, bounds.size.z);
            bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Migration/Low Poly Weapon_2a461ee17a7f0b8449bd2c6e34b0e286.mat");
            if (material == null || !material.shader.name.StartsWith("Universal Render Pipeline/")) throw new InvalidOperationException("Existing URP weapon material missing.");
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials; for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            float scale = visual.transform.localScale.x;
            var muzzle = Point(root, "Muzzle", new Vector3(0, .08825f * scale, bounds.max.z + .01f));
            var right = Point(root, "Right Grip", new Vector3(0, -.025f * scale, -.0625f * scale));
            var left = Point(root, "Left Grip", new Vector3(0, .04f * scale, .25f * scale));
            root.AddComponent<NpcFirearm>().Configure(muzzle, right, left);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Output).GetComponent<NpcFirearm>();
            var npc = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Blockout/BuyerNPC.prefab");
            try
            {
                var serialized = new SerializedObject(npc.GetComponent<FarmGuardian>());
                serialized.FindProperty("firearmPrefab").objectReferenceValue = saved;
                serialized.FindProperty("attackRange").floatValue = 18;
                serialized.FindProperty("preferredCombatDistance").floatValue = 7;
                serialized.FindProperty("attackInterval").floatValue = .9f;
                serialized.FindProperty("attackDamage").floatValue = 12;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(npc, "Assets/Prefabs/Blockout/BuyerNPC.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(npc); }
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static Transform Point(GameObject parent, string name, Vector3 position)
    {
        var point = new GameObject(name).transform; point.SetParent(parent.transform, false); point.localPosition = position; return point;
    }
    public static void ValidateInBatch() { Install(); UnityMigrationBatchCheck.PlayExisting(); }
}
