using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WhatTheFarm.Prototype
{
    // Build a temporary route against the scene's actual colliders, including trees and buildings.
    internal sealed class FarmReturnRoute : System.IDisposable
    {
        private NavMeshDataInstance meshInstance;
        private NavMeshData meshData;
        private int agentType = -1;
        public Vector3[] Corners { get; private set; } = System.Array.Empty<Vector3>();
        public bool Build(FarmPrototype world, CharacterController body, Vector3 destination)
        {
            var settings = NavMesh.CreateSettings(); agentType = settings.agentTypeID;
            Vector3 scale = body.transform.lossyScale;
            settings.agentRadius = Mathf.Max(.1f, body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            settings.agentHeight = Mathf.Max(.5f, body.height * Mathf.Abs(scale.y));
            settings.agentClimb = body.stepOffset * Mathf.Abs(scale.y); settings.agentSlope = body.slopeLimit;
            settings.overrideVoxelSize = true; settings.voxelSize = settings.agentRadius / 3;
            var ignored = new List<NavMeshBuildMarkup>();
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.GetComponentInParent<FarmGuardian>() != null || collider.GetComponentInParent<LocalFarmer>() != null ||
                    collider.GetComponentInParent<FarmItem>() != null || collider.GetComponentInParent<FleeingCrop>() != null)
                    ignored.Add(new NavMeshBuildMarkup { root = collider.transform, ignoreFromBuild = true });
            var bounds = new Bounds(world.transform.position + Vector3.up * 10,
                new Vector3(world.ArenaHalfSize * 2 + 4, 60, world.ArenaHalfSize * 2 + 4));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~((1 << 8) | (1 << 9)), NavMeshCollectGeometry.PhysicsColliders, 0, ignored, sources);
            sources.RemoveAll(source => source.component != null && source.component.gameObject.scene != world.gameObject.scene);
            meshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (meshData == null) return false;
            meshInstance = NavMesh.AddNavMeshData(meshData);
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(body.transform.position, out var start, 4, filter) ||
                !NavMesh.SamplePosition(destination, out var end, 3, filter)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            Corners = path.corners; return Corners.Length > 0;
        }
        public void Dispose()
        {
            if (meshInstance.valid) meshInstance.Remove();
            if (meshData != null) Object.Destroy(meshData);
            if (agentType >= 0) { NavMesh.RemoveSettings(agentType); agentType = -1; }
        }
    }
}
