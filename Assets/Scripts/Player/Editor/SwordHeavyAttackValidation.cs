using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using ElementalGems;

[InitializeOnLoad]
public static class SwordHeavyAttackValidation
{
    const string Key = "SwordHeavyAttackValidation";
    static readonly List<string> checks = new List<string>();
    static double began;
    static SwordHeavyAttackValidation()
    {
        EditorApplication.playModeStateChanged += s => {
            if (s == PlayModeStateChange.EnteredPlayMode) began = EditorApplication.timeSinceStartup;
        };
        EditorApplication.update += Tick;
    }
    [MenuItem("Tools/Combat/Validate Sword Heavy Attack")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetBool(Key, true); SessionState.SetString(Key + "Result", "RUNNING");
        EditorApplication.EnterPlaymode();
    }
    public static string Result() => SessionState.GetString(Key + "Result", "Not run");
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks.Add("PASS: " + message);
    }
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static object Field(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    static void TimelineChecks()
    {
        var settings = new SwordHeavyChargeSettings();
        foreach (float fps in new[] { 15f, 30f, 60f, 120f })
        foreach (bool held in new[] { false, true })
        {
            var t = new SwordHeavyAttackTimeline(); t.Begin(100, settings);
            if (!held) t.Release();
            int low = 0, high = 0, window = 0;
            for (int i = 0; i < 1000 && !t.Finished; i++)
            {
                t.Advance(1 / fps, 30);
                if (t.WaitingForRelease)
                {
                    Check(t.Frame == 12 && t.LowPass == 0 && t.DamagePhase == SwordHeavyDamagePhase.None, "Hold freezes at frame 12 before any damage");
                    t.Release();
                }
                if (t.DamagePhase == SwordHeavyDamagePhase.None || t.WindowId == window) continue;
                window = t.WindowId;
                if (t.DamagePhase == SwordHeavyDamagePhase.Low) { low++; Check(t.Frame == 17, "Low slash begins exactly at frame 17"); }
                else { high++; Check(t.Frame == 48, "High slash begins exactly at frame 48"); }
            }
            Check(t.Finished && t.Frame == 100 && low == (held ? 2 : 1) && high == 1,
                $"{fps} FPS {(held ? "held" : "tap")}: correct low/high pass count and full completion");
        }
        var gate = new SwordHeavyAttackTimeline(); gate.Begin(100, settings);
        gate.Advance(11f / 30, 30); Check(!gate.HoldDecided && Mathf.Abs(gate.Frame - 11) < .001f, "Hold decision stays open through frame 11");
        gate.Advance(1f / 30, 30); Check(gate.WaitingForRelease && gate.Frame == 12 && gate.LowPass == 0, "Held attack freezes exactly at frame 12");
        gate.Advance(100, 30); Check(gate.Frame == 12 && gate.LowPass == 0 && gate.DamagePhase == SwordHeavyDamagePhase.None, "Long holds stay frozen without slash or damage");
        gate.Release(); gate.Advance(0, 30);
        Check(gate.Frame == 17 && gate.LowPass == 1, "Release starts the first low pass");
        gate.Advance(0, 30); Check(gate.Frame == 17, "Held time cannot fast-forward the released attack");
        gate.Advance(100, 30);
        Check(gate.Frame == 32 && gate.DamagePhase == SwordHeavyDamagePhase.None, "First low window ends at frame 32");
        gate.Advance(0, 30); Check(gate.Frame == 17 && gate.LowPass == 2, "Releasing after the decision still completes the second accepted low pass");
        gate.Advance(0, 30); gate.Advance(0, 30);
        Check(gate.Frame == 48 && gate.DamagePhase == SwordHeavyDamagePhase.High, "Hitch cannot skip high slash start");
        gate.Advance(0, 30); Check(gate.Frame == 56 && gate.DamagePhase == SwordHeavyDamagePhase.None, "High damage ends at frame 56");
        gate.Advance(0, 30); Check(gate.Finished, "Hitch completes only after presenting all three windows");
        var tap = new SwordHeavyAttackTimeline(); tap.Begin(100, settings);
        tap.Advance(11.9f / 30, 30); tap.Release(); tap.Advance(.2f / 30, 30);
        Check(tap.HoldDecided && !tap.RepeatingLow && tap.Frame < 17, "Release just before frame 12 preserves full tap windup");
        var pause = new SwordHeavyAttackTimeline(); pause.Begin(100, settings); pause.Advance(0, 30);
        Check(pause.Frame == 0 && !pause.HoldDecided, "Pause cannot advance windup or damage");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup - began < 1) return;
        SessionState.SetBool(Key, false);
        GameObject target = null;
        var surroundingTargets = new List<GameObject>();
        GemDefinition testGem = null;
        try
        {
            checks.Clear(); TimelineChecks();
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateManager>();
            player.GetComponent<PlayerInput>()?.DeactivateInput();
            var animator = player.anim; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 0;
            var config = animator.GetBehaviours<CombatAttackState>().Select(b => b.configuration).First(c => c != null && c.weapon == PlayerCombatMode.Sword);
            Check(config.Validate(out _), "Sword configuration validates");
            Check(AnimationUtility.GetAnimationClipSettings(config.heavyAttackChain[0].animation).loopBlendOrientation,
                "Heavy clip retains authored root rotation in its pose");
            var settings = config.swordHeavyCharge;
            Check(settings.lowDamageHeavyAttack.slash.fullCircle && !settings.highDamageHeavyAttack.slash.fullCircle,
                "Only low heavy uses the full circular slash");
            Check(settings.lowDamageHeavyAttack.slash.reverseSweep && settings.lowDamageHeavyAttack.slash.localEulerAngles == Vector3.zero,
                "Low slash is right-to-left");
            Check(!settings.highDamageHeavyAttack.slash.reverseSweep && settings.highDamageHeavyAttack.slash.localEulerAngles == new Vector3(0, 0, -90),
                "High slash is top-down");
            animator.SetInteger("CombatMode", 1); animator.SetInteger("ParryReturnMode", 1);
            animator.Play(config.LocomotionStatePath, 0, 0); animator.Update(0);
            var effects = player.GetComponent<GemLightSlashEffects>();
            var melee = player.GetComponent<GemSwordCombat>();
            var visuals = player.GetComponent<PlayerSwordVisuals>();
            var manager = player.GetComponent<GemManager>();
            testGem = ScriptableObject.CreateInstance<GemDefinition>();
            testGem.element = ElementType.Wind; testGem.damageScale = 1; testGem.knockback = 4;
            manager.gems = manager.gems.Where(g => g.element != ElementType.Wind).Concat(new[] { testGem }).ToArray();
            manager.Equip(ElementType.Wind, false);
            target = new GameObject("Temporary Heavy Attack Validation Target"); target.SetActive(false);
            target.transform.position = player.transform.position + player.transform.forward * 1.5f;
            var collider = target.AddComponent<CapsuleCollider>(); collider.height = 2; collider.radius = .3f; collider.center = Vector3.up;
            var enemyTarget = target.AddComponent<Enemy>();
            Set(enemyTarget, "maxHealth", 10000); Set(enemyTarget, "currentHealth", 10000); Set(enemyTarget, "debugLog", false);
            var elemental = target.AddComponent<ElementalEnemy>();
            target.SetActive(true); melee.enemyLayers = ~0; Physics.SyncTransforms();
            var damageEvents = new List<int>(); enemyTarget.OnDamaged += damageEvents.Add;
            var surroundingHits = new int[5];
            var offsets = new[] { -player.transform.forward * 1.5f, player.transform.right * 1.5f,
                -player.transform.right * 1.5f, player.transform.forward * (melee.slashReach + 3), Vector3.up * 10 };
            for (int n = 0; n < offsets.Length; n++)
            {
                int index = n;
                var other = UnityEngine.Object.Instantiate(target);
                other.name = "Temporary Circular Slash Target " + n;
                other.transform.position = player.transform.position + offsets[n];
                other.GetComponent<Enemy>().OnDamaged += _ => surroundingHits[index]++;
                surroundingTargets.Add(other);
            }
            Physics.SyncTransforms();
            void Sample(float seconds)
            {
                Call(player, "AdvanceSwordHeavyAnimation", seconds);
                animator.Update(0); animator.Update(0);
                Call(visuals, "LateUpdate"); Call(effects, "LateUpdate");
            }
            foreach (bool held in new[] { false, true })
            {
                damageEvents.Clear();
                Array.Clear(surroundingHits, 0, surroundingHits.Length);
                if (held) player.BeginHeavyAttackHold(); else player.HeavyAttack();
                animator.Update(0); animator.Update(0);
                Check(player.IsSwordHeavyAttacking && player.IsAttackMovementLocked, "Heavy starts immediately and locks movement");
                Check(!player.TryAttack(CombatAttackInput.LightAttack) && !player.TryAttack(CombatAttackInput.SpecialAttack) && !player.TryParry(), "Competing actions cannot interrupt heavy");
                float baseDamage = player.CurrentWeaponAttackStats.Damage(melee.heavyDamage);
                Vector3 initialChest = Vector3.ProjectOnPlane(animator.GetBoneTransform(HumanBodyBones.Chest).forward, Vector3.up).normalized;
                Vector3 initialPosition = player.transform.position;
                Quaternion initialRotation = player.transform.rotation;
                float minimumFacingDot = 1;
                int observedWindows = 0;
                bool holdReleased = !held;
                var seen = new HashSet<GemCrescentSlash>();
                for (int i = 0; i < 1000 && player.IsSwordHeavyAttacking; i++)
                {
                    int before = damageEvents.Count;
                    Sample(1f / 60);
                    if (!holdReleased && player.SwordHeavyAnimationFrame == 12)
                    {
                        Sample(10f);
                        Check(player.SwordHeavyAnimationFrame == 12 && effects.ActiveSlash == null && damageEvents.Count == 0,
                            "Live held attack remains at frame 12 without VFX or damage");
                        player.EndHeavyAttackHold();
                        holdReleased = true;
                    }
                    Vector3 chest = Vector3.ProjectOnPlane(animator.GetBoneTransform(HumanBodyBones.Chest).forward, Vector3.up).normalized;
                    minimumFacingDot = Mathf.Min(minimumFacingDot, Vector3.Dot(initialChest, chest));
                    if (effects.ActiveSlash == null || !seen.Add(effects.ActiveSlash)) continue;
                    observedWindows++;
                    bool high = player.CurrentSwordHeavyDamagePhase == SwordHeavyDamagePhase.High;
                    Check(effects.ActiveSlash.FullCircle == !high, "Low visual is a 360 ring; high visual retains the overhead crescent");
                    var renderer = effects.ActiveSlash.GetComponent<MeshRenderer>();
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                    Check(properties.GetFloat("_FullCircle") == (high ? 0 : 1) && !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),
                        "Slash shader compiles and selects the matching visual geometry");
                    if (!high)
                    {
                        Check(surroundingHits[0] == player.SwordHeavyLowPass && surroundingHits[1] == player.SwordHeavyLowPass && surroundingHits[2] == player.SwordHeavyLowPass,
                            "Low slash hits enemies behind, left and right once per pass");
                        effects.ActiveSlash.GetLocalHitSpan(.75f, out _, out Vector3 backPoint);
                        Check(backPoint.z < 0 && renderer.localBounds.Contains(backPoint), "Circular geometry and renderer bounds include the back half");
                    }
                    float multiplier = high ? 1.25f : 1f;
                    Check(damageEvents.Count == before + 1, "Each new slash hits the same enemy once");
                    Check(damageEvents.Last() == Mathf.RoundToInt(baseDamage * multiplier), $"Actual {(high ? "high" : "low")} damage applies {multiplier}x");
                    Check(Mathf.Abs(((Vector3)Field(elemental, "push")).magnitude - Mathf.Max(4f, settings.baseKnockback) * multiplier) < .001f,
                        $"Actual {(high ? "high" : "low")} knockback strength applies {multiplier}x");
                    elemental.ClearStatuses();
                    Call(effects, "LateUpdate"); Call(effects, "LateUpdate");
                    Check(damageEvents.Count == before + 1, "Repeated samples inside one slash cannot duplicate damage");
                }
                int expected = held ? 3 : 2;
                Check(observedWindows == expected && damageEvents.Count == expected, held ? "Held attack deals two low hits and one high hit" : "Tap plays once with one low hit and one high hit");
                Check(surroundingHits[0] == (held ? 2 : 1), "High slash does not hit the enemy behind the player");
                Check(surroundingHits[3] == 0 && surroundingHits[4] == 0, "Circular slash retains range and height limits");
                Check(minimumFacingDot < -.8f, "Character visibly turns backward during the authored animation");
                Check(Vector3.Distance(initialPosition, player.transform.position) < .001f && Quaternion.Angle(initialRotation, player.transform.rotation) < .001f,
                    "Pose rotation does not displace the character or change gameplay facing");
                Check(!player.IsAttacking && !player.IsAttackMovementLocked && animator.GetCurrentAnimatorStateInfo(0).IsName(config.LocomotionStatePath), "Full animation completes and returns to locomotion");
            }
            Check(testGem.knockback == 4 && settings.highDamageHeavyAttack.knockbackMultiplier == 1.25f, "Multipliers never mutate shared gem/configuration values");
            player.BeginHeavyAttackHold(); animator.Update(0); animator.Update(0); Sample(.1f); player.CancelHeavyAttackHold();
            for (int i = 0; i < 1000 && player.IsSwordHeavyAttacking; i++) Sample(1f / 60);
            Check(!player.IsAttacking && player.SwordHeavyLowPass == 1, "Input loss before frame 12 finishes as a tap without getting stuck");
            string result = "PASS\n" + string.Join("\n", checks);
            SessionState.SetString(Key + "Result", result); File.WriteAllText("Library/SwordHeavyAttackValidation.txt", result);
        }
        catch (Exception ex)
        {
            string result = "FAIL\n" + string.Join("\n", checks) + "\n" + ex;
            SessionState.SetString(Key + "Result", result); File.WriteAllText("Library/SwordHeavyAttackValidation.txt", result);
        }
        finally
        {
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            foreach (var other in surroundingTargets) if (other != null) UnityEngine.Object.DestroyImmediate(other);
            if (testGem != null) UnityEngine.Object.DestroyImmediate(testGem);
            EditorApplication.ExitPlaymode();
        }
    }
}
