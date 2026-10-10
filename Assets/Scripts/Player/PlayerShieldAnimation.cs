using UnityEngine;

/// <summary>Opens the attached shield during the player's existing parry/defence lifecycle.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public sealed class PlayerShieldAnimation : MonoBehaviour
{
    [SerializeField] private PlayerStateManager player;
    [Tooltip("A non-looping clip containing only this shield's blend shape curves.")]
    [SerializeField] private AnimationClip deployAnimation;
    [Min(0.01f)] [SerializeField] private float playbackSpeed = 1f;

    private float playbackTime;
    private bool wasActive;
    private float sampledTime = -1f;

    public float NormalizedTime => deployAnimation != null && deployAnimation.length > 0f
        ? Mathf.Clamp01(playbackTime / deployAnimation.length) : 0f;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerStateManager>();
    }

    private void OnEnable() => ResetPose();

    private void LateUpdate()
    {
        bool active = player != null && player.isActiveAndEnabled &&
            player.anim != null && player.anim.isActiveAndEnabled && player.IsParrying;
        float deltaTime = player != null && player.anim != null &&
            player.anim.updateMode == AnimatorUpdateMode.UnscaledTime
            ? Time.unscaledDeltaTime : Time.deltaTime;
        float animatorSpeed = player != null && player.anim != null
            ? Mathf.Max(0f, player.anim.speed) : 1f;
        UpdatePose(active, deltaTime * animatorSpeed);
    }

    private void UpdatePose(bool active, float deltaTime)
    {
        if (deployAnimation == null) return;
        if (!active || !wasActive) playbackTime = 0f;
        if (active)
            playbackTime = Mathf.Min(playbackTime + Mathf.Max(0f, deltaTime) *
                Mathf.Max(0.01f, playbackSpeed), deployAnimation.length);
        wasActive = active;
        SamplePose();
    }

    private void OnDisable() => ResetPose();

    private void ResetPose()
    {
        playbackTime = 0f;
        wasActive = false;
        sampledTime = -1f;
        SamplePose();
    }

    private void SamplePose()
    {
        if (deployAnimation == null || sampledTime == playbackTime) return;
        // Clamp explicitly so the final pose stays held without looping.
        deployAnimation.SampleAnimation(gameObject, playbackTime);
        sampledTime = playbackTime;
    }
}
