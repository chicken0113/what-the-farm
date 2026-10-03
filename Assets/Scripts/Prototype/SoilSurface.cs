using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class SoilSurface : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<FarmPlot> areas = new();

        public FarmPlot FindPlot(Vector3 point)
        {
            FarmPlot closest = null;
            float distance = float.PositiveInfinity;
            foreach (FarmPlot area in areas)
            {
                if (area == null) continue;
                float squared = new Vector2(point.x - area.transform.position.x,
                    point.z - area.transform.position.z).sqrMagnitude;
                if (squared <= area.Radius * area.Radius && squared < distance)
                {
                    closest = area;
                    distance = squared;
                }
            }
            return closest;
        }

        public FarmPlot Till(Vector3 point, float radius, Material dry, Material wet)
        {
            if (FindPlot(point) != null) return null;
            radius = Mathf.Max(.1f, radius);
            Collider ground = GetComponent<Collider>();
            if (ground == null) return null;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            const int segments = 48;
            float height = .012f + areas.Count * .0001f;
            // A thin fan follows this collider and clips at the ground's edges.
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                float b = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 first = point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                Vector3 second = point + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                first = ClipToGround(ground, point, first);
                second = ClipToGround(ground, point, second);
                int start = vertices.Count;
                vertices.Add(Vector3.up * height);
                vertices.Add(second - point + Vector3.up * height);
                vertices.Add(first - point + Vector3.up * height);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            var root = new GameObject("Tilled ground area");
            root.transform.position = point;
            // Keep world dimensions independent of the ground block's scale.
            root.transform.SetParent(transform, true);
            var mesh = new Mesh { name = "Local tilled soil" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FarmPlot plot = root.AddComponent<FarmPlot>();
            plot.ConfigureArea(renderer, dry, wet, radius);
            areas.Add(plot);
            return plot;
        }

        private static Vector3 ClipToGround(Collider ground, Vector3 center, Vector3 edge)
        {
            bool Sample(Vector3 point, out Vector3 surface)
            {
                var ray = new Ray(new Vector3(point.x, ground.bounds.max.y + 1, point.z), Vector3.down);
                bool found = ground.Raycast(ray, out RaycastHit hit, ground.bounds.size.y + 2);
                surface = hit.point;
                return found;
            }
            if (Sample(edge, out Vector3 result)) return result;
            float inside = 0, outside = 1;
            result = center;
            for (int i = 0; i < 12; i++)
            {
                float fraction = (inside + outside) * .5f;
                if (Sample(Vector3.Lerp(center, edge, fraction), out Vector3 surface))
                {
                    inside = fraction;
                    result = surface;
                }
                else outside = fraction;
            }
            return result;
        }
    }
}
