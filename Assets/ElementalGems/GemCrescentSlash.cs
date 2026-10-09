using UnityEngine;

namespace ElementalGems
{
    /// <summary>A short-lived, world-space visual. Never performs hit detection or damage.</summary>
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
        MaterialPropertyBlock properties;
        Vector3 origin;
        Quaternion rotation;
        float age;
        int direction = 1;
        static readonly int Age = Shader.PropertyToID("_Age");
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public float NormalizedAge => age / Mathf.Max(.05f, lifetime);

        void Awake()
        {
            surface = GetComponentInChildren<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            origin = transform.position;
            rotation = transform.rotation;
        }
        public void Initialize(int swingDirection)
        {
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
            properties.SetColor(Tint, color);
            properties.SetFloat(Intensity, intensity);
            properties.SetFloat("_WidthScale", bandWidth);
            properties.SetFloat("_Direction", direction);
            surface.SetPropertyBlock(properties);
            surface.enabled = t < 1;
        }
    }
}
