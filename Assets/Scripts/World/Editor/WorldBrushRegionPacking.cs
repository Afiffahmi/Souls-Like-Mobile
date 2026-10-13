using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    public static class WorldBrushRegionPacking
    {
        [MenuItem("Tools/Nusantara/Recombine Brush Region Grids")]
        public static void RecombineMenu() => Debug.Log(Recombine());

        public static string Recombine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene = SceneManager.GetActiveScene();
            var world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if (scene.path != WorldReferenceBuilder.ScenePath || world == null || !world.brushAuthoredTerrain)
                throw new InvalidOperationException("Open the combined NusantaraQuest_World first.");
            foreach (var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>()) tool.DisablePainting();
            foreach (var tool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>()) tool.DisablePainting();
            foreach (var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>()) tool.Close();
            var profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            var packed = world.regions.Select(r => new WorldRegion { id = r.id, chunkGridSize = r.chunkGridSize }).ToArray();
            var grid = WorldRegionGridLayout.Pack(packed);
            if (packed.Any(r => r.chunkGridSize.x > 16 || r.chunkGridSize.y > 16))
                throw new InvalidOperationException("Use region dimensions between 1 and 16 chunks.");
            var oldOffsets = world.chunks.GroupBy(c => c.region).ToDictionary(g => g.Key, g => new Vector2Int(g.Min(c => c.coordinate.x), g.Min(c => c.coordinate.y)));
            var region5 = packed.Single(r => r.id == 5);
            Vector2 oldOrigin = world.origin;
            Vector2 origin = oldOrigin + (Vector2)(oldOffsets[5] - region5.chunkOffset) * world.chunkSize;
            var shifts = packed.ToDictionary(r => r.id, r => new Vector3(origin.x - oldOrigin.x + (r.chunkOffset.x - oldOffsets[r.id].x) * world.chunkSize, 0,
                origin.y - oldOrigin.y + (r.chunkOffset.y - oldOffsets[r.id].y) * world.chunkSize));
            var oldRecords = world.chunks.ToDictionary(c => (c.region, c.coordinate - oldOffsets[c.region]));
            var oldByCoordinate = world.chunks.ToDictionary(c => c.coordinate);
            bool Kept(WorldChunkRecord c)
            {
                var region = packed.Single(r => r.id == c.region); var local = c.coordinate - oldOffsets[c.region];
                return local.x < region.chunkGridSize.x && local.y < region.chunkGridSize.y;
            }
            var preview = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "World preview (Editor only)");
            var vegetation = AssetDatabase.FindAssets("t:VegetationPaintData", new[] { world.chunkAssetRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<VegetationPaintData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var data in vegetation)
                if (oldByCoordinate.TryGetValue(data.coordinate, out var owner) && !Kept(owner) && data.stamps.Count > 0)
                    throw new InvalidOperationException("Resizing would remove staged vegetation. Save or erase it first.");
            if (preview != null && preview.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                && PrefabUtility.HasPrefabInstanceAnyOverrides(t.gameObject, false)))
                throw new InvalidOperationException("Apply your chunk prefab overrides before resizing regions.");
            foreach (var record in world.chunks.Where(c => !Kept(c)))
            {
                var folder = WorldTerrainSculptAssets.Folder(world, record.coordinate);
                var owner=world.regions.Single(r=>r.id==record.region);
                var mesh = world.regionalGroundPlanes?WorldRegionalGroundAssets.Tile(world,owner,record.coordinate-oldOffsets[record.region]):AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Terrain.asset");
                var essential = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Essential.prefab");
                bool unsafeContent=record.decorationAddresses.Length>0||record.gameplayAddresses.Length>0||mesh==null
                    ||mesh.vertices.Any(v=>Mathf.Abs(v.y-profile.groundLevel-owner.groundElevation)>.00001f)||mesh.colors.Length>0;
                if(world.regionalGroundPlanes){unsafeContent|=essential!=null;UnityEngine.Object.DestroyImmediate(mesh);}
                else unsafeContent|=essential==null||essential.GetComponentsInChildren<Transform>(true).Length!=2
                    ||essential.GetComponentsInChildren<Component>(true).Any(c=>c==null||!(c is Transform||c is MeshFilter||c is MeshRenderer||c is MeshCollider));
                if(unsafeContent)throw new InvalidOperationException($"Region {record.region} would lose authored area {record.coordinate}. Move that work inside the smaller grid first.");
            }
            // Move whole brush objects only when every painted cell follows the same region shift.
            var moves = new Dictionary<Transform, Vector3>();
            foreach (var water in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldPaintedWater>(true)))
            {
                Vector3? shift = null;
                foreach (var cell in water.cells)
                {
                    var p = water.transform.TransformPoint(new Vector3(cell.x + .5f, 0, cell.y + .5f));
                    if (!oldByCoordinate.TryGetValue(world.WorldToChunk(p), out var record) || !Kept(record))
                        throw new InvalidOperationException("Resizing would remove painted water. Keep those chunks or erase the water first.");
                    if (shift.HasValue && shift.Value != shifts[record.region])
                        throw new InvalidOperationException("A water area crosses regions that would move apart. Split that water area before recombining.");
                    shift = shifts[record.region];
                }
                if (shift.HasValue) moves[water.transform] = shift.Value;
            }
            foreach (var zone in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldAINoWalkZone>(true)))
            {
                if (!oldByCoordinate.TryGetValue(world.WorldToChunk(zone.transform.position), out var record) || !Kept(record))
                    throw new InvalidOperationException("Resizing would remove an AI no-walk area. Move it inside the smaller grid first.");
                moves[zone.transform] = shifts[record.region];
            }
            var streamer = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldChunkStreamer>(true)).Single(s => s.definition == world);
            if (!oldByCoordinate.TryGetValue(world.WorldToChunk(streamer.player.position), out var playerRecord) || !Kept(playerRecord))
                throw new InvalidOperationException("Move the player inside a retained chunk before resizing.");
            WorldTerrainSculptAssets.Save(profile);
            string backup = "Library/Nusantara/RegionPackingBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            System.IO.Directory.CreateDirectory(backup);
            foreach (string path in new[] { WorldFoundationSetup.DefinitionPath, WorldReferenceBuilder.ProfilePath, AssetDatabase.GetAssetPath(profile.sculptData) })
                VegetationPaintAssets.Backup(path, backup);
            EditorSceneManager.SaveScene(scene, backup + "/NusantaraQuest_World.unity", true);
            var settings = AddressableAssetSettingsDefaultObject.Settings; var group = settings.FindGroup("Nusantara World Chunks");
            var records = new List<WorldChunkRecord>();
            var sculpt = ScriptableObject.CreateInstance<WorldTerrainSculptData>(); sculpt.origin = origin; sculpt.spacing = profile.sculptData.spacing;
            // Freeze folders before changing any coordinates. Prefab addresses and GUIDs remain stable.
            foreach (var record in world.chunks) record.assetFolder = WorldTerrainSculptAssets.Folder(world, record.coordinate);
            foreach (var data in vegetation)
            {
                if (!oldByCoordinate.TryGetValue(data.coordinate, out var record)) continue;
                VegetationPaintAssets.Backup(AssetDatabase.GetAssetPath(data), backup);
                if (!Kept(record)) { AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(data)); continue; }
                var local = data.coordinate - oldOffsets[record.region];
                data.coordinate = packed.Single(r => r.id == record.region).chunkOffset + local;
                foreach (var stamp in data.stamps) stamp.position += shifts[record.region];
                EditorUtility.SetDirty(data);
            }
            world.origin = origin; world.gridSize = grid;
            try
            {
                foreach (var region in world.regions)
                {
                    region.chunkOffset = packed.Single(r => r.id == region.id).chunkOffset;
                    Vector3 o = world.ChunkOrigin(region.chunkOffset); float w = region.chunkGridSize.x * world.chunkSize, d = region.chunkGridSize.y * world.chunkSize;
                    region.boundary = new[] { new Vector2(o.x, o.z), new Vector2(o.x + w, o.z), new Vector2(o.x + w, o.z + d), new Vector2(o.x, o.z + d) };
                    for (int z = 0; z < region.chunkGridSize.y; z++) for (int x = 0; x < region.chunkGridSize.x; x++)
                    {
                        var local = new Vector2Int(x, z); var coordinate = region.chunkOffset + local;
                        WorldChunkRecord record;
                        if (oldRecords.TryGetValue((region.id, local), out record)) record.coordinate = coordinate;
                        else record = WorldBrushFoundation.CreateChunk(world, profile, settings, group, coordinate, region.id, null,
                            $"{world.chunkAssetRoot}/RegionChunks/{region.id}/{x:D2}_{z:D2}_{Guid.NewGuid():N}");
                        records.Add(record);
                        var mesh = world.regionalGroundPlanes?WorldRegionalGroundAssets.Tile(world,region,local):AssetDatabase.LoadAssetAtPath<Mesh>(record.assetFolder + "/Terrain.asset");
                        var vertices = mesh.vertices; int side = Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));
                        for (int iz = 0; iz < side; iz++) for (int ix = 0; ix < side; ix++)
                            sculpt.Set(coordinate.x * (side - 1) + ix, coordinate.y * (side - 1) + iz, vertices[iz * side + ix].y - profile.groundLevel);
                        if(world.regionalGroundPlanes)UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
                foreach (var record in oldRecords.Values.Where(c => !records.Contains(c)))
                    foreach (var entry in group.entries.Where(e => e.AssetPath.StartsWith(record.assetFolder + "/", StringComparison.Ordinal)).ToArray())
                        settings.RemoveAssetEntry(entry.guid, false);
                world.chunks = records.ToArray();
                if(world.regionalGroundPlanes)
                {foreach(var region in world.regions)WorldRegionalGroundAssets.Rebuild(world,region);WorldRegionalGroundAssets.RefreshScene(world);}
                EditorUtility.CopySerialized(sculpt, profile.sculptData); profile.sculptData.InvalidateCache();
                foreach (var move in moves) move.Key.position += move.Value;
                foreach(var portal in UnityEngine.Object.FindObjectsByType<WorldRegionPortal>())
                {
                    var oldCoordinate=new Vector2Int(Mathf.FloorToInt((portal.transform.position.x-oldOrigin.x)/world.chunkSize),Mathf.FloorToInt((portal.transform.position.z-oldOrigin.y)/world.chunkSize));
                    if(oldByCoordinate.TryGetValue(oldCoordinate,out var owner))portal.transform.position+=shifts[owner.region];
                    portal.destination+=shifts[portal.destinationRegion];EditorUtility.SetDirty(portal);
                }
                Vector3 playerShift = shifts[playerRecord.region]; streamer.player.position += playerShift;
                foreach (string name in new[] { "Camera", "Isometric Camera", "Rideable Horse" })
                { var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name); if (go != null) go.transform.position += playerShift; }
                world.playerStart = streamer.player.position;
                foreach (var nav in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldAINavigation>(true)))
                { nav.walkingGround = null; nav.ReloadGround(); EditorUtility.SetDirty(nav); }
                var errors = world.ConfigurationErrors(true).ToArray();
                if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
                EditorUtility.SetDirty(world); EditorUtility.SetDirty(profile.sculptData); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
                WorldTerrainSculptAssets.InvalidateCache();
                WorldSceneBookmarks.LoadPreview(world.playerStart, false); EditorSceneManager.SaveScene(scene);
                return $"Combined {records.Count} chunks using independent region sizes. Region 5 stayed at its authored world position. Backup: {backup}";
            }
            finally { UnityEngine.Object.DestroyImmediate(sculpt); }
        }
    }
}
