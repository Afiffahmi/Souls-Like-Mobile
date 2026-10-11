using UnityEngine;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Enemy Profile")]
    public sealed class EnemyProfile : ScriptableObject
    {
        [Header("Perception and home territory (world metres)")]
        [Tooltip("Distance from the enemy at which a player can be acquired, including while returning home.")]
        [Min(.1f)] public float detectionRadius = 12;
        [Tooltip("Home territory radius. Keep pursuing inside this radius around spawn; return home when the player leaves it.")]
        [Min(.1f)] public float chaseRadius = 22;
        [Min(.05f)] public float detectionInterval = .25f;
        [Tooltip("Require sight to acquire a new target. Existing targets are pursued around obstacles while inside the home territory. Attacks still require a clear line of sight.")]
        public bool requireLineOfSight = true;
        public LayerMask obstructionLayers = ~0;
        [HideInInspector, Min(.1f)] public float lostSightGrace = 3; // Retained for existing assets; pursuit is range-based.
        [Header("Movement")]
        [Min(.1f)] public float movementSpeed = 2.8f;
        [Min(.1f)] public float acceleration = 10;
        [Min(1)] public float rotationSpeed = 420;
        [Min(.05f)] public float repathInterval = .25f;
        [Min(.05f)] public float homeTolerance = .25f;
        [InspectorName("Stuck Path Retry Seconds")]
        [Tooltip("Reset and retry a blocked path after this interval, without giving up an in-range target.")]
        [Min(1)] public float unreachableTimeout = 5;
        [Header("Combat")]
        [Min(0)] public float globalAttackCooldown = .65f;
        public bool avoidRepeatingAttack = true;
        public EnemyAttackDefinition[] attacks = new EnemyAttackDefinition[0];
        public EnemyAttackSelector selector;
        [Header("Random specials after health loss")]
        public bool healthLossSpecialsEnabled;
        [Tooltip("Actual HP, not percent. Each threshold is consumed once per life. Healing does not rearm it. Completed hit reactions and reset-position jumps take priority.")]
        [Min(1)] public int healthLossSpecialInterval = 25;
        [Header("Reset Position")]
        public bool resetPositionEnabled;
        [Tooltip("Health lost between jump-backs. 25 triggers at 75%, 50%, and 25% remaining. Each threshold is consumed once per life; healing does not rearm it.")]
        [Range(1, 100)] public float resetPositionHealthInterval = 25f;
        public AnimationClip resetPositionAnimation;
        [Min(.05f)] public float resetPositionPlaybackSpeed = 1f;
        [InspectorName("Jump Start Frame")]
        [Tooltip("Clip frame where travel toward spawn begins. Uses the animation's own frame rate.")]
        [Min(0)] public float resetPositionJumpStartFrame = 2f;
        [InspectorName("Jump End Frame")]
        [Tooltip("Landing frame: travel reaches spawn here, then stays still for the rest of the animation.")]
        [Min(1)] public float resetPositionJumpEndFrame = 16f;
        [Tooltip("Brief idle recovery at spawn before normal AI resumes.")]
        [Min(0)] public float resetPositionRecovery = 1f;
        [Header("Animations (replace placeholders, then rebuild controller)")]
        public AnimationClip idle, walk, run, hit, die;
        [Header("Spawn entrance (optional)")]
        [Tooltip("Play once on spawn before detection, walking or attacks. Leave empty for ordinary enemies. Hit reactions wait until this finishes; damage and death still apply.")]
        public AnimationClip spawnAnimation;
        [Min(.05f)] public float spawnPlaybackSpeed = 1f;
        [Min(.01f)] public float transitionDuration = .12f;
        [Min(.1f)] public float hitFallbackDuration = .5f;
        [InspectorName("Hit Fallback Milliseconds")]
        [Tooltip("Used only when no playable Hit clip exists. 100 = 0.1 second; zero uses Hit Fallback Duration. A configured Hit clip always plays to completion. Elemental stun can hold the enemy longer.")]
        [Min(0)] public float hitReactionMilliseconds;
        [Tooltip("Zero keeps the corpse. Positive values destroy it after this many seconds.")]
        [Min(0)] public float corpseLifetime;
        private void OnValidate()
        {
            detectionRadius = Mathf.Max(.1f, detectionRadius);
            chaseRadius = Mathf.Max(detectionRadius, chaseRadius);
            detectionInterval = Mathf.Max(.05f, detectionInterval);
            repathInterval = Mathf.Max(.05f, repathInterval);
            resetPositionJumpStartFrame = Mathf.Max(0, resetPositionJumpStartFrame);
            resetPositionJumpEndFrame = Mathf.Max(resetPositionJumpStartFrame + 1, resetPositionJumpEndFrame);
        }
    }
}
