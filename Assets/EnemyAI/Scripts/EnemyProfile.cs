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
        [Header("Animations (replace placeholders, then rebuild controller)")]
        public AnimationClip idle, walk, run, hit, die;
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
        }
    }
}
