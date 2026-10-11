using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SoulsLike.Enemies
{
    /// <summary>Per-summoner ownership and capacity. Shared action assets hold no runtime army state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Enemy))]
    public sealed class EnemySummoner : MonoBehaviour
    {
        private struct Soldier { public Enemy health; public bool dismiss; public float expires; }
        private readonly List<Soldier> soldiers = new List<Soldier>(EnemySummonArmyAction.MaximumArmySize);
        private Enemy owner;
        public int LivingCount
        {
            get { RemoveDead(); return soldiers.Count; }
        }
        private void Awake() => owner = GetComponent<Enemy>();
        private void OnEnable() { owner.OnDeath += Dismiss; }
        private void OnDisable() { if (owner != null) owner.OnDeath -= Dismiss; Dismiss(); }
        private void RemoveDead()
        {
            for (int i = soldiers.Count - 1; i >= 0; i--)
                if (soldiers[i].health == null || soldiers[i].health.IsDead) soldiers.RemoveAt(i);
        }
        private void Update()
        {
            for (int i = soldiers.Count - 1; i >= 0; i--)
            {
                var soldier = soldiers[i];
                if (soldier.health != null && !soldier.health.IsDead && soldier.expires > 0 && Time.time >= soldier.expires)
                    soldier.health.ForceKill();
            }
            RemoveDead();
        }
        private void Dismiss()
        {
            foreach (var soldier in soldiers)
                if (soldier.dismiss && soldier.health != null && !soldier.health.IsDead) soldier.health.ForceKill();
            RemoveDead();
        }
        public void Spawn(EnemySummonArmyAction settings, EnemyAttackContext context)
        {
            if (!settings.IsConfigured || owner == null || owner.IsDead || !isActiveAndEnabled) return;
            int slots = Mathf.Clamp(settings.maxAlive, 1, EnemySummonArmyAction.MaximumArmySize) - LivingCount;
            int count = Mathf.Min(Mathf.Clamp(settings.spawnCount, 1, EnemySummonArmyAction.MaximumArmySize), slots);
            if (count <= 0) return;
            var templateAgent = settings.armyPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = templateAgent.agentTypeID, areaMask = templateAgent.areaMask };
            if (!NavMesh.SamplePosition(transform.position, out var origin, 1f, filter)) return;
            float scale = Mathf.Max(Mathf.Abs(settings.armyPrefab.transform.localScale.x), Mathf.Abs(settings.armyPrefab.transform.localScale.z));
            float radius = Mathf.Max(.1f, templateAgent.radius * scale);
            float height = Mathf.Max(radius * 2, templateAgent.height * Mathf.Abs(settings.armyPrefab.transform.localScale.y));
            float spacing = Mathf.Max(settings.minimumSpacing, radius * 2 + .1f);
            float ring = Mathf.Max(settings.spawnRadius, spacing * 1.1f);
            float phase = Random.Range(0, 360f);
            // Equally spaced sectors, with bounded retries. Never fall back to spawning at the boss origin.
            for (int slot = 0; slot < count; slot++)
            {
                for (int attempt = 0; attempt < Mathf.Clamp(settings.attemptsPerSoldier, 1, 8); attempt++)
                {
                    float angle = (phase + slot * 360f / count + (attempt % 3 - 1) * 12f) * Mathf.Deg2Rad;
                    float distance = ring + (attempt / 3) * spacing;
                    Vector3 candidate = origin.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * distance;
                    if (!NavMesh.SamplePosition(candidate, out var point, Mathf.Clamp(settings.navMeshSampleRadius, .1f, 1), filter)) continue;
                    if (Mathf.Abs(point.position.y - origin.position.y) > 1f ||
                        NavMesh.Raycast(origin.position, point.position, out _, filter)) continue;
                    if ((point.position - context.source.HomePosition).sqrMagnitude > context.source.profile.chaseRadius * context.source.profile.chaseRadius) continue;
                    if (Vector3.Distance(point.position, transform.position) < spacing) continue;
                    bool crowded = false;
                    foreach (var soldier in soldiers)
                        if (soldier.health != null && Vector3.Distance(point.position, soldier.health.transform.position) < spacing) { crowded = true; break; }
                    if (crowded) continue;
                    Vector3 bottom = point.position + Vector3.up * (radius + .08f);
                    Vector3 top = point.position + Vector3.up * (height - radius + .08f);
                    if (Physics.CheckCapsule(bottom, top, radius, settings.spawnBlockingLayers, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 facing = context.target != null ? context.target.GroundPosition - point.position : transform.forward;
                    facing.y = 0;
                    var minion = Instantiate(settings.armyPrefab, point.position,
                        facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : transform.rotation);
                    minion.name = settings.armyPrefab.name + " (Summoned)";
                    minion.targetOverride = context.target;
                    // Activate supports inactive prefab templates; target is assigned before Start acquires it.
                    minion.gameObject.SetActive(true);
                    soldiers.Add(new Soldier { health = minion.GetComponent<Enemy>(), dismiss = settings.dismissOnSummonerDeath,
                        expires = settings.lifetimeSeconds > 0 ? Time.time + settings.lifetimeSeconds : 0 });
                    break;
                }
            }
        }
    }
}
