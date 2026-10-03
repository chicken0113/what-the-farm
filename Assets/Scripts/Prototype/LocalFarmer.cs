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
        private Vector2 inventoryScroll;
        private int selectedSlot;
        private int movingSlot = -1;
        private bool inventoryOpen;
        private float pitch;
        private float verticalSpeed;
        private float nextSwingTime;
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
            inventory = new FarmItem[Mathf.Max(1, slotCount)];
            LockCursor();
        }

        private void Update()
        {
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
                Drop();
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

        private void SelectSlot(int index)
        {
            selectedSlot = index;
            RefreshHeldItem();
        }

        private void RefreshHeldItem()
        {
            for (int index = 0; index < inventory.Length; index++)
            {
                FarmItem item = inventory[index];
                if (item == null)
                    continue;

                bool inHand = index == selectedSlot;
                item.gameObject.SetActive(inHand);
                if (inHand)
                {
                    item.transform.localPosition = new Vector3(0.42f, -0.36f, 0.8f);
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

        private void Interact()
        {
            if (!TryLook(out RaycastHit hit))
            {
                world.SetMessage("Look at an item or a tilled plot.");
                return;
            }

            if (hit.collider.TryGetComponent(out FarmItem groundItem))
            {
                int slot = FindEmptySlot();
                if (slot < 0)
                {
                    world.SetMessage("Inventory full. Drop an item with Q or rearrange slots with Tab.");
                    return;
                }

                groundItem.GetComponent<Collider>().enabled = false;
                Rigidbody rb = groundItem.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;
                groundItem.transform.SetParent(view.transform, false);
                inventory[slot] = groundItem;
                RefreshHeldItem();
                world.SetMessage($"Picked up {groundItem.DisplayName} in slot {slot + 1}.");
            }
            else if (hit.collider.TryGetComponent(out SoilSurface soil))
            {
                if (hit.normal.y < .9f) return;
                FarmPlot plot = soil.FindPlot(hit.point);
                FarmItem item = HeldItem;
                if (item == null)
                {
                    world.SetMessage("Select an item in the hotbar before planting.");
                    return;
                }
                if (world.TryPlant(item, plot, hit.point))
                {
                    inventory[selectedSlot] = null;
                    Destroy(item.gameObject);
                }
            }
            else
                world.SetMessage("Aim at an item to pick up or a tilled plot to plant.");
        }

        private void Drop()
        {
            FarmItem item = HeldItem;
            if (item == null)
                return;

            inventory[selectedSlot] = null;
            item.transform.SetParent(null);
            item.transform.position = transform.position + transform.forward * 1.2f + Vector3.up * 0.8f;
            item.GetComponent<Collider>().enabled = true;
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(transform.forward * 2.5f, ForceMode.Impulse);
            }
            world.SetMessage($"Dropped {item.DisplayName}.");
        }

        private void Swing()
        {
            if (Time.time < nextSwingTime)
                return;
            nextSwingTime = Time.time + 0.42f;

            if (!TryLook(out RaycastHit hit))
                return;

            FarmItem item = HeldItem;
            if (hit.collider.TryGetComponent(out FleeingCrop crop))
            {
                if (item != null && item.Kind == ItemKind.WateringCan && !crop.IsMature)
                {
                    world.TryWater(crop.Plot);
                    return;
                }

                float damage = item != null && item.Kind == ItemKind.Tool ? 2f : 1f;
                crop.TakeHit(damage);
            }
            else if (item != null && hit.collider.TryGetComponent(out SoilSurface soil))
            {
                if (hit.normal.y < .9f)
                {
                    world.SetMessage("Aim at the top of the ground.");
                    return;
                }
                if (item.Kind == ItemKind.Tool)
                    world.TryTill(soil, hit.point);
                else if (item.Kind == ItemKind.WateringCan)
                    world.TryWater(soil.FindPlot(hit.point));
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
                if (GUI.Button(rect, $"{prefix}\n{SlotLabel(inventory[index])}"))
                    ClickInventorySlot(index);
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
                GUI.Box(new Rect(startX + index * slotSize, Screen.height - slotSize - 10f,
                    slotSize - 3f, slotSize), $"{index + 1}\n{SlotLabel(inventory[index])}");
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

        private static string SlotLabel(FarmItem item)
        {
            if (item == null)
                return "Empty";

            string name = item.Kind switch
            {
                ItemKind.Seed => "Seed",
                ItemKind.Produce => "Crop",
                ItemKind.Tool => "Hoe",
                ItemKind.WateringCan => "Can",
                _ => "Stone"
            };
            return item.Generation > 0 ? $"{name} +{item.Generation}" : name;
        }
    }
}
