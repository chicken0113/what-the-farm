using UnityEngine;
using UnityEngine.InputSystem;

namespace WhatTheFarm.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LocalFarmer : MonoBehaviour
    {
        private FarmPrototype world;
        private CharacterController body;
        private Camera view;
        private FarmItem[] inventory;
        private readonly InventoryModelIcons inventoryIcons = new();
        private void OnDestroy() => inventoryIcons.Dispose();
        private void LateUpdate() => inventoryIcons.Prepare(inventory);
        private Vector2 inventoryScroll;
        private int selectedSlot;
        private int movingSlot = -1;
        private bool inventoryOpen;
        private float pitch;
        private float verticalSpeed;
        private float nextSwingTime;
        private FarmActionAnimation actions;
        private FarmerEmptyHand emptyHand;
        public FarmerEmptyHand EmptyHand => emptyHand;
        [SerializeField, Min(1)] private float maxHealth = 100;
        public float Health { get; private set; } = 100;
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }
        public string ActorId { get; private set; }
        public PlantableCorpse DeathBody { get; private set; }
        public int SelectedSlot => selectedSlot;
        public Camera View => view;
        public FarmActionAnimation Actions => actions;
        public FarmItem[] CaptureInventory() => (FarmItem[])inventory.Clone();
        public void SetActions(FarmActionAnimation model)
        {
            if(actions!=null)
            {
                actions.Stop();
                if(emptyHand!=null) emptyHand.transform.SetParent(view.transform,true);
                if(inventory!=null) foreach(var item in inventory) if(item!=null) item.transform.SetParent(view.transform,true);
                if(actions!=model) Destroy(actions.gameObject);
            }
            actions=model;
            if(emptyHand!=null && actions!=null) emptyHand.transform.SetParent(actions.transform,true);
            RefreshHeldItem();
        }
        public void RestoreInventory(FarmItem[] items, int selected)
        {
            inventory = new FarmItem[Mathf.Max(inventory.Length, items.Length)];
            System.Array.Copy(items, inventory, items.Length);
            selectedSlot = Mathf.Clamp(selected, 0, inventory.Length-1);
            foreach (var item in inventory) if (item != null) item.transform.SetParent(view.transform, true);
            RefreshHeldItem();
        }
        public void ReceiveDamage(float damage)
        {
            if (damage <= 0 || IsDead) return;
            Health = Mathf.Max(0, Health-damage);
            if (Health > 0) { world.SetMessage($"Guardian hit! HP {Health:0}/{MaxHealth:0}"); return; }
            foreach (var merchant in FindObjectsByType<FarmGuardian>(FindObjectsSortMode.None)) merchant.ResetAfterPlayerDeath();
            if (world.PlayerRevivalRequiresPlanting)
            {
                IsDead = true; body.enabled = false;
                var remains = world.CreateItem(ItemKind.Corpse, 0, 0, transform.position + Vector3.up * .4f);
                remains.transform.localScale = new Vector3(.5f, .9f, .5f);
                remains.transform.rotation = Quaternion.Euler(90, 0, 0);
                DeathBody = remains.gameObject.AddComponent<PlantableCorpse>();
                DeathBody.BindPlayer(this, world.PlayerRevivalHealingSeconds, world.PlayerPlantBuriedFraction);
                foreach (var item in inventory) if (item != null) item.gameObject.SetActive(false);
                emptyHand?.SetVisible(false);
                world.SetMessage("You died. A teammate must plant your body so it can recover health and revive you.");
                return;
            }
            ReviveAt(world.SpawnPosition);
            world.SetMessage("Knocked out. Returned to the farm; your items are kept.");
        }
        public void ReviveAt(Vector3 position)
        {
            body.enabled = false; transform.position = position; body.enabled = true;
            IsDead = false; Health = maxHealth; verticalSpeed = 0; DeathBody = null;
            RefreshHeldItem();
        }
        public void RecoverWhilePlanted(float amount)
        {
            if (IsDead && amount > 0) Health = Mathf.Min(maxHealth, Health + amount);
        }
        [SerializeField, Min(1)] private float throwSpeed = 8f;
        private static readonly Key[] HotbarKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        public FarmItem HeldItem => inventory != null ? inventory[selectedSlot] : null;
        public bool InventoryOpen => inventoryOpen;

        private int HotbarSlots => Mathf.Min(9, inventory.Length);

        public void Configure(FarmPrototype prototype, Camera playerCamera, int slotCount)
        {
            world = prototype;
            view = playerCamera;
            body = GetComponent<CharacterController>();
            gameObject.layer = 9;
            Health = maxHealth;
            ActorId = System.Guid.NewGuid().ToString("N");
            inventory = new FarmItem[Mathf.Max(1, slotCount)];
            emptyHand = FarmerEmptyHand.Create(view.transform);
            SetActions(FarmActionAnimation.Create(view.transform));
            LockCursor();
        }

        private void Update()
        {
            if (IsDead) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                inventoryOpen = !inventoryOpen;
                movingSlot = -1;
                if (inventoryOpen)
                    UnlockCursor();
                else
                    LockCursor();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (inventoryOpen)
                {
                    inventoryOpen = false;
                    movingSlot = -1;
                    LockCursor();
                }
                else
                    UnlockCursor();
                return;
            }
            if (inventoryOpen)
                return;
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 look = mouse.delta.ReadValue() * 0.12f;
            transform.Rotate(Vector3.up, look.x);
            pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            view.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);

            Vector2 move = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            Vector3 direction = transform.right * move.x + transform.forward * move.y;
            direction = Vector3.ClampMagnitude(direction, 1f);
            float speed = keyboard.leftShiftKey.isPressed ? 7f : 4.5f;
            verticalSpeed = body.isGrounded ? -1f : verticalSpeed - 18f * Time.deltaTime;
            body.Move((direction * speed + Vector3.up * verticalSpeed) * Time.deltaTime);

            for (int index = 0; index < HotbarSlots; index++)
            {
                if (keyboard[HotbarKeys[index]].wasPressedThisFrame)
                {
                    SelectSlot(index);
                    break;
                }
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                SelectSlot((selectedSlot + (scroll > 0f ? HotbarSlots - 1 : 1)) % HotbarSlots);

            if (keyboard.eKey.wasPressedThisFrame)
                Interact();
            if (keyboard.qKey.wasPressedThisFrame)
                ThrowSelectedItem();
            if (mouse.leftButton.wasPressedThisFrame)
                Swing();
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SelectSlot(int index)
        {
            if (inventory == null || index < 0 || index >= inventory.Length) return;
            selectedSlot = index;
            RefreshHeldItem();
        }

        private void RefreshHeldItem()
        {
            if (inventory != null) inventoryIcons.Prune(inventory);
            actions?.Stop();
            if (emptyHand != null) emptyHand.SetVisible(HeldItem == null);
            for (int index = 0; index < inventory.Length; index++)
            {
                FarmItem item = inventory[index];
                if (item == null)
                    continue;

                bool inHand = index == selectedSlot;
                item.gameObject.SetActive(inHand);
                if (inHand)
                {
                    item.transform.SetParent(actions != null ? actions.Grip : view.transform, true);
                    item.transform.localPosition = actions != null ? Vector3.zero : new Vector3(0.42f, -0.36f, 0.8f);
                    item.transform.localRotation = Quaternion.Euler(0f, 25f, 20f);
                }
            }
        }

        private int FindEmptySlot()
        {
            if (inventory[selectedSlot] == null)
                return selectedSlot;
            for (int index = 0; index < inventory.Length; index++)
            {
                if (inventory[index] == null)
                    return index;
            }
            return -1;
        }

        public bool TryLook(out RaycastHit hit)
        {
            return Physics.Raycast(view.transform.position, view.transform.forward, out hit, 4f,
                ~0, QueryTriggerInteraction.Ignore);
        }

        public bool TryLookSoil(out RaycastHit hit)
        {
            hit = default;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit candidate in Physics.RaycastAll(view.transform.position,
                view.transform.forward, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Planting aims at soil behind crops; solid scenery still blocks the ray.
                if (candidate.collider.GetComponentInParent<FleeingCrop>() != null) continue;
                if (candidate.distance >= nearest) continue;
                nearest = candidate.distance;
                hit = candidate;
            }
            return nearest < float.PositiveInfinity && hit.normal.y >= .9f &&
                hit.collider.GetComponent<SoilSurface>() != null;
        }

        public void Interact()
        {
            if (IsDead) return;
            if (!TryLook(out RaycastHit hit))
            {
                world.SetMessage("Look at an item or a tilled plot.");
                return;
            }

            FarmItem groundItem = hit.collider.GetComponentInParent<FarmItem>();
            FarmStageExit exit = hit.collider.GetComponentInParent<FarmStageExit>();
            if (exit != null) { exit.TryTravel(world, this); return; }
            NpcMerchant npc = hit.collider.GetComponentInParent<NpcMerchant>();
            if (npc != null && !npc.IsDefeated)
            {
                npc.Talk();
                return;
            }
            if (groundItem != null)
            {
                int slot = FindEmptySlot();
                if (slot < 0)
                {
                    world.SetMessage("Inventory full. Drop an item with Q or rearrange slots with Tab.");
                    return;
                }

                foreach (Collider collider in groundItem.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                Rigidbody rb = groundItem.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;
                groundItem.MarkHeld();
                groundItem.transform.SetParent(view.transform, false);
                inventory[slot] = groundItem;
                RefreshHeldItem();
                actions?.PlayPickup();
                world.SetMessage($"Picked up {groundItem.DisplayName} in slot {slot + 1}.");
            }
            else if (TryLookSoil(out RaycastHit soilHit))
            {
                SoilSurface soil = soilHit.collider.GetComponent<SoilSurface>();
                FarmPlot plot = soil.FindPlot(soilHit.point);
                FarmItem item = HeldItem;
                if (item == null)
                {
                    world.SetMessage("Select an item in the hotbar before planting.");
                    return;
                }
                if (world.TryPlant(item, plot, soilHit.point))
                {
                    inventory[selectedSlot] = null;
                    if (item.GetComponent<PlantableCorpse>() == null && item.Kind != ItemKind.Weed) Destroy(item.gameObject);
                    RefreshHeldItem();
                    actions?.PlayPlant();
                }
            }
            else
                world.SetMessage("Aim at an item to pick up or a tilled plot to plant.");
        }

        public void ThrowSelectedItem()
        {
            if (IsDead) return;
            FarmItem item = HeldItem;
            if (item == null)
                return;

            inventory[selectedSlot] = null;
            item.transform.SetParent(null);
            item.transform.position = view.transform.position + view.transform.forward * .9f;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                collider.enabled = item.Kind != ItemKind.Corpse || collider is BoxCollider;
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = view.transform.forward * throwSpeed;
                item.MarkThrown();
            }
            world.SetMessage($"Threw {item.DisplayName}. Throw to the buyer to sell it.");
            RefreshHeldItem();
            actions?.PlayUse();
        }

        public void Swing()
        {
            if (IsDead) return;
            if (Time.time < nextSwingTime)
                return;
            nextSwingTime = Time.time + 0.42f;
            actions?.PlaySwing(HeldItem != null);

            if (!TryLook(out RaycastHit hit))
                return;

            FarmItem item = HeldItem;
            if (item != null && item.Kind == ItemKind.Tool && !item.CanUseTool)
            { world.SetMessage("This shovel head needs planting, watering and harvesting before use."); return; }
            FarmGuardian guardian = hit.collider.GetComponentInParent<FarmGuardian>();
            if (guardian != null && !guardian.IsDefeated) { guardian.TakeHit(item != null && item.Kind == ItemKind.Tool ? 2*item.SizeMultiplier : 1); return; }
            FleeingCrop crop = hit.collider.GetComponentInParent<FleeingCrop>();
            if (crop != null)
            {
                if (item != null && item.Kind == ItemKind.WateringCan && !crop.IsMature)
                {
                    actions?.PlayUse();
                    if (crop.IsPlanted) world.TryWater(crop.Plot.Surface, crop.transform.position, item);
                    return;
                }

                float damage = item != null && item.Kind == ItemKind.Tool ? 2f * item.SizeMultiplier : 1f;
                crop.TakeHit(damage);
            }
            else if (hit.collider.TryGetComponent(out SoilSurface soil))
            {
                if (hit.normal.y < .9f)
                {
                    world.SetMessage("Aim at the top of the ground.");
                    return;
                }
                if (item == null || item.Kind == ItemKind.Tool)
                    world.TryTill(soil, hit.point, item);
                else if (item.Kind == ItemKind.WateringCan)
                {
                    actions?.PlayUse();
                    world.TryWater(soil, hit.point, item);
                }
            }
        }

        public void DrawInventoryGUI()
        {
            if (inventory == null)
                return;

            if (!inventoryOpen)
            {
                DrawHotbar();
                return;
            }

            const float slotSize = 76f;
            const float gap = 5f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((Screen.width - 40f) / (slotSize + gap)), 1, 6);
            int rows = Mathf.CeilToInt((float)inventory.Length / columns);
            float width = columns * (slotSize + gap) + 40f;
            float height = Mathf.Min(Screen.height - 20f, rows * (slotSize + gap) + 95f);
            Rect panel = new Rect((Screen.width - width) * 0.5f,
                Mathf.Max(10f, (Screen.height - height) * 0.5f), width, height);
            GUI.Box(panel, "Inventory (Tab / Esc to close)");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 25f, width - 25f, 22f),
                "Click two slots to move or swap.");

            inventoryScroll = GUI.BeginScrollView(
                new Rect(panel.x + 14f, panel.y + 55f, width - 28f, height - 65f),
                inventoryScroll,
                new Rect(0f, 0f, columns * (slotSize + gap) + 8f,
                    rows * (slotSize + gap) + 8f));

            for (int index = 0; index < inventory.Length; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect rect = new Rect(5f + column * (slotSize + gap),
                    row * (slotSize + gap), slotSize, slotSize);
                Color original = GUI.backgroundColor;
                GUI.backgroundColor = index == movingSlot ? new Color(1f, 0.8f, 0.3f) :
                    index == selectedSlot ? new Color(0.5f, 0.85f, 1f) : Color.white;
                string prefix = index < HotbarSlots ? $"{index + 1}" : $"#{index + 1}";
                if (GUI.Button(rect, GUIContent.none))
                    ClickInventorySlot(index);
                DrawItemModel(rect, inventory[index], prefix);
                GUI.backgroundColor = original;
            }
            GUI.EndScrollView();
        }

        private void DrawHotbar()
        {
            float slotSize = Mathf.Min(70f, (Screen.width - 20f) / HotbarSlots);
            float startX = (Screen.width - HotbarSlots * slotSize) * 0.5f;
            for (int index = 0; index < HotbarSlots; index++)
            {
                Color original = GUI.backgroundColor;
                GUI.backgroundColor = index == selectedSlot ? new Color(0.5f, 0.85f, 1f) : Color.white;
                var rect = new Rect(startX + index * slotSize, Screen.height - slotSize - 10f, slotSize - 3f, slotSize);
                GUI.Box(rect, GUIContent.none);
                DrawItemModel(rect, inventory[index], (index + 1).ToString());
                GUI.backgroundColor = original;
            }
        }

        private void ClickInventorySlot(int index)
        {
            if (movingSlot < 0)
            {
                if (inventory[index] != null)
                    movingSlot = index;
                else if (index < HotbarSlots)
                    SelectSlot(index);
                return;
            }

            if (movingSlot == index)
            {
                if (index < HotbarSlots)
                    SelectSlot(index);
            }
            else
            {
                (inventory[movingSlot], inventory[index]) = (inventory[index], inventory[movingSlot]);
                RefreshHeldItem();
            }
            movingSlot = -1;
        }

        private void DrawItemModel(Rect rect, FarmItem item, string number)
        {
            Texture image = inventoryIcons.Get(item);
            if (image != null)
                GUI.DrawTexture(new Rect(rect.x + 5, rect.y + 7, rect.width - 10, rect.height - 12), image, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, rect.width - 10, 18), number);
        }
    }
}
