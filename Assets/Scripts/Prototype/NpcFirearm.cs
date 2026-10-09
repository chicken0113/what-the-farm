using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatTheFarm.Prototype
{
    public sealed class NpcFirearm : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform rightGrip;
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Vector3 holdPosition = new(.18f, 1.22f, .3f);
        [SerializeField, Min(1)] private float bulletSpeed = 18;
        [SerializeField, Min(1)] private float bulletMaxDistance = 30;
        private sealed class Bullet
        {
            public Vector3 position, direction;
            public float travelled, damage;
            public LineRenderer visual;
        }
        private readonly List<Bullet> bullets = new();
        private FarmGuardian owner;
        private LocalFarmer target;
        private Transform[] armBones;
        private Quaternion[] restPose;
        private GameObject flash;
        private Material effectMaterial;
        private float effectUntil;
        private float recoil;
        public bool IsEquipped => gameObject.activeSelf && owner != null && owner.IsHostile;
        public int ShotsFired { get; private set; }
        public int ActiveBulletCount => bullets.Count;
        public float BulletSpeed => bulletSpeed;
        public Vector3 MuzzlePosition => muzzle.position;
        public Vector3 AimDirection => transform.forward;
        public void Configure(Transform barrel, Transform right, Transform left)
        { muzzle = barrel; rightGrip = right; leftGrip = left; }
        public void Bind(FarmGuardian npc)
        {
            owner = npc;
            var names = new[] { "upper_arm.R", "forearm.R", "hand.R", "upper_arm.L", "forearm.L", "hand.L" };
            var parts = owner.GetComponentsInChildren<Transform>(true);
            armBones = names.Select(name => parts.FirstOrDefault(part => part.name == name)).ToArray();
            restPose = armBones.Select(bone => bone != null ? bone.localRotation : Quaternion.identity).ToArray();
            effectMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            effectMaterial.SetColor("_BaseColor", new Color(1, .65f, .15f));
            flash = GameObject.CreatePrimitive(PrimitiveType.Sphere); flash.name = "Muzzle Flash";
            var collider = flash.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            flash.transform.SetParent(muzzle, false); flash.transform.localScale = Vector3.one * .08f;
            flash.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; flash.SetActive(false);
        }
        public void SetEquipped(bool equipped)
        {
            if (!equipped) { RestoreArms(); recoil = 0; ClearBullets(); if (flash != null) flash.SetActive(false); }
            gameObject.SetActive(equipped);
        }
        private void RestoreArms()
        {
            if (armBones == null) return;
            for (int i = 0; i < armBones.Length; i++) if (armBones[i] != null) armBones[i].localRotation = restPose[i];
        }
        public void AimAt(LocalFarmer player)
        {
            target = player; RestoreArms();
            transform.localPosition = holdPosition - Vector3.forward * recoil;
            Vector3 point = player.transform.position + Vector3.up * 1.05f;
            transform.rotation = Quaternion.LookRotation(point - transform.position, owner.transform.up);
            PoseArm(0, rightGrip.position, owner.transform.right - owner.transform.up);
            PoseArm(3, leftGrip.position, -owner.transform.right - owner.transform.up);
        }
        private void PoseArm(int first, Vector3 goal, Vector3 elbowSide)
        {
            var upper = armBones[first]; var lower = armBones[first + 1]; var hand = armBones[first + 2];
            if (upper == null || lower == null || hand == null) return;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < .001f || b < .001f) return;
            Vector3 direction = (goal - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(goal, upper.position), Mathf.Abs(a - b) + .001f, a + b - .001f);
            float cosine = Mathf.Clamp((a * a + distance * distance - b * b) / (2 * a * distance), -1, 1);
            Vector3 bend = Vector3.ProjectOnPlane(elbowSide, direction).normalized;
            Vector3 elbow = upper.position + direction * (a * cosine) + bend * (a * Mathf.Sqrt(1 - cosine * cosine));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * lower.rotation;
            hand.rotation = transform.rotation * Quaternion.Euler(0, -90, 90);
        }
        private bool ClearSegment(Vector3 from, Vector3 to, LocalFarmer player)
        {
            Vector3 difference = to - from;
            foreach (var hit in Physics.RaycastAll(from, difference.normalized, difference.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(owner.transform) && !hit.collider.transform.IsChildOf(player.transform)) return false;
            return true;
        }
        public bool TryFire(LocalFarmer player, float damage)
        {
            if (!IsEquipped || player == null || player.IsDead) return false;
            AimAt(player); Physics.SyncTransforms();
            Vector3 chest = owner.transform.position + Vector3.up * 1.22f;
            Vector3 end = player.transform.position + Vector3.up * 1.05f;
            if (!ClearSegment(chest, end, player) || !ClearSegment(chest, muzzle.position, player)) return false;
            Vector3 direction = (end - muzzle.position).normalized;
            var visual = new GameObject("NPC Bullet").AddComponent<LineRenderer>();
            visual.sharedMaterial = effectMaterial; visual.useWorldSpace = true; visual.positionCount = 2;
            visual.startWidth = .025f; visual.endWidth = .015f;
            visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
            visual.SetPosition(0, muzzle.position); visual.SetPosition(1, muzzle.position + direction * .15f);
            // The direction is fixed at firing time; bullets never follow the target.
            bullets.Add(new Bullet { position = muzzle.position, direction = direction, damage = damage, visual = visual });
            flash.SetActive(true); effectUntil = Time.time + .07f; recoil = .045f; ShotsFired++;
            return true;
        }
        public void AdvanceProjectiles(float deltaTime)
        {
            if (deltaTime <= 0) return;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                float step = Mathf.Min(bulletSpeed * deltaTime, bulletMaxDistance - bullet.travelled);
                RaycastHit? first = null;
                // Sweep the entire frame's travel so bullets cannot skip thin walls at low FPS.
                foreach (var hit in Physics.RaycastAll(bullet.position, bullet.direction, step, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(owner.transform)) continue;
                    if (!first.HasValue || hit.distance < first.Value.distance) first = hit;
                }
                if (first.HasValue)
                {
                    var farmer = first.Value.collider.GetComponentInParent<LocalFarmer>();
                    Destroy(bullet.visual.gameObject); bullets.RemoveAt(i);
                    // Damage may reset the merchant and clear the remaining bullets.
                    if (farmer != null && !farmer.IsDead) farmer.ReceiveDamage(bullet.damage);
                    if (!IsEquipped) return;
                    continue;
                }
                bullet.position += bullet.direction * step; bullet.travelled += step;
                bullet.visual.SetPosition(0, bullet.position - bullet.direction * Mathf.Min(.45f, bullet.travelled));
                bullet.visual.SetPosition(1, bullet.position);
                if (bullet.travelled >= bulletMaxDistance)
                { Destroy(bullet.visual.gameObject); bullets.RemoveAt(i); }
            }
        }
        private void ClearBullets()
        {
            foreach (var bullet in bullets) if (bullet.visual != null) Destroy(bullet.visual.gameObject);
            bullets.Clear();
        }
        private void Update() { if (IsEquipped) AdvanceProjectiles(Time.deltaTime); }
        private void LateUpdate()
        {
            if (!IsEquipped) return;
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * .5f);
            if (target != null && !target.IsDead) AimAt(target);
            if (Time.time >= effectUntil) flash.SetActive(false);
        }
        private void OnDisable() { ClearBullets(); }
        private void OnDestroy() { ClearBullets(); if (effectMaterial != null) Destroy(effectMaterial); }
    }
}
