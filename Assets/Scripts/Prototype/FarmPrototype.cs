using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPrototype : MonoBehaviour
    {
        private const float HalfSize = 13f;
        [SerializeField] private Material baseMaterial;
        [SerializeField, Min(1)] private int inventorySlotCount = 12;
        [SerializeField, Min(1)] private float arenaHalfSize = 49.2f;
        [SerializeField] private FarmActionAnimation playerActions;
        [SerializeField] private bool spawnSuppliesOnStart = true;
        public void SetSupplySpawning(bool enabled) => spawnSuppliesOnStart = enabled;
        public void SetArenaHalfSize(float size) => arenaHalfSize = size;
        [SerializeField] private bool buildArenaAtRuntime = true;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField, Min(.1f)] private float tillingRadius = .8f;
        [SerializeField, Min(.01f), Tooltip("Bare-hand tilling radius in metres; 0.08 gives a roughly fist-sized 16 cm patch.")]
        private float bareHandTillingRadius = .08f;
        [SerializeField, Min(.1f)] private float wateringRadius = .8f;
        public float TillingRadiusFor(FarmItem tool) => tool != null ? tillingRadius * tool.SizeMultiplier : bareHandTillingRadius;
        public float WateringRadiusFor(FarmItem tool) => wateringRadius * (tool != null ? tool.SizeMultiplier : 1);
        [SerializeField, Min(0)] private int startingGold;
        [SerializeField, Range(1, 100)] private float waterPerUse = 25;
        [SerializeField] private PlantGrowthProfile defaultGrowthProfile;
        [SerializeField] private SoilType defaultSoilType;
        [System.Serializable]
        public sealed class GrowthBinding
        {
            public ItemKind kind;
            public PlantGrowthProfile profile;
        }
        [SerializeField] private GrowthBinding[] growthProfiles = System.Array.Empty<GrowthBinding>();
        [System.Serializable]
        public sealed class ItemPrefabBinding
        {
            public ItemKind kind;
            public FarmItem prefab;
        }
        [SerializeField] private ItemPrefabBinding[] itemPrefabs = System.Array.Empty<ItemPrefabBinding>();
        public void SetItemPrefabs(ItemPrefabBinding[] bindings) => itemPrefabs = bindings;
        public void SetGrowthDefaults(PlantGrowthProfile fallback, SoilType soil, GrowthBinding[] bindings)
        {
            defaultGrowthProfile = fallback; defaultSoilType = soil; growthProfiles = bindings;
        }
        private PlantGrowthProfile ProfileFor(ItemKind kind)
        {
            if (growthProfiles != null)
                foreach (GrowthBinding binding in growthProfiles)
                    if (binding != null && binding.kind == kind && binding.profile != null) return binding.profile;
            return defaultGrowthProfile;
        }
        public long Gold { get; private set; }
        private string dialogueSpeaker;
        private string dialogueText;
        private float dialogueUntil;

        public void AddGold(int amount) { if (amount > 0) Gold += amount; }
        public void ShowDialogue(string speaker, string text, float seconds)
        {
            dialogueSpeaker = speaker;
            dialogueText = text;
            dialogueUntil = Time.time + seconds;
        }

        public void UseSceneMap() => buildArenaAtRuntime = false;
        public void SetSpawnPoint(Transform spawnPoint) => playerSpawnPoint = spawnPoint;
        private LocalFarmer player;
        private float messageUntil;
        private string message = "Pick up the hoe and till the soil first.";
        private Material soilMaterial;
        private Material dryPlotMaterial;
        private Material wetPlotMaterial;
        private Material seedMaterial;
        private Material toolMaterial;
        private Material wateringCanMaterial;
        private Material curioMaterial;
        private Material cropMaterial;

        public float ArenaHalfSize => arenaHalfSize;
        public LocalFarmer Player => player;
        public Vector3 SpawnPosition => playerSpawnPoint != null ? playerSpawnPoint.position + Vector3.up * .1f : new Vector3(0, .1f, -9.5f);
        public void SetActionPrefab(FarmActionAnimation prefab) => playerActions = prefab;
        public void RestoreGold(long amount) => Gold = amount;
        public Vector3 PlayerPosition => player == null ? Vector3.zero : player.transform.position;

        public void SetBaseMaterial(Material material)
        {
            baseMaterial = material;
        }

        private void Awake()
        {
            Gold = startingGold;
            EnsureMaterials();
            if (buildArenaAtRuntime)
                CreateArena();
            CreatePlayer();
            FarmTravel.Restore(this, player);
            if (!spawnSuppliesOnStart) return;
            CreateRestockingItem(ItemKind.Tool, 0, 16, new Vector3(-2.5f, 0.55f, -7f));
            CreateRestockingItem(ItemKind.WateringCan, 0, 14, new Vector3(-1.2f, 0.55f, -7f));
            CreateRestockingItem(ItemKind.Seed, 0, 10, new Vector3(0f, 0.45f, -7f));
            CreateRestockingItem(ItemKind.Seed, 0, 10, new Vector3(1f, 0.45f, -7f));
            CreateRestockingItem(ItemKind.Seed, 0, 10, new Vector3(2f, 0.45f, -7f));
            CreateRestockingItem(ItemKind.Curio, 0, 6, new Vector3(3.2f, 0.55f, -7f));
        }

        private void EnsureMaterials()
        {
            if (cropMaterial != null) return;
            soilMaterial = MakeMaterial(new Color(0.32f, 0.22f, 0.13f));
            dryPlotMaterial = MakeMaterial(new Color(0.18f, 0.10f, 0.06f));
            wetPlotMaterial = MakeMaterial(new Color(0.15f, 0.22f, 0.31f));
            seedMaterial = MakeMaterial(new Color(0.91f, 0.75f, 0.27f));
            toolMaterial = MakeMaterial(new Color(0.52f, 0.72f, 0.84f));
            wateringCanMaterial = MakeMaterial(new Color(0.18f, 0.68f, 0.93f));
            curioMaterial = MakeMaterial(new Color(0.62f, 0.57f, 0.76f));
            cropMaterial = MakeMaterial(new Color(0.27f, 0.77f, 0.33f));

        }

        private Material MakeMaterial(Color color)
        {
            var material = new Material(baseMaterial);
            material.color = color;
            return material;
        }

        private void CreateArena()
        {
            GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Plane);
            soil.name = "Plantable Soil";
            soil.transform.SetParent(transform);
            soil.transform.localScale = Vector3.one * (HalfSize * 2f / 10f);
            soil.GetComponent<Renderer>().material = soilMaterial;
            var surface = soil.AddComponent<SoilSurface>();
            surface.SetEnvironment(defaultSoilType, 80);

            Material fenceMaterial = MakeMaterial(new Color(0.47f, 0.34f, 0.2f));
            CreateBlock("North Fence", new Vector3(0f, 0.6f, HalfSize),
                new Vector3(HalfSize * 2f, 1.2f, 0.5f), fenceMaterial);
            CreateBlock("South Fence", new Vector3(0f, 0.6f, -HalfSize),
                new Vector3(HalfSize * 2f, 1.2f, 0.5f), fenceMaterial);
            CreateBlock("East Fence", new Vector3(HalfSize, 0.6f, 0f),
                new Vector3(0.5f, 1.2f, HalfSize * 2f), fenceMaterial);
            CreateBlock("West Fence", new Vector3(-HalfSize, 0.6f, 0f),
                new Vector3(0.5f, 1.2f, HalfSize * 2f), fenceMaterial);

            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(transform);
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            surface.SetEnvironment(defaultSoilType, 80, 0, light);
            RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.79f);
        }

        private void CreatePlayer()
        {
            GameObject farmer = new GameObject("Local Farmer");
            farmer.transform.SetParent(transform);
            farmer.transform.position = playerSpawnPoint != null
                ? playerSpawnPoint.position + Vector3.up * .1f
                : new Vector3(0f, 0.1f, -9.5f);
            CharacterController body = farmer.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);

            GameObject eye = new GameObject("Player Camera");
            eye.transform.SetParent(farmer.transform);
            eye.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Camera camera = eye.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 72f;
            camera.clearFlags = CameraClearFlags.Skybox;
            eye.AddComponent<AudioListener>();

            player = farmer.AddComponent<LocalFarmer>();
            player.Configure(this, camera, inventorySlotCount);
            if (playerActions != null)
            {
                var actions = Instantiate(playerActions, camera.transform, false);
                player.SetActions(actions);
            }
        }

        private void CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(transform);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material = material;
        }

        public FarmItem CreateRestockingItem(ItemKind kind, int generation, int baseValue, Vector3 position)
        {
            FarmItem item = CreateItem(kind, generation, baseValue, position);
            Rigidbody stockBody = item.GetComponent<Rigidbody>();
            if (stockBody != null) stockBody.isKinematic = true;
            Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
            { if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds); }
            float floorHeight = 0;
            if (Physics.Raycast(position + Vector3.up * 10, Vector3.down, out var floor, 100,
                ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) floorHeight = floor.point.y;
            if (found) item.transform.position += Vector3.up * (floorHeight + .005f - bounds.min.y);
            item.SetStockRefill(() =>
            {
                if (this != null)
                    CreateRestockingItem(kind, generation, baseValue, position).SetGrowthProfile(item.GrowthProfile);
            });
            return item;
        }

        public FarmItem CreateItem(ItemKind kind, int generation, int baseValue, Vector3 position)
        {
            EnsureMaterials();
            foreach (var binding in itemPrefabs)
            {
                if (binding == null || binding.kind != kind || binding.prefab == null) continue;
                var modelItem = Instantiate(binding.prefab, position, Quaternion.identity, transform);
                modelItem.Configure(kind, generation, baseValue);
                modelItem.SetGrowthProfile(ProfileFor(kind));
                return modelItem;
            }
            PrimitiveType shape = kind switch
            {
                ItemKind.Seed => PrimitiveType.Sphere,
                ItemKind.Produce => PrimitiveType.Capsule,
                ItemKind.Tool => PrimitiveType.Cylinder,
                ItemKind.WateringCan => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };
            GameObject instance = GameObject.CreatePrimitive(shape);
            instance.transform.SetParent(transform);
            instance.transform.position = position;
            instance.transform.localScale = kind == ItemKind.Tool
                ? new Vector3(0.17f, 0.5f, 0.17f)
                : kind == ItemKind.WateringCan
                    ? new Vector3(0.36f, 0.28f, 0.36f)
                : Vector3.one * (kind == ItemKind.Produce ? 0.65f : 0.5f);
            instance.transform.localScale *= .3f;
            instance.GetComponent<Renderer>().material = kind switch
            {
                ItemKind.Seed => seedMaterial,
                ItemKind.Tool => toolMaterial,
                ItemKind.WateringCan => wateringCanMaterial,
                ItemKind.Curio => curioMaterial,
                _ => cropMaterial
            };
            Rigidbody body = instance.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            FarmItem item = instance.AddComponent<FarmItem>();
            item.Configure(kind, generation, baseValue);
            item.SetGrowthProfile(ProfileFor(kind));
            return item;
        }

        public bool TryTill(SoilSurface soil, Vector3 point, FarmItem tool = null)
        {
            if (soil == null) return false;
            if (soil.Till(point, TillingRadiusFor(tool), dryPlotMaterial, wetPlotMaterial) == null)
            {
                SetMessage("This ground is already tilled.");
                return false;
            }
            SetMessage("Ground tilled. Press E here with an item to plant it.");
            return true;
        }

        public bool TryPlant(FarmItem item, FarmPlot plot, Vector3 position)
        {
            if (item == null) return false;
            if (item.HasBeenPlanted)
            { SetMessage("This item was already grown. Use it or sell it; it cannot be planted again."); return false; }
            if (plot == null)
            {
                SetMessage("Till this ground with the hoe before planting.");
                return false;
            }
            if (!plot.IsTilled || !plot.ContainsPoint(position))
            {
                SetMessage("Aim inside the tilled ground before planting.");
                return false;
            }
            if (plot.IsOccupied)
            {
                SetMessage("This tilled area already has a plant. Use an empty area.");
                return false;
            }
            if (item.GrowthProfile == null) item.SetGrowthProfile(ProfileFor(item.Kind));
            EnsureMaterials();
            Vector3 size = item.transform.lossyScale;
            GameObject plant = Instantiate(item.gameObject);
            plant.transform.SetParent(null);
            plant.transform.rotation = Quaternion.identity;
            plant.transform.localScale = size;
            plant.transform.position = position;
            plant.transform.SetParent(transform, true);
            plant.SetActive(true);
            foreach (Collider body in plant.GetComponentsInChildren<Collider>(true))
                body.enabled = true;
            foreach (Rigidbody body in plant.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            foreach (Renderer renderer in plant.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    if (materials[index] != null) materials[index] = new Material(materials[index]);
                renderer.sharedMaterials = materials;
            }
            FarmItem plantedItem = plant.GetComponent<FarmItem>();
            if (Application.isPlaying) Destroy(plantedItem);
            else DestroyImmediate(plantedItem);
            FleeingCrop cropComponent = plant.AddComponent<FleeingCrop>();
            if (!plot.Plant(cropComponent))
            {
                Destroy(plant);
                return false;
            }
            cropComponent.Configure(this, item, plot, position.y);
            item.MarkPlanted();
            SetMessage($"Planted {item.DisplayName}. Use the watering can to start growth.");
            return true;
        }

        public void TryWater(FarmPlot plot)
        {
            if (plot == null || !plot.IsTilled)
            {
                SetMessage("Till a plot before watering it.");
                return;
            }
            if (!plot.IsOccupied)
            {
                SetMessage("Plant an item here before watering.");
                return;
            }
            if (plot.WaterAmount >= 100)
            {
                SetMessage("Water amount is already 100.");
                return;
            }

            plot.Water(waterPerUse);
            SetMessage($"Water amount: {plot.WaterAmount:0}/100. Water again to increase it.");
        }

        public int TryWater(SoilSurface soil, Vector3 point, FarmItem tool)
        {
            if (soil == null) return 0;
            float radius = WateringRadiusFor(tool);
            int count = soil.WaterArea(point, radius, waterPerUse);
            SetMessage(count > 0 ? $"Watered {count} plant(s) in a {radius:0.00}m radius." :
                "No plants needing water within range.");
            return count;
        }

        public void SetMessage(string text)
        {
            message = text;
            messageUntil = Time.time + 4f;
        }

        private void OnGUI()
        {
            if (player == null)
                return;

            GUI.Box(new Rect(14f, 14f, 490f, 157f), "WHAT THE FARM - prototype");
            GUI.Label(new Rect(28f, 42f, 420f, 22f), "WASD move  |  Mouse look  |  Shift sprint");
            GUI.Label(new Rect(28f, 64f, 460f, 22f), "E pick up / plant / talk  |  Q throw selected item");
            GUI.Label(new Rect(28f, 86f, 460f, 22f), "1-9 / wheel: select hotbar  |  Tab: inventory");
            GUI.Label(new Rect(28f, 108f, 460f, 22f), "Left click: hoe to till / can to water / hit a crop");
            GUI.Label(new Rect(28f, 130f, 460f, 22f), "Esc release mouse  |  Click Game view to resume");

            string hand = player.HeldItem == null ? "Empty" :
                $"{player.HeldItem.DisplayName} ({player.HeldItem.Value} gold)";
            if (player.HeldItem != null && (player.HeldItem.Kind == ItemKind.Tool || player.HeldItem.Kind == ItemKind.WateringCan))
                hand += $" | size x{player.HeldItem.SizeMultiplier:0.00} | radius {(player.HeldItem.Kind == ItemKind.Tool ? TillingRadiusFor(player.HeldItem) : WateringRadiusFor(player.HeldItem)):0.00}m";
            GUI.Box(new Rect(14f, 180f, 490f, 68f), $"Hand: {hand}\n{(Time.time < messageUntil ? message : "Till > plant > water > grow > harvest.")}");
            GUI.Box(new Rect(Screen.width - 190, 14, 176, 38), $"Gold: {Gold}");
            GUI.Label(new Rect(Screen.width - 190, 55, 176, 25), $"HP: {player.Health:0} / {player.MaxHealth:0}");
            var stage = GetComponent<FarmFirstStage>();
            if (stage != null) GUI.Label(new Rect(20, 260, 540, 25), stage.Cleared ? "Guardian defeated. Purple exit + E." : stage.Spawned ? "Defeat the guardian to unlock the exit." : "Leave the original farm to encounter the guardian.");
            if (Time.time < dialogueUntil)
            {
                float width = Mathf.Min(620, Screen.width - 28);
                GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height - 180, width, 90),
                    $"{dialogueSpeaker}\n\n{dialogueText}");
            }

            if (player.InventoryOpen)
            {
                player.DrawInventoryGUI();
                return;
            }

            if (player.TryLook(out RaycastHit hit))
            {
                string target = null;
                FarmItem item = hit.collider.GetComponentInParent<FarmItem>();
                FleeingCrop crop = hit.collider.GetComponentInParent<FleeingCrop>();
                NpcMerchant npc = hit.collider.GetComponentInParent<NpcMerchant>();
                FarmGuardian guardian = hit.collider.GetComponentInParent<FarmGuardian>();
                FarmStageExit exit = hit.collider.GetComponentInParent<FarmStageExit>();
                if (item != null)
                    target = $"{item.DisplayName} - {item.Value} gold";
                else if (crop != null)
                    target = crop.IsMature
                        ? $"Crop +{crop.Generation} - {Mathf.CeilToInt(crop.Health)}/{Mathf.CeilToInt(crop.MaxHealth)} HP - {crop.Value} gold"
                        : $"Growing - size rate {crop.GrowthRatePercent:0}% | water {crop.Plot.WaterAmount:0}/100";
                else if (npc != null)
                    target = $"{npc.DisplayName} - E talk / Q throw to sell";
                else if (guardian != null) target = $"Guardian - {guardian.Health:0}/{guardian.MaxHealth:0} HP";
                else if (exit != null) target = exit.CanTravel ? "E: next stage" : "Defeat the guardian first";
                else if (hit.collider.TryGetComponent(out SoilSurface soil))
                {
                    FarmPlot plot = soil.FindPlot(hit.point);
                    target = plot == null ? "Untilled ground - use hoe" : plot.IsOccupied
                        ? (plot.IsWatered ? "Occupied soil - watered" : "Occupied soil - water with can")
                        : "Tilled ground - press E to plant";
                }
                if (target != null)
                    GUI.Box(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f + 20f, 360f, 36f), target);
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
            player.DrawInventoryGUI();
        }
    }
}
