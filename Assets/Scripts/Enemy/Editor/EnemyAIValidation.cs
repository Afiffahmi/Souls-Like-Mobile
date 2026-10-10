using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoulsLike.Enemies.Editor
{
    [InitializeOnLoad]
    public static class EnemyAIValidation
    {
        private const string Key = "EnemyAI.Validation.Pending";
        private static EnemyBrain brain;
        private static PlayerEnemyTarget target;
        private static PlayerOverall health;
        private static CharacterController controller;
        private static GameObject wall;
        private static readonly List<string> checks = new List<string>();
        private static readonly HashSet<string> attacks = new HashSet<string>();
        private static int stage, damageBefore, attackStartHealth;
        private static float stageAt, started;
        private static bool preHitChecked;
        private static EnemyAttackDefinition observedAttack;
        private static Quaternion walkBoneRotation;
        private static bool sampledWalkBone;
        private static float walkBoneDelta;
        private static int expectedDamage, expectedAttackCount;
        private static bool[] availableBefore;
        private static int[] priorityBefore;
        private static float observedReactionDuration;
        private static float hitTimeAtExit, repeatedHitTime;
        private static bool testedRepeatedHit;
        private static double launchAt;
        static EnemyAIValidation()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Tick;
        }
        [MenuItem("Tools/Enemy AI/Run Play Mode Validation")]
        public static void Begin()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before running validation.");
            SessionState.SetBool(Key, true); SessionState.SetString(Key + ".Result", "RUNNING");
            launchAt = EditorApplication.timeSinceStartup;
            EditorApplication.EnterPlaymode();
        }
        private static void ModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) { stage = -1; launchAt = EditorApplication.timeSinceStartup; }
            if (change == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        }
        private static void Check(bool condition, string description)
        { if (!condition) throw new Exception(description); checks.Add("PASS: " + description); }
        private static void MovePlayer(Vector3 feet)
        {
            Vector3 offset = controller != null && controller.enabled ? target.transform.position - target.GroundPosition : Vector3.zero;
            target.transform.position = feet + offset; Physics.SyncTransforms();
        }
        private static void Next(int value) { stage = value; stageAt = Time.time; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (stage == -1)
                {
                    if (EditorApplication.timeSinceStartup - launchAt < 1) return;
                    checks.Clear(); attacks.Clear(); observedAttack = null; preHitChecked = false;
                    sampledWalkBone = false; walkBoneDelta = 0;
                    brain = UnityEngine.Object.FindFirstObjectByType<EnemyBrain>();
                    target = UnityEngine.Object.FindFirstObjectByType<PlayerEnemyTarget>();
                    if (brain == null || target == null) throw new Exception("Configured enemy and player adapter required.");
                    health = target.GetComponent<PlayerOverall>(); controller = target.GetComponent<CharacterController>();
                    var movement = target.GetComponent<PlayerStateManager>(); if (movement != null) movement.enabled = false;
                    target.evadeDuringRoll = target.parryEnabled = false;
                    health.currentHealth = health.maxHealth = 10000;
                    brain.profile = UnityEngine.Object.Instantiate(brain.profile);
                    brain.profile.attacks = (EnemyAttackDefinition[])brain.profile.attacks.Clone();
                    availableBefore = new bool[brain.profile.attacks.Length];
                    priorityBefore = new int[brain.profile.attacks.Length];
                    expectedDamage = expectedAttackCount = 0; observedReactionDuration = -1;
                    hitTimeAtExit = -1; testedRepeatedHit = false;
                    for (int i = 0; i < brain.profile.attacks.Length; i++)
                    {
                        brain.profile.attacks[i] = UnityEngine.Object.Instantiate(brain.profile.attacks[i]);
                        availableBefore[i] = brain.profile.attacks[i].available;
                        priorityBefore[i] = brain.profile.attacks[i].priority;
                        if (brain.profile.attacks[i].IsUsable) expectedAttackCount++;
                    }
                    brain.targetOverride = target;
                    brain.AttackStarted += a => { attacks.Add(a.stateName); observedAttack = a; attackStartHealth = health.currentHealth; expectedDamage += a.damage; if (stage == 3) a.priority = -100; };
                    brain.StateChanged += s => {
                        if (stage == 6 && s != EnemyState.TakingDamage && observedReactionDuration < 0)
                        {
                            observedReactionDuration = Time.time - stageAt;
                            if (brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Hit", out var normalizedHit)) hitTimeAtExit = normalizedHit;
                        }
                    };
                    started = Time.time; MovePlayer(brain.HomePosition + Vector3.forward * 28); Next(0);
                    return;
                }
                if (Time.time - started > 100 || Time.time - stageAt > 22) throw new Exception("Validation timeout in stage " + stage + ", state=" + brain.State);
                float elapsed = Time.time - stageAt;
                switch (stage)
                {
                    case 0:
                        if (brain.State != EnemyState.Idle || elapsed < .7f) break;
                        Check(brain.GetComponent<EnemyNavMeshMotor>().Ready, "Enemy spawns on the baked NavMesh");
                        Check(Vector3.Distance(brain.transform.position, brain.HomePosition) < .3f, "Idle outside detection and settled at home");
                        var initialBar = brain.GetComponentInChildren<HealthBar>();
                        if (initialBar != null)
                        {
                            Check(initialBar.slider.maxValue == brain.Health.MaxHealth && initialBar.slider.value == brain.Health.CurrentHealth, "World health bar starts with the enemy's actual health");
                            Check(initialBar.transform.position.y > brain.transform.position.y + 2.8f && Quaternion.Angle(initialBar.transform.rotation, Camera.main.transform.rotation) < 1, "Health bar is above the enemy and faces the camera");
                        }
                        MovePlayer(brain.HomePosition + Vector3.forward * 8);
                        wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Validation LOS wall";
                        wall.transform.position = brain.HomePosition + new Vector3(0, 1.5f, 4); wall.transform.localScale = new Vector3(5, 3, .5f); Physics.SyncTransforms(); Next(1); break;
                    case 1:
                        if (elapsed < .8f) break;
                        Check(brain.State == EnemyState.Idle, "Obstacle blocks player detection");
                        UnityEngine.Object.DestroyImmediate(wall); Next(2); break;
                    case 2:
                        var walkAnimator = brain.GetComponent<EnemyAnimationDriver>().animator;
                        if (walkAnimator.isHuman && brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Walk", out _))
                        {
                            var leg = walkAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                            if (!sampledWalkBone) { walkBoneRotation = leg.localRotation; sampledWalkBone = true; }
                            else walkBoneDelta = Mathf.Max(walkBoneDelta, Quaternion.Angle(walkBoneRotation, leg.localRotation));
                        }
                        if (brain.State != EnemyState.Chasing || Vector3.Distance(brain.transform.position, brain.HomePosition) < .5f) break;
                        if (brain.profile.walk != null && brain.profile.walk.humanMotion)
                        {
                            Check(walkAnimator.avatar != null && walkAnimator.avatar.isValid && walkAnimator.isHuman, "Humanoid clips have a valid model Avatar");
                            Check(walkBoneDelta > .1f, "Walk animation actually moves the model's leg bones");
                        }
                        Check(true, "Detects player and physically chases along NavMesh");
                        MovePlayer(brain.transform.position + brain.transform.forward * 1.5f); Next(3); break;
                    case 3:
                        var driver = brain.GetComponent<EnemyAnimationDriver>();
                        if (brain.State == EnemyState.Attacking && observedAttack != null && driver.TryGetTime(observedAttack.stateName, out float t) && t > .02f && t < observedAttack.hitStart - .02f)
                        {
                            if (health.currentHealth != attackStartHealth) throw new Exception("Damage happened before the animation hit window");
                            preHitChecked = true;
                        }
                        if (attacks.Count < expectedAttackCount || brain.State == EnemyState.Attacking) break;
                        Check(attacks.Contains("Attack1") && attacks.Contains("Attack2") && (!availableBefore[2] || attacks.Contains("Attack3")), "All enabled melee attacks selected, including Attack3 when enabled");
                        Check(preHitChecked, "Windup does not apply early damage");
                        Check(health.currentHealth == 10000 - expectedDamage, "One damage application per animation, through PlayerOverall");
                        for (int i = 0; i < brain.profile.attacks.Length; i++) brain.profile.attacks[i].priority = priorityBefore[i];
                        MovePlayer(brain.transform.position + Vector3.forward * 7); Next(4); break;
                    case 4:
                        if (brain.State != EnemyState.Chasing || elapsed < .4f) break;
                        Check(brain.GetComponent<EnemyNavMeshMotor>().Agent.velocity.sqrMagnitude > .01f, "Resumes chasing when player leaves attack range");
                        MovePlayer(brain.transform.position + brain.transform.forward * 1.5f); Next(5); break;
                    case 5:
                        if (brain.State != EnemyState.Attacking || !brain.GetComponent<EnemyAnimationDriver>().TryGetTime(brain.CurrentAttack.stateName, out float windup) || windup > brain.CurrentAttack.hitStart - .04f) break;
                        damageBefore = health.currentHealth; brain.Health.TakeDamage(1, true);
                        foreach (var a in brain.profile.attacks) a.available = false;
                        Check(brain.State == EnemyState.TakingDamage, "Incoming player-compatible damage interrupts windup into Hit"); Next(6); break;
                    case 6:
                        if (brain.profile.hit != null && elapsed >= .2f && !testedRepeatedHit)
                        {
                            var hitDriver = brain.GetComponent<EnemyAnimationDriver>();
                            Check(brain.State == EnemyState.TakingDamage && hitDriver.TryGetTime("Hit", out repeatedHitTime), "Hit animation remains active before it finishes");
                            brain.Health.TakeDamage(1, true);
                            var damagedBar = brain.GetComponentInChildren<HealthBar>();
                            if (damagedBar != null) Check(damagedBar.slider.value == brain.Health.CurrentHealth, "Health bar decreases immediately with damage");
                            brain.Health.Heal(1);
                            if (damagedBar != null) Check(damagedBar.slider.value == brain.Health.CurrentHealth, "Health bar increases with healing");
                            MovePlayer(brain.HomePosition + Vector3.forward * 28);
                            testedRepeatedHit = true;
                            break;
                        }
                        if (brain.profile.hit != null)
                        {
                            if (brain.State == EnemyState.TakingDamage)
                            {
                                if (testedRepeatedHit && brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Hit", out float currentHitTime) && currentHitTime + .001f < repeatedHitTime)
                                    throw new Exception("Repeated damage restarted the Hit clip");
                                break;
                            }
                            Check(hitTimeAtExit >= 1, "Hit reaches its final frame before locomotion/return, even after repeat damage and target escape");
                        }
                        if (elapsed < .4f) break;
                        Check(health.currentHealth == damageBefore, "Interrupted attack cannot deliver a stale hit");
                        if (brain.profile.hit == null && brain.profile.hitReactionMilliseconds > 0)
                            Check(observedReactionDuration >= brain.profile.hitReactionMilliseconds * .001f - .02f && observedReactionDuration < brain.profile.hitReactionMilliseconds * .001f + .1f,
                                "Hit reaction recovers within the configured millisecond duration (one-frame tolerance)");
                        for (int i = 0; i < brain.profile.attacks.Length; i++) brain.profile.attacks[i].available = availableBefore[i];
                        MovePlayer(brain.HomePosition + Vector3.forward * 28); Next(7); break;
                    case 7:
                        if (brain.State == EnemyState.Returning)
                        {
                            Check(true, "Leaving territory starts return state");
                            brain.Health.TakeDamage(1, true);
                            Check(brain.State == EnemyState.TakingDamage, "Returning enemy reacts to damage without reacquiring the player"); Next(8);
                        }
                        break;
                    case 8:
                        if (brain.State != EnemyState.Idle) break;
                        Check(Vector3.Distance(brain.transform.position, brain.HomePosition) <= brain.profile.homeTolerance + .05f, "Returns to spawn and becomes idle");
                        MovePlayer(brain.HomePosition + Vector3.forward * 1.5f); Next(9); break;
                    case 9:
                        if (brain.State != EnemyState.Attacking) break;
                        damageBefore = health.currentHealth;
                        var gem = ScriptableObject.CreateInstance<ElementalGems.GemDefinition>();
                        gem.element = ElementalGems.ElementType.Lightning;
                        gem.statuses = new[]{new ElementalGems.StatusSpec {kind=ElementalGems.StatusKind.Stun,chance=1,duration=2}};
                        brain.GetComponent<ElementalGems.ElementalEnemy>().Apply(new ElementalGems.GemAttack(gem), Vector3.zero);
                        UnityEngine.Object.Destroy(gem); Next(11); break;
                    case 11:
                        if (elapsed < .5f) break;
                        Check(brain.State == EnemyState.TakingDamage && health.currentHealth == damageBefore, "Existing elemental stun cancels the attack and suppresses damage");
                        Check(brain.GetComponent<EnemyNavMeshMotor>().Agent.velocity.sqrMagnitude < .001f, "Stunned enemy stops navigation");
                        brain.GetComponent<ElementalGems.ElementalEnemy>().ClearStatuses(); Next(12); break;
                    case 12:
                        if (brain.State != EnemyState.Attacking) break;
                        ValidateFrameBoundaries();
                        damageBefore = health.currentHealth; brain.Health.ForceKill();
                        Check(brain.State == EnemyState.Dying, "Death overrides an active attack"); Next(10); break;
                    case 10:
                        if (elapsed < 1.8f) break;
                        Check(health.currentHealth == damageBefore, "Dead enemy never applies queued attack damage");
                        Check(!brain.GetComponent<LockOnTarget>().IsAvailable, "Death releases existing lock-on eligibility");
                        Check(!brain.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Death disables navigation");
                        var deadBar = brain.GetComponentInChildren<HealthBar>(true);
                        if (deadBar != null) Check(!deadBar.GetComponent<Canvas>().enabled && deadBar.slider.value == 0, "Health bar empties and hides on death");
                        Check(brain.GetComponent<EnemyAnimationDriver>().TryGetTime("Die", out var deathTime), "Death Animator state is playing");
                        Finish(true, "All behavior checks passed, including configured frame windows and hit-reaction recovery."); break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }
        private static void ValidateFrameBoundaries()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var begin = typeof(EnemyBrain).GetMethod("BeginAttack", flags);
            var tick = typeof(EnemyBrain).GetMethod("TickAttack", flags);
            var animator = brain.GetComponent<EnemyAnimationDriver>().animator;
            for (int i = 0; i < brain.profile.attacks.Length; i++)
            {
                var attack = brain.profile.attacks[i];
                if (!attack.IsUsable || !attack.useFrameWindow) continue;
                MovePlayer(brain.transform.position + brain.transform.forward * 1.5f);
                int before = health.currentHealth;
                begin.Invoke(brain, new object[]{i});
                SampleFrame(animator, attack, attack.hitEndFrame + 1);
                tick.Invoke(brain, null);
                Check(health.currentHealth == before, attack.stateName + ": skipped window cannot apply a delayed hit");
                begin.Invoke(brain, new object[]{i});
                SampleFrame(animator, attack, attack.hitStartFrame - 1); tick.Invoke(brain, null);
                Check(health.currentHealth == before, attack.stateName + ": no damage before the start frame");
                float middle = (attack.hitStartFrame + attack.hitEndFrame) * .5f;
                MovePlayer(brain.transform.position + brain.transform.forward * (attack.range + 2));
                SampleFrame(animator, attack, middle - .25f); tick.Invoke(brain, null);
                Check(health.currentHealth == before, attack.stateName + ": out-of-range target is not damaged");
                MovePlayer(brain.transform.position + brain.transform.forward * 1.5f);
                SampleFrame(animator, attack, middle); tick.Invoke(brain, null);
                Check(health.currentHealth == before - attack.damage, attack.stateName + ": entering range inside the window applies damage");
                SampleFrame(animator, attack, attack.hitEndFrame - .1f); tick.Invoke(brain, null);
                SampleFrame(animator, attack, attack.hitEndFrame + 1); tick.Invoke(brain, null);
                Check(health.currentHealth == before - attack.damage, attack.stateName + ": only one hit and no recovery-frame damage");
            }
        }
        private static void SampleFrame(Animator animator, EnemyAttackDefinition attack, float frame)
        {
            animator.Play(Animator.StringToHash("Base Layer." + attack.stateName), 0, frame / (attack.animation.length * attack.animation.frameRate));
            animator.Update(0);
        }
        private static void Finish(bool passed, string detail)
        {
            string result = (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks) + "\n" + detail;
            SessionState.SetString(Key + ".Result", result);
            File.WriteAllText("Library/EnemyAIValidation.txt", result);
            Debug.Log(result); SessionState.SetBool(Key, false); EditorApplication.ExitPlaymode();
        }
        public static string Result() => SessionState.GetString(Key + ".Result", "Not run");
    }
}
