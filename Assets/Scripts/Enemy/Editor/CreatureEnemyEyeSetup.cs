using System;
using System.Linq;
using ElementalGems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoulsLike.Enemies.Editor
{
    public static class CreatureEnemyEyeSetup
    {
        static Vector3[] Positions(string species)
        {
            if (species == "Wolf") return new[] {new Vector3(-.036f,.414f,.451f),new Vector3(.036f,.414f,.451f)};
            if (species == "Spider") return new[] {
                new Vector3(-.0265f,.293f,.146f),new Vector3(.0265f,.293f,.146f),
                new Vector3(-.046f,.308f,.112f),new Vector3(.046f,.308f,.112f),
                new Vector3(-.008f,.273f,.168f),new Vector3(.008f,.273f,.168f),
                new Vector3(-.025f,.273f,.165f),new Vector3(.025f,.273f,.165f)};
            var head = new Vector3(.0322f,.451f,.3437f); var forward = new Vector3(.16f,.08f,.984f).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            return new[] {head+forward*.07f+Vector3.up*.016f-right*.035f,head+forward*.07f+Vector3.up*.016f+right*.035f};
        }
        public static Material EnsureRibbonMaterial()
        {
            const string path = "Assets/Materials/ElementalGems/Enemy Eye Energy.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("ElementalGems/Enemy Eye Energy");
            if (shader == null) throw new InvalidOperationException("Import the enemy eye energy shader first.");
            if (material == null) { material = new Material(shader) {name = "Enemy Eye Energy"}; AssetDatabase.CreateAsset(material, path); }
            else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
            AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        public static void Configure(GameObject root, string species)
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/OneHand/Enemy1OneHand.prefab").GetComponentInChildren<EnemyEyeParticles>(true);
            if (template == null || template.arrowVisualsSource == null) throw new InvalidOperationException("The one-hand enemy must have its eye VFX source configured.");
            var visual = root.transform.Find("Visual");
            var head = visual.GetComponentsInChildren<Transform>().First(t => t.name == (species == "Wolf" ? "Head" : "head"));
            var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Enemy/Shared/{species}/{species.ToLower()}_rigged.fbx"));
            var mesh = new Mesh();
            try
            {
                // Raycast the rest-pose skin to put each emitter just outside the actual eye surface.
                var skin = source.GetComponentInChildren<SkinnedMeshRenderer>(); skin.BakeMesh(mesh);
                var surface = new GameObject("Temporary eye surface"); surface.transform.SetParent(source.transform, false);
                surface.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                var collider = surface.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                Physics.SyncTransforms();
                var positions = Positions(species); var markers = new Transform[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 outward = species == "Snake" ? new Vector3(i == 0 ? -.85f : .85f, .05f, .5f).normalized : Vector3.forward;
                    var point = positions[i]; var normal = outward;
                    if (collider.Raycast(new Ray(point + outward * .3f, -outward), out var hit, .6f))
                    { point = hit.point + hit.normal * .0015f; normal = hit.normal; }
                    string name = "Trail_Particle_Eye_" + (i + 1);
                    var marker = head.Find(name);
                    if (marker == null) { marker = new GameObject(name).transform; marker.SetParent(head, false); }
                    marker.position = visual.TransformPoint(point);
                    marker.rotation = Quaternion.LookRotation(visual.TransformDirection(normal), visual.up);
                    marker.localScale = Vector3.one; marker.gameObject.layer = root.layer;
                    markers[i] = marker;
                }
                var eyes = root.GetComponent<EnemyEyeParticles>(); if (eyes == null) eyes = root.AddComponent<EnemyEyeParticles>();
                eyes.arrowVisualsSource = template.arrowVisualsSource; eyes.eyeMarkers = markers;
                eyes.useEyeBonesWhenMarkersMissing = false;
                eyes.eyeSize = species == "Spider" ? .13f : .2f;
                eyes.chargedParticleTrails = true; eyes.ribbonMaterial = EnsureRibbonMaterial();
                eyes.tailLength = species == "Spider" ? .95f : 1.2f;
                eyes.tailWidth = species == "Spider" ? .023f : .035f; eyes.brightness = 3; eyes.tailFadeSeconds = .25f;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        static void TuneOneHand(EnemyEyeParticles eyes, Material material)
        {
            eyes.ribbonMaterial = material; eyes.chargedParticleTrails = true; eyes.eyeSize = .25f;
            eyes.tailLength = 1.5f; eyes.tailWidth = .045f; eyes.brightness = 3; eyes.tailFadeSeconds = .25f;
        }
        [MenuItem("Tools/Enemy AI/Configure Creature Eye VFX")]
        public static string ConfigureAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Configure eyes in Edit mode.");
            var material = EnsureRibbonMaterial();
            const string oneHandPath = "Assets/Prefabs/Enemy/OneHand/Enemy1OneHand.prefab";
            var oneHand = PrefabUtility.LoadPrefabContents(oneHandPath);
            try
            {
                foreach (var eyes in oneHand.GetComponentsInChildren<EnemyEyeParticles>(true)) TuneOneHand(eyes, material);
                PrefabUtility.SaveAsPrefabAsset(oneHand, oneHandPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(oneHand); }
            foreach (var eyes in UnityEngine.Object.FindObjectsByType<EnemyEyeParticles>(UnityEngine.FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!eyes.gameObject.scene.IsValid()) continue;
                Undo.RecordObject(eyes, "Tune enemy movement eye trails");
                var brain = eyes.GetComponent<EnemyBrain>();
                string species = brain != null && brain.profile != null ? CreatureEnemySetup.Species.FirstOrDefault(s => AssetDatabase.GetAssetPath(brain.profile) == CreatureEnemySetup.ProfilePath(s)) : null;
                if (species == null) TuneOneHand(eyes, material);
                else
                {
                    eyes.ribbonMaterial = material; eyes.chargedParticleTrails = true; eyes.eyeSize = species == "Spider" ? .13f : .2f;
                    eyes.tailLength = species == "Spider" ? .95f : 1.2f; eyes.tailWidth = species == "Spider" ? .023f : .035f; eyes.brightness = 3; eyes.tailFadeSeconds = .25f;
                }
                EditorUtility.SetDirty(eyes);
                if (PrefabUtility.IsPartOfPrefabInstance(eyes)) PrefabUtility.RecordPrefabInstancePropertyModifications(eyes);
                EditorSceneManager.MarkSceneDirty(eyes.gameObject.scene);
            }
            foreach (string species in CreatureEnemySetup.Species)
            {
                string path = $"Assets/Prefabs/Enemy/Shared/{species}_Base.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root, species); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return "Eye VFX configured on all 3 bases and inherited by their 21 elemental variants.";
        }
    }
}
