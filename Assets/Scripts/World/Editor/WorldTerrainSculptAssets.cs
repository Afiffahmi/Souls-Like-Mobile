using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    [InitializeOnLoad]
    public static class WorldTerrainSculptAssets
    {
        const string Root = "Assets/NusantaraWorld/TerrainSculpt";
        const string DataPath = Root + "/SculptData.asset";
        static readonly Dictionary<Vector2Int, Mesh> groundMeshes = new Dictionary<Vector2Int, Mesh>();
        static readonly Dictionary<Vector2Int, Vector3[]> groundVertices = new Dictionary<Vector2Int, Vector3[]>();
        static readonly HashSet<Vector2Int> sessionChanged = new HashSet<Vector2Int>();
        static WorldGeographyProfile sessionProfile;

        static WorldTerrainSculptAssets()
        {
            Undo.undoRedoPerformed += AfterUndo;
            EditorApplication.playModeStateChanged += BeforePlay;
        }
        public static string Folder(Vector2Int coordinate) => $"{WorldReferenceBuilder.Root}/Chunks/{coordinate.x:D2}_{coordinate.y:D2}";
        public static void InvalidateCache() { groundMeshes.Clear(); groundVertices.Clear(); }
        public static WorldTerrainSculptData Prepare(WorldGeographyProfile profile)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Terrain painting requires Edit mode.");
            if (profile == null || profile.world == null) throw new InvalidOperationException("Assign a world geography profile.");
            if (profile.sculptData != null) return profile.sculptData;
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/NusantaraWorld", "TerrainSculpt");
            var data = AssetDatabase.LoadAssetAtPath<WorldTerrainSculptData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WorldTerrainSculptData>();
                data.origin = profile.world.origin;
                data.spacing = profile.world.chunkSize / Mathf.Max(1, profile.terrainIntervals);
                AssetDatabase.CreateAsset(data, DataPath);
            }
            if (data.origin != profile.world.origin) throw new InvalidOperationException("Sculpt data belongs to a different world origin.");
            Undo.RecordObject(profile, "Set up terrain brushes");
            profile.sculptData = data; EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }
        public static bool TryCoordinate(Mesh mesh, out Vector2Int coordinate)
        {
            coordinate = default;
            if (mesh == null) return false;
            string path = AssetDatabase.GetAssetPath(mesh).Replace('\\', '/');
            string prefix = WorldReferenceBuilder.Root + "/Chunks/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith("/Terrain.asset", StringComparison.Ordinal)) return false;
            string name = path.Substring(prefix.Length, path.Length - prefix.Length - "/Terrain.asset".Length);
            var parts = name.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) return false;
            coordinate = new Vector2Int(x, z); return true;
        }
        public static float SampleGround(WorldGeographyProfile profile, float x, float z)
        {
            var world = profile.world;
            var coordinate = world.WorldToChunk(new Vector3(x, 0, z));
            if (!world.Contains(coordinate)) return profile.Height(x, z);
            if (!groundMeshes.TryGetValue(coordinate, out var mesh))
            {
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(coordinate) + "/Terrain.asset");
                groundMeshes[coordinate] = mesh;
            }
            if (mesh == null) return profile.Height(x, z);
            if (!groundVertices.TryGetValue(coordinate, out var vertices)) { vertices = mesh.vertices; groundVertices[coordinate] = vertices; }
            int side = Mathf.RoundToInt(Mathf.Sqrt(vertices.Length)), intervals = side - 1;
            if (intervals < 1 || side * side != vertices.Length) return profile.Height(x, z);
            var origin = world.ChunkOrigin(coordinate);
            float gx = Mathf.Clamp((x - origin.x) / world.chunkSize * intervals, 0, intervals);
            float gz = Mathf.Clamp((z - origin.z) / world.chunkSize * intervals, 0, intervals);
            int ix = Mathf.Min(Mathf.FloorToInt(gx), intervals - 1), iz = Mathf.Min(Mathf.FloorToInt(gz), intervals - 1);
            float u = gx - ix, v = gz - iz; int n = iz * side + ix;
            return u + v <= 1 ? vertices[n].y + (vertices[n + 1].y - vertices[n].y) * u + (vertices[n + side].y - vertices[n].y) * v
                : vertices[n + side + 1].y + (vertices[n + side].y - vertices[n + side + 1].y) * (1 - u) + (vertices[n + 1].y - vertices[n + side + 1].y) * (1 - v);
        }
        // The existing attributes and skirt are retained. The same function also serves transient verification fixtures.
        public static bool ApplyMeshDelta(Mesh mesh, Vector3 origin, Func<float, float, float> delta, Action beforeChange = null)
        {
            if (mesh == null) return false;
            var vertices = mesh.vertices; var normals = mesh.normals; bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                float x = origin.x + vertices[i].x, z = origin.z + vertices[i].z;
                float dy = delta(x, z);
                float dx = delta(x + .5f, z) - delta(x - .5f, z), dz = delta(x, z + .5f) - delta(x, z - .5f);
                if (Mathf.Abs(dy) < .0000001f && Mathf.Abs(dx) < .0000001f && Mathf.Abs(dz) < .0000001f) continue;
                if (!float.IsFinite(dy) || !float.IsFinite(dx) || !float.IsFinite(dz)) throw new InvalidOperationException("Brush generated an invalid height.");
                if (!changed) { beforeChange?.Invoke(); changed = true; }
                vertices[i].y += dy;
                if (i < normals.Length && normals[i].y > .001f)
                    normals[i] = new Vector3(normals[i].x / normals[i].y - dx, 1, normals[i].z / normals[i].y - dz).normalized;
            }
            if (!changed) return false;
            mesh.vertices = vertices; if (normals.Length == vertices.Length) mesh.normals = normals;
            mesh.RecalculateBounds(); return true;
        }
        public static void ApplyDelta(WorldGeographyProfile profile, Rect bounds, Func<float, float, float> delta, ISet<Mesh> recordedMeshes, ISet<Vector2Int> changedChunks)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Terrain painting requires Edit mode.");
            if (profile == null || profile.world == null || profile.sculptData == null) throw new InvalidOperationException("Set up the brush first.");
            sessionProfile = profile;
            var world = profile.world;
            int minX = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - .51f - world.origin.x) / world.chunkSize));
            int maxX = Mathf.Min(world.gridSize.x - 1, Mathf.FloorToInt((bounds.xMax + .51f - world.origin.x) / world.chunkSize));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - .51f - world.origin.y) / world.chunkSize));
            int maxZ = Mathf.Min(world.gridSize.y - 1, Mathf.FloorToInt((bounds.yMax + .51f - world.origin.y) / world.chunkSize));
            var changedMeshes = new HashSet<Mesh>();
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                var coordinate = new Vector2Int(x, z); bool chunkChanged = false;
                foreach (string file in new[] { "Terrain.asset", "DistantTerrain.asset" })
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(coordinate) + "/" + file);
                    if (ApplyMeshDelta(mesh, world.ChunkOrigin(coordinate), delta, () =>
                    {
                        if (recordedMeshes.Add(mesh)) Undo.RegisterCompleteObjectUndo(mesh, "Sculpt terrain");
                    }))
                    { EditorUtility.SetDirty(mesh); changedMeshes.Add(mesh); chunkChanged = true; }
                }
                if (!chunkChanged) continue;
                changedChunks.Add(coordinate); sessionChanged.Add(coordinate);
                if (!profile.sculptData.pendingChunks.Contains(coordinate)) profile.sculptData.pendingChunks.Add(coordinate);
            }
            EditorUtility.SetDirty(profile.sculptData);
            InvalidateCache(); RefreshColliders(changedMeshes);
        }
        public static void RefreshColliders() => RefreshColliders(null);
        static void RefreshColliders(ISet<Mesh> meshes)
        {
            foreach (var collider in Resources.FindObjectsOfTypeAll<MeshCollider>())
            {
                if (collider == null || !collider.gameObject.scene.IsValid() || EditorUtility.IsPersistent(collider) || collider.sharedMesh == null) continue;
                if (meshes != null ? !meshes.Contains(collider.sharedMesh) : !TryCoordinate(collider.sharedMesh, out _)) continue;
                Mesh mesh = collider.sharedMesh; collider.sharedMesh = null; collider.sharedMesh = mesh;
            }
            Physics.SyncTransforms();
        }
        static void AfterUndo()
        {
            InvalidateCache(); RefreshColliders();
            if (sessionProfile == null || sessionProfile.sculptData == null || sessionChanged.Count == 0) return;
            var data = sessionProfile.sculptData; data.InvalidateCache();
            foreach (var coordinate in sessionChanged)
            {
                if (!data.pendingChunks.Contains(coordinate)) data.pendingChunks.Add(coordinate);
                foreach (string file in new[] { "Terrain.asset", "DistantTerrain.asset" })
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder(coordinate) + "/" + file);
                    if (mesh != null) { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
                }
            }
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        }
        static void BeforePlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var profile = AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            if (profile == null || profile.sculptData == null || profile.sculptData.pendingChunks.Count == 0) return;
            try { Debug.Log(Save(profile)); }
            catch (Exception error) { EditorApplication.isPlaying = false; Debug.LogException(error); }
        }
        public static string Save(WorldGeographyProfile profile)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before saving terrain.");
            if (profile == null || profile.world == null || profile.sculptData == null) return "Enable the brush to set up terrain painting.";
            var data = profile.sculptData; var world = profile.world;
            if (data.pendingChunks.Count == 0) { AssetDatabase.SaveAssetIfDirty(data); return "Terrain is saved. Use the AI brush to rebuild walking ground after sculpting."; }
            var queue = new HashSet<Vector2Int>(data.pendingChunks);
            EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            string backup = "Library/Nusantara/TerrainSculptBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            int count = 0;
            try
            {
                foreach (var coordinate in queue.OrderBy(c => c.y).ThenBy(c => c.x))
                {
                    string folder = Folder(coordinate), path = folder + "/Essential.prefab";
                    foreach (string file in new[] { "Terrain.asset", "DistantTerrain.asset" })
                    {
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/" + file);
                        if (mesh != null) AssetDatabase.SaveAssetIfDirty(mesh);
                    }
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing terrain chunk prefab: " + path);
                    EditorUtility.DisplayProgressBar("Saving sculpted terrain", "Saving ground " + coordinate, (float)count / queue.Count);
                    VegetationPaintAssets.Backup(path, backup);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                        { var mesh = collider.sharedMesh; if (mesh != null) { collider.sharedMesh = null; collider.sharedMesh = mesh; } }
                        PrefabUtility.SaveAsPrefabAsset(root, path);

                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    count++;
                }
                data.pendingChunks.Clear(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                RefreshColliders();
                return $"Saved terrain for {count} chunks. Use Build AI Walking Ground after sculpting. Undo remains available.";
            }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
