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
        private FarmItem heldItem;
        private float pitch;
        private float verticalSpeed;
        private float nextSwingTime;

        public FarmItem HeldItem => heldItem;
        public Camera View => view;

        public void Configure(FarmPrototype prototype, Camera playerCamera)
        {
            world = prototype;
            view = playerCamera;
            body = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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

            if (keyboard.eKey.wasPressedThisFrame)
                Interact();
            if (keyboard.qKey.wasPressedThisFrame)
                Drop();
            if (mouse.leftButton.wasPressedThisFrame)
                Swing();

            if (heldItem != null)
            {
                heldItem.transform.localPosition = new Vector3(0.42f, -0.36f, 0.8f);
                heldItem.transform.localRotation = Quaternion.Euler(0f, 25f, 20f);
            }
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
                world.SetMessage("Look at an item or the soil.");
                return;
            }

            if (heldItem == null)
            {
                FarmItem item = hit.collider.GetComponent<FarmItem>();
                if (item == null)
                {
                    world.SetMessage("Pick up an item first.");
                    return;
                }

                heldItem = item;
                Collider itemCollider = item.GetComponent<Collider>();
                itemCollider.enabled = false;
                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;
                item.transform.SetParent(view.transform, false);
                world.SetMessage($"Holding {item.DisplayName}. Aim at soil and press E to plant.");
            }
            else if (hit.collider.GetComponent<SoilSurface>() != null)
            {
                if (world.TryPlant(heldItem, hit.point))
                {
                    Destroy(heldItem.gameObject);
                    heldItem = null;
                }
            }
            else
            {
                world.SetMessage("Aim at an empty patch of soil to plant.");
            }
        }

        private void Drop()
        {
            if (heldItem == null)
                return;

            FarmItem item = heldItem;
            heldItem = null;
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

            if (TryLook(out RaycastHit hit) && hit.collider.TryGetComponent(out FleeingCrop crop))
            {
                float damage = heldItem != null && heldItem.Kind == ItemKind.Tool ? 2f : 1f;
                crop.TakeHit(damage);
            }
        }
    }
}
