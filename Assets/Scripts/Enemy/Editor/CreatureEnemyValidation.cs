using System;
using System.Collections.Generic;
using System.Linq;
using ElementalGems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SoulsLike.Enemies.Editor
{
    public sealed class CreatureValidationTarget : EnemyTarget
    {
        public int damage, calls; public ElementType receivedElement;
        public override bool IsAlive => true;
        public override Vector3 GroundPosition => transform.position;
        public override Vector3 AimPosition => transform.position + Vector3.up * .25f;
        public override bool ReceiveDamage(int amount, EnemyBrain source) { damage += amount; calls++; return true; }
        public override bool ReceiveElementalDamage(int amount, EnemyBrain source, ElementType element, bool allowParry)
        { receivedElement = element; return ReceiveDamage(amount, source); }
    }

    [InitializeOnLoad]
    public static class CreatureEnemyValidation
    {
        const string Key = "CreatureEnemyValidation";
        static readonly List<string> checks = new List<string>();
        static readonly Vector3 Origin = new Vector3(10000, 0, 10000);
        static Scene scene;
        static NavMeshData navData; static NavMeshDataInstance navInstance;
        static CreatureValidationTarget target;
        static EnemyBrain brain; static Enemy health; static ElementalEnemy elemental;
        static float stageTime; static double began;
        static int speciesIndex, stage, damageBefore;
        static bool observedChase, observedRun;
        public static string Result => SessionState.GetString(Key + ".result", "Not run");
        static CreatureEnemyValidation() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        static Bounds SampleBounds(SkinnedMeshRenderer skin, Mesh mesh)
        {
            skin.BakeMesh(mesh); var scale = skin.transform.lossyScale;
            var vertices = mesh.vertices.Select(v => skin.transform.TransformPoint(new Vector3(v.x / scale.x, v.y / scale.y, v.z / scale.z))).ToArray();
            if (vertices.Any(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.sqrMagnitude)))
                throw new InvalidOperationException("Animation produced invalid mesh vertices");
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) bounds.Encapsulate(v);
            return bounds;
        }
        static void ValidateGeometry(GameObject prefab, AnimationClip[] clips)
        {
            var go = UnityEngine.Object.Instantiate(prefab); go.hideFlags = HideFlags.HideAndDontSave;
            var mesh = new Mesh();
            try
            {
                var animator = go.GetComponentInChildren<Animator>(); animator.enabled = false;
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var neutral = SampleBounds(skin, mesh);
                foreach (var clip in clips) foreach (float fraction in new[] {0f, .25f, .5f, .75f, 1f})
                {
                    clip.SampleAnimation(animator.gameObject, clip.length * fraction);
                    var bounds = SampleBounds(skin, mesh);
                    if (Mathf.Abs(bounds.min.y) > .04f || Vector3.ProjectOnPlane(bounds.center - neutral.center, Vector3.up).magnitude > .08f)
                        throw new InvalidOperationException(clip.name + " drifts away from its root or ground at " + fraction);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(go); }
        }
        [MenuItem("Tools/Enemy AI/Validate Creature Assets")]
        public static string ValidateAssets()
        {
            int prefabs = 0, clips = 0;
            foreach (string species in CreatureEnemySetup.Species)
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>(CreatureEnemySetup.ProfilePath(species));
                if (profile == null || profile.attacks.Length != 1 || !profile.attacks[0].IsUsable)
                    throw new InvalidOperationException(species + " profile/attack invalid");
                foreach (var element in Enum.GetValues(typeof(ElementType)).Cast<ElementType>())
                {
                    string path = element == ElementType.Normal ? $"Assets/Prefabs/Enemy/Shared/{species}_Base.prefab" : $"Assets/Prefabs/Enemy/{species}/{species}_{element}.prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var identity = prefab.GetComponent<ElementalEnemy>(); var appearance = prefab.GetComponent<ElementalEnemyAppearance>();
                    var driver = prefab.GetComponent<EnemyAnimationDriver>(); var ai = prefab.GetComponent<EnemyBrain>();
                    if (identity == null || identity.element != element || appearance == null || ai == null || ai.profile != profile ||
                        prefab.GetComponent<Enemy>() == null || prefab.GetComponent<NavMeshAgent>() == null || prefab.GetComponent<Collider>() == null ||
                        prefab.GetComponent<LockOnTarget>() == null || prefab.GetComponent<ElementalEnemyControl>() == null ||
                        driver == null || !driver.runForLocomotion || driver.animator == null || driver.animator.applyRootMotion)
                        throw new InvalidOperationException(path + " missing AI configuration");
                    if (appearance.elementMaterials.Length != 8 || appearance.elementMaterials.Any(m => m == null) || appearance.elementMaterials.Distinct().Count() != 8 ||
                        appearance.modelRenderers.Any(r => r == null || r.sharedMaterial != appearance.elementMaterials[(int)element]))
                        throw new InvalidOperationException(path + " invalid gem material palette");
                    prefabs++;
                    foreach (var clip in new[]{profile.idle, profile.run, profile.hit, profile.die, profile.attacks[0].animation})
                    {
                        if (clip == null || AnimationUtility.GetCurveBindings(clip).Any(b => driver.animator.transform.Find(b.path) == null))
                            throw new InvalidOperationException(path + " broken animation bindings");
                    }
                }
                if (!profile.idle.isLooping || !profile.run.isLooping || profile.hit.isLooping || profile.die.isLooping)
                    throw new InvalidOperationException(species + " clip loop settings invalid");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>($"Assets/Animation/Enemy/{species}/{species}.controller");
                foreach (string state in new[]{"Idle", "Run", "Hit", "Attack", "Die"})
                    if (!controller.layers[0].stateMachine.states.Any(s => s.state.name == state && s.state.motion != null))
                        throw new InvalidOperationException(species + " controller missing " + state);
                ValidateGeometry(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy/Shared/{species}_Base.prefab"),
                    new[] {profile.idle, profile.run, profile.hit, profile.attacks[0].animation, profile.die});
                clips += 5;
            }
            string result = $"PASS: {prefabs} AI prefabs, {clips} animations, 3 controllers and all elemental material palettes.";
            Debug.Log(result); return result;
        }
        [MenuItem("Tools/Enemy AI/Run Creature Play Mode Validation")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            ValidateAssets(); SessionState.SetBool(Key, true); SessionState.SetString(Key + ".result", "RUNNING");
            EditorApplication.EnterPlaymode();
        }
        static void Mode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode) { stage = -1; began = EditorApplication.timeSinceStartup; checks.Clear(); }
            if (mode == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        }
        static void Next(int value) { stage = value; stageTime = Time.time; }
        static void Spawn()
        {
            string species = CreatureEnemySetup.Species[speciesIndex];
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy/{species}/{species}_Fire.prefab"), Origin, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(go, scene);
            brain = go.GetComponent<EnemyBrain>(); health = go.GetComponent<Enemy>(); elemental = go.GetComponent<ElementalEnemy>();
            brain.targetOverride = target; target.transform.position = Origin + Vector3.forward * 5;
            target.damage = target.calls = 0; target.receivedElement = ElementType.Normal;
            observedChase = observedRun = false;
            Next(0);
        }
        static void Finish(string result)
        {
            SessionState.SetString(Key + ".result", result + "\n" + string.Join("\n", checks));
            SessionState.SetBool(Key, false);
            if (navInstance.valid) navInstance.Remove();
            if (navData != null) UnityEngine.Object.Destroy(navData);
            if (brain != null) UnityEngine.Object.Destroy(brain.gameObject);
            if (target != null) UnityEngine.Object.Destroy(target.gameObject);
            EditorApplication.ExitPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup - began > 75) throw new InvalidOperationException("Creature validation timeout");
                if (stage == -1)
                {
                    scene = SceneManager.CreateScene("Creature AI Validation");
                    var sources = new List<NavMeshBuildSource> {new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,
                        size=new Vector3(20,.2f,20), transform=Matrix4x4.TRS(Origin-Vector3.up*.1f,Quaternion.identity,Vector3.one), area=0}};
                    navData=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Origin,new Vector3(24,4,24)),Vector3.zero,Quaternion.identity);
                    navInstance=NavMesh.AddNavMeshData(navData);
                    var targetGO=new GameObject("Creature Test Target");SceneManager.MoveGameObjectToScene(targetGO,scene);
                    target=targetGO.AddComponent<CreatureValidationTarget>(); speciesIndex=0; Spawn();return;
                }
                string species=CreatureEnemySetup.Species[speciesIndex];
                float elapsed=Time.time-stageTime;
                var driver=brain.GetComponent<EnemyAnimationDriver>();
                if (stage==0)
                {
                    observedChase |= brain.State==EnemyState.Chasing;
                    observedRun |= driver.TryGetTime("Run",out _);
                    if (target.calls==0) {if(elapsed>15)throw new InvalidOperationException(species+" did not chase/attack");return;}
                    Check(observedChase && observedRun,species+" chases using Run on the NavMesh");
                    Check(target.damage==brain.profile.attacks[0].damage && target.receivedElement==ElementType.Fire,species+" attack deals its configured elemental damage");
                    damageBefore=target.calls; Next(1);
                }
                else if(stage==1)
                {
                    if(brain.State==EnemyState.Attacking){if(elapsed>5)throw new InvalidOperationException("Attack did not finish");return;}
                    Check(target.calls==damageBefore,species+" attack applies damage once per animation");
                    // Test all palette entries through live CurrentElement and restore Fire afterwards.
                    var appearance=brain.GetComponent<ElementalEnemyAppearance>();
                    foreach(ElementType element in Enum.GetValues(typeof(ElementType)))
                    {
                        elemental.element=element; appearance.Refresh();
                        Check(appearance.modelRenderers.All(r=>r.sharedMaterial==appearance.elementMaterials[(int)element]),species+" uses "+element+" material");
                    }
                    elemental.element=ElementType.Fire;appearance.Refresh();
                    elemental.ApplyLinkUpElement(ElementType.Water);appearance.Refresh();
                    Check(elemental.CurrentElement==ElementType.Normal && appearance.modelRenderers.All(r=>r.sharedMaterial==appearance.elementMaterials[0]),species+" link-up temporarily uses Normal material");
                    // End the window via the same state API used by the elemental update.
                    var field=typeof(ElementalEnemy).GetField("linkUpElement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    ((LinkUpElementWindow)field.GetValue(elemental)).Expire(Time.timeAsDouble+LinkUpElementWindow.Duration);
                    appearance.Refresh();
                    Check(elemental.CurrentElement==ElementType.Fire && appearance.modelRenderers.All(r=>r.sharedMaterial==appearance.elementMaterials[(int)ElementType.Fire]),species+" restores innate material after link-up");
                    target.transform.position=Origin+Vector3.forward*30;
                    health.TakeDamage(1,true);
                    Check(brain.State==EnemyState.TakingDamage,species+" enters Hit after interrupting damage");
                    Next(2);
                }
                else if(stage==2)
                {
                    if(brain.State==EnemyState.TakingDamage){if(elapsed>brain.profile.hit.length+2)throw new InvalidOperationException(species+" stuck in Hit");return;}
                    Check(elapsed>=brain.profile.hit.length-.15f,species+" completes hit reaction before recovery");
                    health.ForceKill();
                    Check(brain.State==EnemyState.Dying && !brain.GetComponent<NavMeshAgent>().enabled &&
                        brain.GetComponentsInChildren<Collider>().All(c=>!c.enabled) && !brain.GetComponent<LockOnTarget>().targetable,species+" death stops navigation, collisions and lock-on");
                    Next(3);
                }
                else if(stage==3)
                {
                    if(elapsed<.2f)return;
                    Check(driver.TryGetTime("Die",out _),species+" plays death animation");
                    UnityEngine.Object.Destroy(brain.gameObject);brain=null;
                    if(++speciesIndex<CreatureEnemySetup.Species.Length)Spawn();else Finish("PASS: creature gameplay validation");
                }
            }
            catch(Exception ex){Debug.LogException(ex);Finish("FAIL: "+ex.Message);}
        }
    }
}
