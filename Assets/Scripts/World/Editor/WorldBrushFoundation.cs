using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldBrushFoundation
    {
        public const string Root = "Assets/NusantaraWorld/BrushWorld";
        public const string SourceScene = "Assets/Region5Rework/Region5_Rework.unity";
        const string SourceDefinition = "Assets/Region5Rework/Region5ChunkWorld.asset";

        [MenuItem("Tools/Nusantara/Combine Brush World and Region 5")]
        public static void CreateMenu() => Debug.Log(Create());
        [MenuItem("Tools/Nusantara/Combine Brush World and Region 5", true)]
        public static bool CanCreate() => !AssetDatabase.IsValidFolder(Root) && AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) != null;

        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != WorldReferenceBuilder.ScenePath) throw new InvalidOperationException("Open NusantaraQuest_World first.");
            var world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if (world == null || world.brushAuthoredTerrain || AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("The brush world already exists, or its definition is missing. Existing brush work is never regenerated.");
            var source = AssetDatabase.LoadAssetAtPath<WorldDefinition>(SourceDefinition);
            if (source == null || source.gridSize != new Vector2Int(6, 6) || source.chunks.Length != 36)
                throw new InvalidOperationException("Region 5 must have its existing complete 6x6 grid.");
            var region5 = world.regions.Single(r => r.id == 5);
            if (region5.chunkGridSize != source.gridSize)
                throw new InvalidOperationException("Keep Region 5 at 6x6 while importing its authored terrain. Other region sizes are independent.");
            string backup = "Library/Nusantara/CombinedWorldBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            foreach (string path in new[] { WorldFoundationSetup.DefinitionPath, WorldReferenceBuilder.ProfilePath, SourceScene, SourceDefinition })
                VegetationPaintAssets.Backup(path, backup);
            if (!EditorSceneManager.SaveScene(scene, backup + "/NusantaraQuest_World.unity", true))
                throw new InvalidOperationException("Could not preserve the original world scene.");
            Scene rework = SceneManager.GetSceneByPath(SourceScene);
            bool opened = !rework.isLoaded;
            if (opened) rework = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            var authoring = rework.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RegionChunkAuthoring>(true)).Single();
            var tiles = authoring.editingRoot.GetComponentsInChildren<RegionChunkTile>(true).ToDictionary(t => t.coordinate);
            if (tiles.Count != 36) throw new InvalidOperationException("Region 5 scene has an incomplete authored grid.");
            var player = scene.GetRootGameObjects().Single(g => g.name == "Player");
            var profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            if (profile == null) throw new InvalidOperationException("The existing brush geography profile is required.");
            if (profile.sculptData != null) VegetationPaintAssets.Backup(AssetDatabase.GetAssetPath(profile.sculptData), backup);
            EnsureFolder(Root + "/Chunks"); EnsureFolder(Root + "/VegetationPatches");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Nusantara World Chunks");
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            EditorUtility.SetDirty(bundle);
            world.gridSize = WorldRegionGridLayout.Pack(world.regions);
            world.chunkSize = source.chunkSize;
            world.origin = source.origin - (Vector2)region5.chunkOffset * world.chunkSize;
            world.authoredChunkLayout = true; world.brushAuthoredTerrain = true; world.layoutApproved = true;
            world.worldId = "nusantara-brush-world"; world.chunkAssetRoot = Root;
            world.gameplayRadius = 1; world.visualRadius = 1; world.distantRadius = 2; world.unloadRadius = 3;
            world.preloadSeconds = 2; world.maxLookAheadMetres = 32; world.maxConcurrentLoads = 1; world.planningInterval = .2f;
            world.locations = Array.Empty<WorldLocation>();
            profile.world = world; profile.groundLevel = 41; profile.terrainIntervals = 32; profile.distantIntervals = 8;
            profile.coast = Array.Empty<Vector2>(); profile.waterways = Array.Empty<WorldWaterway>(); profile.routes = Array.Empty<WorldWalkingRoute>();
            profile.scenicIslands = Array.Empty<WorldScenicIsland>(); profile.walkingPlatforms = Array.Empty<WorldWalkingPlatform>();
            profile.buildingFoundations = Array.Empty<WorldBuildingFoundation>(); profile.region5ReliefStrength = 0;
            var sculpt = ScriptableObject.CreateInstance<WorldTerrainSculptData>();
            sculpt.origin = world.origin; sculpt.spacing = world.chunkSize / profile.terrainIntervals;
            AssetDatabase.CreateAsset(sculpt, Root + "/SculptData.asset"); profile.sculptData = sculpt;
            var records = new List<WorldChunkRecord>();
            try
            {
                foreach (var region in world.regions.OrderBy(r => r.id))
                {
                    var origin = world.ChunkOrigin(region.chunkOffset);
                    float width = region.chunkGridSize.x * world.chunkSize, depth = region.chunkGridSize.y * world.chunkSize;
                    region.detailedEnvironment = true;
                    region.boundary = new[] { new Vector2(origin.x, origin.z), new Vector2(origin.x + width, origin.z),
                        new Vector2(origin.x + width, origin.z + depth), new Vector2(origin.x, origin.z + depth) };
                    for (int z = 0; z < region.chunkGridSize.y; z++) for (int x = 0; x < region.chunkGridSize.x; x++)
                    {
                        var local = new Vector2Int(x, z); var coordinate = region.chunkOffset + local;
                        EditorUtility.DisplayProgressBar("Combining brush world", $"Region {region.id}: {x + 1}, {z + 1}", records.Count / 216f);
                        var tile = region.id == 5 ? tiles[local] : null;
                        records.Add(CreateChunk(world, profile, settings, group, coordinate, region.id, tile));
                    }
                }
                world.chunks = records.ToArray();
                // Keep the source player's established Region 5 position, with the main world's player and cameras.
                var delta = authoring.streamer.player.position - player.transform.position;
                player.transform.position += delta;
                foreach (string name in new[] { "Camera", "Isometric Camera", "Rideable Horse" })
                {
                    var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                    if (go != null) go.transform.position += delta;
                }
                world.playerStart = player.transform.position; world.playerStartConfirmed = true;
                var oldPreview = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "World preview (Editor only)");
                if (oldPreview != null) Object.DestroyImmediate(oldPreview);
                var root = scene.GetRootGameObjects().Single(g => g.name == "World");
                var streamer = root.GetComponent<WorldChunkStreamer>(); streamer.definition = world; streamer.player = player.transform;
                root.SetActive(true);
                foreach (var nav in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldAINavigation>(true)))
                { nav.world = world; nav.walkingGround = null; nav.ReloadGround(); }
                // Hand-painted water and no-walk masks belong to the authored source, not its old generated preview.
                foreach (var go in rework.GetRootGameObjects())
                    if (go.GetComponent<WorldPaintedWater>() != null || go.GetComponent<WorldAINoWalkZone>() != null)
                    {
                        var copy = Object.Instantiate(go); copy.name = go.name; SceneManager.MoveGameObjectToScene(copy, scene);
                    }
                var errors = world.ConfigurationErrors(true).ToArray();
                if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
                // Archived procedural chunks stay on disk for recovery, but are excluded from mobile content builds.
                foreach (var entry in group.entries.Where(e => e.AssetPath.StartsWith(WorldReferenceBuilder.Root + "/Chunks/", StringComparison.Ordinal)).ToArray())
                    settings.RemoveAssetEntry(entry.guid, false);
                var sourceGroup = settings.FindGroup("Region 5 Authored Chunks");
                if (sourceGroup != null) { sourceGroup.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = false; EditorUtility.SetDirty(sourceGroup.GetSchema<BundledAssetGroupSchema>()); }
                EditorUtility.SetDirty(world); EditorUtility.SetDirty(profile); EditorUtility.SetDirty(sculpt); EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets(); WorldTerrainSculptAssets.InvalidateCache();
                SceneManager.SetActiveScene(scene);
                WorldSceneBookmarks.LoadPreview(world.playerStart, false);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return $"Combined {records.Count} brush-authored chunks in six independently sized regions. Region 5 terrain and painted water preserved. Backup: {backup}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (opened && rework.isLoaded) EditorSceneManager.CloseScene(rework, true);
            }
        }

        internal static WorldChunkRecord CreateChunk(WorldDefinition world, WorldGeographyProfile profile, AddressableAssetSettings settings,
            AddressableAssetGroup group, Vector2Int coordinate, int region, RegionChunkTile tile, string assetFolder = null)
        {
            if(world.regionalGroundPlanes)
            {var record=new WorldChunkRecord{coordinate=coordinate,region=region};record.assetFolder=WorldRegionalGroundAssets.DetailFolder(world,record);return record;}
            string folder = assetFolder ?? WorldTerrainSculptAssets.Folder(world, coordinate); EnsureFolder(folder);
            var essential = tile != null ? Object.Instantiate(tile.transform.Find("Ground and Structures").gameObject) : new GameObject("Essential");
            essential.name = "Essential"; essential.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); essential.transform.localScale = Vector3.one;
            try
            {
                var ground = tile != null ? essential.transform.Find("Ground").gameObject : new GameObject("Terrain");
                if (tile == null) ground.transform.SetParent(essential.transform, false);
                ground.name = "Terrain";
                var mesh = tile != null ? Object.Instantiate(ground.GetComponent<MeshFilter>().sharedMesh)
                    : WorldTerrainMesher.Build(world, coordinate, profile.terrainIntervals, (_, __) => profile.groundLevel);
                mesh.name = "Terrain"; AssetDatabase.CreateAsset(mesh, folder + "/Terrain.asset");
                var filter = ground.GetComponent<MeshFilter>(); if (filter == null) filter = ground.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                var collider = ground.GetComponent<MeshCollider>(); if (collider == null) collider = ground.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                var renderer = ground.GetComponent<MeshRenderer>(); if (renderer == null) renderer = ground.AddComponent<MeshRenderer>();
                if (tile == null) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_Terrain.mat");
                if (renderer.sharedMaterial == null) renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_Terrain.mat");
                var vertices = mesh.vertices; var chunkOrigin = world.ChunkOrigin(coordinate);
                // Import existing authored height offsets into the same sparse brush lattice.
                int side = Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));
                if (tile != null && side * side == vertices.Length)
                    for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
                        profile.sculptData.Set(coordinate.x * (side - 1) + x, coordinate.y * (side - 1) + z, vertices[z * side + x].y - profile.groundLevel);
                string prefix = $"brush-world/region-{region}/{coordinate.x:D2}_{coordinate.y:D2}";
                var record = new WorldChunkRecord { coordinate = coordinate, region = region, assetFolder = folder,
                    essentialAddress = SavePart(settings, group, essential, folder + "/Essential.prefab", prefix + "/essential") };
                var distant = new GameObject("Distant Ground");
                try
                {
                    var farGround = new GameObject("Terrain"); farGround.transform.SetParent(distant.transform, false);
                    float Sample(float x, float z)
                    {
                        float gx = Mathf.Clamp((x - chunkOrigin.x) / world.chunkSize * (side - 1), 0, side - 1);
                        float gz = Mathf.Clamp((z - chunkOrigin.z) / world.chunkSize * (side - 1), 0, side - 1);
                        int ix = Mathf.Min((int)gx, side - 2), iz = Mathf.Min((int)gz, side - 2); float u = gx - ix, v = gz - iz; int n = iz * side + ix;
                        return u + v <= 1 ? vertices[n].y + (vertices[n + 1].y - vertices[n].y) * u + (vertices[n + side].y - vertices[n].y) * v
                            : vertices[n + side + 1].y + (vertices[n + side].y - vertices[n + side + 1].y) * (1 - u) + (vertices[n + 1].y - vertices[n + side + 1].y) * (1 - v);
                    }
                    var farMesh = WorldTerrainMesher.Build(world, coordinate, profile.distantIntervals, Sample);
                    AssetDatabase.CreateAsset(farMesh, folder + "/DistantTerrain.asset");
                    farGround.AddComponent<MeshFilter>().sharedMesh = farMesh;
                    var farRenderer = farGround.AddComponent<MeshRenderer>(); farRenderer.sharedMaterials = renderer.sharedMaterials; farRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    record.distantAddress = SavePart(settings, group, distant, folder + "/Distant.prefab", prefix + "/distant");
                }
                finally { Object.DestroyImmediate(distant); }
                if (tile != null)
                {
                    record.decorationAddresses = SaveOptional(settings, group, tile.transform.Find("Details"), folder + "/Details.prefab", prefix + "/details");
                    record.gameplayAddresses = SaveOptional(settings, group, tile.transform.Find("Gameplay"), folder + "/Gameplay.prefab", prefix + "/gameplay");
                }
                return record;
            }
            finally { Object.DestroyImmediate(essential); }
        }
        static string[] SaveOptional(AddressableAssetSettings settings, AddressableAssetGroup group, Transform source, string path, string address)
        {
            if (source == null || source.childCount == 0) return Array.Empty<string>();
            var copy = Object.Instantiate(source.gameObject);
            try { copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); return new[] { SavePart(settings, group, copy, path, address) }; }
            finally { Object.DestroyImmediate(copy); }
        }
        static string SavePart(AddressableAssetSettings settings, AddressableAssetGroup group, GameObject source, string path, string address)
        {
            source.tag = "Untagged"; source.SetActive(true); PrefabUtility.SaveAsPrefabAsset(source, path);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false); entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, false, true); return address;
        }
        static void EnsureFolder(string path) => Region5EnvironmentAssets.EnsureFolder(path);
    }
}
