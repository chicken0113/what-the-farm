using UnityEngine;

namespace WhatTheFarm.Prototype
{
    // Keeps the original actor/owner identity while the body is carried, planted and revived.
    public sealed class PlantableCorpse : MonoBehaviour
    {
        public enum BodyState { Loose, Planted, Revived }
        [SerializeField] private string ownerId;
        [SerializeField] private string ownerName;
        [SerializeField] private FarmGuardian npc;
        [SerializeField] private LocalFarmer player;
        [SerializeField, Min(0)] private float pickupPadding = .15f;
        [SerializeField, Min(.1f)] private float healingSeconds = 4;
        [SerializeField, Range(.1f, .9f)] private float buriedFraction = .5f;
        private BoxCollider pickupCollider;
        private Vector3 plantedGroundPosition;
        private FarmPrototype world;
        public string OwnerId => ownerId;
        public string DisplayName => ownerName + " Body";
        public BodyState State { get; private set; }
        public Quaternion PlantingRotation => npc != null ? npc.HomeRotation : Quaternion.identity;
        public float Health => npc != null ? npc.Health : player != null ? player.Health : 0;
        public float MaxHealth => npc != null ? npc.MaxHealth : player != null ? player.MaxHealth : 1;
        public void BindNpc(FarmGuardian target)
        {
            npc = target; ownerId = target.ActorId;
            ownerName = target.GetComponent<NpcMerchant>()?.DisplayName ?? "NPC";
            healingSeconds = target.RevivalHealingSeconds; buriedFraction = target.PlantBuriedFraction;
            PreparePickup();
        }
        public void BindPlayer(LocalFarmer target, float recoverySeconds, float buriedDepth)
        {
            player = target; ownerId = target.ActorId; ownerName = "Player";
            healingSeconds = Mathf.Max(.1f, recoverySeconds); buriedFraction = Mathf.Clamp(buriedDepth, .1f, .9f);
            PreparePickup();
        }
        private void PreparePickup()
        {
            var item = GetComponent<FarmItem>();
            if (item == null) item = gameObject.AddComponent<FarmItem>();
            item.Configure(ItemKind.Corpse, 0, 0);
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            pickupCollider = gameObject.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero); bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Mesh baked = null;
                Bounds local = renderer.localBounds;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    baked = new Mesh(); skin.BakeMesh(baked, false); baked.RecalculateBounds(); local = baked.bounds;
                    skin.localBounds = local;
                }
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
                if (baked != null) Destroy(baked);
            }
            pickupCollider.center = bounds.center;
            Vector3 scale = transform.lossyScale;
            Vector3 padding = new Vector3(pickupPadding / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                pickupPadding / Mathf.Max(.001f, Mathf.Abs(scale.y)), pickupPadding / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            pickupCollider.size = (found ? Vector3.Max(bounds.size, Vector3.one * .05f) : Vector3.one * .5f) + padding * 2;
            if (Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down, out var floor, 3,
                ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
            {
                float bottom = float.PositiveInfinity;
                foreach (var renderer in GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (bottom < floor.point.y) transform.position += Vector3.up * (floor.point.y - bottom);
            }
            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
        }
        public bool Plant(FarmPrototype world, FarmItem item, FarmPlot plot, Vector3 position)
        {
            if (State != BodyState.Loose || (npc == null && player == null)) return false;
            var crop = gameObject.AddComponent<FleeingCrop>();
            if (!plot.Plant(crop)) { Destroy(crop); return false; }
            State = BodyState.Planted;
            this.world = world;
            plantedGroundPosition = position;
            Vector3 size = transform.lossyScale;
            transform.SetParent(null, true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, world.gameObject.scene);
            transform.SetParent(world.transform, true);
            transform.SetPositionAndRotation(position, PlantingRotation); transform.localScale = size;
            gameObject.SetActive(true);
            var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            pickupCollider.enabled = true;
            crop.Configure(world, item, plot, position.y);
            Bounds model = ModelBounds();
            transform.position += Vector3.up * (position.y - model.min.y - model.size.y * buriedFraction);
            item.MarkPlanted(); Destroy(item);
            world.SetMessage("Body planted. Its health is recovering; it will revive at full health.");
            return true;
        }
        private Bounds ModelBounds()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero); bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds : pickupCollider.bounds;
        }
        private void Update() => Recover(Time.deltaTime);
        public void Recover(float elapsed)
        {
            if (State != BodyState.Planted || elapsed <= 0 || (npc == null && player == null)) return;
            float amount = MaxHealth / Mathf.Max(.1f, healingSeconds) * elapsed;
            if (npc != null) npc.RecoverWhilePlanted(amount); else player.RecoverWhilePlanted(amount);
            if (Health < MaxHealth) return;
            GetComponent<FleeingCrop>()?.FinishCorpseRecovery();
            CompleteRevival(world);
        }
        public void CompleteRevival(FarmPrototype world)
        {
            if (State != BodyState.Planted || Health < MaxHealth) return;
            State = BodyState.Revived;
            // Raise the recovered body out of the soil before restoring movement and collisions.
            transform.position += Vector3.up * (plantedGroundPosition.y - ModelBounds().min.y);
            pickupCollider.enabled = false; Destroy(pickupCollider);
            var body = GetComponent<Rigidbody>(); body.detectCollisions = false; Destroy(body);
            if (npc != null) npc.ReviveFromPlant(world);
            else if (player != null)
            {
                player.ReviveAt(plantedGroundPosition + Vector3.up * .1f);
                Destroy(gameObject);
            }
            world.SetMessage(ownerName + " revived!");
            if (npc != null) Destroy(this);
        }
    }
}
