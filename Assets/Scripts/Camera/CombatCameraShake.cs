using Unity.Cinemachine;
using UnityEngine;

/// <summary>Short combat jolts on the output camera, after Cinemachine has finished framing.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(10000)]
public sealed class CombatCameraShake : MonoBehaviour
{
    [Tooltip("Overall combat shake strength. Zero disables the effect.")]
    [Range(0, 2)] public float intensity = 1f;
    [Min(0)] public float lightAttackStrength = .045f;
    [Min(0)] public float heavyAttackStrength = .09f;
    [Min(0)] public float specialAttackStrength = .13f;
    [Tooltip("Successful knockback is deliberately stronger than an ordinary swing.")]
    [Min(0)] public float knockbackStrength = .28f;
    [Tooltip("Maximum world-space shake at the default isometric zoom, including overlapping impacts.")]
    [Min(0)] public float maximumDisplacement = .38f;

    struct Pulse { public double start; public float strength, duration, phase; }
    readonly Pulse[] pulses = new Pulse[8];
    Camera output;
    CinemachineBrain brain;
    Vector3 basePosition, appliedPosition;
    Quaternion baseRotation, appliedRotation;
    bool applied;
    int sequence;

    public static void PlayerAttack(Vector3 position, CombatAttackInput attack, float multiplier = 1f)
    {
        var receiver = Receiver(position, out float visibility);
        if (receiver == null) return;
        float strength = attack == CombatAttackInput.SpecialAttack ? receiver.specialAttackStrength :
            attack == CombatAttackInput.HeavyAttack ? receiver.heavyAttackStrength : receiver.lightAttackStrength;
        receiver.AddPulse(strength * multiplier * visibility, attack == CombatAttackInput.LightAttack ? .13f : .2f);
    }

    public static void EnemyAttack(Vector3 position, bool heavy, float multiplier = 1f)
    {
        var receiver = Receiver(position, out float visibility);
        if (receiver != null)
            receiver.AddPulse((heavy ? receiver.specialAttackStrength : receiver.heavyAttackStrength) * multiplier * visibility,
                heavy ? .28f : .18f);
    }

    public static void Knockback(Vector3 position, float effectiveStrength)
    {
        var receiver = Receiver(position, out float visibility);
        if (receiver == null) return;
        float weight = Mathf.InverseLerp(0, 10, effectiveStrength);
        receiver.AddPulse(receiver.knockbackStrength * Mathf.Lerp(.8f, 1.3f, weight) * visibility,
            Mathf.Lerp(.26f, .38f, weight));
    }

    static CombatCameraShake Receiver(Vector3 position, out float visibility)
    {
        visibility = 0;
        if (!Application.isPlaying) return null;
        var camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled) return null;
        Vector3 viewport = camera.WorldToViewportPoint(position);
        if (viewport.z <= 0) return null;
        float edge = Mathf.Max(Mathf.Abs(viewport.x - .5f), Mathf.Abs(viewport.y - .5f));
        visibility = 1f - Mathf.InverseLerp(.5f, .7f, edge);
        if (visibility <= 0) return null;
        if (!camera.TryGetComponent<CombatCameraShake>(out var receiver))
            receiver = camera.gameObject.AddComponent<CombatCameraShake>();
        return receiver.isActiveAndEnabled ? receiver : null;
    }

    void Awake() { output = GetComponent<Camera>(); brain = GetComponent<CinemachineBrain>(); }
    void OnEnable() => CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        RestorePose();
        System.Array.Clear(pulses, 0, pulses.Length);
    }

    void AddPulse(float strength, float duration)
    {
        if (strength <= 0 || float.IsNaN(strength) || float.IsInfinity(strength)) return;
        double now = Time.timeAsDouble;
        int slot = 0;
        float weakest = float.MaxValue;
        for (int i = 0; i < pulses.Length; i++)
        {
            float remaining = RemainingStrength(pulses[i], now);
            if (remaining < weakest) { slot = i; weakest = remaining; }
        }
        if (weakest > strength) return;
        // Deterministic phases do not consume Unity's gameplay random stream.
        pulses[slot] = new Pulse { start = now, strength = strength, duration = duration, phase = ++sequence * 2.39996f };
    }

    static float RemainingStrength(Pulse pulse, double now)
    {
        if (pulse.duration <= 0) return 0;
        float remaining = Mathf.Clamp01(1f - (float)(now - pulse.start) / pulse.duration);
        return pulse.strength * remaining * remaining;
    }

    void Update() => RestorePose();
    void LateUpdate()
    {
        if (brain == null || !brain.isActiveAndEnabled) ApplyShake();
    }
    void OnCameraUpdated(CinemachineBrain updatedBrain)
    {
        if (updatedBrain.OutputCamera != output) return;
        // Cinemachine just wrote a fresh base pose, including any blend or lock-on change.
        applied = false;
        ApplyShake();
    }

    void ApplyShake()
    {
        if (Time.timeScale <= 0 || intensity <= 0) return;
        double now = Time.timeAsDouble;
        Vector2 offset = Vector2.zero;
        float roll = 0;
        foreach (var pulse in pulses)
        {
            float strength = RemainingStrength(pulse, now);
            if (strength <= 0) continue;
            float t = (float)(now - pulse.start);
            offset += new Vector2(Mathf.Sin(t * 121 + pulse.phase), Mathf.Cos(t * 97 + pulse.phase)) * strength;
            roll += Mathf.Sin(t * 83 + pulse.phase) * strength * 3f;
        }
        offset = Vector2.ClampMagnitude(offset * intensity, maximumDisplacement);
        roll = Mathf.Clamp(roll * intensity, -.9f, .9f);
        if (offset.sqrMagnitude < .00000001f && Mathf.Abs(roll) < .0001f) return;
        basePosition = transform.position; baseRotation = transform.rotation;
        float zoom = output.orthographic ? output.orthographicSize / 8f : 1f;
        appliedPosition = basePosition + baseRotation * new Vector3(offset.x, offset.y, 0) * zoom;
        appliedRotation = baseRotation * Quaternion.Euler(0, 0, roll);
        transform.SetPositionAndRotation(appliedPosition, appliedRotation);
        applied = true;
    }

    void RestorePose()
    {
        if (!applied) return;
        // A camera cut or another controller may already have replaced the pose.
        if ((transform.position - appliedPosition).sqrMagnitude < .00000001f &&
            Quaternion.Angle(transform.rotation, appliedRotation) < .001f)
            transform.SetPositionAndRotation(basePosition, baseRotation);
        applied = false;
    }
}
