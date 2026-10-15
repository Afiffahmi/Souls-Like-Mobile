using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    public static class WorldFoundationChecks
    {
        [MenuItem("Tools/Nusantara/Run Foundation Checks")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var passed = new List<string>();
            var world = ScriptableObject.CreateInstance<WorldDefinition>();
            var meshes = new List<Mesh>();
            string temporary = Path.Combine(Path.GetTempPath(), "NusantaraChecks-" + Guid.NewGuid().ToString("N"));
            Scene fixture = default;
            void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); passed.Add(name); }
            try
            {
                world.origin = Vector2.zero;
                Check(world.WorldToChunk(new Vector3(-.1f, 0, -256.1f)) == new Vector2Int(-1, -2), "Negative world coordinates use floor division");
                Check(world.WorldToChunk(new Vector3(256, 0, 256)) == new Vector2Int(1, 1), "Exact chunk boundary belongs to next chunk");
                Check(!world.Contains(new Vector2Int(48, 1)), "World limits reject out-of-bounds chunks");
                var player = new Vector3(10.5f * 256, 0, 10.5f * 256);
                Check(WorldStreamingPlan.DesiredTier(world, new Vector2Int(11, 10), player, Vector3.zero) == ChunkTier.Gameplay, "Neighbouring gameplay chunks preload before entry");
                Check(WorldStreamingPlan.DesiredTier(world, new Vector2Int(13, 10), player, Vector3.zero) == ChunkTier.Visual, "Visual ring loads beyond gameplay");
                Check(WorldStreamingPlan.DesiredTier(world, new Vector2Int(15, 10), player, Vector3.zero) == ChunkTier.Distant, "Far ring uses distant terrain");
                Check(WorldStreamingPlan.RetainedTier(world, new Vector2Int(16, 10), ChunkTier.Distant, player, Vector3.zero) == ChunkTier.Distant, "Unload hysteresis retains boundary chunks");
                Check(WorldStreamingPlan.RetainedTier(world, new Vector2Int(17, 10), ChunkTier.Distant, player, Vector3.zero) == ChunkTier.Unloaded, "Chunks beyond retention radius unload");
                Check(WorldStreamingPlan.RetainedTier(world, new Vector2Int(14, 10), ChunkTier.Visual, player, Vector3.zero) == ChunkTier.Visual, "Visual detail has its own hysteresis band");
                world.visualRadius = 2; world.maxLookAheadMetres = 768;
                Check(WorldStreamingPlan.DesiredTier(world, new Vector2Int(14, 10), player, Vector3.right * 1000) == ChunkTier.Visual, "Movement prediction preloads ahead and clamps lookahead");
                world.visualRadius = 3; world.maxLookAheadMetres = 256;
                var candidates = new HashSet<Vector2Int>();
                WorldStreamingPlan.GetCandidates(world, Vector3.zero, candidates);
                Check(candidates.All(world.Contains) && candidates.Count < world.gridSize.x * world.gridSize.y, "Candidate planning stays local and inside the grid");

                float Height(float x, float z) => Mathf.Sin(x / 100) * 3 + Mathf.Cos(z / 150) * 2;
                var left = WorldTerrainMesher.Build(world, new Vector2Int(8, 7), 64, Height); meshes.Add(left);
                var right = WorldTerrainMesher.Build(world, new Vector2Int(9, 7), 64, Height); meshes.Add(right);
                bool seamsMatch = true, normalsMatch = true;
                var lv = left.vertices; var rv = right.vertices; var ln = left.normals; var rn = right.normals;
                for (int z = 0; z <= 64; z++)
                {
                    seamsMatch &= Mathf.Abs(lv[z * 65 + 64].y - rv[z * 65].y) <= .000001f;
                    normalsMatch &= Vector3.Distance(ln[z * 65 + 64], rn[z * 65]) <= .000001f;
                }
                Check(seamsMatch, "Adjacent terrain edges share exact height samples");
                Check(normalsMatch, "Adjacent terrain edge normals match");
                Check(left.triangles.Length == 64 * 64 * 6 && left.normals.All(n => n.y > .99f), "Terrain topology faces upward");
                world.chunkSize = 200.3f;
                Check(WorldTerrainMesher.SamplePosition(world, new Vector2Int(20, 15), 64, 32, 64) == WorldTerrainMesher.SamplePosition(world, new Vector2Int(21, 15), 0, 32, 64), "Non-power-of-two chunk sizes share the same world lattice");

                var store = new WorldStateStore("test-world");
                string first = WorldStateStore.Key(new Vector2Int(1, 2), "chest-a");
                string second = WorldStateStore.Key(new Vector2Int(2, 2), "chest-a");
                store.Set(first, "{\"opened\":true}"); store.Set(second, "{\"opened\":false}");
                var loaded = new WorldStateStore("test-world"); loaded.LoadJson(store.ToJson());
                Check(loaded.TryGet(first, out var value) && value.Contains("true") && loaded.Count == 2, "Persistent state survives a serialized unload/reload round trip");
                int revision = loaded.Revision; loaded.Set(first, value);
                Check(loaded.Revision == revision, "Unchanged state does not schedule another save");
                bool rejected = false;
                try { loaded.LoadJson("{\"version\":9,\"worldId\":\"test-world\",\"entries\":[]}"); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && loaded.Count == 2, "Unsupported saves are rejected without erasing memory state");
                rejected = false;
                try { loaded.LoadJson(new WorldStateStore("different-world").ToJson()); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Save files are scoped to their world ID");
                string path = Path.Combine(temporary, "state.json");
                WorldStateStore.WriteAtomic(path, store.ToJson()); store.Set(first, "{\"opened\":false}"); WorldStateStore.WriteAtomic(path, store.ToJson());
                Check(File.Exists(path + ".bak") && !File.Exists(path + ".tmp"), "Atomic save replacement retains the previous backup");

                fixture = EditorSceneManager.NewPreviewScene();
                GameObject Make(string name)
                {
                    var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, fixture); return go;
                }
                var ground = Make("Validation Ground");
                var floor = ground.AddComponent<BoxCollider>(); floor.size = new Vector3(12, .5f, 12); floor.center = new Vector3(0, -.25f, 0);
                var area = Make("Validation Area").AddComponent<WorldWalkabilityArea>(); area.size = new Vector3(8, 8, 8); area.sampleSpacing = 1;
                var loot = Make("Collected Loot").AddComponent<WorldPersistentObject>();
                loot.AssignId("collected-loot"); loot.BindState(loaded, "loot-test"); loot.SetState("{\"collected\":true}");
                UnityEngine.Object.DestroyImmediate(loot.gameObject);
                Check(loaded.TryGet("loot-test", out var lootState) && lootState.Contains("true"), "Collected object writes state before being destroyed");
                area.entry = Make("Entry").transform; area.entry.position = new Vector3(-3, 0, -3);
                var destination = Make("Destination").transform; destination.position = new Vector3(3, 0, 3); area.requiredDestinations = new[] { destination };
                WalkabilityReport Validate()
                {
                    Physics.SyncTransforms();
                    var result = new WalkabilityReport(); var iterator = WorldWalkabilityValidator.Validate(world, area, result);
                    while (iterator.MoveNext()) { }
                    return result;
                }
                var flat = Validate();
                Check(flat.finished && flat.issueCount == 0, "Flat ground and connected walking routes pass physics validation");
                var wallObject = Make("Blocking Wall"); var wall = wallObject.AddComponent<BoxCollider>(); wall.size = new Vector3(.4f, 3, 10); wall.center = new Vector3(0, 1.5f, 0);
                var blocked = Validate();
                Check(blocked.issues.Any(i => i.category == "Unreachable destination"), "Full-width obstacle disconnects required destination");
                UnityEngine.Object.DestroyImmediate(wallObject);
                ground.transform.rotation = Quaternion.Euler(0, 0, 25);
                Check(Validate().issues.Any(i => i.category == "Slope"), "25-degree terrain fails the 15-degree slope limit");
                ground.transform.rotation = Quaternion.identity;
                floor.size = new Vector3(5.8f, .5f, 12); floor.center = new Vector3(-3.1f, -.25f, 0);
                var bank = Make("Opposite Bank"); var bankCollider = bank.AddComponent<BoxCollider>(); bankCollider.size = new Vector3(5.8f, .5f, 12); bankCollider.center = new Vector3(3.1f, -.25f, 0);
                var gap = Validate();
                Check(gap.issues.Any(i => i.category == "Missing ground") && gap.issues.Any(i => i.category == "Unreachable destination"), "River gap fails connectivity without assuming jumping or swimming");
                floor.size = new Vector3(6, .5f, 12); floor.center = new Vector3(-3, -.25f, 0);
                bankCollider.size = new Vector3(6, .5f, 12); bankCollider.center = new Vector3(3, .55f, 0);
                var step = Validate();
                Check(step.issues.Any(i => i.category == "Broken connection") && step.issues.Any(i => i.category == "Unreachable destination"), "Abrupt 0.8 m step fails walking connectivity");

                string resultText = $"{passed.Count} foundation checks passed.\n" + string.Join("\n", passed);
                Directory.CreateDirectory("Library/Nusantara");
                File.WriteAllText("Library/Nusantara/FoundationChecks.txt", resultText);
                return resultText;
            }
            finally
            {
                foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(world);
                if (fixture.IsValid()) EditorSceneManager.ClosePreviewScene(fixture);
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }
    }
}
