using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ElementalGems;
using SoulsLike.Enemies;
using UnityEditor;
using UnityEngine;

/// <summary>Runs the actual four-step thrust sequence and checks its movement, visuals and accepted hits.</summary>
[InitializeOnLoad]
public static class EnemySpecialAttack3Validation
{
    const string Key = "EnemySpecialAttack3Validation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static EnemyBrain brain;
    static EnemyNavMeshMotor motor;
    static EnemyAnimationDriver driver;
    static EnemySlashVisual visual;
    static ElementalEnemy element;
    static PlayerEnemyTarget target;
    static PlayerOverall player;
    static Vector3 start, heading, stepStart, interruptedPosition;
    static Quaternion rotation;
    static float distance, interruptedAt;
    static int step, mode, healthBefore, stepHealth;
    static float observedPauseStart;
    static Vector3 pausePosition;
    static int pauseHealth, observedGaps;
    static bool pauseObserved;
    static string callbackFailure;
    static double began;
    static bool initialized, dashCaptured, shifted;

    static EnemySpecialAttack3Validation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            { initialized = false; began = EditorApplication.timeSinceStartup; }
        };
        EditorApplication.update += Tick;
    }
    [MenuItem("Tools/Enemy AI/Validate Special Attack 3 Dashes")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        SessionState.SetString(Key + "Result", "RUNNING"); SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    public static string Result() => SessionState.GetString(Key + "Result", "Not run");
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks.Add("PASS: " + message); }
    static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    static void Sample(float frame)
    {
        var a = brain.CurrentAttack;
        driver.animator.Play("Base Layer." + a.stateName, 0, frame / (a.animation.length * a.animation.frameRate));
        driver.animator.Update(0);
    }
    static void Initialize()
    {
        checks.Clear(); step = mode = 0; callbackFailure = null;
        brain = UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)
            .First(b => b.profile != null && b.profile.name == "Enemy1OneHand");
        foreach (var other in UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (other != brain) other.enabled = false;
        motor = brain.GetComponent<EnemyNavMeshMotor>(); driver = brain.GetComponent<EnemyAnimationDriver>();
        visual = brain.GetComponent<EnemySlashVisual>(); element = brain.GetComponent<ElementalEnemy>();
        Check(motor.Ready && visual != null, "Enemy has navigation and the reusable energy visual");
        brain.profile = UnityEngine.Object.Instantiate(brain.profile);
        brain.profile.healthLossSpecialsEnabled = false; brain.profile.resetPositionEnabled = false;
        brain.debugRepeatSpecialAttacks = false; brain.postAttackStandbySeconds = 60;
        target = UnityEngine.Object.FindFirstObjectByType<PlayerEnemyTarget>(); player = target.GetComponent<PlayerOverall>();
        target.GetComponent<PlayerStateManager>().enabled = false;
        target.GetComponent<CharacterController>().enabled = false;
        target.GetComponent<PlayerElementalState>().enabled = false; // Isolate direct hits from status ticks.
        target.GetComponent<GemManager>().Equip(ElementType.Normal, false);
        player.maxHealth = player.currentHealth = 1000; healthBefore = player.currentHealth;
        brain.targetOverride = target; typeof(EnemyBrain).GetProperty("Target").SetValue(brain, target);
        motor.Agent.Warp(brain.HomePosition);
        pauseObserved = false; observedGaps = 0;
        target.transform.position = brain.HomePosition + brain.transform.forward * 4.5f;
        brain.AttackStarted += OnStep; initialized = true; StartSequence();
    }
    static void ProbeRepeatSettings()
    {
        var attacks = brain.profile.attacks;
        var copy = UnityEngine.Object.Instantiate(attacks.First(a => a != null && a.stateName == "EnemySpecialAttack3"));
        Vector3 origin = motor.NavigationPosition;
        Vector3 forward = brain.transform.forward;
        int starts = 0;
        Action<EnemyAttackDefinition> countStart = _ => starts++;
        brain.AttackStarted += countStart;
        brain.profile.attacks = new[] { copy };
        typeof(EnemyBrain).GetField("attackReady", Private).SetValue(brain, new float[1]);
        try
        {
            copy.repeatCount = 1; copy.repeatDelay = .25f;
            target.transform.position = origin + forward * 4;
            Call(brain, "BeginAttack", 0); Sample(85.05f); Call(brain, "TickAttack");
            Check(starts == 1 && brain.CurrentSequence == null, "Repeat Count 1 plays exactly one thrust");

            motor.Agent.Warp(origin); starts = 0;
            copy.repeatCount = 2;
            Call(brain, "BeginAttack", 0); Sample(85.05f); Call(brain, "TickAttack");
            Check(brain.CurrentAttack == null && brain.SequencePauseRemaining > .2f && brain.SequencePauseRemaining <= .251f,
                "A configured 0.25-second pause replaces the default one-second gap (remaining " + brain.SequencePauseRemaining + ")");
            Call(brain, "TickAttack");
            Check(starts == 1 && brain.CurrentAttack == null, "A repeat cannot start before the configured pause expires");
            typeof(EnemyBrain).GetField("sequenceResumeAt", Private).SetValue(brain, Time.time - .01f);
            Call(brain, "TickAttack"); Sample(85.05f); Call(brain, "TickAttack");
            Check(starts == 2 && brain.CurrentSequence == null, "Repeat Count 2 plays exactly two thrusts");

            motor.Agent.Warp(origin); starts = 0;
            copy.repeatDelay = 0;
            Call(brain, "BeginAttack", 0); Sample(85.05f); Call(brain, "TickAttack");
            Check(starts == 2 && brain.CurrentAttack == copy && brain.SequencePauseRemaining == 0,
                "Setting the gap to zero starts the next repeat immediately");
            Sample(85.05f); Call(brain, "TickAttack");

            motor.Agent.Warp(origin); starts = 0;
            copy.repeatDelay = .25f;
            Call(brain, "BeginAttack", 0); Sample(85.05f); Call(brain, "TickAttack");
            brain.Interrupt();
            Check(starts == 1 && Field(brain, "queuedSequenceStep") == null && brain.SequencePauseRemaining == 0,
                "Interruption during the pause cancels the pending thrust");
        }
        finally
        {
            brain.AttackStarted -= countStart;
            brain.profile.attacks = attacks;
            typeof(EnemyBrain).GetField("attackReady", Private).SetValue(brain, new float[attacks.Length]);
            Call(brain, "SetState", EnemyState.Idle); motor.Agent.Warp(origin);
            UnityEngine.Object.Destroy(copy);
        }
    }
    static void ProbeStartingOutsideRange()
    {
        var attacks = brain.profile.attacks;
        var attack = attacks.First(a => a != null && a.stateName == "EnemySpecialAttack3");
        Vector3 origin = motor.NavigationPosition;
        Vector3 forward = brain.transform.forward;
        target.transform.position = origin + forward * 10f;
        brain.transform.rotation = Quaternion.LookRotation(-forward);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "Temporary special approach selection obstruction";
        obstacle.transform.position = origin + forward * 5f + Vector3.up;
        obstacle.transform.localScale = new Vector3(2, 6, 2);
        try
        {
            brain.profile.attacks = new[] { attack };
            Physics.SyncTransforms();
            Check(!brain.HasLineOfSight(target), "Selection probe starts beyond melee range and behind an obstruction");
            foreach (bool debug in new[] { false, true })
            {
                brain.debugRepeatSpecialAttacks = debug; brain.debugBypassHealthMilestone = debug;
                brain.debugSpecialAttacks = new[] { new EnemySpecialDebugSlot { play = true, attack = attack } };
                typeof(EnemyBrain).GetField("attackReady", Private).SetValue(brain, new float[1]);
                typeof(EnemyBrain).GetField("nextAttack", Private).SetValue(brain, 0f);
                typeof(EnemyBrain).GetField("standbyUntil", Private).SetValue(brain, 0f);
                typeof(EnemyBrain).GetField("resetPositionPending", Private).SetValue(brain, false);
                typeof(EnemyBrain).GetField("pendingHealthSpecials", Private).SetValue(brain, debug ? 0 : 1);
                typeof(EnemyBrain).GetField("healthSpecialReturnRequired", Private).SetValue(brain, false);
                brain.transform.rotation = Quaternion.LookRotation(-forward);
                Call(brain, "SetState", EnemyState.Chasing);
                Call(brain, "Update");
                Check(brain.CurrentAttack == attack && brain.State == EnemyState.Attacking && motor.IsApproachingAttack,
                    (debug ? "Debug" : "Queued health") + " selection commits to Special Attack 3 from 10m without waiting for range, facing or sight");
                Call(brain, "FinishAttack");
            }
            var ordinary = UnityEngine.Object.Instantiate(attack);
            ordinary.approachBeforeDash = false;
            brain.profile.attacks = new[] { ordinary };
            typeof(EnemyBrain).GetField("attackReady", Private).SetValue(brain, new float[1]);
            Check(!(bool)typeof(EnemyBrain).GetMethod("CanUseAttack", Private).Invoke(brain, new object[] { 0 }),
                "An attack without an approach phase still obeys its starting range and sight checks");
            UnityEngine.Object.Destroy(ordinary);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(obstacle);
            brain.profile.attacks = attacks;
            brain.debugRepeatSpecialAttacks = brain.debugBypassHealthMilestone = false;
            brain.debugSpecialAttacks = Array.Empty<EnemySpecialDebugSlot>();
            typeof(EnemyBrain).GetField("attackReady", Private).SetValue(brain, new float[attacks.Length]);
            typeof(EnemyBrain).GetField("pendingHealthSpecials", Private).SetValue(brain, 0);
            Call(brain, "SetState", EnemyState.Idle);
            brain.transform.rotation = Quaternion.LookRotation(forward);
            motor.Agent.Warp(origin);
        }
    }
    static void ProbeFixedDistance()
    {
        var source = brain.profile.attacks.First(a => a != null && a.stateName == "EnemySpecialAttack3");
        var copy = UnityEngine.Object.Instantiate(source); copy.approachBeforeDash = false;
        Vector3 origin = motor.NavigationPosition, forward = Vector3.zero;
        for (int i = 0; i < 16; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
            if (!motor.Agent.Raycast(origin + direction * copy.movementDistance, out _)) { forward = direction; break; }
        }
        Check(forward.sqrMagnitude > .5f, "An unobstructed six-metre dash lane is available");
        foreach (float targetDistance in new[] { .5f, 9f })
        {
            motor.Agent.Warp(origin); motor.Stop();
            target.transform.position = origin + forward * targetDistance;
            motor.BeginAttackMovement(copy, target);
            Check(Mathf.Abs((float)Field(motor, "attackMoveDistance") - 6f) < .01f,
                "Player at " + targetDistance + "m still produces a six-metre dash");
            motor.TickAttackMovement(copy, 41f / (copy.animation.length * copy.animation.frameRate));
            Vector3 travelled = Vector3.ProjectOnPlane(motor.NavigationPosition - origin, Vector3.up);
            Check(Vector3.Distance(travelled, forward * 6f) < .1f,
                "The full dash passes a player at " + targetDistance + "m without shortening (travel " + travelled + ")");
        }
        motor.Agent.Warp(origin); motor.CancelAttackMovement(); motor.Stop();
        UnityEngine.Object.Destroy(copy);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (callbackFailure != null) throw new Exception(callbackFailure);
            if (EditorApplication.timeSinceStartup - began > 55) throw new Exception("Validation timed out.");
            if (!initialized)
            { if (EditorApplication.timeSinceStartup - began >= 1) Initialize(); return; }
            if (brain.State == EnemyState.Attacking && mode < 2)
            {
                var a = brain.CurrentAttack;
                if (a == null)
                {
                    if (!pauseObserved)
                    {
                        pauseObserved = true;
                        observedPauseStart = Time.time + brain.SequencePauseRemaining - brain.CurrentSequence.repeatDelay;
                        pausePosition = motor.NavigationPosition; pauseHealth = player.currentHealth;
                    }
                    Vector3 drift = Vector3.ProjectOnPlane(motor.NavigationPosition - pausePosition, Vector3.up);
                    if (drift.magnitude > .05f || motor.IsApproachingAttack ||
                        visual.ActiveSlash != null || player.currentHealth != pauseHealth)
                        throw new Exception("Inter-attack pause: drift=" + drift + ", approach=" + motor.IsApproachingAttack +
                            ", VFX=" + (visual.ActiveSlash != null) + ", HP=" + player.currentHealth + "/" + pauseHealth);
                    return;
                }
                if (!driver.TryGetTime(a.stateName, out float time)) return;
                float frame = time * a.animation.length * a.animation.frameRate;
                if (motor.IsApproachingAttack)
                {
                    if (visual.ActiveSlash != null || player.currentHealth != stepHealth)
                        throw new Exception("Thrust effect or damage appeared before frame 32.");
                    if (mode == 0 && !shifted && frame > 10)
                    { target.transform.position += brain.transform.right * 1.5f; shifted = true; }
                    if (frame >= 24) CommitDash();
                    return;
                }
                if (!dashCaptured) throw new Exception("Dash started before the test captured its frame-32 commitment.");
                if (mode == 0)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(motor.NavigationPosition - start, Vector3.up);
                    if (Vector3.Cross(delta, heading).magnitude > .15f) throw new Exception("Dash curved after commitment.");
                    if (Quaternion.Angle(rotation, brain.transform.rotation) > .1f) throw new Exception("Dash followed the dodging player.");
                }
                if (frame >= 41 && visual.ActiveSlash != null) throw new Exception("Thrust remained visible after frame 41.");
                return;
            }
            if (mode == 0)
            {
                VerifyEndpoint();
                Check(step == 4 && player.currentHealth == healthBefore, "Four committed thrusts finish and sideways dodges avoid damage");
                mode = 1; step = 0; StartSequence(); return;
            }
            if (mode == 1)
            {
                VerifyEndpoint();
                Check(step == 4 && player.currentHealth < healthBefore, "All four thrusts hit a player staying in their forward lanes");
                Check(observedGaps == 6, "Both four-thrust sequences wait one second in each of their three gaps");
                mode = 2; step = 0; StartSequence();
                ProbePhysicalKnockback();
                brain.Interrupt();
                interruptedPosition = motor.NavigationPosition; interruptedAt = Time.time;
                Check(brain.CurrentAttack == null && !motor.IsApproachingAttack, "Interruption cancels the approach and pending dash");
                typeof(EnemyBrain).GetField("standbyUntil", Private).SetValue(brain, Time.time + 60f);
                mode = 3; return;
            }
            if (mode == 3 && Time.time - interruptedAt > 1.5f)
            {
                Check(step == 1 && Vector3.Distance(motor.NavigationPosition, interruptedPosition) < .05f,
                    "An interrupted attack cannot move or launch stale follow-ups");
                // Warp/manual-timeline probes run after the live sequence so their pending engine
                // synchronization cannot contaminate the real-time pause measurement.
                brain.AttackStarted -= OnStep;
                ProbeFixedDistance();
                ProbeStartingOutsideRange();
                ProbeRepeatSettings();
                Finish(true, "Live approach, fixed-distance dash, frame 32-41 elemental thrusts, red warnings, per-hit knockback and interruption verified.");
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void CommitDash()
    {
        var a = brain.CurrentAttack;
        Check(visual.ActiveSlash == null, "Step " + step + " has no thrust visual during approach");
        if (mode == 0 && step == 1)
            Check(Vector3.Distance(motor.NavigationPosition, stepStart) > .05f, "Enemy runs toward the live player before frame 32");
        Sample(32.01f);
        motor.TickAttackApproach(a, 32.01f / (a.animation.length * a.animation.frameRate), target, brain.profile, 1);
        start = (Vector3)Field(motor, "attackMoveStart"); heading = (Vector3)Field(motor, "attackMoveDirection");
        distance = (float)Field(motor, "attackMoveDistance"); rotation = brain.transform.rotation;
        Check(Vector3.Angle(heading, Vector3.ProjectOnPlane(target.GroundPosition - start, Vector3.up)) < .1f,
            "Step " + step + " aims at the player's frame-32 position");
        dashCaptured = true;
        if (mode == 0) { target.transform.position += Vector3.Cross(Vector3.up, heading) * 4f; Physics.SyncTransforms(); }
        int before = player.currentHealth;
        Call(brain, "TickAttack"); Call(visual, "LateUpdate");
        Check(visual.ActiveSlash != null && visual.ActiveSlash.IsThrust, "Step " + step + " shows the long thrust at frame 32");
        var expected = Array.Find(a.elementalSlashPrefabs, p => p.element == element.CurrentElement);
        Check(visual.ActiveSlash.element == element.CurrentElement && visual.ActiveSlash.color == expected.color,
            "Thrust uses the " + element.CurrentElement + " gem's existing colour and particle prefab");
        var warning = brain.transform.Find("Attack warning rings");
        var line = warning != null ? warning.GetComponentInChildren<LineRenderer>() : null;
        Check(warning != null && warning.gameObject.activeSelf && line.startColor.r > .9f && line.startColor.g < .1f && !a.canBeParried,
            "Step " + step + " displays a red unparryable warning");
        if (mode == 1)
        {
            Check(player.currentHealth < before && target.KnockbackRemaining > 0,
                "Step " + step + " deals damage and adds a fresh knockback impulse");
        }
    }
    static void StartSequence()
    {
        int index = Array.FindIndex(brain.profile.attacks, a => a != null && a.stateName == "EnemySpecialAttack3");
        Check(index >= 0 && brain.profile.attacks[index].IsUsable, "Special Attack 3 remains usable");
        Call(brain, "BeginAttack", index);
    }
    static void OnStep(EnemyAttackDefinition attack)
    {
        try { ObserveStep(attack); }
        catch (Exception e) { callbackFailure = e.ToString(); }
    }
    static void ObserveStep(EnemyAttackDefinition attack)
    {
        if (!attack.stateName.StartsWith("EnemySpecialAttack3")) return;
        if (step > 0 && mode < 2) VerifyEndpoint();
        if (step > 0 && mode < 2)
        {
            Check(pauseObserved && Time.time - observedPauseStart >= brain.CurrentSequence.repeatDelay - .01f,
                "One-second idle pause completes before thrust " + (step + 1));
            observedGaps++;
        }
        pauseObserved = false;
        step++; if (step > 4) throw new Exception("Unexpected fifth attack step.");
        dashCaptured = shifted = false; stepStart = motor.NavigationPosition;
        element.element = (ElementType)(1 + (step - 1) % 4);
        if (mode == 1)
        {
            Vector3 towardHome = Vector3.ProjectOnPlane(brain.HomePosition - motor.NavigationPosition, Vector3.up).normalized;
            if (towardHome.sqrMagnitude < .001f) towardHome = brain.transform.forward;
            target.transform.position = motor.NavigationPosition + towardHome * 4f;
        }
        stepHealth = player.currentHealth;
    }
    static void ProbePhysicalKnockback()
    {
        var body = target.GetComponent<CharacterController>();
        // Keep the probe clear of terrain penetration caused by the earlier collider-free teleports.
        target.transform.position = brain.HomePosition + Vector3.up * 2f;
        body.enabled = true;
        Physics.SyncTransforms();
        Vector3 before = target.transform.position;
        bool accepted = target.ReceiveDamage(1, brain, false);
        Call(target, "TickKnockback", .1f);
        Check(Vector3.Dot(target.transform.position - before, brain.transform.forward) > .0001f,
            "Accepted special contact physically pushes the CharacterController along the thrust direction (accepted " + accepted + ", travel " + (target.transform.position - before) + ")");
        Check((bool)typeof(PlayerStateManager).GetProperty("ElementalMovementBlocked", Private).GetValue(target.GetComponent<PlayerStateManager>()),
            "Knockback blocks voluntary movement and combat through the existing control gate");
        body.enabled = false;
    }
    static void VerifyEndpoint()
    {
        Check(dashCaptured && Vector3.Distance(Vector3.ProjectOnPlane(motor.NavigationPosition - start, Vector3.up), heading * distance) < .15f,
            "Step " + step + " finishes its committed straight dash");
    }
    static void Finish(bool success, string detail)
    {
        if (brain != null) brain.AttackStarted -= OnStep;
        string report = (success ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks) + "\n" + detail;
        SessionState.SetString(Key + "Result", report); SessionState.SetBool(Key, false);
        File.WriteAllText("Library/EnemySpecialAttack3Validation.txt", report); Debug.Log(report);
        EditorApplication.ExitPlaymode();
    }
}
