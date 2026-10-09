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
        [SerializeField] private Color hostileTint = new(1, .35f, .3f, 1);
        private CharacterController body;
        private FarmPrototype world;
        private FarmFirstStage stage;
        private float nextAttack;
        private float fallSpeed;
        private bool defeated;
        public bool IsHostile { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        private void Awake() { body = GetComponent<CharacterController>(); Health = maxHealth; }
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
            IsHostile = true;
            gameObject.layer = 9;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                if (collider != body && !collider.isTrigger) collider.enabled = false;
            body.enabled = true;
            if (GetComponent<NpcMerchant>() != null)
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    var tint = new MaterialPropertyBlock(); renderer.GetPropertyBlock(tint);
                    tint.SetColor("_BaseColor", hostileTint); renderer.SetPropertyBlock(tint);
                }
            stage?.MerchantBecameHostile(this);
            if (stage == null) world?.SetMessage("The merchant is hostile!");
            return true;
        }
        private void Update()
        {
            if (defeated || !IsHostile || world == null || world.Player == null) return;
            var player = world.Player;
            Vector3 direction = player.transform.position-transform.position; direction.y = 0;
            Vector3 movement = direction.magnitude > attackRange*.75f ? direction.normalized*moveSpeed : Vector3.zero;
            fallSpeed = body.isGrounded ? -1 : fallSpeed-18*Time.deltaTime;
            body.Move((movement+Vector3.up*fallSpeed)*Time.deltaTime);
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            Attack(player);
        }
        public bool Attack(LocalFarmer player)
        {
            if (defeated || !IsHostile || player == null || Time.time < nextAttack || Vector3.Distance(player.transform.position, transform.position) > attackRange) return false;
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
            stage?.MonsterDefeated(this);
            Destroy(gameObject);
        }
    }
}
