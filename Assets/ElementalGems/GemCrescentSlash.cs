using UnityEngine;

namespace ElementalGems
{
    /// <summary>A world-space slash that drives melee timing, position, size and facing.</summary>
    public sealed class GemCrescentSlash : MonoBehaviour
    {
        public ElementType element;
        [Min(.05f)] public float lifetime = .46f;
        [Min(.1f)] public float size = 1;
        [Range(.4f, 1.8f)] public float bandWidth = 1;
        [Range(0, 6)] public float intensity = 1.5f;
        [ColorUsage(true, true)] public Color color = Color.cyan;
        [Range(0, 60)] public float sweepRotation = 24;
        [Min(0)] public float forwardDrift = .2f;
        [Header("Particles scattered across the crescent")]
        public ParticleSystem[] accents;
        [Range(0, 48)] public int particlesPerLayer = 16;
        [Min(.1f)] public float particleRadius = 2.3f;
        [Range(30, 210)] public float arcDegrees = 170;
        MeshRenderer surface;
        Bounds authoredBounds;
        MaterialPropertyBlock properties;
        Vector3 origin;
        Quaternion rotation;
        float age;
        int direction = 1;
        GemSlashSettings drivenSettings;
        float drivenProgress;
        int emittedParticles;
        static readonly int Age = Shader.PropertyToID("_Age");
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public float NormalizedAge => drivenSettings != null ? drivenProgress : age / Mathf.Max(.05f, lifetime);

        // Matches the primary Band in GemCrescentBuilder and _WidthScale in CrescentSlash.shader.
        public const float BodyRadius = 2.12f, BodyWidth = 1.52f, BodyArc = 172f;
        public float RevealedSweep => drivenSettings != null ? drivenSettings.Evaluate(drivenProgress) : 0;
        public bool ReverseSweep => direction < 0;
        public bool FullCircle => drivenSettings != null && drivenSettings.fullCircle;
        public bool HasDrivenGeometry => drivenSettings != null && gameObject.activeInHierarchy;
        public Quaternion HitFacing { get; private set; } = Quaternion.identity;

        public void GetLocalHitSpan(float sweep, out Vector3 inner, out Vector3 outer)
        {
            float u = direction < 0 ? 1 - sweep : sweep;
            float angle = (FullCircle ? u * 360f - 270f : (u - .5f) * BodyArc) * Mathf.Deg2Rad;
            float taper = FullCircle ? 1f : Mathf.Pow(Mathf.Max(.001f, Mathf.Sin(u * Mathf.PI)), .7f) * (.7f + .6f * u);
            float halfWidth = .5f * BodyWidth * taper * bandWidth;
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            inner = radial * (BodyRadius - halfWidth);
            outer = radial * (BodyRadius + halfWidth);
        }

        public void MakeNeutral()
        {
            element = ElementType.Normal;
            color = new Color(.85f, .92f, 1f);
            if (accents != null)
                foreach (var ps in accents)
                    if (ps != null) ps.gameObject.SetActive(false);
            accents = System.Array.Empty<ParticleSystem>();
        }

        public void InitializeDriven(GemSlashSettings settings)
        {
            drivenSettings = settings;
            direction = settings.reverseSweep ? -1 : 1;
            drivenProgress = 0; emittedParticles = 0;
            transform.localScale = settings.visualScale * size * settings.size;
            if (surface != null)
            {
                // Shader expands the front crescent into a full ring; include its back half in culling.
                float radius = Mathf.Max(Mathf.Abs(authoredBounds.min.x), Mathf.Abs(authoredBounds.max.x),
                    Mathf.Abs(authoredBounds.min.z), Mathf.Abs(authoredBounds.max.z));
                surface.localBounds = FullCircle ? new Bounds(Vector3.zero, new Vector3(radius * 2, Mathf.Max(2, authoredBounds.size.y), radius * 2)) : authoredBounds;
            }
            if (accents == null) return;
            foreach (var ps in accents)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(false);
            }
        }

