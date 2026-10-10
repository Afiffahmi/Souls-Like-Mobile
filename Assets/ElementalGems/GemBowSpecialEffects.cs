using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalGems
{
    /// <summary>Layered force plume, hot core, interwoven ribbons and three particle depths.</summary>
    public sealed class GemBowSpecialEffects : MonoBehaviour
    {
        const int Segments = 72, Rows = 12, Columns = 10;
        readonly Material[] materials = new Material[5];
        readonly LineRenderer[] ribbons = new LineRenderer[6];
        readonly Vector3[] points = new Vector3[Segments];
        readonly Vector3[] frontPoints = new Vector3[33];
        readonly Vector3[] vertices = new Vector3[(Rows + 1) * (Columns + 1)];
        readonly System.Random random = new System.Random();
        Mesh frontMesh, glowMesh;
        LineRenderer core, coreGlow, trailBody, trailHalo, frontRim, innerRim;
        Transform halo, hotSpot;
        MeshRenderer haloRenderer, hotRenderer;
        MaterialPropertyBlock haloProperties;
        ParticleSystem motes, streaks, wisps;
        BowSpecialAttackSettings settings;
        Camera view;
        Color tint, hot;
        float elapsed, previousDistance, moteDebt, streakDebt, wispDebt, particleScale;
        bool ready;

        public void Initialize(GemAttack attack, BowSpecialAttackSettings settings)
        {
            this.settings = settings;
            tint = attack.element == ElementType.Normal ? new Color(.42f, 1, .10f) : attack.color;
            // Pale gold for normal/nature, a pale version of the equipped element otherwise.
            hot = Color.Lerp(tint, new Color(1, 1, .8f), .72f);
            var template = Resources.Load<Material>("BowSpecialEnergy");
            if (template == null) { enabled = false; return; }
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(template) { name = "Bow energy layer " + i };
                materials[i].SetFloat("_ShapeMode", i);
                materials[i].SetFloat("_Intensity", settings.vfxIntensity);
            }
            view = Camera.main;
            particleScale = settings.vfxParticleDensity * Mathf.Clamp(Mathf.Sqrt(settings.width / 6f), .6f, 2);
            trailHalo = Line("Diffused wake envelope", 3.2f, tint, .13f);
            trailHalo.sharedMaterial = materials[4];
            coreGlow = Line("Saturated energy sheath", 1.65f, tint * 1.25f, .4f);
            trailBody = Line("Flowing plasma body", 1.05f, Color.Lerp(tint,hot,.3f) * 1.6f, .7f);
            trailBody.sharedMaterial = materials[4];
            core = Line("White-hot beam core", .28f, hot * 2f, .9f);
            frontRim = Line("Wide force-front glow", .40f, tint * 1.7f, .5f);
            innerRim = Line("Bright force-front crest", .095f, hot * 2, .85f);
            for (int i = 0; i < ribbons.Length; i++)
            {
                bool broad = i < 2, filament = i >= 2 && i < 4;
                ribbons[i] = Line((broad ? "Flowing ribbon " : filament ? "Accent filament " : "Outer vapor ribbon ") + i,
                    broad ? .38f : filament ? .12f : .58f,
                    filament ? hot * 1.8f : tint * 1.5f, broad ? .65f : filament ? .8f : .22f);
                if (i >= 4) ribbons[i].sharedMaterial = materials[4];
            }

            frontMesh = BuildFrontMesh();
            AddMesh("Broad turbulent force plume", frontMesh, materials[2], ColorValue(tint * 1.8f, .75f * settings.vfxFrontFill));
            var inner = AddMesh("Luminous inner force layer", frontMesh, materials[2], ColorValue(hot * 1.7f, .5f * settings.vfxFrontFill));
            inner.transform.localPosition = Vector3.up * .035f;
            inner.transform.localScale = new Vector3(.64f, 1, 1);

            glowMesh = BuildQuad();
            haloRenderer = AddMesh("Soft wide front corona", glowMesh, materials[1], ColorValue(tint * 1.4f, .38f));
            hotRenderer = AddMesh("Front light bloom", glowMesh, materials[1], ColorValue(hot * 2.2f, .9f));
            halo = haloRenderer.transform; hotSpot = hotRenderer.transform;
            haloProperties = new MaterialPropertyBlock();
            motes = Particles("Fine glitter and wake dust", materials[1], 640, false);
            streaks = Particles("Fast energy splinters", materials[1], 200, true);
            wisps = Particles("Rolling luminous wisps", materials[3], 128, false);
            ready = true;
            EmitMuzzleBurst();
            Draw(0, 1, 0);
        }

        static Color ColorValue(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        float RandomBetween(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        LineRenderer Line(string name, float width, Color color, float alpha)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = materials[0];
            line.widthMultiplier = width; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.07f, .6f),
                new Keyframe(.6f, 1), new Keyframe(.93f, 1), new Keyframe(1, 0));
            line.startColor = line.endColor = ColorValue(color, alpha);
            line.textureMode = LineTextureMode.Stretch;
            ConfigureRenderer(line);
            return line;
        }
        MeshRenderer AddMesh(string name, Mesh mesh, Material material, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Tint", color);
            renderer.SetPropertyBlock(properties); ConfigureRenderer(renderer);
            return renderer;
        }
        static void ConfigureRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        Mesh BuildFrontMesh()
        {
            var mesh = new Mesh { name = "Bow pointed plume surface" }; mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            var triangles = new int[Rows * Columns * 6];
            for (int row = 0; row <= Rows; row++)
                for (int col = 0; col <= Columns; col++)
                {
                    int index = row * (Columns + 1) + col;
                    uv[index] = new Vector2(col / (float)Columns, row / (float)Rows);
                    colors[index] = Color.white;
                    if (row == Rows || col == Columns) continue;
                    int t = (row * Columns + col) * 6;
                    triangles[t] = index; triangles[t + 1] = index + Columns + 1; triangles[t + 2] = index + 1;
                    triangles[t + 3] = index + 1; triangles[t + 4] = index + Columns + 1; triangles[t + 5] = index + Columns + 2;
                }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            return mesh;
        }
        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Bow front glow quad" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 }; mesh.RecalculateBounds();
            return mesh;
        }
        ParticleSystem Particles(string name, Material material, int capacity, bool stretched)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = true; main.duration = 1; main.startLifetime = .35f;
            main.startSpeed = 0; main.startSize = .06f; main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.35f), new Keyframe(.12f,1), new Keyframe(1,0)));
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.08f), new GradientAlphaKey(.65f,.5f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched) { renderer.lengthScale = 2.5f; renderer.velocityScale = .08f; }
            ConfigureRenderer(renderer);
            particles.Play(); // Emission is manual; Play advances lifetime, drift, size and alpha.
            return particles;
        }

        public void Draw(float distance, float alpha, float dt)
        {
            if (!ready) return;
            elapsed += Mathf.Max(0, dt);
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetFloat("_Opacity", alpha);
                materials[i].SetFloat("_EffectTime", elapsed);
            }
            float length = Mathf.Min(distance, settings.length);
            float grow = Mathf.Clamp01(distance / settings.length);
            float half = settings.width * .5f * grow;
            // Front scale is visual only; upgrades still scale the underlying attack footprint.
            length *= settings.vfxFrontScale;
            half *= settings.vfxFrontScale;
            DrawFront(distance, length, half);
            DrawBeam(distance);
            DrawHalo(distance, length, half);
            float advanced = Mathf.Max(0, distance - previousDistance);
            if (advanced > 0 && alpha > .001f) EmitFlightParticles(distance, advanced, length, half);
            previousDistance = distance;
        }
        void DrawFront(float distance, float length, float half)
        {
            for (int row = 0; row <= Rows; row++)
            {
                float t = row / (float)Rows;
                for (int col = 0; col <= Columns; col++)
                {
                    float across = col / (float)Columns * 2 - 1;
                    float arch = Mathf.Sin(t * Mathf.PI) * (1 - across * across);
                    vertices[row * (Columns + 1) + col] = new Vector3(across * half * t,
                        arch * Mathf.Min(settings.height * .3f, .5f), distance - t * length);
                }
            }
            frontMesh.vertices = vertices; frontMesh.RecalculateBounds();
            for (int i = 0; i < frontPoints.Length; i++)
            {
                float x = i / (float)(frontPoints.Length - 1) * 2 - 1;
                float flutter = Mathf.Sin(x * 13 + elapsed * 19) * .04f * (1 - Mathf.Abs(x));
                frontPoints[i] = new Vector3(x * half, .06f + flutter, distance - Mathf.Abs(x) * length);
            }
            frontRim.positionCount = innerRim.positionCount = frontPoints.Length;
            frontRim.SetPositions(frontPoints); innerRim.SetPositions(frontPoints);
        }
        void DrawBeam(float distance)
        {
            float scale = TrailScale;
            float start = Mathf.Max(0, distance - 17);
            core.widthMultiplier = .28f * scale;
            coreGlow.widthMultiplier = 1.65f * scale;
            trailBody.widthMultiplier = 1.05f * scale;
            trailHalo.widthMultiplier = 3.2f * scale;
            // Width curves are sampled at vertices. Two zero-width endpoints erase the core;
            // a fully sampled spine makes the hot centre and broad sheath visible throughout.
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1), z = Mathf.Lerp(start, distance, t);
                float taper = Mathf.Sin(t * Mathf.PI);
                points[i] = new Vector3(Mathf.Sin(z * 1.1f - elapsed * 4) * .075f * scale * taper,
                    Mathf.Cos(z * .8f - elapsed * 3) * .045f * scale * taper, z);
            }
            core.positionCount = coreGlow.positionCount = trailBody.positionCount = trailHalo.positionCount = Segments;
            core.SetPositions(points); coreGlow.SetPositions(points);
            trailBody.SetPositions(points); trailHalo.SetPositions(points);
            for (int r = 0; r < ribbons.Length; r++)
            {
                bool broad = r < 2, filament = r >= 2 && r < 4;
                ribbons[r].widthMultiplier = (broad ? .38f : filament ? .12f : .58f) * scale;
                for (int i = 0; i < Segments; i++)
                {
                    float t = i / (float)(Segments - 1), z = Mathf.Lerp(start, distance, t);
                    float phase = r * Mathf.PI + (filament ? .9f : broad ? 0 : 1.8f);
                    float angle = z * (broad ? 1.1f : filament ? 1.6f : .72f) + elapsed * (broad ? -4.2f : filament ? 5 : -2.4f) + phase;
                    float envelope = Mathf.Pow(Mathf.Sin(t * Mathf.PI), .7f);
                    float breathing = 1 + Mathf.Sin(z * 2.1f - elapsed * 5 + r) * .14f;
                    float radius = (broad ? .68f : filament ? .94f : 1.08f) * scale * envelope * breathing;
                    points[i] = new Vector3(Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius * .6f + Mathf.Sin(z * 1.9f + phase) * .035f, z);
                }
                ribbons[r].positionCount = Segments; ribbons[r].SetPositions(points);
            }
        }
        float TrailScale => Mathf.Clamp(settings.width / 6, .55f, 2.2f) * settings.vfxTrailWidth;

        void DrawHalo(float distance, float length, float half)
        {
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            Quaternion facing = view != null ? view.transform.rotation : Quaternion.LookRotation(Vector3.up, transform.forward);
            halo.rotation = hotSpot.rotation = facing;
            halo.localPosition = new Vector3(0, .08f, distance - length * .55f);
            hotSpot.localPosition = new Vector3(0, .12f, distance - length * .3f);
            float pulse = 1 + Mathf.Sin(elapsed * 26) * .035f;
            float launchFlash = Mathf.Clamp01(1 - elapsed / .13f);
            float width = Mathf.Max(half * 1.5f, launchFlash * .9f * settings.vfxFrontScale);
            halo.localScale = new Vector3(width, Mathf.Max(.2f, width * .65f), 1) * pulse;
            hotSpot.localScale = new Vector3(Mathf.Max(.3f, half * .8f), Mathf.Max(.25f, half * .6f), 1) * pulse;
            haloProperties.SetColor("_Tint", ColorValue(tint * 1.4f, .4f * settings.vfxFrontFill));
            haloRenderer.SetPropertyBlock(haloProperties);
            haloProperties.SetColor("_Tint", ColorValue(hot * 2.2f, (.8f + launchFlash) * settings.vfxFrontFill));
            hotRenderer.SetPropertyBlock(haloProperties);
        }
        int EmissionCount(ref float debt, float distance, float perMeter, int max)
        {
            debt += distance * perMeter * particleScale;
            int count = Mathf.FloorToInt(debt); debt -= count;
            return Mathf.Min(count, max);
        }
        void EmitFlightParticles(float distance, float advanced, float length, float half)
        {
            float scale = TrailScale;
            int fineCount = EmissionCount(ref moteDebt, advanced, 40, 112);
            for (int i = 0; i < fineCount; i++)
            {
                Vector3 position, velocity;
                if (i % 3 != 0)
                {
                    // Scatter behind the tip along the whole visible wake, not only at the head.
                    float z = RandomBetween(Mathf.Max(0,distance - 13), distance);
                    float angle = z * 1.1f - elapsed * 4.2f + RandomBetween(-.7f,.7f) + (i % 2) * Mathf.PI;
                    float radius = RandomBetween(.3f,1.15f) * scale;
                    position = new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.55f,z);
                    velocity = new Vector3(Mathf.Cos(angle)*.5f,RandomBetween(-.1f,.5f),RandomBetween(-2.5f,-.5f));
                }
                else
                {
                    float tip = RandomBetween(distance - advanced, distance);
                    float spread = settings.width * .5f * settings.vfxFrontScale * Mathf.Clamp01(tip / settings.length);
                    float across = RandomBetween(-1,1), depth = RandomBetween(Mathf.Abs(across),1);
                    position = new Vector3(across*spread,RandomBetween(-.3f,.5f),Mathf.Max(0,tip-depth*settings.length*settings.vfxFrontScale));
                    velocity = new Vector3(across*1.5f,RandomBetween(-.3f,.8f),RandomBetween(-2,-.4f));
                }
                Emit(motes,position,velocity,RandomBetween(.045f,.12f),RandomBetween(.24f,.43f),
                    ColorValue(i % 4 == 0 ? hot*2.2f : tint*1.7f,.8f));
            }
            int splinterCount = EmissionCount(ref streakDebt, advanced, 12, 40);
            for (int i = 0; i < splinterCount; i++)
            {
                float across = RandomBetween(-.95f,.95f);
                bool wake = i % 2 == 0;
                Vector3 position = wake
                    ? new Vector3(across*scale,RandomBetween(-.25f,.4f),RandomBetween(Mathf.Max(0,distance-10),distance))
                    : new Vector3(across*half,RandomBetween(-.1f,.3f),distance-Mathf.Abs(across)*length);
                Emit(streaks,position,new Vector3(across*.7f,RandomBetween(-.2f,.4f),RandomBetween(-9,-4)),
                    RandomBetween(.045f,.09f),RandomBetween(.2f,.35f),ColorValue(hot*1.9f,.8f));
            }
            int wispCount = EmissionCount(ref wispDebt, advanced, 7, 22);
            for (int i = 0; i < wispCount; i++)
            {
                float z = RandomBetween(Mathf.Max(0,distance-12),distance);
                float angle = z*.72f-elapsed*2.4f+i*Mathf.PI;
                Vector3 position = new Vector3(Mathf.Cos(angle)*.6f*scale,Mathf.Sin(angle)*.35f*scale,z);
                Emit(wisps,position,new Vector3(Mathf.Cos(angle)*.4f,.22f,-1.4f),
                    RandomBetween(.85f,1.5f)*scale,RandomBetween(.3f,.44f),ColorValue(tint,.2f));
            }
        }
        void EmitMuzzleBurst()
        {
            int count = Mathf.RoundToInt(22 * settings.vfxParticleDensity);
            for (int i = 0; i < count; i++)
            {
                float angle = RandomBetween(0,Mathf.PI*2);
                Emit(streaks, Vector3.zero, new Vector3(Mathf.Cos(angle)*2.4f,Mathf.Sin(angle)*1.6f,RandomBetween(1,5)),
                    RandomBetween(.04f,.09f), .22f, ColorValue(hot*2,.9f));
            }
        }
        void Emit(ParticleSystem particles, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            var p = new ParticleSystem.EmitParams { position=position, velocity=velocity, startSize=size,
                startLifetime=lifetime, startColor=color, rotation=RandomBetween(0,360), angularVelocity=RandomBetween(-100,100) };
            particles.Emit(p,1);
        }
        void OnDestroy()
        {
            foreach (var material in materials) Dispose(material);
            Dispose(frontMesh); Dispose(glowMesh);
        }
        static void Dispose(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
