using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalGems
{
    /// <summary>Small runtime-only elemental tells. No colliders or changes to enemy materials.</summary>
    public sealed class ElementalReactionVfx : MonoBehaviour
    {
        enum Shape { Burst, Aura, Link, Roots, Vortex, Shield }
        Shape shape;
        float age, life, radius;
        Color tint;
        Material lineMaterial, particleMaterial;
        Texture2D softTexture;
        LineRenderer[] lines;
        readonly Vector3[] points = new Vector3[33];
        Vector3 linkEnd;

        public static Color Tint(ElementType type)
        {
            switch (type)
            {
                case ElementType.Fire: return new Color(1, .25f, .05f);
                case ElementType.Water: return new Color(.12f, .65f, 1);
                case ElementType.Nature: return new Color(.25f, .95f, .3f);
                case ElementType.Earth: return new Color(.85f, .57f, .2f);
                case ElementType.Lightning: return new Color(.6f, .5f, 1);
                case ElementType.Wind: return new Color(.6f, 1, .9f);
                case ElementType.Darkness: return new Color(.65f, .12f, .95f);
                default: return Color.white;
            }
        }
        public static ElementalReactionVfx Aura(Transform target, ElementType element, GemAttack visual)
        {
            var effect = Create(target.gameObject.scene, target.position, Tint(element), .65f, 0, Shape.Aura, visual);
            effect.transform.SetParent(target, true);
            return effect;
        }
        public static void Burst(Scene scene, Vector3 point, Color color, float radius, GemAttack visual) =>
            Create(scene, point, color, radius, .7f, Shape.Burst, visual);
        public static ElementalReactionVfx Shield(Transform target, GemAttack visual)
        {
            var effect = Create(target.gameObject.scene, target.position, Tint(ElementType.Darkness), 1, 0, Shape.Shield, visual);
            effect.transform.SetParent(target, true);
            return effect;
        }
        public static void Roots(Transform target, GemAttack visual)
        {
            var effect = Create(target.gameObject.scene, target.position, Tint(ElementType.Nature), .65f, 2.5f, Shape.Roots, visual);
            effect.transform.SetParent(target, true);
        }
        public static void Vortex(Scene scene, Vector3 point, float radius, Color color, GemAttack visual) =>
            Create(scene, point, color, radius, 1.8f, Shape.Vortex, visual);
        public static void Link(Scene scene, Vector3 from, Vector3 to, Color color, GemAttack visual)
        {
            var effect = Create(scene, from, color, 1, .4f, Shape.Link, visual);
            effect.linkEnd = to - from;
            effect.Draw();
        }
        static ElementalReactionVfx Create(Scene scene, Vector3 point, Color color, float radius, float lifetime, Shape shape, GemAttack visual)
        {
            var go = new GameObject("Elemental " + shape);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = point;
            var effect = go.AddComponent<ElementalReactionVfx>();
            effect.shape = shape; effect.life = lifetime; effect.radius = radius; effect.tint = color;
            effect.Build(visual); effect.Draw();
            return effect;
        }
        void Build(GemAttack visual)
        {
            var shader = visual != null && visual.trailMaterial != null ? visual.trailMaterial.shader : Shader.Find("ElementalGems/Fantasy Particles");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            lineMaterial = new Material(shader);
            if (lineMaterial.HasProperty("_Tint")) lineMaterial.SetColor("_Tint", Color.white);
            if (lineMaterial.HasProperty("_Glow")) lineMaterial.SetFloat("_Glow", 1.7f);
            int count = shape == Shape.Roots || shape == Shape.Vortex || shape == Shape.Shield ? 3 : 1;
            lines = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Element strand"); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); lines[i] = line;
                line.sharedMaterial = lineMaterial; line.useWorldSpace = false;
                line.positionCount = points.Length;
                line.widthCurve = AnimationCurve.Linear(0, 1, 1, 1);
                line.widthMultiplier = shape == Shape.Aura ? .025f : .07f;
                line.numCornerVertices = 2; line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            if (shape == Shape.Link || shape == Shape.Shield) return;
            var particles = new GameObject("Element motes"); particles.transform.SetParent(transform, false);
            var ps = particles.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = life <= 0; main.duration = life > 0 ? life : 2;
            main.startLifetime = .8f; main.startSpeed = shape == Shape.Burst ? 2 : .35f;
            main.startSize = shape == Shape.Burst ? .25f : .1f; main.startColor = tint;
            main.maxParticles = 64; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission; emission.rateOverTime = shape == Shape.Aura ? 14 : 24;
            if (shape == Shape.Burst) { emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 32) }); }
            var volume = ps.shape; volume.shapeType = ParticleSystemShapeType.Sphere;
            volume.radius = shape == Shape.Burst ? .2f : radius * .7f;
            volume.position = shape == Shape.Aura || shape == Shape.Roots ? Vector3.up : Vector3.zero;
            var colorOverLife = ps.colorOverLifetime; colorOverLife.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(.8f, 0), new GradientAlphaKey(0, 1) });
            colorOverLife.color = gradient;
            softTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            softTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float a = Mathf.Clamp01(1 - new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).sqrMagnitude);
                softTexture.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            softTexture.Apply(false, true);
            particleMaterial = new Material(lineMaterial); particleMaterial.mainTexture = softTexture;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }
        void Update()
        {
            age += Time.deltaTime;
            if (life > 0 && age >= life) { Destroy(gameObject); return; }
            Draw();
        }
        void Draw()
        {
            if (lines == null) return;
            float fade = life > 0 ? Mathf.Clamp01((life - age) / .3f) : .65f;
            for (int strand = 0; strand < lines.Length; strand++)
            {
                Color c = tint; c.a = fade; lines[strand].startColor = lines[strand].endColor = c;
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / (float)(points.Length - 1);
                    if (shape == Shape.Link)
                    {
                        var side = Vector3.Cross(linkEnd.normalized, Vector3.up);
                        points[i] = linkEnd * t + side * (Mathf.Sin(i * 8.3f + age * 35) * .18f * Mathf.Sin(t * Mathf.PI));
                        continue;
                    }
                    float angle = t * Mathf.PI * 2 + strand * 2.09f + age * (shape == Shape.Vortex ? -7 : 1.5f);
                    float r = radius, height = .08f;
                    if (shape == Shape.Shield)
                    {
                        float tilt = strand * Mathf.PI / 3 + age * .4f;
                        points[i] = Vector3.up + new Vector3(Mathf.Cos(angle) * Mathf.Cos(tilt), Mathf.Sin(angle), Mathf.Cos(angle) * Mathf.Sin(tilt)) * radius;
                        continue;
                    }
                    if (shape == Shape.Burst) r *= Mathf.Lerp(.15f, 1, Mathf.Clamp01(age / .55f));
                    if (shape == Shape.Roots) { angle += t * 6; height = t * 1.6f; r *= 1 - .4f * t; }
                    if (shape == Shape.Vortex) { angle += t * 7; height = t * 2.2f; r *= .35f + .65f * t; }
                    points[i] = new Vector3(Mathf.Cos(angle) * r, height, Mathf.Sin(angle) * r);
                }
                lines[strand].SetPositions(points);
            }
        }
        void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (softTexture != null) Destroy(softTexture);
        }
    }
}
