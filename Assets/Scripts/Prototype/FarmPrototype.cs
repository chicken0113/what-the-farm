using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPrototype : MonoBehaviour
    {
        private const float HalfSize = 13f;
        [SerializeField] private Material baseMaterial;
        [SerializeField, Min(1)] private int inventorySlotCount = 12;
        [SerializeField] private bool buildArenaAtRuntime = true;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField, Min(.1f)] private float tillingRadius = .8f;

        public void UseSceneMap() => buildArenaAtRuntime = false;
        public void SetSpawnPoint(Transform spawnPoint) => playerSpawnPoint = spawnPoint;
        private LocalFarmer player;
        private readonly System.Collections.Generic.List<FleeingCrop> plantedCrops = new();

        public void RegisterPlant(FleeingCrop crop)
        {
            if (!plantedCrops.Contains(crop)) plantedCrops.Add(crop);
        }

        public void UnregisterPlant(FleeingCrop crop) => plantedCrops.Remove(crop);

        public void ResolveGrowthOverlap(FleeingCrop growing)
        {
            if (!growing.IsPlanted) return;
            // Only registered, rooted crops qualify. Scenery and loose items never enter this list.
            for (int index = plantedCrops.Count - 1; index >= 0; index--)
            {
                FleeingCrop other = plantedCrops[index];
                if (other == null || other == growing || !other.IsPlanted) continue;
                if (growing.Overlaps(other))
                    other.DestroyFromGrowth();
            }
        }
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

        public float ArenaHalfSize => HalfSize - 0.8f;
        public Vector3 PlayerPosition => player == null ? Vector3.zero : player.transform.position;

        public void SetBaseMaterial(Material material)
        {
            baseMaterial = material;
        }

        private void Awake()
        {
            EnsureMaterials();
            if (buildArenaAtRuntime)
                CreateArena();
            CreatePlayer();
            CreateItem(ItemKind.Tool, 0, 16, new Vector3(-2.5f, 0.55f, -7f));
            CreateItem(ItemKind.WateringCan, 0, 14, new Vector3(-1.2f, 0.55f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(0f, 0.45f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(1f, 0.45f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(2f, 0.45f, -7f));
            CreateItem(ItemKind.Curio, 0, 6, new Vector3(3.2f, 0.55f, -7f));
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
            soil.AddComponent<SoilSurface>();

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

        public FarmItem CreateItem(ItemKind kind, int generation, int baseValue, Vector3 position)
        {
            EnsureMaterials();
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
            return item;
        }

        public bool TryTill(SoilSurface soil, Vector3 point)
        {
            if (soil == null) return false;
            if (soil.Till(point, tillingRadius, dryPlotMaterial, wetPlotMaterial) == null)
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
            if (plot.IsWatered)
            {
                SetMessage("This plot has already been watered.");
                return;
            }

            plot.Water();
            SetMessage("Watered! The crop is growing now.");
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
            GUI.Label(new Rect(28f, 64f, 460f, 22f), "E pick up / plant selected item  |  Q drop selected item");
            GUI.Label(new Rect(28f, 86f, 460f, 22f), "1-9 / wheel: select hotbar  |  Tab: inventory");
            GUI.Label(new Rect(28f, 108f, 460f, 22f), "Left click: hoe to till / can to water / hit a crop");
            GUI.Label(new Rect(28f, 130f, 460f, 22f), "Esc release mouse  |  Click Game view to resume");

            string hand = player.HeldItem == null ? "Empty" :
                $"{player.HeldItem.DisplayName} ({player.HeldItem.Value} gold)";
            GUI.Box(new Rect(14f, 180f, 490f, 68f), $"Hand: {hand}\n{(Time.time < messageUntil ? message : "Till > plant > water > grow > harvest.")}");

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
                if (item != null)
                    target = $"{item.DisplayName} - {item.Value} gold";
                else if (crop != null)
                    target = crop.IsMature
                        ? $"Crop +{crop.Generation} - {Mathf.CeilToInt(crop.Health)}/{Mathf.CeilToInt(crop.MaxHealth)} HP - {crop.Value} gold"
                        : crop.IsWatered ? "Growing crop" : "Dry crop - water its plot";
                else if (hit.collider.TryGetComponent(out SoilSurface soil))
                {
                    FarmPlot plot = soil.FindPlot(hit.point);
                    target = plot == null ? "Untilled ground - use hoe" : plot.IsOccupied
                        ? (plot.IsWatered ? "Watered soil" : "Dry soil - water with can")
                        : "Tilled ground - press E to plant";
                }
                if (target != null)
                    GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f + 20f, 260f, 30f), target);
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
            player.DrawInventoryGUI();
        }
    }
}
