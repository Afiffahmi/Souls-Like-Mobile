using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldBrushChecks
    {
        [MenuItem("Tools/Nusantara/Run Brush World Checks")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var passed = new List<string>();
            void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAILED: " + name); passed.Add(name); }
            var regions = Enumerable.Range(1, 6).Select(id => new WorldRegion { id = id }).ToArray();
            Check(WorldRegionGridLayout.Pack(regions) == new Vector2Int(18, 12), "Six 6x6 regions combine into an 18x12 grid");
            regions[0].chunkGridSize = new Vector2Int(4, 4); regions[1].chunkGridSize = new Vector2Int(5, 5);
            var dimensions = WorldRegionGridLayout.Pack(regions); var cells = new HashSet<Vector2Int>(); bool overlaps = false;
            foreach (var region in regions)
                for (int z = 0; z < region.chunkGridSize.y; z++) for (int x = 0; x < region.chunkGridSize.x; x++)
                    overlaps |= !cells.Add(region.chunkOffset + new Vector2Int(x, z));
            Check(!overlaps && cells.Count == 185 && cells.All(c => c.x < dimensions.x && c.y < dimensions.y), "4x4, 5x5 and 6x6 grids combine without overlapping chunks");
            regions[0].chunkGridSize = Vector2Int.zero; bool rejected = false;
            try { WorldRegionGridLayout.Pack(regions); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Empty region dimensions are rejected");
            var world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            var profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            Check(world != null && world.brushAuthoredTerrain && world.authoredChunkLayout, "The combined world uses brush-authored terrain");
            Check(!world.ConfigurationErrors(true).Any(), "Combined streaming definition is valid");
            Check(world.regions.All(r => world.chunks.Count(c => c.region == r.id) == r.chunkGridSize.x * r.chunkGridSize.y), "Every region contains its configured grid");
            Check(profile.routes.Length == 0 && profile.waterways.Length == 0 && profile.coast.Length == 0,
                "No procedural roads, rivers or coastline remain in the geography profile");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entries = settings.groups.Where(g => g != null).SelectMany(g => g.entries).ToDictionary(e => e.address, e => e.AssetPath);
            bool prefabsValid = true, meshesValid = true, materialsValid = true;
            foreach (var record in world.chunks)
            {
                var addresses=new[]{record.essentialAddress,record.distantAddress}.Concat(record.decorationAddresses).Concat(record.gameplayAddresses).Where(a=>!string.IsNullOrEmpty(a));
                prefabsValid &= addresses.All(a=>entries.ContainsKey(a)&&AssetDatabase.LoadAssetAtPath<GameObject>(entries[a])!=null);
                if(world.regionalGroundPlanes)continue;
                string folder = WorldTerrainSculptAssets.Folder(world, record.coordinate);
                var near = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Terrain.asset");var far = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/DistantTerrain.asset");
                meshesValid &= near != null && far != null && near.vertexCount == 1089 && far.vertexCount == 81;
                var essential = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Essential.prefab");
                materialsValid &= essential != null && essential.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null));
            }
            if(world.regionalGroundPlanes)foreach(var region in world.regions.Where(r=>!r.paintOnly))
            {
                var marker=region.groundPrefab.GetComponent<WorldRegionGround>();var near=marker.ground;var far=marker.distantGround;
                meshesValid &= near!=null&&far!=null&&near.vertexCount==(region.chunkGridSize.x*marker.nearIntervals+1)*(region.chunkGridSize.y*marker.nearIntervals+1)
                    &&far.vertexCount==(region.chunkGridSize.x*marker.distantIntervals+1)*(region.chunkGridSize.y*marker.distantIntervals+1)
                    &&near.vertices.All(v=>float.IsFinite(v.y))&&near.indexFormat==UnityEngine.Rendering.IndexFormat.UInt16;
                materialsValid &= region.groundPrefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null));
            }
            Check(prefabsValid,"All nonempty detail addresses resolve");
            Check(meshesValid,"Ground meshes have complete finite vertex lattices and 16-bit indices");
            Check(materialsValid,"All ground prefabs have valid materials");
            var player = world.ChunkOrigin(new Vector2Int(9, 8)) + new Vector3(64, 41, 64);
            Check(world.chunks.Count(c => WorldStreamingPlan.DesiredTier(world, c.coordinate, player, Vector3.zero) >= ChunkTier.Visual) <= 9,
                "Mobile full detail remains limited to the nearby 3x3 chunks");
            Check(world.chunks.Count(c => WorldStreamingPlan.DesiredTier(world, c.coordinate, player, Vector3.zero) != ChunkTier.Unloaded) <= 25
                && world.maxConcurrentLoads == 1, "The mobile plan uses a local 5x5 terrain ring and one concurrent load");
            Check(world.regions.Where(r=>!r.paintOnly).All(r => VegetationPainterWindow.Allowed(profile,
                world.ChunkOrigin(r.chunkOffset) + new Vector3(64, 41, 64), Vector3.up, VegetationPaintKind.Tree)),
                "Vegetation brushes work in every region");
            string result = $"{passed.Count} brush world checks passed.\n" + string.Join("\n", passed);
            System.IO.Directory.CreateDirectory("Library/Nusantara"); System.IO.File.WriteAllText("Library/Nusantara/BrushWorldChecks.txt", result);
            return result;
        }
    }
}
