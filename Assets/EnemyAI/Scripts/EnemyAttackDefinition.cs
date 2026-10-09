using UnityEngine;

namespace SoulsLike.Enemies
{
    public enum EnemyAttackType { Melee, Ranged, Magic, Special }
    [CreateAssetMenu(menuName = "Enemies/Attack Definition")]
    public sealed class EnemyAttackDefinition : ScriptableObject
    {
        public bool available = true;
        public EnemyAttackType attackType;
        public string stateName = "Attack1";
        public AnimationClip animation;
        [Min(.05f)] public float playbackSpeed = 1;
        [Min(0)] public float minimumRange;
        [Min(.1f)] public float range = 2.3f;
        [Range(1, 360)] public float arc = 100;
        [Min(0)] public int damage = 15;
        [Min(0)] public float cooldown = 2;
        [Header("Damage timing")]
        [Tooltip("Use exact clip frames instead of normalized Hit Start/End. No damage is delivered after the final frame, even if a slow frame skipped the window.")]
        public bool useFrameWindow;
        [Tooltip("Zero-based frame shown on Unity's clip timeline, using this clip's frame rate.")]
        [Min(0)] public int hitStartFrame;
        [Min(0)] public int hitEndFrame;
        [Range(0, 1)] public float hitStart = .4f;
        [Range(0, 1)] public float hitEnd = .55f;
        [Range(0, 1)] public float turnUntil = .25f;
        [Min(0)] public float weight = 1;
        public int priority;
        [Range(0, 1)] public float minimumHealthRatio;
        [Range(0, 1)] public float maximumHealthRatio = 1;
        [Tooltip("Stateless reusable effect; runtime state belongs to the enemy/projectile.")]
        public EnemyAttackEffect effect;
        public bool IsUsable => available && animation != null && effect != null && !string.IsNullOrWhiteSpace(stateName) && weight > 0;
        public bool IsInsideFrameWindow(float normalizedTime)
        {
            if (animation == null || animation.length <= 0 || animation.frameRate <= 0) return false;
            float frame = normalizedTime * animation.length * animation.frameRate;
            return frame >= hitStartFrame && frame <= hitEndFrame;
        }
        private void OnValidate()
        {
            range = Mathf.Max(.1f, range); minimumRange = Mathf.Clamp(minimumRange, 0, range);
            hitEnd = Mathf.Max(hitStart, hitEnd); maximumHealthRatio = Mathf.Max(minimumHealthRatio, maximumHealthRatio);
            playbackSpeed = Mathf.Max(.05f, playbackSpeed);
            hitStartFrame = Mathf.Max(0, hitStartFrame); hitEndFrame = Mathf.Max(hitStartFrame, hitEndFrame);
        }
    }
}
