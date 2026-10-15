using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElementalGems;
using Nusantara.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies.Editor
{
    [InitializeOnLoad]
    public static class EnemyEyeVfxValidation
    {
        const string Key = "EnemyEyeVfxValidation";
        static readonly List<string> checks = new List<string>();
        static readonly List<EnemyEyeParticles> actors = new List<EnemyEyeParticles>();
        static readonly MethodInfo Refresh = typeof(EnemyEyeParticles).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Effects = typeof(EnemyEyeParticles).GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic);
        static GameObject container; static WorldDayNightCycle clock; static double began; static bool initialized;
        public static string Result => SessionState.GetString(Key + ".result", "Not run");
        static EnemyEyeVfxValidation() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        static List<GameObject> CurrentEffects(EnemyEyeParticles eye) => (List<GameObject>)Effects.GetValue(eye);
        static void UpdateEyes() { foreach (var eye in actors) Refresh.Invoke(eye, null); }
        static void Mode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) { began = EditorApplication.timeSinceStartup; initialized = false; actors.Clear(); checks.Clear(); }
            if (change == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        }
        [MenuItem("Tools/Enemy AI/Validate Night Eye VFX")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start validation in Edit mode.");
            SessionState.SetString(Key + ".result", "RUNNING"); SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        static void Finish(string result)
        {
            SessionState.SetString(Key + ".result", result + "\n" + string.Join("\n", checks)); SessionState.SetBool(Key, false);
            if (container != null) UnityEngine.Object.Destroy(container); EditorApplication.ExitPlaymode();
        }
        static void ValidateMovement(EnemyEyeParticles eye)
        {
            var ribbon = CurrentEffects(eye)[0].GetComponent<EnemyEyeRibbon>();
            var owner = eye.transform; var start = owner.position;
            const float begin = 1000;
            ribbon.UpdateGeometry(begin, null);
            for (int i = 1; i <= 6; i++)
            {
                owner.position = start + Vector3.right * (.06f * i);
                ribbon.UpdateGeometry(begin + i * .02f, null);
            }
            for (int i = 1; i <= 6; i++)
            {
                owner.position = start + Vector3.right * .36f + Vector3.forward * (.06f * i);
                ribbon.UpdateGeometry(begin + .12f + i * .02f, null);
            }
            Check(ribbon.MainRibbon.enabled, eye.name + " leaves a tail during movement");
            var points = new Vector3[ribbon.MainRibbon.positionCount]; ribbon.MainRibbon.GetPositions(points);
            Check(Vector3.Distance(points[0], ribbon.transform.position) < .005f, eye.name + " trail starts at the moving eye");
            float pathLength = 0; foreach (int i in Enumerable.Range(1, points.Length - 1)) pathLength += Vector3.Distance(points[i - 1], points[i]);
            Check(pathLength <= ribbon.Length + .01f, eye.name + " trail respects maximum length");
            Check(Vector3.Distance(points[0], points[points.Length - 1]) < pathLength - .05f, eye.name + " trail follows turns instead of a fixed straight line");
            ribbon.UpdateGeometry(begin + .34f, null);
            Check(ribbon.MainRibbon.enabled && ribbon.MainRibbon.startColor.a < .7f, eye.name + " tail fades after stopping");
            ribbon.UpdateGeometry(begin + .5f, null);
            Check(!ribbon.MainRibbon.enabled && ribbon.Core.enabled, eye.name + " has only its small core after stopping");
            // Eye motion from animation must work even with a stationary actor root.
            var local = ribbon.transform.localPosition;
            ribbon.transform.position += Vector3.up * .06f;
            ribbon.UpdateGeometry(begin + .52f, null);
            Check(ribbon.MainRibbon.enabled && ribbon.Core.enabled, eye.name + " animated eye movement leaves a trail with a stationary root");
            ribbon.UpdateGeometry(begin + .8f, null);
            Check(!ribbon.MainRibbon.enabled && ribbon.Core.enabled, eye.name + " stationary eyes retain glow without a tail");
            ribbon.transform.localPosition = local;
            owner.position += Vector3.right * 10;
            ribbon.UpdateGeometry(begin + .82f, null);
            Check(!ribbon.MainRibbon.enabled, eye.name + " does not streak across teleports");
            owner.position = start;
            ribbon.UpdateGeometry(begin + .84f, null);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup - began > 30) throw new InvalidOperationException("Eye VFX validation timed out");
                if (EditorApplication.timeSinceStartup - began < .5) return;
                if (!initialized)
                {
                    clock = UnityEngine.Object.FindAnyObjectByType<WorldDayNightCycle>(); Check(clock != null, "World clock exists in the scene");
                    clock.runCycle = false;
                    container = new GameObject("Temporary Eye VFX Validation"); container.SetActive(false); container.transform.position = new Vector3(10000, 0, 10000);
                    string[] paths = {"Assets/Prefabs/Enemy/OneHand/Enemy1OneHand.prefab", "Assets/Prefabs/Enemy/Wolf/Wolf_Fire.prefab",
                        "Assets/Prefabs/Enemy/Spider/Spider_Fire.prefab", "Assets/Prefabs/Enemy/Snake/Snake_Fire.prefab"};
                    foreach (string path in paths)
                    {
                        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), container.transform, false);
                        go.GetComponent<NavMeshAgent>().enabled = false; go.GetComponent<EnemyBrain>().enabled = false;
                        foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
                        var eyes = go.GetComponentInChildren<EnemyEyeParticles>(); Check(eyes != null, go.name + " has eye VFX");
                        eyes.GetComponent<ElementalEnemy>().element = ElementType.Fire; actors.Add(eyes);
                    }
                    container.SetActive(true); initialized = true; return;
                }
                clock.TestDay(); UpdateEyes();
                Check(!clock.runCycle && clock.IsDay && Mathf.Approximately(clock.CurrentHour, 12), "Test Day pauses at noon");
                Check(actors.All(e => CurrentEffects(e).Count == 0), "All enemy eye VFX is hidden during daytime");
                clock.TestNight(); UpdateEyes();
                Check(!clock.runCycle && clock.IsNight && Mathf.Approximately(clock.CurrentHour, 0), "Test Night pauses at midnight");
                Check(actors.All(e => CurrentEffects(e).Count == (e.name.StartsWith("Spider") ? 8 : 2)), "Correct eye counts: one-hand 2, wolf 2, spider 8, snake 2");
                foreach (var element in Enum.GetValues(typeof(ElementType)).Cast<ElementType>().Where(e => e != ElementType.Normal))
                {
                    foreach (var eye in actors) eye.GetComponent<ElementalEnemy>().element = element;
                    UpdateEyes();
                    foreach (var eye in actors)
                    {
                        var effects = CurrentEffects(eye);
                        Check(effects.Count > 0 && effects.All(e => e.activeSelf && e.name == "Eye Particle - " + element), eye.name + " displays " + element + " eyes at night");
                        var ribbons = effects.Select(e => e.GetComponent<EnemyEyeRibbon>()).ToArray();
                        Check(ribbons.All(r => r != null && r.Length >= .6f && r.Width >= .01f && r.Width <= .045f && r.Brightness == 3 && r.Duration == .25f), eye.name + " has smaller movement-only " + element + " ribbons");
                        foreach (var ribbon in ribbons)
                        {
                            Check(!ribbon.MainRibbon.enabled, eye.name + " has no tail when first standing still");
                            Check(ribbon.MainRibbon.widthCurve.Evaluate(1) == 0 && ribbon.Core.enabled, eye.name + " has a tapered tip and visible core");
                        }
                    }
                }
                foreach (var eye in actors) ValidateMovement(eye);
                clock.SetHour(5.999f); UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count > 0), "Eyes remain active before sunrise");
                clock.SetHour(6); UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count == 0), "Eyes turn off at 06:00");
                clock.SetHour(17.999f); UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count == 0), "Eyes stay off before sunset");
                clock.SetHour(18); UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count > 0), "Eyes turn on at 18:00");
                foreach (var eye in actors) { var identity = eye.GetComponent<ElementalEnemy>(); identity.element = ElementType.Fire; identity.ApplyLinkUpElement(ElementType.Water); }
                UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count == 0), "Link-up Normal windows hide eyes at night");
                foreach (var eye in actors) eye.GetComponent<ElementalEnemy>().RestoreInnateElementForReset();
                UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count > 0), "Restored innate elements restore eye effects");
                clock.enabled = false; UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count == 0), "Eyes stay hidden without an active clock");
                clock.enabled = true; clock.TestNight(); UpdateEyes();
                foreach (var eye in actors) eye.GetComponent<Enemy>().ForceKill();
                UpdateEyes(); Check(actors.All(e => CurrentEffects(e).Count == 0), "Death removes every eye effect");
                clock.dayMinutes = 1; clock.nightMinutes = 2; clock.SetHour(6); clock.Advance(60);
                Check(Mathf.Approximately(clock.CurrentHour, 18), "Day lasts its configured real minute");
                clock.Advance(120); Check(Mathf.Approximately(clock.CurrentHour, 6), "Night lasts its configured two real minutes");
                clock.SetHour(12); clock.dayMinutes = 2; clock.Advance(0);
                Check(Mathf.Approximately(clock.CurrentHour, 12), "Changing day duration preserves the current hour");
                clock.Advance(60); Check(Mathf.Approximately(clock.CurrentHour, 18), "Edited day duration uses the new clock rate");
                clock.SetHour(0); clock.nightMinutes = 4; clock.Advance(0);
                Check(Mathf.Approximately(clock.CurrentHour, 0), "Changing night duration preserves the current hour");
                clock.Advance(120); Check(Mathf.Approximately(clock.CurrentHour, 6), "Edited night duration uses the new clock rate");
                Finish("PASS: night eye VFX, all seven gems, smaller movement trails, stopping fade and day/night controls");
            }
            catch (Exception ex) { Debug.LogException(ex); Finish("FAIL: " + ex.GetBaseException().Message); }
        }
    }
}
