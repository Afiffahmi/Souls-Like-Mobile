using System;
using System.Collections.Generic;
using System.Linq;
using ElementalGems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies.Editor
{
    /// <summary>Builds combat-ready creature prefabs without modifying the source FBX or source clips.</summary>
    public static class CreatureEnemySetup
    {
        public static readonly string[] Species = { "Wolf", "Spider", "Snake" };
        public static string ProfilePath(string species) => $"Assets/Data/Enemy/{species}/{species}.asset";
        static T Require<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing asset: " + path);
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/'); Folder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
        static T Get<T>(GameObject root) where T : Component
        { var component = root.GetComponent<T>(); return component != null ? component : root.AddComponent<T>(); }
        static AnimationCurve Read(Dictionary<string, AnimationCurve> curves, string path, string property)
            => curves.TryGetValue(path + ":" + property, out var curve) ? curve : null;
        static Dictionary<string, AnimationCurve> Curves(AnimationClip clip)
            => AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).ToDictionary(
                b => b.path + ":" + b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b));
        static Quaternion Rotation(Dictionary<string, AnimationCurve> curves, string path, float time, Quaternion fallback)
        {
            var q = fallback;
            for (int a = 0; a < 4; a++) { var c = Read(curves, path, "m_LocalRotation." + "xyzw"[a]); if (c != null) q[a] = c.Evaluate(time); }
            return q.normalized;
        }
        static Vector3 Position(Dictionary<string, AnimationCurve> curves, string path, float time, Vector3 fallback)
        {
            var p = fallback;
            for (int a = 0; a < 3; a++) { var c = Read(curves, path, "m_LocalPosition." + "xyz"[a]); if (c != null) p[a] = c.Evaluate(time); }
            return p;
        }
        static Quaternion SourceParentRotation(Dictionary<string, AnimationCurve> reference, string path, float time)
        {
            var parts = path.Split('/'); var q = Quaternion.identity; string parent = "";
            for (int i = 0; i < parts.Length - 1; i++)
            { parent += (i == 0 ? "" : "/") + parts[i]; q *= Rotation(reference, parent, time, Quaternion.identity); }
            return q;
        }
        static void Ground(AnimationClip clip, GameObject model, float[] times)
        {
            var go = UnityEngine.Object.Instantiate(model);
            var mesh = new Mesh();
            try
            {
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var anchor = go.transform.Find(model.name.StartsWith("wolf", StringComparison.OrdinalIgnoreCase) ? "Wolf_Rig/root/Hips" :
                    model.name.StartsWith("spider", StringComparison.OrdinalIgnoreCase) ? "Spider_Rig/root/hips" : "Snake_Rig/root/head");
                string path = AnimationUtility.CalculateTransformPath(anchor, go.transform);
                skin.BakeMesh(mesh);
                var initialScale = skin.transform.lossyScale;
                var original = mesh.vertices.Select(v => skin.transform.TransformPoint(new Vector3(v.x / initialScale.x, v.y / initialScale.y, v.z / initialScale.z))).ToArray();
                float floor = original.Min(v => v.y);
                var originalCenter = (new Vector3(original.Min(v => v.x), floor, original.Min(v => v.z)) +
                    new Vector3(original.Max(v => v.x), original.Max(v => v.y), original.Max(v => v.z))) * .5f;
                var positions = new Vector3[times.Length];
                for (int i = 0; i < times.Length; i++)
                {
                    clip.SampleAnimation(go, times[i]); skin.BakeMesh(mesh);
                    var scale = skin.transform.lossyScale;
                    var vertices = mesh.vertices.Select(v => skin.transform.TransformPoint(new Vector3(v.x / scale.x, v.y / scale.y, v.z / scale.z))).ToArray();
                    float min = vertices.Min(v => v.y);
                    var center = new Vector3((vertices.Min(v => v.x) + vertices.Max(v => v.x)) * .5f, 0,
                        (vertices.Min(v => v.z) + vertices.Max(v => v.z)) * .5f);
                    // Navigation owns travel. Keep the deformed body aligned with the root and collider.
                    anchor.position += new Vector3(originalCenter.x - center.x, floor - min, originalCenter.z - center.z);
                    positions[i] = anchor.localPosition;
                }
                for (int a = 0; a < 3; a++) Bind(clip, path, "m_LocalPosition." + "xyz"[a], times, positions.Select(v => v[a]).ToArray());
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(go); }
        }
        static void Bind(AnimationClip clip, string path, string property, float[] times, float[] values)
        {
            bool constant = values.All(v => Mathf.Abs(v - values[0]) < .0000001f);
            var keys = constant ? new[] {new Keyframe(0, values[0]), new Keyframe(times[times.Length-1], values[0])}
                : times.Select((t, i) => new Keyframe(t, values[i])).ToArray();
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }
        static AnimationClip PrepareClip(string species, string action, GameObject model, AnimationClip reference)
        {
            string sourceName = action == "Run" && species == "Spider" ? "Walk" :
                action == "Attack" && species != "Wolf" ? "Bite" : action;
            var source = Require<AnimationClip>($"Assets/Prefabs/Enemy/{species}_{sourceName}.anim");
            string path = $"Assets/Animation/Enemy/{species}/{species}_{action}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            else Undo.RecordObject(clip, "Configure creature animation");
            clip.ClearCurves(); clip.name = species + "_" + action; clip.frameRate = 30; clip.legacy = false;
            bool native = AnimationUtility.GetCurveBindings(source).All(b => model.transform.Find(b.path) != null);
            if (native) EditorUtility.CopySerialized(source, clip);
            else
            {
                // The extracted clips use a different imported rest pose and omit the Rig parent.
                // Preserve the authored motion as local deltas from their own idle reference.
                var curves = Curves(source); var neutral = Curves(reference);
                float start = curves.Values.Min(c => c.keys[0].time);
                float refTime = neutral.Values.Min(c => c.keys[0].time);
                float duration = source.length - start;
                int frames = Mathf.Max(2, Mathf.CeilToInt(duration * 30));
                var times = Enumerable.Range(0, frames + 1).Select(i => duration * i / frames).ToArray();
                var rig = model.transform.Find(species + "_Rig");
                foreach (var bone in rig.GetComponentsInChildren<Transform>().Where(b => b != rig))
                {
                    string sourcePath = AnimationUtility.CalculateTransformPath(bone, rig);
                    string targetPath = AnimationUtility.CalculateTransformPath(bone, model.transform);
                    Quaternion bindQ = bone.localRotation;
                    Vector3 bindP = bone.localPosition;
                    var referenceQ = Rotation(neutral, sourcePath, refTime, bindQ);
                    var referenceP = Position(curves, sourcePath, start, bindP);
                    var endP = Position(curves, sourcePath, source.length, bindP);
                    var positionBasis = Quaternion.Inverse(bone.parent.rotation) * SourceParentRotation(neutral, sourcePath, refTime);
                    var positions = new Vector3[times.Length]; var rotations = new Quaternion[times.Length];
                    for (int i = 0; i < times.Length; i++)
                    {
                        bool root = bone == rig.Find("root");
                        rotations[i] = root ? bindQ : bindQ * Quaternion.Inverse(referenceQ) * Rotation(curves, sourcePath, start + times[i], referenceQ);
                        var delta = Position(curves, sourcePath, start + times[i], referenceP) - referenceP;
                        if (action == "Run") delta -= (endP - referenceP) * (times[i] / duration);
                        positions[i] = root ? bindP : bindP + positionBasis * delta;
                        if (i > 0 && Quaternion.Dot(rotations[i - 1], rotations[i]) < 0)
                        { var q = rotations[i]; rotations[i] = new Quaternion(-q.x, -q.y, -q.z, -q.w); }
                    }
                    if (action == "Idle" || action == "Run")
                    { positions[positions.Length-1] = positions[0]; rotations[rotations.Length-1] = rotations[0]; }
                    for (int a = 0; a < 4; a++) Bind(clip, targetPath, "m_LocalRotation." + "xyzw"[a], times, rotations.Select(q => q[a]).ToArray());
                    for (int a = 0; a < 3; a++) Bind(clip, targetPath, "m_LocalPosition." + "xyz"[a], times, positions.Select(p => p[a]).ToArray());
                }
                Ground(clip, model, times);
            }
            bool loop = action == "Idle" || action == "Run";
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = clip.length; settings.loopTime = loop; settings.loopBlend = loop;
            settings.keepOriginalPositionXZ = settings.keepOriginalPositionY = settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once; clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }
        static Bounds MeshBounds(SkinnedMeshRenderer[] renderers)
        {
            var mesh = new Mesh(); bool first = true; var bounds = new Bounds();
            try
            {
                foreach (var renderer in renderers)
                {
                    renderer.BakeMesh(mesh); var scale = renderer.transform.lossyScale;
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = renderer.transform.TransformPoint(new Vector3(vertex.x / scale.x, vertex.y / scale.y, vertex.z / scale.z));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        static Material NeutralMaterial(string species)
        {
            string path = $"Assets/Materials/Enemy/{species}/{species}_Normal.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Require<Material>($"Assets/Materials/Enemy/{species}/{species}_Earth.mat")) {name = species + "_Normal"};
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.7f, .7f, .7f, 1));
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION"); AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static void ConfigureBase(string species, EnemyProfile profile, AnimatorController controller)
        {
            string path = $"Assets/Prefabs/Enemy/Shared/{species}_Base.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.layer = LayerMask.NameToLayer("Enemy"); root.tag = "Enemy";
                var visual = root.transform.Find("Visual");
                foreach (var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = root.layer;
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bounds = MeshBounds(renderers);
                visual.position += Vector3.up * (root.transform.position.y - bounds.min.y);
                bounds.center += Vector3.up * (root.transform.position.y - bounds.min.y);
                float height = Mathf.Max(.3f, bounds.size.y);
                var animator = Get<Animator>(visual.gameObject);
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var agent = Get<NavMeshAgent>(root); agent.agentTypeID = 0;
                agent.radius = Mathf.Clamp(bounds.size.x * .28f, .15f, .3f); agent.height = height;
                agent.baseOffset = 0; agent.speed = profile.movementSpeed; agent.acceleration = profile.acceleration;
                agent.angularSpeed = profile.rotationSpeed; agent.stoppingDistance = profile.attacks[0].range * .75f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                var collider = Get<BoxCollider>(root); collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size; collider.isTrigger = false;
                var health = Get<Enemy>(root);
                using (var so = new SerializedObject(health))
                {
                    int hp = species == "Wolf" ? 100 : species == "Spider" ? 70 : 80;
                    so.FindProperty("maxHealth").intValue = hp; so.FindProperty("currentHealth").intValue = hp;
                    so.FindProperty("debugLog").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
                }
                var lockOn = Get<LockOnTarget>(root); lockOn.localAimOffset = collider.center;
                Get<EnemyNavMeshMotor>(root);
                var driver = Get<EnemyAnimationDriver>(root); driver.animator = animator; driver.runForLocomotion = true;
                var elemental = Get<ElementalEnemy>(root); elemental.element = ElementType.Normal;
                elemental.interruptOnStun = Array.Empty<Behaviour>();
                Get<ElementalEnemyControl>(root);
                var appearance = Get<ElementalEnemyAppearance>(root);
                appearance.modelRenderers = renderers.Cast<Renderer>().ToArray();
                appearance.elementMaterials = new Material[8];
                appearance.elementMaterials[0] = NeutralMaterial(species);
                foreach (var renderer in renderers) renderer.sharedMaterial = appearance.elementMaterials[0];
                for (int i = 1; i < 8; i++) appearance.elementMaterials[i] = Require<Material>($"Assets/Materials/Enemy/{species}/{species}_{(ElementType)i}.mat");
                var brain = Get<EnemyBrain>(root); brain.profile = profile; brain.postAttackStandbySeconds = .55f;
                var origin = root.transform.Find("Attack Origin");
                if (origin == null) { origin = new GameObject("Attack Origin").transform; origin.SetParent(root.transform, false); }
                origin.localPosition = new Vector3(0, height * .65f, bounds.max.z - root.transform.position.z);
                brain.attackOrigin = origin;
                CreatureEnemyEyeSetup.Configure(root, species);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [MenuItem("Tools/Enemy AI/Configure Wolf Spider and Snake")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            foreach (string species in Species)
            {
                Folder($"Assets/Data/Enemy/{species}"); Folder($"Assets/Animation/Enemy/{species}");
                var model = Require<GameObject>($"Assets/Models/Enemy/Shared/{species}/{species.ToLower()}_rigged.fbx");
                var reference = Require<AnimationClip>($"Assets/Prefabs/Enemy/{species}_Idle.anim");
                var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>(ProfilePath(species));
                bool fresh = profile == null;
                if (fresh) { profile = ScriptableObject.CreateInstance<EnemyProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath(species)); }
                profile.idle = PrepareClip(species, "Idle", model, reference);
                profile.walk = profile.run = PrepareClip(species, "Run", model, reference);
                profile.hit = PrepareClip(species, "Hit", model, reference);
                profile.die = PrepareClip(species, "Death", model, reference);
                var clip = PrepareClip(species, "Attack", model, reference);
                string effectPath = $"Assets/Data/Enemy/{species}/MeleeEffect.asset";
                var effect = AssetDatabase.LoadAssetAtPath<EnemyMeleeEffect>(effectPath);
                if (effect == null) { effect = ScriptableObject.CreateInstance<EnemyMeleeEffect>(); AssetDatabase.CreateAsset(effect, effectPath); }
                string attackPath = $"Assets/Data/Enemy/{species}/Attack.asset";
                var attack = AssetDatabase.LoadAssetAtPath<EnemyAttackDefinition>(attackPath);
                if (attack == null)
                {
                    attack = ScriptableObject.CreateInstance<EnemyAttackDefinition>();
                    attack.damage = species == "Wolf" ? 15 : species == "Spider" ? 10 : 12;
                    attack.range = species == "Snake" ? 1.5f : 1.3f; attack.cooldown = 1.4f;
                    attack.hitStart = .32f; attack.hitEnd = .62f; attack.arc = 110;
                    attack.applyElementToPlayer = true;
                    AssetDatabase.CreateAsset(attack, attackPath);
                }
                attack.stateName = "Attack"; attack.animation = clip; attack.effect = effect;
                profile.attacks = new[] { attack };
                if (fresh)
                {
                    profile.movementSpeed = species == "Wolf" ? 3.8f : species == "Spider" ? 2.8f : 2.5f;
                    profile.detectionRadius = 10; profile.chaseRadius = 20; profile.transitionDuration = .08f;
                    profile.hitFallbackDuration = profile.hit.length; profile.corpseLifetime = 12;
                }
                EditorUtility.SetDirty(attack); EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(attack); AssetDatabase.SaveAssetIfDirty(profile);
                var controller = EnemyControllerBuilder.Build(profile, $"Assets/Animation/Enemy/{species}/{species}.controller");
                ConfigureBase(species, profile, controller);
                for (int i = 1; i < 8; i++)
                {
                    var element = (ElementType)i;
                    string variantPath = $"Assets/Prefabs/Enemy/{species}/{species}_{element}.prefab";
                    var variant = PrefabUtility.LoadPrefabContents(variantPath);
                    try
                    {
                        var identity = variant.GetComponent<ElementalEnemy>(); identity.element = element;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
                        var appearance = variant.GetComponent<ElementalEnemyAppearance>();
                        foreach (var r in appearance.modelRenderers)
                        {
                            r.sharedMaterial = appearance.elementMaterials[i];
                            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                        }
                        PrefabUtility.SaveAsPrefabAsset(variant, variantPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(variant); }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Creature AI configured: 3 base enemies and 21 elemental variants. Place the prefab root on a baked NavMesh.");
        }
    }
}
