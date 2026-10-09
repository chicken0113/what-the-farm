using UnityEngine;

namespace WhatTheFarm.Prototype
{
    public sealed class SoilSurface : MonoBehaviour
    {
        [SerializeField] private SoilType soilType;
        [SerializeField, Range(0, 100)] private float lightAmount = 80;
        [SerializeField, Range(0, 100)] private float initialWaterAmount;
        [SerializeField] private Light sunlight;
        [SerializeField, Min(.01f)] private float fullSunIntensity = 1.5f;
        [SerializeField, Range(0, 100)] private float shadeLightAmount = 20;
        [SerializeField, Range(0, .45f), Tooltip("Visual edge irregularity. Planting and tool radius stay unchanged; zero gives a circle.")]
        private float tillEdgeVariation = .3f;
        public SoilType Type => soilType;
        public float InitialWaterAmount => Mathf.Clamp(initialWaterAmount, 0, 100);
        public void SetEnvironment(SoilType type, float light, float water = 0, Light sun = null)
        {
            soilType = type; lightAmount = Mathf.Clamp(light, 0, 100);
            initialWaterAmount = Mathf.Clamp(water, 0, 100); sunlight = sun;
        }

        public float GetLight(Vector3 point, Transform growingObject = null)
        {
            if (sunlight == null) return Mathf.Clamp(lightAmount, 0, 100);
            if (!sunlight.enabled || !sunlight.gameObject.activeInHierarchy) return shadeLightAmount;
            Vector3 direction = -sunlight.transform.forward;
            if (sunlight.type != LightType.Directional) return Mathf.Clamp(lightAmount, 0, 100);
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * .05f, direction, 100, ~0,
                QueryTriggerInteraction.Ignore))
            {
                if (growingObject != null && hit.collider.transform.IsChildOf(growingObject)) continue;
                if (hit.collider.GetComponentInParent<FarmItem>() != null) continue;
                return shadeLightAmount;
            }
            return Mathf.Clamp(lightAmount * sunlight.intensity / Mathf.Max(.01f, fullSunIntensity), 0, 100);
        }
        private readonly System.Collections.Generic.List<FarmPlot> areas = new();
        public int WaterArea(Vector3 point, float radius, float amount)
        {
            int watered = 0;
            foreach (var area in areas)
            {
                if (area == null || !area.IsOccupied || area.WaterAmount >= 100) continue;
                Vector3 target = area.PlantPosition;
                if (new Vector2(target.x - point.x, target.z - point.z).sqrMagnitude > radius * radius) continue;
                area.Water(amount);
                watered++;
            }
            return watered;
        }

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
            const int edgeControls = 12;
            var edgeRadii = new float[edgeControls];
            float largest = 0;
            for (int i = 0; i < edgeControls; i++)
            {
                edgeRadii[i] = Random.Range(1 - Mathf.Clamp(tillEdgeVariation, 0, .45f), 1f);
                largest = Mathf.Max(largest, edgeRadii[i]);
            }
            float rotation = Random.Range(0, Mathf.PI * 2);
            var edge = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float control = (float)i * edgeControls / segments;
                int index = Mathf.FloorToInt(control);
                float blend = Mathf.SmoothStep(0, 1, control - index);
                float outlineRadius = radius * Mathf.Lerp(edgeRadii[index], edgeRadii[(index + 1) % edgeControls], blend) / largest;
                float angle = rotation + i * Mathf.PI * 2 / segments;
                edge[i] = ClipToGround(ground, point,
                    point + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * outlineRadius);
            }
            float height = .012f + areas.Count * .0001f;
            // Only the visible outline varies; the stored gameplay radius stays the same.
            // Reuse the closed perimeter so neighbouring triangles share exactly the same edge.
            for (int i = 0; i < segments; i++)
            {
                int start = vertices.Count;
                vertices.Add(Vector3.up * height);
                vertices.Add(edge[(i + 1) % segments] - point + Vector3.up * height);
                vertices.Add(edge[i] - point + Vector3.up * height);
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
            plot.BindSurface(this);
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