        public void SetAnimationFrame(float progress, Vector3 position, Quaternion facing)
        {
            if (drivenSettings == null) return;
            drivenProgress = Mathf.Clamp01(progress);
            // Gameplay facing stays horizontal even when the visual slash plane is tilted.
            HitFacing = facing;
            float sweep = drivenSettings.Evaluate(drivenProgress);
            rotation = facing * Quaternion.Euler(drivenSettings.localEulerAngles);
            transform.position = position + facing * Vector3.forward * (sweep * drivenSettings.forwardDrift);
            transform.rotation = rotation * Quaternion.Euler(0,
                Mathf.LerpUnclamped(drivenSettings.startSweepAngle, drivenSettings.endSweepAngle, sweep), 0);
            if (surface != null)
            {
                properties.SetFloat(Age, drivenProgress);
                properties.SetFloat("_SweepProgress", sweep);
                properties.SetColor(Tint, color);
                properties.SetFloat(Intensity, intensity);
                properties.SetFloat("_WidthScale", bandWidth);
                properties.SetFloat("_Direction", direction);
                properties.SetFloat("_FullCircle", FullCircle ? 1f : 0f);
                surface.SetPropertyBlock(properties);
                surface.enabled = drivenProgress < 1;
            }
            // Accents advance along the same arc instead of appearing across the entire slash at spawn.
            int targetCount = Mathf.CeilToInt(sweep * particlesPerLayer);
            for (; emittedParticles < targetCount; emittedParticles++)
            {
                float u = (emittedParticles + .5f) / Mathf.Max(1, particlesPerLayer);
                if (direction < 0) u = 1 - u;
                float a = (FullCircle ? u * 360f - 270f : (u - .5f) * arcDegrees) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (accents == null) continue;
                foreach (var ps in accents)
                {
                    if (ps == null) continue;
                    ps.Emit(new ParticleSystem.EmitParams {
                        position = transform.TransformPoint(radial * particleRadius),
                        velocity = transform.rotation * radial * .3f,
                        applyShapeToPosition = false
                    }, 1);
                }
            }
        }

        void Awake()
        {
            surface = GetComponentInChildren<MeshRenderer>();
            if (surface != null) authoredBounds = surface.localBounds;
            properties = new MaterialPropertyBlock();
            origin = transform.position;
            rotation = transform.rotation;
        }
        public void Initialize(int swingDirection)
        {
            drivenSettings = null;
            direction = swingDirection < 0 ? -1 : 1;
            origin = transform.position;
            rotation = transform.rotation;
            transform.localScale = Vector3.one * size;
            age = 0;
            ApplyFrame(0);
            foreach (var ps in accents)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(false);
                for (int i = 0; i < particlesPerLayer; i++)
                {
                    float u = (i + Random.value) / Mathf.Max(1, particlesPerLayer);
                    float a = (u - .5f) * arcDegrees * Mathf.Deg2Rad;
                    Vector3 radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = transform.TransformPoint(radial * (particleRadius + Random.Range(-.25f, .2f)) + Vector3.up * Random.Range(-.14f, .14f)),
                        velocity = rotation * (radial * Random.Range(.3f, .85f) + Vector3.up * Random.Range(-.1f, .5f)),
                        applyShapeToPosition = false
                    };
                    ps.Emit(emit, 1);
                }
            }
        }
        void Update()
        {
            if (drivenSettings != null) return;
            age += Time.deltaTime;
            ApplyFrame(NormalizedAge);
            // Allow the leaf, bubble, spark, and smoke tails to finish naturally.
            if (age > lifetime + 1.2f) Destroy(gameObject);
        }
        void ApplyFrame(float t)
        {
            transform.position = origin + rotation * Vector3.forward * (Mathf.Clamp01(t) * forwardDrift);
            transform.rotation = rotation * Quaternion.Euler(0, direction * (Mathf.Clamp01(t) - .5f) * sweepRotation, 0);
            if (surface == null) return;
            properties.SetFloat(Age, Mathf.Clamp01(t));
            properties.SetFloat("_SweepProgress", -1);
            properties.SetColor(Tint, color);
            properties.SetFloat(Intensity, intensity);
            properties.SetFloat("_WidthScale", bandWidth);
            properties.SetFloat("_Direction", direction);
            surface.SetPropertyBlock(properties);
            surface.enabled = t < 1;
        }
    }
}
