using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Riding
{
    /// <summary>Animated flame tongues and running slipstream, driven by actual horse movement.</summary>
    [DefaultExecutionOrder(120), DisallowMultipleComponent, RequireComponent(typeof(HorseController))]
    public sealed class HorseMotionAura : MonoBehaviour
    {
        public HorseController horse;
        public HorseTailAura gemSource;
        public Transform bodyAnchor, headAnchor;
        [Tooltip("Animated bone segments carrying the thin surface flames.")] public Transform[] contourRoots, contourTips;
        public float[] contourRadii;
        public Material flameMaterial, windMaterial;
        [Header("Flame envelope (horse local metres)")]
        public Vector3 bodyOffset = new Vector3(0, -.42f, -.25f);
        public Vector3 bodyRadius = new Vector3(.52f, .62f, 1.18f);
        [Range(16, 192)] public int flameCount = 168;
        [Range(8, 24)] public int flameSegments = 16;
        [Min(.05f)] public float runFlameLength = .65f;
        [Min(.02f)] public float flameWidth = .2f;
        [Range(.05f, .8f)] public float walkingAura = .3f;
        [Min(.1f)] public float response = 6f;
        [Min(0)] public float pulseAmount = .22f;
        [Min(.1f)] public float flameFlow = 1.65f;
        [Min(0)] public float brightness = 1.6f;
        [Header("Running wind")]
        [Range(4, 24)] public int windCount = 12;
        [Min(.1f)] public float windLength = 3.8f;
        [Min(.005f)] public float windWidth = .035f;
        public Color windColor = new Color(.78f, .9f, 1f, .48f);
        public float AuraAmount { get; private set; }
        public float WindAmount { get; private set; }
        public Color CurrentColor { get; private set; }
        RibbonMesh flames, wind;
        Camera view;
        const int WindSegments = 18;

        void Awake() { Resolve(); }
        void Resolve()
        {
            if (!horse) horse = GetComponent<HorseController>();
            if (!gemSource) gemSource = GetComponent<HorseTailAura>();
        }
        void LateUpdate()
        {
            Resolve();
            if (!view || !view.isActiveAndEnabled) view = Camera.main;
            bool active = horse && horse.isActiveAndEnabled && !horse.IsDead;
            bool running = horse && (horse.Motion == HorseMotion.Run || horse.Motion == HorseMotion.SpecialAttack);
            Color color = gemSource ? gemSource.ElementColor : new Color(.7f, .78f, .88f);
            float visibility = gemSource ? gemSource.Visibility : 1f;
            Tick(horse ? horse.CurrentSpeed : 0, running, active, color, visibility, Time.deltaTime, Time.time, view);
        }
        // Shared with the Editor validation: only actual travel produces an aura.
        public static Vector2 MovementTargets(float speed, float walkSpeed, float runSpeed, float walkAmount, bool running, bool active)
        {
            if (!active || speed <= .06f) return Vector2.zero;
            float walk = Mathf.Max(.1f, walkSpeed);
            float run = Mathf.Max(walk + .1f, runSpeed);
            float moving = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.06f, walk * .55f, speed));
            float sprint = running ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(walk, run, speed)) : 0;
            return new Vector2(Mathf.Lerp(walkAmount, 1, sprint) * moving, sprint);
        }
        void Tick(float speed, bool running, bool active, Color color, float visibility, float dt, float time, Camera camera)
        {
            var target = MovementTargets(speed, horse ? horse.EffectiveWalkSpeed : 3.5f,
                horse ? horse.EffectiveRunSpeed : 8, walkingAura, running, active);
            float blend = 1 - Mathf.Exp(-Mathf.Max(.1f, response) * Mathf.Max(0, dt));
            AuraAmount = Mathf.Lerp(AuraAmount, target.x * Mathf.Clamp01(visibility), blend);
            WindAmount = Mathf.Lerp(WindAmount, target.y * Mathf.Clamp01(visibility), blend);
            if (target.x == 0 && AuraAmount < .002f) AuraAmount = 0;
            if (target.y == 0 && WindAmount < .002f) WindAmount = 0;
            CurrentColor = color;
            if (AuraAmount <= 0 && WindAmount <= 0) { Hide(); return; }
            EnsureMeshes();
            Vector3 eye = camera ? transform.InverseTransformPoint(camera.transform.position) : new Vector3(6, 4, 6);
            Vector3 center = bodyAnchor ? transform.InverseTransformPoint(bodyAnchor.position) + bodyOffset : new Vector3(0, 1.85f, 0);
            if (flames != null)
            {
                flames.renderer.enabled = AuraAmount > 0;
                if (AuraAmount > 0) DrawFlames(center, eye, color, time);
            }
            if (wind != null)
            {
                wind.renderer.enabled = WindAmount > 0;
                if (WindAmount > 0) DrawWind(center, eye, time);
            }
        }
        void EnsureMeshes()
        {
            int count = Mathf.Clamp(flameCount, 16, 192), segments = Mathf.Clamp(flameSegments, 8, 24);
            if (flames != null && (flames.count != count || flames.segments != segments)) { flames.Dispose(); flames = null; }
            if (wind != null && wind.count != Mathf.Clamp(windCount, 4, 24)) { wind.Dispose(); wind = null; }
            if (flames == null && flameMaterial) flames = new RibbonMesh(transform, "Horse Moving Flames", count, segments, flameMaterial);
            if (wind == null && windMaterial) wind = new RibbonMesh(transform, "Horse Running Wind", Mathf.Clamp(windCount, 4, 24), WindSegments, windMaterial);
            if (flames != null) flames.renderer.sharedMaterial = flameMaterial;
            if (wind != null) wind.renderer.sharedMaterial = windMaterial;
        }
        static float Noise01(int n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f, 1);
        void DrawFlames(Vector3 center, Vector3 eye, Color color, float time)
        {
            float t = time * flameFlow;
            int sections = contourRoots != null && contourRoots.Length > 0 ? contourRoots.Length : 21;
            for (int n = 0; n < flames.count; n++)
            {
                float seed = Noise01(n), phase = seed * Mathf.PI * 2;
                int layer = (n / sections) % 3;
                float layerLength = layer == 0 ? .6f : layer == 1 ? .85f : 1.05f;
                // Golden-angle distribution prevents rows or a rigid circular outline.
                float angle = n * 2.399963f;
                float height = Mathf.Lerp(-.88f, .86f, Noise01(n + 191));
                Vector3 radial = new Vector3(Mathf.Cos(angle), height * .7f, Mathf.Sin(angle));
                Vector3 root = center + Vector3.Scale(radial, bodyRadius);
                if (n % 7 == 0 && headAnchor)
                {
                    root = transform.InverseTransformPoint(headAnchor.position) + new Vector3(radial.x * .27f, height * .25f, radial.z * .28f);
                }
                Vector3 outwards = new Vector3(radial.x, height * .28f + .22f, radial.z * .65f).normalized;
                if (contourRoots != null && contourTips != null && contourRadii != null && contourRoots.Length > 0)
                {
                    int section = n % contourRoots.Length;
                    if (section < contourTips.Length && section < contourRadii.Length && contourRoots[section] && contourTips[section])
                    {
                        Vector3 a = transform.InverseTransformPoint(contourRoots[section].position);
                        Vector3 b = transform.InverseTransformPoint(contourTips[section].position);
                        Vector3 axis = (b - a).normalized;
                        Vector3 side = Vector3.Cross(axis, Vector3.forward).normalized;
                        if (side.sqrMagnitude < .01f) side = Vector3.right;
                        Vector3 around = Vector3.Cross(axis, side).normalized;
                        outwards = side * Mathf.Cos(angle) + around * Mathf.Sin(angle);
                        root = Vector3.Lerp(a, b, Noise01(n + 811)) + outwards * contourRadii[section];
                    }
                }
                root += outwards * (layer * runFlameLength * .025f);
                Vector3 sideways = Vector3.Cross(Vector3.up, outwards).normalized;
                float cycle = .5f + .5f * Mathf.Sin(t * 3.7f + phase);
                float breath = 1 + pulseAmount * Mathf.Sin(t * 2.1f + phase * .4f);
                float length = runFlameLength * layerLength * AuraAmount * Mathf.Lerp(.65f, 1.2f, seed) * breath;
                float width = flameWidth * (layer == 2 ? .72f : 1f) * Mathf.Lerp(.65f, 1.25f, Noise01(n + 73)) * Mathf.Sqrt(AuraAmount);
                for (int s = 0; s < flames.segments; s++)
                {
                    float u = s / (float)(flames.segments - 1);
                    // The tip curls back toward the horse while the middle billows out.
                    float outward = Mathf.Sin(u * 2.65f) * (.22f + .14f * cycle);
                    float curl = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(t * 3 + phase + u * 3) * .23f;
                    Vector3 p = root + outwards * (length * outward) + Vector3.up * (length * u * .95f)
                        + sideways * (length * curl) - Vector3.forward * (WindAmount * length * u * u * .5f);
                    float flutter = Mathf.Sin(u * 12 - t * 7 + phase) * .055f * length * u;
                    p += sideways * flutter;
                    float taper = Mathf.Pow(1 - u, .72f) * (.8f + .2f * Mathf.Sin(u * 17 - t * 5 + phase));
                    Color tint = color * brightness;
                    tint.a = AuraAmount * (layer == 0 ? 1f : layer == 1 ? .88f : .72f) * Mathf.Lerp(.4f, .68f, seed) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / .16f)) * (1 - u * u);
                    flames.Set(n, s, p, eye, width * taper, tint, u, phase);
                }
            }
            flames.Upload();
        }
        void DrawWind(Vector3 center, Vector3 eye, float time)
        {
            for (int n = 0; n < wind.count; n++)
            {
                float seed = Noise01(n + 421), phase = seed * 6.283185f;
                float life = Mathf.Repeat(time * Mathf.Lerp(.8f, 1.25f, seed) + seed, 1);
                float envelope = Mathf.Sin(life * Mathf.PI);
                float angle = n * 2.399963f + .16f * Mathf.Sin(time + phase);
                for (int s = 0; s < wind.segments; s++)
                {
                    float u = s / (float)(wind.segments - 1);
                    float z = Mathf.Lerp(2.8f, -3.2f, life) + (u - .5f) * windLength;
                    float bulge = .85f + .3f * Mathf.Exp(-z * z * .28f);
                    Vector3 p = center + new Vector3(Mathf.Cos(angle) * bulge,
                        Mathf.Sin(angle) * bulge * .95f + .12f * Mathf.Sin(z + phase), z);
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), .65f);
                    Color tint = windColor;
                    tint.a *= envelope * WindAmount;
                    wind.Set(n, s, p, eye, windWidth * taper * (.7f + .5f * seed), tint, u, phase);
                }
            }
            wind.Upload();
        }
        void Hide()
        {
            if (flames != null) flames.renderer.enabled = false;
            if (wind != null) wind.renderer.enabled = false;
        }
        void OnDisable() { AuraAmount = WindAmount = 0; Hide(); }
        void OnDestroy() { flames?.Dispose(); wind?.Dispose(); flames = wind = null; }

        sealed class RibbonMesh
        {
            public readonly int count, segments;
            public readonly MeshRenderer renderer;
            readonly GameObject owner;
            readonly Mesh mesh;
            readonly Vector3[] vertices, centers;
            readonly Vector2[] uv;
            readonly Vector2[] variation;
            readonly Color[] colors;
            public RibbonMesh(Transform parent, string name, int count, int segments, Material material)
            {
                this.count = count; this.segments = segments;
                owner = new GameObject(name) { layer = parent.gameObject.layer, hideFlags = HideFlags.DontSave };
                owner.transform.SetParent(parent, false);
                mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
                owner.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = owner.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                vertices = new Vector3[count * segments * 2]; centers = new Vector3[count * segments];
                uv = new Vector2[vertices.Length]; variation = new Vector2[vertices.Length]; colors = new Color[vertices.Length];
                int[] triangles = new int[count * (segments - 1) * 6]; int index = 0;
                for (int n = 0; n < count; n++) for (int s = 0; s < segments - 1; s++)
                {
                    int v = (n * segments + s) * 2;
                    triangles[index++] = v; triangles[index++] = v + 2; triangles[index++] = v + 1;
                    triangles[index++] = v + 1; triangles[index++] = v + 2; triangles[index++] = v + 3;
                }
                mesh.vertices = vertices; mesh.triangles = triangles;
            }
            public void Set(int n, int s, Vector3 p, Vector3 eye, float width, Color tint, float along, float phase)
            {
                int c = n * segments + s, v = c * 2;
                centers[c] = p;
                Vector3 tangent = s > 0 ? p - centers[c - 1] : Vector3.up;
                Vector3 side = Vector3.Cross(tangent, eye - p).normalized;
                if (side.sqrMagnitude < .01f) side = Vector3.right;
                vertices[v] = p - side * width * .5f; vertices[v + 1] = p + side * width * .5f;
                uv[v] = new Vector2(along, 0); uv[v + 1] = new Vector2(along, 1);
                variation[v] = variation[v + 1] = new Vector2(phase, 0);
                colors[v] = colors[v + 1] = tint;
            }
            public void Upload() { mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = variation; mesh.colors = colors; mesh.RecalculateBounds(); }
            public void Dispose()
            {
                if (Application.isPlaying) { Object.Destroy(owner); Object.Destroy(mesh); }
                else { Object.DestroyImmediate(owner); Object.DestroyImmediate(mesh); }
            }
        }
    }
}
