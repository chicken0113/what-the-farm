using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WhatTheFarm.Prototype;

public static class GrowableShovelBuilder
{
    const string Prefab = "Assets/Prefabs/Game/Hoe.prefab";
    const string Head = "Assets/Prefabs/Game/ShovelHead.asset";
    [MenuItem("What The Farm/Configure Growable Shovel")]
    public static void Install()
    {
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var tool = root.GetComponent<GrowableTool>();
            var filter = root.GetComponentInChildren<MeshFilter>(); var source = tool != null ? tool.CompleteMesh : filter.sharedMesh;
            var vertices = source.vertices; var normals = source.normals; var uv = source.uv;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                float y = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)).y;
                bottom = Mathf.Min(bottom,y); top = Mathf.Max(top,y);
            }
            // In the existing upright Shovel_A model the metal blade is at the upper end.
            float cut = top - (top-bottom)*.35f;
            var remap = new Dictionary<int,int>(); var points = new List<Vector3>();
            var tex = new List<Vector2>(); var normal = new List<Vector3>(); var triangles = new List<int>();
            var indices = source.triangles;
            for (int i=0;i<indices.Length;i+=3)
            {
                bool keep = true;
                for (int j=0;j<3;j++)
                    if (root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[i+j]])).y<cut) keep=false;
                if (!keep) continue;
                for (int j=0;j<3;j++)
                {
                    int original=indices[i+j];
                    if (!remap.TryGetValue(original,out int mapped))
                    {
                        mapped=points.Count; remap.Add(original,mapped); points.Add(vertices[original]);
                        if (uv.Length==vertices.Length) tex.Add(uv[original]);
                        if (normals.Length==vertices.Length) normal.Add(normals[original]);
                    }
                    triangles.Add(mapped);
                }
            }
            if (triangles.Count==0) throw new InvalidOperationException("No shovel blade triangles found.");
            var mesh=new Mesh { name="Shovel Blade" }; mesh.SetVertices(points); mesh.SetTriangles(triangles,0);
            if(tex.Count==points.Count) mesh.SetUVs(0,tex);
            if(normal.Count==points.Count) mesh.SetNormals(normal); else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Head);
            if (existing != null) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh,Head);
            if (tool == null) { root.transform.localScale*=3; tool=root.AddComponent<GrowableTool>(); }
            tool.Configure(filter,mesh,source);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab); AssetDatabase.SaveAssets();
            Debug.Log($"GROWABLE_SHOVEL_READY: {triangles.Count/3} head triangles, complete scale {root.transform.localScale}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void ValidateInBatch() { Install(); UnityMigrationBatchCheck.PlayExisting(); }
}
