using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldFoundationSetup
    {
        public const string Root = "Assets/NusantaraWorld";
        public const string DefinitionPath = Root + "/NusantaraWorld.asset";
        const string PropsPath = WorldAssetLayout.Props;

        [MenuItem("Tools/Nusantara/Create Foundation Assets")]
        public static string Create()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(DefinitionPath);
            if (world == null)
            {
                world = ScriptableObject.CreateInstance<WorldDefinition>();
                world.regions = new[] {
                    Region(1, "Naga Seri Gumum", "Naga Seri Gumum"), Region(2, "Harimau Penjaga Johor", "Harimau Penjaga Johor"),
                    Region(3, "Sang Kelembai", "Sang Kelembai"), Region(4, "Batu Belah Batu Bertangkup", "Batu Belah Batu Bertangkup"),
                    Region(5, "Bujang Senang", "Bujang Senang"), Region(6, "Raja Bersiong", "Raja Bersiong") };
                string[] names = { "Kampung Sungai", "Hutan Rimba", "Delta Sungai", "Rawa Berkabus", "Air Terjun Suci", "Runtuhan Lama", "Gua Sunyi", "Pulau Buaya", "Sarang Bujang Senang" };
                world.locations = names.Select((name, i) => new WorldLocation { id = "region5-" + (char)('a' + i), name = name, region = 5, clearRadius = i == 8 ? 80 : 20 }).ToArray();
                AssetDatabase.CreateAsset(world, DefinitionPath);
            }
            string catalogPath = Root + "/Region5PropCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<WorldPropCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldPropCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                var entries = new List<WorldPropEntry>();
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PropsPath }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                    var renderers = model.GetComponentsInChildren<Renderer>(true);
                    var bounds = new Bounds(); bool first = true;
                    foreach (var renderer in renderers) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                    entries.Add(new WorldPropEntry {
                        name = model.name, assetGuid = guid, assetPath = path, importedSize = bounds.size,
                        vertices = meshes.Sum(m => m.vertexCount), triangles = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (int)(m.GetIndexCount(s) / 3))),
                        materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
                        lodGroups = model.GetComponentsInChildren<LODGroup>(true).Length,
                        colliders = model.GetComponentsInChildren<Collider>(true).Length,
                        authoringNotes = "Imported source model. Scale, material assignment, LODs and walking collision need review before placement. No production-ready claim." });
                }
                catalog.entries = entries.ToArray();
                EditorUtility.SetDirty(catalog);
            }
            string prefabPath = Root + "/WorldBootstrap.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var root = new GameObject("World");
                root.SetActive(false);
                root.AddComponent<WorldChunkStreamer>().definition = world;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
            }
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            const string groupName = "Nusantara World Chunks";
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var bundle = group.GetSchema<BundledAssetGroupSchema>();
                bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                EditorUtility.SetDirty(bundle);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = world;
            return $"World assets ready: {world.regions.Length} regions, {world.locations.Length} locations, {world.chunks.Length} chunks and {catalog.entries.Length} source models. Reference layout ready: {world.layoutApproved}.";
        }
        static WorldRegion Region(int id, string name, string boss) => new WorldRegion { id = id, name = name, boss = boss, detailedEnvironment = id == 5 };
    }
}
