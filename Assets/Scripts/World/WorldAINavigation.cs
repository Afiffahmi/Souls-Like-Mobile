using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Nusantara.World
{
    [ExecuteAlways, DefaultExecutionOrder(-8000), DisallowMultipleComponent]
    [AddComponentMenu("Nusantara/AI Walking Ground")]
    public sealed class WorldAINavigation : MonoBehaviour
    {
        [HideInInspector] public NavMeshData walkingGround;
        [HideInInspector] public WorldDefinition world;
        NavMeshData runtimeData;
        NavMeshDataInstance instance;
        AsyncOperation building;
        WorldChunkStreamer streamer;
        bool rebuild;
        float nextBuild;
        Vector3 lastBuildCenter;
        public bool IsBuilding => building != null && !building.isDone;
        public bool HasWalkingGround => walkingGround != null;
        void OnEnable()
        {
            if (Application.isPlaying)
            {
                runtimeData = walkingGround != null ? Instantiate(walkingGround) : new NavMeshData(Settings().agentTypeID);
                instance = NavMesh.AddNavMeshData(runtimeData);
                foreach (var candidate in FindObjectsByType<WorldChunkStreamer>())
                    if (candidate.gameObject.scene == gameObject.scene) { streamer = candidate; break; }
                if (streamer != null) streamer.NavigationGeometryChanged += RequestRebuild;
                RequestRebuild();
            }
            else if (walkingGround != null) instance = NavMesh.AddNavMeshData(walkingGround);
        }
        void OnDisable()
        {
            if (streamer != null) streamer.NavigationGeometryChanged -= RequestRebuild;
            if (runtimeData != null) NavMeshBuilder.Cancel(runtimeData);
            if (instance.valid) instance.Remove();
            if (runtimeData != null) { if (Application.isPlaying) Destroy(runtimeData); else DestroyImmediate(runtimeData); }
            runtimeData = null; building = null;
        }
        public void ReloadGround()
        {
            if (Application.isPlaying) { RequestRebuild(); return; }
            if (instance.valid) instance.Remove();
            if (isActiveAndEnabled && walkingGround != null) instance = NavMesh.AddNavMeshData(walkingGround);
        }
        public void RequestRebuild() { rebuild = true; nextBuild = Time.unscaledTime + .75f; }
        void Update()
        {
            if (!Application.isPlaying) return;
            if (world != null && world.regionalGroundPlanes && streamer != null && streamer.player != null && !rebuild && !IsBuilding
                && Vector3.Distance(streamer.player.position,lastBuildCenter)>world.chunkSize*.5f) RequestRebuild();
            if (!rebuild || IsBuilding || Time.unscaledTime < nextBuild) return;
            if (streamer != null && streamer.PendingLoadCount > 0) return;
            rebuild = false;
            if (streamer != null && streamer.player != null) lastBuildCenter=streamer.player.position;
            var sources = CollectGround(out var bounds);
            building = NavMeshBuilder.UpdateNavMeshDataAsync(runtimeData, Settings(), sources, bounds);
        }
        public NavMeshBuildSettings Settings()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            if (world != null)
            {
                settings.agentSlope = world.maxWalkableSlope; settings.agentClimb = world.maxStepHeight;
                settings.agentRadius = world.agentRadius; settings.agentHeight = world.agentHeight;
            }
            settings.overrideVoxelSize = true; settings.voxelSize = .4f;
            settings.overrideTileSize = true; settings.tileSize = 256;
            return settings;
        }
        public static bool IsGround(Collider collider) => collider.name == "Terrain" || collider.name.Contains("Connected paths")
            || collider.name.Contains("Smooth bridges") || collider.name.Contains("Arena floor") || collider.name.StartsWith("Kampung walking")
            || collider.name == "Ground" || collider.name == "Floor";

        public List<NavMeshBuildSource> CollectGround(out Bounds bounds)
        {
            var markups = new List<NavMeshBuildMarkup>();
            bounds = new Bounds(transform.position, Vector3.one); bool hasBounds = false;
            foreach (var collider in FindObjectsByType<Collider>())
            {
                bool ignore = collider.gameObject.scene != gameObject.scene || collider.isTrigger || collider is CharacterController
                    || collider.GetComponentInParent<NavMeshAgent>() != null || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic);
                markups.Add(new NavMeshBuildMarkup { root = collider.transform, ignoreFromBuild = ignore, overrideArea = true, area = IsGround(collider) ? 0 : 1 });
                if (ignore || !IsGround(collider)) continue;
                if (hasBounds) bounds.Encapsulate(collider.bounds); else { bounds = collider.bounds; hasBounds = true; }
            }
            bounds.Expand(new Vector3(8, 40, 8));
            if (Application.isPlaying && world != null && world.regionalGroundPlanes && streamer != null && streamer.player != null)
                bounds=new Bounds(streamer.player.position,new Vector3(world.chunkSize*5,100,world.chunkSize*5));
            var sources = new List<NavMeshBuildSource>();
            if (!hasBounds) return sources;
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(s => s.component != null && s.component.gameObject.scene != gameObject.scene);
            sources.Sort((a,b) => (a.component != null ? a.component.GetEntityId().GetHashCode() : 0).CompareTo(b.component != null ? b.component.GetEntityId().GetHashCode() : 0));
            return sources;
        }
    }
}
