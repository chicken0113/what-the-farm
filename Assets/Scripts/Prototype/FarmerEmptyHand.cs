using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatTheFarm.Prototype
{
    // A small Unity-native blockout fist. It has no collider or interaction component.
    public sealed class FarmerEmptyHand : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new();
        private readonly List<Material> materials = new();
        public bool IsVisible => gameObject.activeSelf;
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public static FarmerEmptyHand Create(Transform camera)
        {
            var root = new GameObject("Empty Hand");
            root.transform.SetParent(camera, false);
            root.transform.localPosition = new Vector3(.34f, -.31f, .6f);
            root.transform.localRotation = Quaternion.Euler(-10, -12, 12);
            var hand = root.AddComponent<FarmerEmptyHand>();
            var skin = new List<CombineInstance>();
            hand.Box(skin, Vector3.zero, new Vector3(.115f, .13f, .12f));
            for (int finger = 0; finger < 4; finger++)
                hand.Box(skin, new Vector3(-.042f + finger * .028f, .006f, .065f), new Vector3(.025f, .09f, .055f));
            hand.Box(skin, new Vector3(-.072f, -.027f, .025f), new Vector3(.045f, .075f, .07f), Quaternion.Euler(0, 15, -25));
            hand.Box(skin, new Vector3(0, -.09f, -.012f), new Vector3(.095f, .075f, .105f));
            hand.Part("Fist", skin, new Color(.72f, .49f, .32f));
            var sleeve = new List<CombineInstance>();
            hand.Box(sleeve, new Vector3(0, -.245f, -.045f), new Vector3(.12f, .25f, .13f));
            hand.Part("Sleeve", sleeve, new Color(.14f, .28f, .36f));
            return hand;
        }

        private void Box(List<CombineInstance> parts, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Face(Vector3 normal, Vector3 across, Vector3 up)
            {
                int first = vertices.Count;
                Vector3 midpoint = normal * .5f;
                vertices.Add(midpoint - across * .5f - up * .5f);
                vertices.Add(midpoint + across * .5f - up * .5f);
                vertices.Add(midpoint + across * .5f + up * .5f);
                vertices.Add(midpoint - across * .5f + up * .5f);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            Face(Vector3.forward, Vector3.right, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.down);
            Face(Vector3.right, Vector3.forward, Vector3.down);
            Face(Vector3.left, Vector3.forward, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.back);
            Face(Vector3.down, Vector3.right, Vector3.forward);
            var mesh = new Mesh { name = "Hand block" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            meshes.Add(mesh);
            parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(center, rotation ?? Quaternion.identity, size) });
        }

        private void Part(string name, List<CombineInstance> boxes, Color color)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Empty hand " + name };
            mesh.CombineMeshes(boxes.ToArray()); mesh.RecalculateBounds();
            meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .08f);
            materials.Add(material);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
