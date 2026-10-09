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
        private FarmGuardian owner;
        private LocalFarmer target;
        private Transform[] armBones;
        private Quaternion[] restPose;
        private LineRenderer tracer;
        private GameObject flash;
        private Material effectMaterial;
        private float effectUntil;
        private float recoil;
        public bool IsEquipped => gameObject.activeSelf && owner != null && owner.IsHostile;
        public int ShotsFired { get; private set; }
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
            tracer = gameObject.AddComponent<LineRenderer>(); tracer.sharedMaterial = effectMaterial;
            tracer.useWorldSpace = true; tracer.positionCount = 2; tracer.startWidth = .015f; tracer.endWidth = .006f;
            tracer.shadowCastingMode = ShadowCastingMode.Off; tracer.receiveShadows = false; tracer.enabled = false;
            flash = GameObject.CreatePrimitive(PrimitiveType.Sphere); flash.name = "Muzzle Flash";
            var collider = flash.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            flash.transform.SetParent(muzzle, false); flash.transform.localScale = Vector3.one * .08f;
            flash.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; flash.SetActive(false);
        }
        public void SetEquipped(bool equipped)
        {
            if (!equipped) { RestoreArms(); recoil = 0; if (tracer != null) tracer.enabled = false; if (flash != null) flash.SetActive(false); }
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
            RaycastHit? first = null;
            foreach (var hit in Physics.RaycastAll(muzzle.position, direction, Vector3.Distance(muzzle.position, end) + .1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(owner.transform)) continue;
                if (!first.HasValue || hit.distance < first.Value.distance) first = hit;
            }
            if (first.HasValue) end = first.Value.point;
            tracer.SetPosition(0, muzzle.position); tracer.SetPosition(1, end); tracer.enabled = true;
            flash.SetActive(true); effectUntil = Time.time + .07f; recoil = .045f; ShotsFired++;
            if (first.HasValue && first.Value.collider.GetComponentInParent<LocalFarmer>() == player) player.ReceiveDamage(damage);
            return true;
        }
        private void LateUpdate()
        {
            if (!IsEquipped) return;
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * .5f);
            if (target != null && !target.IsDead) AimAt(target);
            if (Time.time >= effectUntil) { tracer.enabled = false; flash.SetActive(false); }
        }
        private void OnDestroy() { if (effectMaterial != null) Destroy(effectMaterial); }
    }
}
