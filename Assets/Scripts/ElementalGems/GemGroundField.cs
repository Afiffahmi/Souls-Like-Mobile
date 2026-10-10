using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalGems
{
    /// <summary>Ground-aligned elemental damage volume. Uses the same GemAttack and enemy damage path as arrows.</summary>
    public sealed class GemGroundField : MonoBehaviour
    {
        [Header("Prefab visuals")]
        public Transform surface;
        public ParticleSystem[] particles;
        [Min(0)] public float particleDensity = 2;
        [Range(16, 400)] public int particleBudgetPerLayer = 120;
        [Tooltip("Keep authored layer budgets and emission ratios (relative to 45 particles/sec). Visual only.")]
        public bool preserveAuthoredParticleRatios;
        [Tooltip("Fade every particle layer with the ground and clear particles when the field ends.")]
        public bool synchronizeParticleFade;
        [Header("Eruption reaction")]
        [Tooltip("Optional outward push on each enemy's first damaging field hit. Zero preserves the normal reaction.")]
        [Min(0)] public float firstHitKnockback;
        GroundFieldSnapshot settings;
        GemAttack attack;
        GemManager owner;
        float age, nextTick, expireTime;
        bool initialized, finished;
        MaterialPropertyBlock block;
        MeshRenderer surfaceRenderer;
        ParticleSystemRenderer[] particleRenderers;
        MaterialPropertyBlock particleBlock;
        static readonly int FadeProperty = Shader.PropertyToID("_Fade");
        readonly HashSet<Enemy> seen = new HashSet<Enemy>();
        readonly HashSet<Enemy> pushedByEruption = new HashSet<Enemy>();
        Collider[] candidates = new Collider[32];
        RaycastHit[] obstacles = new RaycastHit[16];
        static readonly List<GemGroundField> active = new List<GemGroundField>();
        public ElementType Element => attack != null ? attack.element : ElementType.Normal;
        public float Radius => settings != null ? settings.radius : 0;
        public bool IsDamaging => initialized && !finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => active.Clear();

        public static GemGroundField Spawn(GroundFieldSnapshot data, GemAttack gem, GemManager source, Vector3 point, Vector3 normal, Scene scene)
        {
            if (data == null || data.prefab == null || normal.y < .25f) return null;
            active.RemoveAll(f => f == null || !f.IsDamaging);
            int owned = 0; GemGroundField oldest = null;
            foreach (var field in active)
            {
                if (field.owner != source || field.gameObject.scene != scene) continue;
                owned++; if (oldest == null || field.expireTime < oldest.expireTime) oldest = field;
                if (field.Element == gem.element && Vector3.Dot(field.transform.up, normal.normalized) > .9f &&
                    Vector3.Distance(field.transform.position, point) < data.radius * data.mergeFraction)
                {
                    // Keep one ticking zone per nearby same-element volley; each arrow still has its impact burst.
                    field.expireTime = Mathf.Max(field.expireTime, Time.time + data.duration);
                    return field;
                }
            }
            if (owned >= data.maxFields && oldest != null) oldest.Finish();
            var effect = Instantiate(data.prefab, point + normal.normalized * .035f, Quaternion.FromToRotation(Vector3.up, normal.normalized));
            SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
            effect.name = gem.element + " Bow Ground Field";
            effect.Initialize(data, gem, source);
            return effect;
        }
        void Initialize(GroundFieldSnapshot data, GemAttack gem, GemManager source)
        {
            settings = data; attack = gem; owner = source; initialized = true;
            expireTime = Time.time + data.duration; nextTick = Time.time + data.tickInterval;
            if (synchronizeParticleFade)
            {
                particleRenderers = new ParticleSystemRenderer[particles.Length];
                particleBlock = new MaterialPropertyBlock();
                for (int i = 0; i < particles.Length; i++)
                    if (particles[i] != null) particleRenderers[i] = particles[i].GetComponent<ParticleSystemRenderer>();
            }
            if (surface != null)
            {
                surface.localScale = new Vector3(data.radius, 1, data.radius);
                surfaceRenderer = surface.GetComponent<MeshRenderer>(); block = new MaterialPropertyBlock();
            }
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.maxParticles = preserveAuthoredParticleRatios ? Mathf.Min(particleBudgetPerLayer, main.maxParticles) : particleBudgetPerLayer;
                var shape = ps.shape; shape.radius = data.radius * .96f;
                var emission = ps.emission;
                float layerRatio = preserveAuthoredParticleRatios ? Mathf.Max(0, emission.rateOverTime.constant / 45f) : 1;
                emission.rateOverTime = Mathf.Min(main.maxParticles * .7f, Mathf.PI * data.radius * data.radius * particleDensity * layerRatio);
                ps.Play(false);
                // Populate the area immediately; a freshly landed arrow should visibly ignite the floor.
                ps.Simulate(.35f, false, false, true); ps.Play(false);
            }
            active.Add(this);
            if (synchronizeParticleFade) SetVisualFade(0);
        }
        void Update()
        {
            if (!initialized) return;
            age += Time.deltaTime;
            if (!finished)
            {
                // Aggregate elapsed ticks after a hitch rather than performing an unbounded physics loop.
                float until = Mathf.Min(Time.time, expireTime);
                if (nextTick <= until)
                {
                    int ticks = 1 + Mathf.FloorToInt((until - nextTick) / settings.tickInterval);
                    Damage(settings.damagePerSecond * settings.tickInterval * ticks);
                    nextTick += ticks * settings.tickInterval;
                }
                if (Time.time >= expireTime) Finish();
            }
            float fade = finished ? 0 : Mathf.Min(Mathf.Clamp01(age / .15f), Mathf.Clamp01((expireTime - Time.time) / .3f));
            SetVisualFade(fade);
        }
        void SetVisualFade(float fade)
        {
            if (surfaceRenderer != null)
            {
                block.SetFloat(FadeProperty, fade); block.SetFloat("_FieldAge", age); surfaceRenderer.SetPropertyBlock(block);
            }
            if (!synchronizeParticleFade || particleRenderers == null) return;
            foreach (var renderer in particleRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(particleBlock);
                particleBlock.SetFloat(FadeProperty, fade);
                renderer.SetPropertyBlock(particleBlock);
            }
        }
        void Damage(float baseDamage)
        {
            if (baseDamage <= 0) return;
            var physics = gameObject.scene.GetPhysicsScene();
            Vector3 origin = transform.position, up = transform.up;
            int count;
            while ((count = physics.OverlapCapsule(origin, origin + up * settings.height, settings.radius, candidates, settings.enemyLayers, QueryTriggerInteraction.Collide)) == candidates.Length)
                System.Array.Resize(ref candidates, candidates.Length * 2);
            seen.Clear();
            for (int i = 0; i < count; i++)
            {
                var collider = candidates[i];
                var enemy = collider.GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || seen.Contains(enemy) ||
                    (owner != null && enemy.transform.IsChildOf(owner.transform))) continue;
                Vector3 nearest = collider.ClosestPoint(origin + up * .15f);
                Vector3 delta = nearest - origin; float height = Vector3.Dot(delta, up);
                if (height < -.2f || height > settings.height || Vector3.ProjectOnPlane(delta, up).sqrMagnitude > settings.radius * settings.radius) continue;
                Vector3 target = collider.bounds.center;
                if (settings.lineOfSight && Blocked(physics, origin + up * .2f, target, enemy)) continue;
                seen.Add(enemy);
                bool eruption = firstHitKnockback > 0 && !pushedByEruption.Contains(enemy);
                var outward = Vector3.ProjectOnPlane(target - origin, Vector3.up);
                if (outward.sqrMagnitude < .0001f) outward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                // Route the eruption through the same hit: it can now prime an ordinary enemy.
                int dealt = ElementalDamage.Hit(attack, baseDamage, owner, enemy, target, outward,
                    knockbackDurationMultiplier: settings.knockbackDurationMultiplier, additionalKnockback: eruption ? firstHitKnockback : 0);
                if (eruption && dealt > 0) pushedByEruption.Add(enemy);
            }
        }
        bool Blocked(PhysicsScene physics, Vector3 start, Vector3 end, Enemy target)
        {
            Vector3 delta = end - start; int count;
            if (delta.sqrMagnitude < .001f) return false;
            while ((count = physics.Raycast(start, delta.normalized, obstacles, delta.magnitude, settings.obstacleLayers, QueryTriggerInteraction.Ignore)) == obstacles.Length)
                System.Array.Resize(ref obstacles, obstacles.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var c = obstacles[i].collider;
                if (c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<PlayerStateManager>() != null) continue;
                if (!c.transform.IsChildOf(transform)) return true;
            }
            return false;
        }
        void Finish()
        {
            if (finished) return;
            finished = true; active.Remove(this);
            if (synchronizeParticleFade) SetVisualFade(0);
            foreach (var ps in particles) if (ps != null)
                ps.Stop(false, synchronizeParticleFade ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            if (surface != null) surface.gameObject.SetActive(false);
            Destroy(gameObject, 2);
        }
        void OnDisable() { active.Remove(this); }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            float r = settings != null ? settings.radius : 1;
            Vector3 previous = transform.position + transform.right * r;
            for (int i = 1; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                Vector3 next = transform.position + (transform.right * Mathf.Cos(a) + transform.forward * Mathf.Sin(a)) * r;
                Gizmos.DrawLine(previous, next); previous = next;
            }
        }
    }
}
