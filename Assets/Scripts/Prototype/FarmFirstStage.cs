using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmFirstStage : MonoBehaviour
    {
        [SerializeField] private Vector2 originalHalfSize = new(13, 13);
        [SerializeField] private NpcMerchant merchant;
        public bool Spawned { get; private set; }
        public bool Cleared { get; private set; }
        public FarmGuardian Monster { get; private set; }
        private FarmPrototype world;
        public Vector2 PeacefulHalfSize => originalHalfSize;
        public bool IsInsidePeacefulArea(Vector3 point, float margin = 0)
        {
            Vector3 offset = point - transform.position;
            return Mathf.Abs(offset.x) <= Mathf.Max(0, originalHalfSize.x - margin) &&
                Mathf.Abs(offset.z) <= Mathf.Max(0, originalHalfSize.y - margin);
        }
        // Retained for old map builders; the scene merchant now owns the encounter.
        public void Configure(FarmGuardian prefab) { }
        private void Awake() => world = GetComponent<FarmPrototype>();
        private void Start() => ResolveMerchant();
        private void ResolveMerchant()
        {
            if (Cleared || Monster != null) return;
            if (merchant == null) merchant = FindFirstObjectByType<NpcMerchant>();
            if (merchant != null) Monster = merchant.EnsureCombat(world, this);
        }
        private void Update() { if (world != null) CheckBoundary(world.Player); }
        public bool CheckBoundary(LocalFarmer player)
        {
            if (Spawned || Cleared || player == null || player.IsDead) return false;
            if (IsInsidePeacefulArea(player.transform.position)) return false;
            ResolveMerchant();
            return Monster != null && Monster.BecomeHostile();
        }
        public void MerchantBecameHostile(FarmGuardian enemy)
        {
            if (Cleared || enemy != Monster) return;
            Spawned = true;
            world.SetMessage("The merchant is hostile! Defeat them to unlock the next stage.");
        }
        public void MonsterDefeated(FarmGuardian enemy)
        {
            if (Cleared || enemy != Monster) return;
            Cleared = true; Monster = null;
            world.SetMessage("Merchant defeated! Go to the purple exit and press E.");
        }
        public void MerchantReturnedToPeace(FarmGuardian enemy)
        {
            if (!Cleared && enemy == Monster) Spawned = false;
        }
    }
}
