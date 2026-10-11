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
        private EnemySummonArmyAction wave;
        private EnemyAttackContext waveContext;
        private EnemyAnimationDriver driver;
        private ElementalGems.ElementType waveInnateElement;
        private GameObject spawnStaging;
        private int waveIndex, waveCount;
        private float nextSpawnFrame, delayFrames, lastCastTime;
        private readonly Vector3[] wavePositions = new Vector3[EnemySummonArmyAction.MaximumArmySize];
        public int PendingCount => Mathf.Max(0, waveCount - waveIndex);
        public int LivingCount
        {
            get { RemoveDead(); return soldiers.Count; }
        }
        private void Awake() => owner = GetComponent<Enemy>();
        private void OnEnable() { owner.OnDeath += Dismiss; }
        private void OnDisable() { if (owner != null) owner.OnDeath -= Dismiss; Dismiss(); }
        private void OnDestroy() { if (spawnStaging != null) Destroy(spawnStaging); }
        private void RemoveDead()
        {
            for (int i = soldiers.Count - 1; i >= 0; i--)
                if (soldiers[i].health == null || soldiers[i].health.IsDead) soldiers.RemoveAt(i);
        }
        private void Update()
        {
            TickWave();
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
            CancelWave();
            foreach (var soldier in soldiers)
                if (soldier.dismiss && soldier.health != null && !soldier.health.IsDead) soldier.health.ForceKill();
            RemoveDead();
        }
        public void Spawn(EnemySummonArmyAction settings, EnemyAttackContext context)
        {
            if (!settings.IsConfigured || owner == null || owner.IsDead || !isActiveAndEnabled) return;
            CancelWave();
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
            waveCount = 0;
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
                    for (int p = 0; p < waveCount; p++)
                        if (Vector3.Distance(point.position, wavePositions[p]) < spacing) { crowded = true; break; }
                    if (crowded) continue;
                    Vector3 bottom = point.position + Vector3.up * (radius + .08f);
                    Vector3 top = point.position + Vector3.up * (height - radius + .08f);
                    if (Physics.CheckCapsule(bottom, top, radius, settings.spawnBlockingLayers, QueryTriggerInteraction.Ignore)) continue;
                    wavePositions[waveCount++] = point.position;
                    break;
                }
            }
            if (waveCount == 0) return;
            // Copy identity, never the boss's temporary infusion or link-up state.
            // Capture once so every soldier in this delayed wave has the same original element.
            var sourceElement = context.source.GetComponent<ElementalGems.ElementalEnemy>();
            waveInnateElement = sourceElement != null ? sourceElement.element : ElementalGems.ElementType.Normal;
            wave = settings; waveContext = context; waveIndex = 0;
            driver = context.source.GetComponent<EnemyAnimationDriver>();
            float totalFrames = context.attack.animation.length * context.attack.animation.frameRate;
            nextSpawnFrame = context.attack.actionFrame;
            float room = Mathf.Max(0, totalFrames - 1 - nextSpawnFrame);
            delayFrames = waveCount > 1 ? Mathf.Min(Mathf.Max(1, settings.spawnDelayFrames), room / (waveCount - 1)) : 0;
            lastCastTime = 0;
            TickWave();
        }
        private void CancelWave() { wave = null; waveCount = waveIndex = 0; }
        private void TickWave()
        {
            if (wave == null) return;
            var source = waveContext.source;
            if (owner == null || owner.IsDead || source == null || !source.isActiveAndEnabled ||
                source.State != EnemyState.Attacking || source.CurrentAttack != waveContext.attack)
            { CancelWave(); return; }
            var control = source.GetComponent<EnemyControlStatus>();
            if (control != null && control.CannotAct) { CancelWave(); return; }
            if (driver == null || !driver.TryGetTime(waveContext.attack.stateName, out float time)) return;
            if (time < lastCastTime) { CancelWave(); return; }
            lastCastTime = time;
            float frame = time * waveContext.attack.animation.length * waveContext.attack.animation.frameRate;
            // Frame-driven delay follows windup speed, playback speed and pauses.
            while (waveIndex < waveCount && frame >= nextSpawnFrame && time < 1f)
            {
                var settings = wave;
                Vector3 point = wavePositions[waveIndex++];
                nextSpawnFrame += delayFrames;
                if (LivingCount >= Mathf.Clamp(settings.maxAlive, 1, EnemySummonArmyAction.MaximumArmySize)) { CancelWave(); return; }
                var agent = settings.armyPrefab.GetComponent<NavMeshAgent>();
                float radius = agent.radius * Mathf.Max(Mathf.Abs(settings.armyPrefab.transform.localScale.x), Mathf.Abs(settings.armyPrefab.transform.localScale.z));
                float height = Mathf.Max(radius * 2, agent.height * Mathf.Abs(settings.armyPrefab.transform.localScale.y));
                // Recheck planned places: actors may have moved there during the casting delay.
                if (Physics.CheckCapsule(point + Vector3.up * (radius + .08f), point + Vector3.up * (height - radius + .08f),
                    radius, settings.spawnBlockingLayers, QueryTriggerInteraction.Ignore)) continue;
                Vector3 facing = waveContext.target != null ? waveContext.target.GroundPosition - point : transform.forward;
                facing.y = 0;
                // An inactive parent delays Awake/OnEnable until identity is assigned, even for active prefabs.
                // Keep this staging root at identity scale so the boss's model scale cannot resize soldiers.
                if (spawnStaging == null)
                {
                    spawnStaging = new GameObject("Summon Initialization (inactive)");
                    spawnStaging.SetActive(false);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(spawnStaging, gameObject.scene);
                }
                var minion = Instantiate(settings.armyPrefab, point,
                    facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : transform.rotation, spawnStaging.transform);
                minion.gameObject.SetActive(false);
                minion.name = settings.armyPrefab.name + " (Summoned)";
                minion.targetOverride = waveContext.target;
                var minionElement = minion.GetComponent<ElementalGems.ElementalEnemy>();
                if (minionElement == null) minionElement = minion.gameObject.AddComponent<ElementalGems.ElementalEnemy>();
                minionElement.element = waveInnateElement;
                if (minion.GetComponent<EnemyControlStatus>() == null)
                    minion.gameObject.AddComponent<ElementalEnemyControl>();
                // From activation onward, only this soldier's own component controls its state and aura.
                minion.transform.SetParent(null, true);
                minion.gameObject.SetActive(true);
                soldiers.Add(new Soldier { health = minion.GetComponent<Enemy>(), dismiss = settings.dismissOnSummonerDeath,
                    expires = settings.lifetimeSeconds > 0 ? Time.time + settings.lifetimeSeconds : 0 });
            }
            if (waveIndex >= waveCount || time >= 1f) CancelWave();
        }
    }
}
