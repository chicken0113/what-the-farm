using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WhatTheFarm.Prototype;

public sealed class ItemPriceWindow : EditorWindow
{
    public const string CatalogPath = "Assets/Resources/ItemPrices.asset";
    private Vector2 scroll;
    private string search = "";
    private ItemPriceCatalog catalog;
    [MenuItem("What The Farm/Item Prices")]
    public static void Open() => GetWindow<ItemPriceWindow>("가격 / 성장");
    private void OnEnable()
    {
        minSize = new Vector2(650, 320);
        catalog = EnsureCatalog();
    }
    public static ItemPriceCatalog EnsureCatalog()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ItemPriceCatalog>(CatalogPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            asset = CreateInstance<ItemPriceCatalog>();
            AssetDatabase.CreateAsset(asset, CatalogPath);
        }
        void Add(string id, string label, int buy, int sell)
        {
            if (asset.Find(id) != null) return;
            asset.entries.Add(new ItemPriceCatalog.Entry { id = id, label = label, purchasePrice = buy, salePrice = sell });
            EditorUtility.SetDirty(asset);
        }
        Add("seed", "씨앗", 10, 10);
        Add("produce", "수확물", 10, 10);
        Add("tool", "기본 괭이", 16, 16);
        Add("shovel-head", "삽 머리 (심기 전)", 16, 16);
        Add("shovel", "완성된 삽", 30, 30);
        Add("wateringcan", "물뿌리개", 14, 14);
        Add("curio", "돌 / 잡동사니", 6, 6);
        Add("npc-body", "NPC 시체", 50, 50);
        Add("player-body", "플레이어 시체", 50, 50);
        Add("weed", "잡초", 0, 3);
        // Discover prefab kinds and assigned custom IDs without resetting existing prices.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var item in prefab.GetComponentsInChildren<FarmItem>(true))
                Add(item.PriceId, item.DisplayName, item.BaseValue, item.BaseValue);
        }
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }
    private void OnGUI()
    {
        if (catalog == null) { catalog = EnsureCatalog(); return; }
        EditorGUILayout.LabelField("아이템 구매 / 판매 가격 및 성장 시간", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("성장 시간: 물주기 등 성장 시작 조건을 만족한 뒤 다 자라기까지 걸리는 초입니다.\n5를 입력하면 5초, 10을 입력하면 10초입니다(최소 0.1초). 세대와 관계없이 입력한 시간을 사용합니다.\n성장 중 수정하면 현재 진행률을 유지하며 남은 진행에 적용됩니다. 삽은 삽 머리 행에서 조절합니다.", MessageType.None);
        EditorGUILayout.HelpBox("구매 가격은 즉시 재입고되는 진열 아이템을 E로 구매할 때 적용됩니다. 골드가 부족하면 구매할 수 없습니다. 일반 드롭과 잡초 줍기는 무료입니다.\n판매 가격은 NPC 환전에 즉시 적용됩니다. 시체도 판매할 수 있습니다.\n판매액 = 기본 판매가 × 세대 배율^세대 × 상인 배율. 0골드도 판매됩니다.", MessageType.Info);
        search = EditorGUILayout.TextField("검색", search);
        if (GUILayout.Button("아이템 목록 새로 확인 (가격 유지)")) catalog = EnsureCatalog();
        Undo.RecordObject(catalog, "Edit item prices");
        EditorGUI.BeginChangeCheck();
        catalog.generationMultiplier = Mathf.Max(1, EditorGUILayout.FloatField("세대별 판매가 배율", catalog.generationMultiplier));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("아이템 / 가격 ID", GUILayout.MinWidth(180));
        GUILayout.Label("구매가", GUILayout.Width(80)); GUILayout.Label("기본 판매가", GUILayout.Width(80));
        GUILayout.Label("성장 시간 (초)", GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var row in catalog.entries.Where(row => row != null &&
            (string.IsNullOrEmpty(search) || (row.label + row.id).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(row.label, row.id), GUILayout.MinWidth(180));
            row.purchasePrice = Mathf.Max(0, EditorGUILayout.IntField(row.purchasePrice, GUILayout.Width(80)));
            row.salePrice = Mathf.Max(0, EditorGUILayout.IntField(row.salePrice, GUILayout.Width(80)));
            bool body = row.id == "npc-body" || row.id == "player-body";
            bool finished = row.id == "shovel";
            if (row.id == "weed" || body || finished)
                GUILayout.Label(body ? "체력 회복" : finished ? "성장 완료" : "성장 없음", GUILayout.Width(110));
            else row.growthSeconds = Mathf.Max(.1f, EditorGUILayout.FloatField(row.growthSeconds, GUILayout.Width(110)));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(catalog);
        if (GUILayout.Button("저장")) AssetDatabase.SaveAssetIfDirty(catalog);
        EditorGUILayout.HelpBox("새 종류는 FarmItem의 Custom Price Id에 고유한 이름을 넣고 목록을 새로 확인하세요. 빈 ID는 종류별 가격을 사용합니다. 삽 머리와 완성된 삽, 두 종류의 시체는 자동 구분됩니다.", MessageType.None);
    }
    private void OnDisable() { if (catalog != null) AssetDatabase.SaveAssetIfDirty(catalog); }
}
