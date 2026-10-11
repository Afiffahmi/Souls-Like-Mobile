using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Animation Actions/Summon Army")]
    public sealed class EnemySummonArmyAction : EnemyAnimationAction
    {
        public const int MaximumArmySize = 5;
        [Tooltip("A separate minion prefab with its own health, profile, basic attacks and aura.")]
        public EnemyBrain armyPrefab;
        [Range(1, MaximumArmySize)] public int spawnCount = 5;
        [Tooltip("Living soldiers across ALL summon actions on this enemy. Never more than five.")]
        [Range(1, MaximumArmySize)] public int maxAlive = 5;
        [Min(1)] public float spawnRadius = 3f;
        [Min(.1f)] public float minimumSpacing = 1.2f;
        [Tooltip("How far a ring position can adjust to the NavMesh. Blocked positions are skipped.")]
        [Range(.1f, 1f)] public float navMeshSampleRadius = .5f;
        [Range(1, 8)] public int attemptsPerSoldier = 6;
        public LayerMask spawnBlockingLayers = ~0;
        [Tooltip("Zero keeps soldiers alive until killed or dismissed with the summoner.")]
        [Min(0)] public float lifetimeSeconds;
        public bool dismissOnSummonerDeath = true;
        public override bool IsConfigured => armyPrefab != null && armyPrefab.profile != null &&
            armyPrefab.GetComponent<Enemy>() != null && armyPrefab.GetComponent<NavMeshAgent>() != null;
        public override bool CanStart(EnemyBrain source)
        {
            if (!base.CanStart(source)) return false;
            var motor = source.GetComponent<EnemyNavMeshMotor>();
            if (motor == null || !motor.Ready) return false;
            var army = source.GetComponent<EnemySummoner>();
            return army == null || army.LivingCount < Mathf.Clamp(maxAlive, 1, MaximumArmySize);
        }
        public override void Execute(EnemyAttackContext context)
        {
            if (!CanStart(context.source)) return;
            var summoner = context.source.GetComponent<EnemySummoner>();
            if (summoner == null) summoner = context.source.gameObject.AddComponent<EnemySummoner>();
            summoner.Spawn(this, context);
        }
    }
}
