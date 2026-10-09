using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class FarmFirstStage : MonoBehaviour
    {
        [SerializeField] private Vector2 originalHalfSize = new(13, 13);
        [SerializeField, Min(1)] private float groundHalfSize = 50;
        [SerializeField, Min(1)] private float spawnDistance = 6.5f;
        [SerializeField] private FarmGuardian monsterPrefab;
        public bool Spawned { get; private set; }
        public bool Cleared { get; private set; }
        public FarmGuardian Monster { get; private set; }
        private FarmPrototype world;
        public void Configure(FarmGuardian prefab) => monsterPrefab = prefab;
        private void Awake() => world = GetComponent<FarmPrototype>();
        private void Update() { if (world != null) CheckBoundary(world.Player); }
        public bool CheckBoundary(LocalFarmer player)
        {
            if (Spawned || Cleared || player == null || monsterPrefab == null) return false;
            Vector3 offset = player.transform.position-transform.position;
            if (Mathf.Abs(offset.x) <= originalHalfSize.x && Mathf.Abs(offset.z) <= originalHalfSize.y) return false;
            offset.y = 0;
            Vector3 point = player.transform.position+offset.normalized*spawnDistance;
            point.x = Mathf.Clamp(point.x, transform.position.x-groundHalfSize+1, transform.position.x+groundHalfSize-1);
            point.z = Mathf.Clamp(point.z, transform.position.z-groundHalfSize+1, transform.position.z+groundHalfSize-1);
            if (Physics.Raycast(point+Vector3.up*10, Vector3.down, out var floor, 100, ~0, QueryTriggerInteraction.Ignore)) point.y = floor.point.y+.1f;
            Monster = Instantiate(monsterPrefab, point, Quaternion.identity);
            Monster.Configure(world, this);
            Spawned = true;
            world.SetMessage("Guardian appeared! Defeat it to unlock the next stage.");
            return true;
        }
        public void MonsterDefeated(FarmGuardian enemy)
        {
            if (Cleared || enemy != Monster) return;
            Cleared = true; Monster = null;
            world.SetMessage("Guardian defeated! Go to the purple exit and press E.");
        }
    }
}
