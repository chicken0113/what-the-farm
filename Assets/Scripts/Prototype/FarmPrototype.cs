using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmPrototype : MonoBehaviour
    {
        private const float HalfSize = 13f;
        [SerializeField] private Material baseMaterial;
        private LocalFarmer player;
        private float messageUntil;
        private string message = "Pick up a seed, then plant it in the soil.";
        private Material soilMaterial;
        private Material seedMaterial;
        private Material toolMaterial;
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
            soilMaterial = MakeMaterial(new Color(0.32f, 0.22f, 0.13f));
            seedMaterial = MakeMaterial(new Color(0.91f, 0.75f, 0.27f));
            toolMaterial = MakeMaterial(new Color(0.52f, 0.72f, 0.84f));
            curioMaterial = MakeMaterial(new Color(0.62f, 0.57f, 0.76f));
            cropMaterial = MakeMaterial(new Color(0.27f, 0.77f, 0.33f));

            CreateArena();
            CreatePlayer();
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(-2f, 0.45f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(-1f, 0.45f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(0f, 0.45f, -7f));
            CreateItem(ItemKind.Seed, 0, 10, new Vector3(1f, 0.45f, -7f));
            CreateItem(ItemKind.Tool, 0, 16, new Vector3(2.4f, 0.55f, -7f));
            CreateItem(ItemKind.Curio, 0, 6, new Vector3(3.7f, 0.55f, -7f));
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
            farmer.transform.position = new Vector3(0f, 0.1f, -9.5f);
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
            player.Configure(this, camera);
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
            PrimitiveType shape = kind switch
            {
                ItemKind.Seed => PrimitiveType.Sphere,
                ItemKind.Produce => PrimitiveType.Capsule,
                ItemKind.Tool => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };
            GameObject instance = GameObject.CreatePrimitive(shape);
            instance.transform.SetParent(transform);
            instance.transform.position = position;
            instance.transform.localScale = kind == ItemKind.Tool
                ? new Vector3(0.17f, 0.5f, 0.17f)
                : Vector3.one * (kind == ItemKind.Produce ? 0.65f : 0.5f);
            instance.GetComponent<Renderer>().material = kind switch
            {
                ItemKind.Seed => seedMaterial,
                ItemKind.Tool => toolMaterial,
                ItemKind.Curio => curioMaterial,
                _ => cropMaterial
            };
            Rigidbody body = instance.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            FarmItem item = instance.AddComponent<FarmItem>();
            item.Configure(kind, generation, baseValue);
            return item;
        }

        public bool TryPlant(FarmItem item, Vector3 point)
        {
            if (Mathf.Abs(point.x) > ArenaHalfSize || Mathf.Abs(point.z) > ArenaHalfSize)
                return false;

            foreach (FleeingCrop crop in FindObjectsByType<FleeingCrop>(FindObjectsSortMode.None))
            {
                Vector3 difference = crop.transform.position - point;
                difference.y = 0f;
                if (difference.sqrMagnitude < 1.5f * 1.5f)
                {
                    SetMessage("Choose a patch of soil farther from another crop.");
                    return false;
                }
            }

            GameObject plant = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            plant.transform.SetParent(transform);
            plant.transform.position = point + Vector3.up * 0.15f;
            plant.GetComponent<Renderer>().material = new Material(cropMaterial);
            FleeingCrop cropComponent = plant.AddComponent<FleeingCrop>();
            cropComponent.Configure(this, item);
            SetMessage($"Planted {item.DisplayName}. Value: {cropComponent.Value} gold.");
            return true;
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

            GUI.Box(new Rect(14f, 14f, 440f, 112f), "WHAT THE FARM - prototype");
            GUI.Label(new Rect(28f, 42f, 420f, 22f), "WASD move  |  Mouse look  |  Shift sprint");
            GUI.Label(new Rect(28f, 64f, 420f, 22f), "E pick up / plant  |  Q drop  |  Left click hit");
            GUI.Label(new Rect(28f, 86f, 420f, 22f), "Esc release mouse  |  Click Game view to resume");

            string hand = player.HeldItem == null ? "Empty" :
                $"{player.HeldItem.DisplayName} ({player.HeldItem.Value} gold)";
            GUI.Box(new Rect(14f, Screen.height - 82f, 440f, 68f), $"Hand: {hand}\n{(Time.time < messageUntil ? message : "Grow, chase, hit, harvest, replant.")}");

            if (player.TryLook(out RaycastHit hit))
            {
                string target = null;
                if (hit.collider.TryGetComponent(out FarmItem item))
                    target = $"{item.DisplayName} - {item.Value} gold";
                else if (hit.collider.TryGetComponent(out FleeingCrop crop))
                    target = crop.IsMature
                        ? $"Crop +{crop.Generation} - {Mathf.CeilToInt(crop.Health)}/{Mathf.CeilToInt(crop.MaxHealth)} HP - {crop.Value} gold"
                        : "Growing crop";
                else if (hit.collider.GetComponent<SoilSurface>() != null && player.HeldItem != null)
                    target = "Press E to plant";
                if (target != null)
                    GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f + 20f, 260f, 30f), target);
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 10f, 20f, 20f), "+");
        }
    }
}
