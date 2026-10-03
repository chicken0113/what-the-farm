using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class NpcMerchantBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Blockout/BuyerNPC.prefab";

    public static GameObject CreatePrefab()
    {
        var root = new GameObject("BuyerNPC");
        Material skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/NpcSkin.mat");
        if (skin == null)
        {
            skin = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
            skin.color = new Color(.88f, .65f, .43f);
            AssetDatabase.CreateAsset(skin, "Assets/Materials/Blockout/NpcSkin.mat");
        }
        Material coat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Sell.mat");
        Material hat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blockout/Wood.mat");
        void Part(string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        Part("Coat", PrimitiveType.Capsule, new Vector3(0, .9f, 0), new Vector3(.65f, .5f, .4f), coat);
        Part("Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * .45f, skin);
        Part("Hat brim", PrimitiveType.Cylinder, new Vector3(0, 1.77f, 0), new Vector3(.7f, .04f, .7f), hat);
        Part("Hat", PrimitiveType.Cylinder, new Vector3(0, 1.88f, 0), new Vector3(.42f, .1f, .42f), hat);
        foreach (float x in new[] { -.2f, .2f })
            Part("Boot", PrimitiveType.Cube, new Vector3(x, .25f, 0), new Vector3(.2f, .5f, .3f), hat);
        foreach (float x in new[] { -.4f, .4f })
            Part("Arm", PrimitiveType.Cube, new Vector3(x, .95f, 0), new Vector3(.16f, .65f, .18f), coat);
        foreach (float x in new[] { -.09f, .09f })
            Part("Eye", PrimitiveType.Sphere, new Vector3(x, 1.58f, -.21f), Vector3.one * .06f, hat);
        var body = root.AddComponent<CapsuleCollider>();
        body.center = new Vector3(0, .95f, 0); body.height = 1.9f; body.radius = .34f;
        var receiver = root.AddComponent<SphereCollider>();
        receiver.isTrigger = true; receiver.center = new Vector3(0, 1, 0); receiver.radius = 1.05f;
        root.AddComponent<NpcMerchant>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    public static void Install()
    {
        var prefab = CreatePrefab();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FirstFarm.unity");
        var world = UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
        var npc = UnityEngine.Object.FindFirstObjectByType<NpcMerchant>();
        if (npc == null)
        {
            npc = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<NpcMerchant>();
            npc.transform.position = new Vector3(9, 0, 7.5f);
        }
        npc.BindWorld(world);
        EditorSceneManager.SaveScene(scene);
        Validate();
        Debug.Log("Buyer NPC installed in FirstFarm.");
    }

    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var npc = UnityEngine.Object.Instantiate(prefab).GetComponent<NpcMerchant>();
        var world = new GameObject("Sale validation world").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        npc.BindWorld(world);
        long expected = world.Gold;
        foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
        {
            var item = world.CreateItem(kind, 2, 10, Vector3.one * 100);
            if (npc.TrySell(item)) throw new InvalidOperationException("Unthrown item was sold.");
            item.MarkThrown();
            item.GetComponent<Rigidbody>().isKinematic = true;
            if (npc.TrySell(item)) throw new InvalidOperationException("Held item was sold.");
            item.GetComponent<Rigidbody>().isKinematic = false;
            item.MarkHeld();
            if (npc.TrySell(item)) throw new InvalidOperationException("Picking up did not cancel sale eligibility.");
            item.MarkThrown();
            expected += item.Value;
            if (!npc.TrySell(item) || world.Gold != expected || npc.TrySell(item))
                throw new InvalidOperationException("Sale value or duplicate sale failed.");
        }
        var protectedItem = world.CreateItem(ItemKind.Tool, 0, 10, Vector3.one * 100);
        protectedItem.MarkThrown();
        protectedItem.gameObject.AddComponent<FleeingCrop>();
        if (npc.TrySell(protectedItem) || world.Gold != expected)
            throw new InvalidOperationException("Planted item was sold.");
        npc.Talk(); npc.Talk(); npc.Talk();
        UnityEngine.Object.DestroyImmediate(npc.gameObject);
        UnityEngine.Object.DestroyImmediate(world.gameObject);
        Debug.Log("NPC dialogue, all item sales, held/planted protection and duplicate sale validation passed.");
    }

    public static void ValidateStockRefill()
    {
        var world = new GameObject("Stock refill validation").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var location = new Vector3(100, 1, 100);
        var stock = world.CreateRestockingItem(ItemKind.Seed, 2, 10, location);
        for (int index = 0; index < 5; index++)
        {
            int count = world.GetComponentsInChildren<FarmItem>().Length;
            stock.GetComponent<Collider>().enabled = false;
            stock.GetComponent<Rigidbody>().isKinematic = true;
            stock.MarkHeld();
            var items = world.GetComponentsInChildren<FarmItem>();
            if (items.Length != count + 1) throw new InvalidOperationException("Stock was not refilled immediately.");
            var replacement = items[items.Length - 1];
            if (replacement == stock || replacement.Kind != stock.Kind || replacement.Generation != stock.Generation ||
                replacement.Value != stock.Value || replacement.transform.position != location ||
                !replacement.GetComponent<Collider>().enabled || replacement.GetComponent<Rigidbody>().isKinematic)
                throw new InvalidOperationException("Replacement stock differs from the supplied item.");
            stock.MarkHeld();
            if (world.GetComponentsInChildren<FarmItem>().Length != items.Length)
                throw new InvalidOperationException("Recollecting an owned item duplicated stock.");
            stock = replacement;
        }
        var ordinary = world.CreateItem(ItemKind.Produce, 1, 10, location);
        int ordinaryCount = world.GetComponentsInChildren<FarmItem>().Length;
        ordinary.MarkHeld();
        if (world.GetComponentsInChildren<FarmItem>().Length != ordinaryCount)
            throw new InvalidOperationException("Harvested items unexpectedly refilled stock.");
        UnityEngine.Object.DestroyImmediate(world.gameObject);
        Debug.Log("Immediate stock refill, repeat pickup and ordinary item protection validation passed.");
    }

    public static void InstallAndValidateThrow()
    {
        Install();
        SessionState.SetBool("BuyerThrowValidation", true);
        StartThrowValidation();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void ResumeThrowValidation()
    {
        if (SessionState.GetBool("BuyerThrowValidation", false))
            EditorApplication.delayCall += StartThrowValidation;
    }

    private static bool validationRunning;
    private static void StartThrowValidation()
    {
        if (validationRunning) return;
        validationRunning = true;
        double deadline = EditorApplication.timeSinceStartup + 40;
        FarmItem thrown = null;
        FarmPrototype world = null;
        long expected = 0;
        bool started = false;
        void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("NPC throw reception timed out.");
                if (!EditorApplication.isPlaying || Time.timeSinceLevelLoad < .5f) return;
                if (!started)
                {
                    world = UnityEngine.Object.FindFirstObjectByType<FarmPrototype>();
                    var npc = UnityEngine.Object.FindFirstObjectByType<NpcMerchant>();
                    var farmer = UnityEngine.Object.FindFirstObjectByType<LocalFarmer>();
                    var camera = farmer.GetComponentInChildren<Camera>();
                    farmer.transform.position = npc.transform.position + Vector3.back * 2.7f + Vector3.up * .1f;
                    camera.transform.LookAt(npc.transform.position + Vector3.up);
                    thrown = world.CreateItem(ItemKind.Produce, 2, 10, camera.transform.position);
                    thrown.MarkHeld();
                    thrown.GetComponent<Rigidbody>().isKinematic = true;
                    foreach (Collider collider in thrown.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    thrown.transform.SetParent(camera.transform, true);
                    var field = typeof(LocalFarmer).GetField("inventory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var slots = (FarmItem[])field.GetValue(farmer);
                    slots[0] = thrown;
                    expected = world.Gold + thrown.Value;
                    farmer.ThrowSelectedItem();
                    if (farmer.HeldItem != null || !thrown.WasThrown)
                        throw new InvalidOperationException("Throw did not release inventory item.");
                    started = true;
                    deadline = EditorApplication.timeSinceStartup + 5;
                }
                else if (world.Gold == expected && thrown == null)
                {
                    EditorApplication.update -= Tick;
                    SessionState.SetBool("BuyerThrowValidation", false);
                    Debug.Log("Actual Q throw, NPC trigger reception and gold payout validation passed.");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool("BuyerThrowValidation", false);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
        EditorApplication.update += Tick;
    }
}
