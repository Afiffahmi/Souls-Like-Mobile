using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    [CustomEditor(typeof(RegionChunkAuthoring))]
    public sealed class RegionChunkAuthoringEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var authoring = (RegionChunkAuthoring)target;
            var root = new VisualElement();
            var world = authoring.Definition;
            root.Add(new HelpBox(world == null ? "Missing chunk definition." :
                $"{world.chunks.Length} chunks • {world.chunkSize:0} × {world.chunkSize:0} metres each\nMid-range Android / 60 FPS starting preset. Nearby 3×3 chunks use full detail; a wider 5×5 ring uses ground only. Borders have a small retention buffer.", HelpBoxMessageType.Info));
            root.Add(new HelpBox("Expand Chunks (edit here), then a Chunk. Put land, bridges and building collision in Ground and Structures; decorative objects in Details; enemies and interactables in Gameplay. Keep each object in one chunk. Save Chunk Changes before Play or building the game.", HelpBoxMessageType.None));
            var status = new HelpBox("Ready to edit chunks.", HelpBoxMessageType.None);
            root.Add(new Button(() =>
            {
                try { status.text = SaveChunks(authoring); }
                catch (Exception e) { status.text = e.Message; Debug.LogException(e); }
            }) { text = "Save Chunk Changes" });
            root.Add(new Button(() => { if (authoring.editingRoot != null) { Selection.activeGameObject = authoring.editingRoot; EditorGUIUtility.PingObject(authoring.editingRoot); } }) { text = "Select Chunks for Editing" });
            root.Add(status);
            root.schedule.Execute(() =>
            {
                if (authoring != null && Application.isPlaying && authoring.streamer != null)
                    status.text = $"Loaded chunk positions: {authoring.streamer.ResidentChunkCount} • Loading: {authoring.streamer.PendingLoadCount}";
            }).Every(500);
            return root;
        }

        [MenuItem("Tools/Nusantara/Save Region Chunk Changes")]
        public static void SaveOpenWorld()
        {
            var a = UnityEngine.Object.FindAnyObjectByType<RegionChunkAuthoring>();
            if (a == null) throw new InvalidOperationException("Open the Region 5 rework scene first.");
            Debug.Log(SaveChunks(a));
        }

        public static string SaveChunks(RegionChunkAuthoring authoring)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save chunks outside Play mode.");
            var world = authoring.Definition;
            if (world == null || authoring.editingRoot == null) throw new InvalidOperationException("Missing authoring data.");
            var tiles = authoring.editingRoot.GetComponentsInChildren<RegionChunkTile>(true);
            if (tiles.Length != world.chunks.Length || tiles.Select(t => t.coordinate).Distinct().Count() != tiles.Length)
                throw new InvalidOperationException("Chunk count changed or coordinates are duplicated. Restore the chunk markers before saving.");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Region 5 Authored Chunks") ?? settings.CreateGroup("Region 5 Authored Chunks", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            EditorUtility.SetDirty(bundle);
            Undo.RecordObject(world, "Save region chunks");
            foreach (var tile in tiles)
            {
                var record = world.chunks.First(c => c.coordinate == tile.coordinate);
                if (Vector3.Distance(tile.transform.position, world.ChunkOrigin(tile.coordinate)) > .01f || Quaternion.Angle(tile.transform.rotation, Quaternion.identity) > .01f || Vector3.Distance(tile.transform.lossyScale, Vector3.one) > .001f)
                    throw new InvalidOperationException("Do not move or scale chunk roots. Move their contents instead: " + tile.name);
                string folder = authoring.assetFolder + $"/{tile.coordinate.x:D2}_{tile.coordinate.y:D2}";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(authoring.assetFolder, $"{tile.coordinate.x:D2}_{tile.coordinate.y:D2}");
                var essential = tile.transform.Find("Ground and Structures");
                var ground = essential != null ? essential.Find("Ground") : null;
                if (ground == null || ground.GetComponent<MeshFilter>()?.sharedMesh == null || ground.GetComponent<MeshCollider>()?.sharedMesh == null)
                    throw new InvalidOperationException("Missing ground mesh/collider in " + tile.name);
                foreach (var filter in essential.GetComponentsInChildren<MeshFilter>(true))
                {
                    var path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (!string.IsNullOrEmpty(path)) Register(settings, group, path, AssetDatabase.AssetPathToGUID(path));
                }
                foreach (var material in essential.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                {
                    var path = AssetDatabase.GetAssetPath(material);
                    if (path.StartsWith("Assets/Region5Rework/")) Register(settings, group, path, AssetDatabase.AssetPathToGUID(path));
                }
                string prefix = "region5-authored/" + tile.coordinate.x + "_" + tile.coordinate.y;
                record.essentialAddress = SavePart(settings, group, essential.gameObject, folder + "/Essential.prefab", prefix + "/essential");
                record.decorationAddresses = SaveOptional(settings, group, tile.transform.Find("Details"), folder + "/Details.prefab", prefix + "/details");
                record.gameplayAddresses = SaveOptional(settings, group, tile.transform.Find("Gameplay"), folder + "/Gameplay.prefab", prefix + "/gameplay");
                var distant = new GameObject("Distant Ground");
                try
                {
                    var meshObject = new GameObject("Ground"); meshObject.transform.SetParent(distant.transform, false);
                    meshObject.transform.localPosition = ground.position - tile.transform.position;
                    meshObject.transform.localRotation = ground.rotation; meshObject.transform.localScale = ground.lossyScale;
                    meshObject.AddComponent<MeshFilter>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
                    var renderer = meshObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = ground.GetComponent<MeshRenderer>().sharedMaterials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    record.distantAddress = SavePart(settings, group, distant, folder + "/Distant.prefab", prefix + "/distant");
                }
                finally { UnityEngine.Object.DestroyImmediate(distant); }
            }
            world.playerStart = authoring.streamer.player.position; world.playerStartConfirmed = true;
            var errors = world.ConfigurationErrors(true).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            EditorUtility.SetDirty(world); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
            if (!EditorSceneManager.SaveScene(authoring.gameObject.scene)) throw new InvalidOperationException("Scene was not saved.");
            return $"Saved {tiles.Length} chunks and the scene. Ready for Play.";
        }

        static string[] SaveOptional(AddressableAssetSettings settings, AddressableAssetGroup group, Transform part, string path, string address)
        {
            if (part == null || part.childCount == 0) return Array.Empty<string>();
            return new[] { SavePart(settings, group, part.gameObject, path, address) };
        }
        static string SavePart(AddressableAssetSettings settings, AddressableAssetGroup group, GameObject source, string path, string address)
        {
            // Authoring roots are at their world position; runtime roots supply that position.
            var copy = UnityEngine.Object.Instantiate(source); copy.name = source.name;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = Vector3.one;
            copy.tag = "Untagged"; copy.SetActive(true);
            try { PrefabUtility.SaveAsPrefabAsset(copy, path); }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            Register(settings, group, path, address); return address;
        }
        static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Cannot register " + path);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false); entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, false, true);
        }
    }

    [CustomEditor(typeof(RegionChunkTile))]
    public sealed class RegionChunkTileEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var tile = (RegionChunkTile)target; var root = new VisualElement();
            root.Add(new HelpBox($"Chunk {tile.coordinate.x + 1}, {tile.coordinate.y + 1} • 128 × 128 metres\nBuild inside this chunk's child folders. Keep the chunk root at its original position and scale.", HelpBoxMessageType.Info));
            root.Add(new Button(RegionChunkAuthoringEditor.SaveOpenWorld) { text = "Save Chunk Changes" }); return root;
        }
    }
}
