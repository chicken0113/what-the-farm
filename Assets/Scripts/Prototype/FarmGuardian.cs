using System.Collections;
using UnityEngine;

namespace WhatTheFarm.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FarmGuardian : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maxHealth = 12;
        [SerializeField, Min(.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(.1f)] private float attackRange = 1.9f;
        [SerializeField, Min(.1f)] private float attackInterval = 1.2f;
        [SerializeField, Min(1)] private float attackDamage = 20;
        [SerializeField] private NpcFirearm firearmPrefab;
        [SerializeField, Min(.5f)] private float preferredCombatDistance = 7;
        private NpcFirearm firearm;
        public NpcFirearm Firearm => firearm;
        [SerializeField, Min(.05f)] private float deathFallDuration = .45f;
        [SerializeField, Min(.1f)] private float revivalHealingSeconds = 4;
        [SerializeField, Range(.1f, .9f)] private float plantBuriedFraction = .5f;
        private string actorId;
        public string ActorId => actorId;
        public float RevivalHealingSeconds => revivalHealingSeconds;
        public float PlantBuriedFraction => plantBuriedFraction;
        private CharacterController body;
        private FarmPrototype world;
        private FarmFirstStage stage;
        private float nextAttack;
        private float fallSpeed;
        private bool defeated;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private int homeLayer;
        private Collider[] originalColliders;
        private bool[] originalColliderStates;
        private FarmReturnRoute returnRoute;
        private int returnCorner;
        public bool IsReturningHome { get; private set; }
        public Vector3 HomePosition => homePosition;
        public Quaternion HomeRotation => homeRotation;
        public bool IsDefeated => defeated;
        public bool IsHostile { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        private void Awake()
        {
            body = GetComponent<CharacterController>(); Health = maxHealth;
            actorId = System.Guid.NewGuid().ToString("N");
            homePosition = transform.position; homeRotation = transform.rotation; homeLayer = gameObject.layer;
            originalColliders = GetComponentsInChildren<Collider>(true);
            originalColliderStates = new bool[originalColliders.Length];
            for (int i = 0; i < originalColliders.Length; i++) originalColliderStates[i] = originalColliders[i].enabled;
        }
        public void Configure(FarmPrototype farm, FarmFirstStage encounter, bool startHostile = true)
        {
            world = farm;
            if (encounter != null) stage = encounter;
            var merchant = GetComponent<NpcMerchant>();
            if (merchant != null)
            {
                var capsule = GetComponent<CapsuleCollider>();
                if (capsule != null) { body.height = capsule.height; body.radius = capsule.radius; body.center = capsule.center; }
                merchant.BindCombat(this);
                if (!IsHostile) body.enabled = false;
            }
            if (startHostile) BecomeHostile();
        }
        public bool BecomeHostile()
        {
            if (defeated || IsHostile) return false;
            StopReturning();
            IsHostile = true;
            gameObject.layer = 9;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                if (collider != body && !collider.isTrigger) collider.enabled = false;
            body.enabled = true;
            if (firearm == null && firearmPrefab != null)
            {
                firearm = Instantiate(firearmPrefab, transform, false); firearm.Bind(this);
            }
            if (firearm != null) firearm.SetEquipped(true);
            nextAttack = Time.time + .2f;
            stage?.MerchantBecameHostile(this);
            if (stage == null) world?.SetMessage("The merchant is hostile!");
            return true;
        }
        public void ResetAfterPlayerDeath()
        {
            if (defeated || !IsHostile) return;
            IsHostile = false; Health = maxHealth; nextAttack = 0; fallSpeed = 0;
            if (firearm != null) firearm.SetEquipped(false);
            body.enabled = false;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            gameObject.layer = homeLayer;
            for (int i = 0; i < originalColliders.Length; i++)
                if (originalColliders[i] != null && originalColliders[i] != body)
                    originalColliders[i].enabled = originalColliderStates[i];
            stage?.MerchantReturnedToPeace(this);
        }
        public void ReviveFromPlant(FarmPrototype farm)
        {
            world = farm; stage = farm.GetComponent<FarmFirstStage>();
            defeated = false; IsHostile = false; Health = maxHealth; nextAttack = 0; fallSpeed = 0;
            transform.rotation = homeRotation;
            foreach (Transform part in GetComponentsInChildren<Transform>(true)) part.gameObject.layer = homeLayer;
            for (int i = 0; i < originalColliders.Length; i++)
                if (originalColliders[i] != null && originalColliders[i] != body)
                    originalColliders[i].enabled = originalColliderStates[i];
            foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = true;
            body.enabled = false;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                float bottom = transform.TransformPoint(capsule.center).y - capsule.height * Mathf.Abs(transform.lossyScale.y) * .5f;
                if (Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down, out var ground, 5,
                    ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
                    transform.position += Vector3.up * (ground.point.y - bottom);
            }
            GetComponent<NpcMerchant>()?.BindWorld(farm);
            gameObject.name = GetComponent<NpcMerchant>()?.DisplayName ?? "NPC";
            returnRoute = new FarmReturnRoute();
            if (returnRoute.Build(farm, body, homePosition))
            {
                IsReturningHome = true; returnCorner = 0;
                foreach (var collider in GetComponentsInChildren<Collider>())
                    if (collider != body && !collider.isTrigger) collider.enabled = false;
                // Keep the sale trigger on its friendly layer while loose items pass through the moving body.
                gameObject.layer = homeLayer; body.excludeLayers |= 1 << 8; body.enabled = true;
            }
            else
            {
                StopReturning();
                farm.SetMessage("Merchant revived, but the original spot has no walkable return route.");
            }
        }
        public void RecoverWhilePlanted(float amount)
        {
            if (defeated && amount > 0) Health = Mathf.Min(maxHealth, Health + amount);
        }
        private void StopReturning()
        {
            IsReturningHome = false; returnRoute?.Dispose(); returnRoute = null;
        }
        private void OnDestroy() => StopReturning();
        private void WalkHome()
        {
            Vector3 point = returnRoute.Corners[returnCorner];
            Vector3 direction = point - transform.position; direction.y = 0;
            if (direction.magnitude < .15f)
            {
                if (++returnCorner >= returnRoute.Corners.Length)
                {
                    StopReturning(); body.enabled = false; gameObject.layer = homeLayer;
                    transform.rotation = homeRotation;
                    for (int i = 0; i < originalColliders.Length; i++)
                        if (originalColliders[i] != null && originalColliders[i] != body)
                            originalColliders[i].enabled = originalColliderStates[i];
                    return;
                }
                point = returnRoute.Corners[returnCorner]; direction = point - transform.position; direction.y = 0;
            }
            fallSpeed = body.isGrounded ? -1 : fallSpeed - 18 * Time.deltaTime;
            body.Move(Vector3.ClampMagnitude(direction, moveSpeed * Time.deltaTime) + Vector3.up * fallSpeed * Time.deltaTime);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        private void Update()
        {
            if (IsReturningHome && !defeated && !IsHostile) { WalkHome(); return; }
            if (defeated || !IsHostile || world == null || world.Player == null || world.Player.IsDead) return;
            var player = world.Player;
            Vector3 direction = player.transform.position-transform.position; direction.y = 0;
            float approachDistance = firearm != null ? Mathf.Min(preferredCombatDistance, attackRange * .75f) : attackRange * .75f;
            Vector3 movement = direction.magnitude > approachDistance ? direction.normalized*moveSpeed : Vector3.zero;
            fallSpeed = body.isGrounded ? -1 : fallSpeed-18*Time.deltaTime;
            body.Move((movement+Vector3.up*fallSpeed)*Time.deltaTime);
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            Attack(player);
        }
        public bool Attack(LocalFarmer player)
        {
            if (defeated || !IsHostile || player == null || player.IsDead || Time.time < nextAttack || Vector3.Distance(player.transform.position, transform.position) > attackRange) return false;
            if (firearm != null)
            {
                bool shot = firearm.TryFire(player, attackDamage);
                nextAttack = Time.time + (shot ? attackInterval : .1f);
                return shot;
            }
            var start = transform.position+Vector3.up*.9f;
            var end = player.transform.position+Vector3.up*.9f;
            foreach (var hit in Physics.RaycastAll(start, (end-start).normalized, (end-start).magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(player.transform)) return false;
            nextAttack = Time.time+attackInterval;
            player.ReceiveDamage(attackDamage); return true;
        }
        public void TakeHit(float damage)
        {
            if (defeated || damage <= 0) return;
            BecomeHostile();
            Health = Mathf.Max(0, Health-damage);
            if (Health > 0) return;
            defeated = true;
            IsHostile = false;
            if (firearm != null) firearm.SetEquipped(false);
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
            stage?.MonsterDefeated(this);
            StartCoroutine(FallDown());
        }
        private IEnumerator FallDown()
        {
            Quaternion upright = transform.rotation;
            Quaternion fallen = upright * Quaternion.Euler(90, 0, 0);
            float groundHeight = transform.position.y;
            if (Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down, out var ground, 3,
                ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) groundHeight = ground.point.y;
            var renderers = GetComponentsInChildren<Renderer>();
            float elapsed = 0;
            while (elapsed < deathFallDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / deathFallDuration);
                transform.rotation = Quaternion.Slerp(upright, fallen, progress * progress * (3 - 2 * progress));
                // Keep the falling model in contact with the floor instead of sinking into it.
                float bottom = float.PositiveInfinity;
                foreach (var renderer in renderers)
                    if (renderer != null && renderer.enabled) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (!float.IsPositiveInfinity(bottom)) transform.position += Vector3.up * (groundHeight - bottom);
                yield return null;
            }
            gameObject.AddComponent<PlantableCorpse>().BindNpc(this);
        }
    }
}
