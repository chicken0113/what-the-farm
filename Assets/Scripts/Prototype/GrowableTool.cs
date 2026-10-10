using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class GrowableTool : MonoBehaviour
    {
        [SerializeField] private MeshFilter model;
        [SerializeField] private Mesh headMesh;
        [SerializeField] private Mesh completeMesh;
        [SerializeField] private bool planted;
        public bool IsComplete => planted;
        public Mesh CompleteMesh => completeMesh;
        public void Configure(MeshFilter visual, Mesh head, Mesh complete)
        { model = visual; headMesh = head; completeMesh = complete; Apply(); }
        private void Awake() => Apply();
        public void ShowComplete() { planted = true; Apply(); }
        private void Apply()
        {
            if (model == null || headMesh == null || completeMesh == null) return;
            model.sharedMesh = planted ? completeMesh : headMesh;
            var bounds = model.sharedMesh.bounds;
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Bounds local = default; bool found = false;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                point = transform.InverseTransformPoint(model.transform.TransformPoint(point));
                if (!found) { local = new Bounds(point, Vector3.zero); found = true; } else local.Encapsulate(point);
            }
            box.center = local.center; box.size = local.size;
        }
    }
}
