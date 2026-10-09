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
        [SerializeField] private Quaternion fallenRotation;
        private BoxCollider pickupCollider;
        private Vector3 plantedGroundPosition;
        public string OwnerId => ownerId;
        public string DisplayName => ownerName + " Body";
        public BodyState State { get; private set; }
        public void BindNpc(FarmGuardian target)
        {
            npc = target; ownerId = target.ActorId;
            ownerName = target.GetComponent<NpcMerchant>()?.DisplayName ?? "NPC";
            PreparePickup(target.RevivalGrowthProfile);
        }
        public void BindPlayer(LocalFarmer target, PlantGrowthProfile profile)
        {
            player = target; ownerId = target.ActorId; ownerName = "Player";
            PreparePickup(profile);
        }
        private void PreparePickup(PlantGrowthProfile profile)
        {
            fallenRotation = transform.rotation;
            var item = GetComponent<FarmItem>();
            if (item == null) item = gameObject.AddComponent<FarmItem>();
            item.Configure(ItemKind.Corpse, 0, 0);
            item.SetGrowthProfile(profile);
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            pickupCollider = gameObject.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero); bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            pickupCollider.center = bounds.center;
            pickupCollider.size = found ? Vector3.Max(bounds.size, Vector3.one * .05f) : Vector3.one * .5f;
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
            plantedGroundPosition = position;
            Vector3 size = transform.lossyScale;
            transform.SetParent(null, true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, world.gameObject.scene);
            transform.SetParent(world.transform, true);
            transform.SetPositionAndRotation(position, fallenRotation); transform.localScale = size;
            gameObject.SetActive(true);
            var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            pickupCollider.enabled = true;
            crop.Configure(world, item, plot, position.y);
            item.MarkPlanted(); Destroy(item);
            world.SetMessage("Body planted. Water it and let it grow to revive its owner.");
            return true;
        }
        public void CompleteRevival(FarmPrototype world)
        {
            if (State != BodyState.Planted) return;
            State = BodyState.Revived;
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
