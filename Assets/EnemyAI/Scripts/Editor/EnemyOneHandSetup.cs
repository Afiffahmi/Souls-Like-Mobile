using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace SoulsLike.Enemies.Editor
{
    public static class EnemyOneHandSetup
    {
        public const string Root = "Assets/EnemyAI";
        public const string ProfilePath = Root + "/Profiles/Enemy1OneHand.asset";
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        private static AnimationClip Placeholder(string name, float duration, bool loop)
        {
            string path = Root + "/Animations/" + name + "_PLACEHOLDER.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip {name = name + "_PLACEHOLDER", frameRate = 30};
            clip.SetCurve("_EnemyAnimationClock", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, duration, 0));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        private static EnemyAttackDefinition Attack(string name, AnimationClip clip, EnemyAttackEffect effect, bool enabled, float range, int damage, float cooldown)
        {
            string path = Root + "/Attacks/" + name + ".asset";
            var attack = AssetDatabase.LoadAssetAtPath<EnemyAttackDefinition>(path);
            if (attack != null) return attack;
            attack = ScriptableObject.CreateInstance<EnemyAttackDefinition>();
            attack.stateName = name; attack.animation = clip; attack.available = enabled; attack.range = range; attack.damage = damage; attack.cooldown = cooldown; attack.effect = effect;
            AssetDatabase.CreateAsset(attack, path); return attack;
        }
        [MenuItem("Tools/Enemy AI/Configure Enemy One Hand in Active Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setup.");
            foreach (var folder in new[]{"Profiles","Attacks","Animations","Prefabs","Navigation","Backups"}) Folder(Root + "/" + folder);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string backup = Root + "/Backups/main_scene_test_before_enemy_ai.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(backup) == null && !EditorSceneManager.SaveScene(scene, backup, true)) throw new InvalidOperationException("Could not preserve the current scene.");
            var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EnemyProfile>();
                profile.idle = Placeholder("Idle", 1, true); profile.walk = Placeholder("Walk", 1, true);
                profile.run = Placeholder("Run", .7f, true); profile.hit = Placeholder("Hit", .6f, false); profile.die = Placeholder("Die", 1.8f, false);
                var melee = ScriptableObject.CreateInstance<EnemyMeleeEffect>(); AssetDatabase.CreateAsset(melee, Root + "/Attacks/MeleeEffect.asset");
                profile.attacks = new[]{
                    Attack("Attack1", Placeholder("Attack1", 1.25f, false), melee, true, 2.3f, 12, 2),
                    Attack("Attack2", Placeholder("Attack2", 1.7f, false), melee, true, 2.6f, 20, 3),
                    Attack("Attack3", null, melee, false, 2.5f, 25, 4),
                    Attack("Ranged", null, null, false, 12, 15, 3),
                    Attack("Cast", null, null, false, 10, 25, 5),
                    Attack("Ability", null, null, false, 6, 30, 8)};
                profile.attacks[3].attackType = EnemyAttackType.Ranged;
                profile.attacks[4].attackType = EnemyAttackType.Magic;
                profile.attacks[5].attackType = EnemyAttackType.Special;
                foreach (var a in profile.attacks) EditorUtility.SetDirty(a);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var controller = EnemyControllerBuilder.Build(profile, Root + "/Animations/Enemy1OneHand.controller");
            var brain = UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).FirstOrDefault(b=>b.profile == profile);
            GameObject root;
            if (brain == null)
            {
                var model = GameObject.Find("enemy_1_one_hand");
                if (model == null) throw new InvalidOperationException("Expected imported enemy_1_one_hand in the active scene.");
                var pos = model.transform.position;
                if (Physics.Raycast(pos + Vector3.up * 5, Vector3.down, out var ground, 100, ~0, QueryTriggerInteraction.Ignore)) pos.y = ground.point.y;
                root = new GameObject("enemy_1_onehand"); Undo.RegisterCreatedObjectUndo(root, "Configure enemy AI");
                root.transform.SetPositionAndRotation(pos, model.transform.rotation);
                Undo.SetTransformParent(model.transform, root.transform, "Parent enemy visual");
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
                model.name = "Visual";
                var bounds = model.GetComponentInChildren<Renderer>().bounds;
                model.transform.position += Vector3.up * (pos.y - bounds.min.y);
                var clock = new GameObject("_EnemyAnimationClock"); clock.transform.SetParent(model.transform, false);
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var agent = root.AddComponent<NavMeshAgent>(); agent.radius = .45f; agent.height = 2.8f; agent.baseOffset = 0; agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                root.AddComponent<Enemy>();
                var health = new SerializedObject(root.GetComponent<Enemy>()); health.FindProperty("maxHealth").intValue = 120; health.FindProperty("currentHealth").intValue = 120; health.FindProperty("debugLog").boolValue = false; health.ApplyModifiedPropertiesWithoutUndo();
                var capsule = root.AddComponent<CapsuleCollider>(); capsule.height = 2.8f; capsule.radius = .45f; capsule.center = Vector3.up * 1.4f;
                var lockOn = root.AddComponent<LockOnTarget>(); lockOn.localAimOffset = Vector3.up * 1.5f;
                root.AddComponent<EnemyNavMeshMotor>(); var driver = root.AddComponent<EnemyAnimationDriver>(); driver.animator = animator;
                var elemental = root.AddComponent<ElementalGems.ElementalEnemy>(); elemental.interruptOnStun = Array.Empty<Behaviour>();
                root.AddComponent<ElementalEnemyControl>();
                brain = root.AddComponent<EnemyBrain>(); brain.profile = profile;
                var origin = new GameObject("Attack Origin"); origin.transform.SetParent(root.transform, false); origin.transform.localPosition = new Vector3(0, 1.2f, .3f); brain.attackOrigin = origin.transform;
            }
            else root = brain.gameObject;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerOverall>();
            if (player == null) throw new InvalidOperationException("No PlayerOverall found in active scene.");
            if (player.GetComponent<PlayerEnemyTarget>() == null) Undo.AddComponent<PlayerEnemyTarget>(player.gameObject);
            var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None).FirstOrDefault(s=>s.name == "Enemy Navigation");
            if (surface == null)
            {
                var nav = new GameObject("Enemy Navigation"); Undo.RegisterCreatedObjectUndo(nav, "Enemy navigation");
                nav.transform.position = root.transform.position + Vector3.up * 3;
                surface = nav.AddComponent<NavMeshSurface>(); surface.agentTypeID = root.GetComponent<NavMeshAgent>().agentTypeID;
                surface.collectObjects = CollectObjects.Volume; surface.center = Vector3.zero; surface.size = new Vector3(70, 16, 70);
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed.");
                AssetDatabase.CreateAsset(surface.navMeshData, Root + "/Navigation/MainSceneEnemyNavigation.asset");
            }
            if (!NavMesh.SamplePosition(root.transform.position, out var spawn, 2, NavMesh.AllAreas)) throw new InvalidOperationException("No NavMesh at the enemy spawn.");
            root.transform.position = spawn.position;
            string prefabPath = Root + "/Prefabs/Enemy1OneHand.prefab";
            if (!PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(brain); EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Enemy AI configured, prefab saved, navigation baked. Animation clips are explicit placeholders; replace them on Enemy1OneHand profile and rebuild its controller.");
        }
    }
}
