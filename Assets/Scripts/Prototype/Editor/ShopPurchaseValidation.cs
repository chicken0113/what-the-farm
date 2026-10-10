using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class ShopPurchaseValidation
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project for this check.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var catalog = ItemPriceCatalog.Active;
        string snapshot = EditorJsonUtility.ToJson(catalog);
        var world = new GameObject("Purchase validation").AddComponent<FarmPrototype>();
        world.SetBaseMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeBaseMaterial.mat"));
        var playerRoot = new GameObject("Buyer");
        var farmer = playerRoot.AddComponent<LocalFarmer>();
        var cameraRoot = new GameObject("Buyer camera"); cameraRoot.transform.SetParent(playerRoot.transform);
        var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(0, 1, -2);
        try
        {
            farmer.Configure(world, camera, 1);
            // The fixture camera is outside the body; keep its own collider out of the item ray.
            farmer.GetComponent<CharacterController>().enabled = false;
            catalog.Find("seed").purchasePrice = 17;
            var stock = world.CreateRestockingItem(ItemKind.Seed, 0, 10, Vector3.zero);
            void Aim(FarmItem item) { Physics.SyncTransforms(); camera.transform.LookAt(item.GetComponent<Collider>().bounds.center); }
            Aim(stock); farmer.Interact();
            Check(world.Gold == 0 && farmer.HeldItem == null && stock.IsShopStock && stock.GetComponent<Collider>().enabled,
                "Zero gold purchase changed inventory/stock");
            Check(world.GetComponentsInChildren<FarmItem>().Count(i => i.IsShopStock) == 1, "Rejected purchase refilled stock");
            world.AddGold(16); Aim(stock); farmer.Interact();
            Check(world.Gold == 16 && farmer.HeldItem == null, "Insufficient gold was spent");
            world.AddGold(1); Aim(stock); farmer.Interact();
            Check(world.Gold == 0 && farmer.HeldItem == stock && !stock.IsShopStock, "Exact-price purchase failed");
            var refill = world.GetComponentsInChildren<FarmItem>().Single(i => i.IsShopStock);
            Check(refill != stock && refill.PurchasePrice == 17 && refill.GetComponent<Collider>().enabled,
                "Purchased stock did not refill as purchasable stock");
            world.AddGold(50); Aim(refill); farmer.Interact();
            Check(world.Gold == 50 && farmer.HeldItem == stock && refill.IsShopStock, "Full inventory consumed gold/stock");
            farmer.RestoreInventory(new FarmItem[1], 0);
            stock.gameObject.SetActive(true); stock.transform.SetParent(world.transform); stock.transform.position = Vector3.right;
            foreach (var collider in stock.GetComponentsInChildren<Collider>()) collider.enabled = true;
            Aim(stock); farmer.Interact();
            Check(farmer.HeldItem == stock && world.Gold == 50, "Re-pickup charged an owned item");
            farmer.RestoreInventory(new FarmItem[1], 0);
            world.RestoreGold(0);
            catalog.Find("seed").purchasePrice = 0;
            Aim(refill); farmer.Interact();
            Check(farmer.HeldItem == refill && world.Gold == 0, "Zero-price purchase failed");
            farmer.RestoreInventory(new FarmItem[1], 0);
            var weed = world.CreateItem(ItemKind.Weed, 0, 3, Vector3.right * 2);
            Aim(weed); farmer.Interact();
            Check(farmer.HeldItem == weed && world.Gold == 0, "Wild pickup incorrectly charged");
            Debug.Log("SHOP_PURCHASE_VALIDATION_SUCCESS: zero/insufficient/exact gold, refill, full inventory, owned pickup, free stock and wild pickup.");
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(snapshot, catalog);
            UnityEngine.Object.DestroyImmediate(playerRoot);
            UnityEngine.Object.DestroyImmediate(world.gameObject);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
