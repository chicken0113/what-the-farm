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
        private CharacterController body;
        private FarmPrototype world;
        private FarmFirstStage stage;
        private float nextAttack;
        private float fallSpeed;
        private bool defeated;
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        private void Awake() { body = GetComponent<CharacterController>(); Health = maxHealth; }
        public void Configure(FarmPrototype farm, FarmFirstStage encounter) { world = farm; stage = encounter; }
        private void Update()
        {
            if (defeated || world == null || world.Player == null) return;
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
            if (defeated || player == null || Time.time < nextAttack || Vector3.Distance(player.transform.position, transform.position) > attackRange) return false;
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
            Health = Mathf.Max(0, Health-damage);
            if (Health > 0) return;
            defeated = true;
            stage?.MonsterDefeated(this);
            Destroy(gameObject);
        }
    }
}
