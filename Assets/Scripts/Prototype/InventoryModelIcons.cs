using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WhatTheFarm.Prototype
{
    // Copies only visual components. Never instantiate an item/NPC's gameplay scripts for an icon.
    public sealed class InventoryModelIcons : System.IDisposable
    {
        private sealed class Icon
        {
            public FarmItem item;
            public RenderTexture image;
        }
        private const int PreviewLayer = 31;
        private readonly Dictionary<int, Icon> icons = new();
        private readonly List<Object> owned = new();
        private GameObject studio;
        private Camera camera;
        private Light previewLight;
        public Texture Get(FarmItem item)
        {
            if (item == null) return null;
            int id = item.GetInstanceID();
            if (icons.TryGetValue(id, out var icon)) return icon.image;
            if (Event.current != null && Event.current.type != EventType.Repaint) return null;
            EnsureStudio();
            var model = new GameObject("Inventory visual"); model.layer = PreviewLayer;
            model.transform.SetParent(studio.transform, false);
            var transient = new List<Object>();
            try
            {
                var transforms = new Dictionary<Transform, Transform> { [item.transform] = model.transform };
                Transform CopyTransform(Transform source)
                {
                    if (transforms.TryGetValue(source, out var existing)) return existing;
                    var proxy = new GameObject(source.name); proxy.layer = PreviewLayer;
                    proxy.transform.SetParent(CopyTransform(source.parent), false);
                    proxy.transform.localPosition = source.localPosition;
                    proxy.transform.localRotation = source.localRotation;
                    proxy.transform.localScale = source.localScale;
                    transforms[source] = proxy.transform;
                    return proxy.transform;
                }
                Bounds bounds = default; bool found = false;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                    bool visible = true;
                    for (var part = renderer.transform; part != item.transform; part = part.parent)
                        if (!part.gameObject.activeSelf) { visible = false; break; }
                    if (!visible) continue; // An inactive inventory root still has a valid model.
                    Mesh mesh;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        mesh = new Mesh(); skin.BakeMesh(mesh, false); transient.Add(mesh);
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null) continue;
                        mesh = filter.sharedMesh;
                    }
                    var target = CopyTransform(renderer.transform).gameObject;
                    target.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var visual = target.AddComponent<MeshRenderer>();
                    // A predictable lit preview keeps the model's textures and base colours.
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var source = renderer.sharedMaterials[i];
                        var shader = Shader.Find(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset
                            ? "Universal Render Pipeline/Lit" : "Standard");
                        if (shader == null) continue;
                        var material = new Material(shader); transient.Add(material);
                        Color color = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                            source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                        Texture texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                            source != null && source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                        material.color = color; material.mainTexture = texture;
                        materials[i] = material;
                    }
                    visual.sharedMaterials = materials;
                    visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
                    if (!found) { bounds = visual.bounds; found = true; } else bounds.Encapsulate(visual.bounds);
                }
                if (!found) return null;
                camera.transform.rotation = Quaternion.Euler(12, -25, 0);
                float extent = .01f;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 local = Quaternion.Inverse(camera.transform.rotation) * (point - bounds.center);
                    extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
                }
                camera.orthographicSize = extent * 1.18f;
                float distance = bounds.size.magnitude * 2 + 1;
                camera.transform.position = bounds.center - camera.transform.forward * distance;
                camera.farClipPlane = distance * 3;
                var image = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32)
                    { name = "Inventory model icon", antiAliasing = 1 };
                image.Create();
                camera.targetTexture = image;
                previewLight.enabled = true;
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = image });
                else camera.Render();
                camera.targetTexture = null;
                previewLight.enabled = false;
                icons[id] = new Icon { item = item, image = image };
                owned.Add(image);
                return image;
            }
            finally
            {
                camera.targetTexture = null;
                previewLight.enabled = false;
                model.SetActive(false);
                Destroy(model);
                foreach (var value in transient) Destroy(value);
            }
        }
        private void EnsureStudio()
        {
            if (studio != null) return;
            studio = new GameObject("Inventory icon studio") { hideFlags = HideFlags.HideAndDontSave };
            studio.transform.position = new Vector3(0, -10000, 0);
            var cameraObject = new GameObject("Icon camera"); cameraObject.transform.SetParent(studio.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << PreviewLayer; camera.orthographic = true; camera.nearClipPlane = .01f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = false; camera.allowMSAA = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
            }
            var lightObject = new GameObject("Icon light"); lightObject.transform.SetParent(studio.transform, false);
            previewLight = lightObject.AddComponent<Light>(); previewLight.type = LightType.Directional;
            previewLight.cullingMask = 1 << PreviewLayer; previewLight.intensity = 1.2f;
            previewLight.transform.rotation = Quaternion.Euler(30, -30, 0);
            previewLight.enabled = false;
        }
        public void Prune(FarmItem[] inventory)
        {
            var keep = new HashSet<int>();
            foreach (var item in inventory) if (item != null) keep.Add(item.GetInstanceID());
            var remove = new List<int>();
            foreach (var entry in icons) if (entry.Value.item == null || !keep.Contains(entry.Key)) remove.Add(entry.Key);
            foreach (int id in remove)
            {
                var image = icons[id].image; owned.Remove(image); image.Release(); Destroy(image); icons.Remove(id);
            }
        }
        public void Dispose()
        {
            foreach (var value in owned) { if (value is RenderTexture texture) texture.Release(); Destroy(value); }
            owned.Clear(); icons.Clear(); if (studio != null) Destroy(studio);
        }
        private static void Destroy(Object value)
        { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
