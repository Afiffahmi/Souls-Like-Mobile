using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ElementalGems
{
    /// <summary>Transparent branching cracks projected onto the existing ground, with bounded elemental VFX.</summary>
    public sealed class ElementalGroundImpact : MonoBehaviour
    {
        private Mesh mesh;
        private float age, life, speed, radius, roughness, glow, seed, nextArc;
        private float opacity, width, coreRatio, coreOpacity;
        private bool initialized;
        private PhysicsScene physics;
        private int groundLayers;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private ElementType element;
        private Color tint;
        private System.Random random;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<LineRenderer> strands = new List<LineRenderer>();
        private readonly List<Vector3[]> strandBases = new List<Vector3[]>();
        private readonly List<bool> strandValid = new List<bool>();
        private readonly Vector3[] strandPoints = new Vector3[13];
        private MaterialPropertyBlock properties;
        private void Awake() => properties = new MaterialPropertyBlock();
        private float Rand(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        public static void Spawn(Scene scene, Vector3 center, Vector3 normal, float radius, float lifetime,
            int detail, int particleCount, ElementType element, Color tint, Material ground, Material particles,
            Material energy = null, float effectSpeed = 1, float edgeRoughness = .2f, float crackGlow = .65f,
            float crackOpacity = .32f, float crackWidth = .035f, int groundLayers = ~0,
            float coreRadiusRatio = .22f, float coreOpacity = .85f)
        {
            var go = new GameObject("Elemental Ground Impact - " + element);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(center, Quaternion.FromToRotation(Vector3.up, normal));
            var effect = go.AddComponent<ElementalGroundImpact>();
            effect.life = Mathf.Max(.2f, lifetime); effect.speed = Mathf.Max(.05f, effectSpeed);
            effect.radius = Mathf.Max(.1f, radius); effect.roughness = Mathf.Clamp(edgeRoughness, 0, .35f);
            effect.element = element; effect.tint = tint; effect.glow = Mathf.Max(0, crackGlow);
            effect.opacity = Mathf.Clamp01(crackOpacity); effect.width = Mathf.Clamp(crackWidth, .005f, .15f);
            effect.coreRatio = Mathf.Clamp(coreRadiusRatio, .05f, .45f); effect.coreOpacity = Mathf.Clamp01(coreOpacity);
            effect.physics = scene.GetPhysicsScene(); effect.groundLayers = groundLayers;
            // Visual variation must not consume the gameplay random stream used by AI attack selection.
            effect.random = new System.Random(System.Guid.NewGuid().GetHashCode()); effect.seed = effect.Rand(0, 100);
            effect.Build(Mathf.Clamp(detail, 1, 5), ground);
            effect.Burst(Mathf.Clamp(particleCount, 0, 80), particles);
            effect.BuildStrands(energy);
            effect.initialized = true;
            effect.UpdateVisuals();
        }
        private bool ProjectGround(Vector2 point, out Vector3 local, out Vector3 localNormal)
        {
            Vector3 world = transform.TransformPoint(new Vector3(point.x, 0, point.y));
            int count = physics.Raycast(world + Vector3.up, Vector3.down, groundHits, 2f, groundLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            local = Vector3.zero; localNormal = Vector3.up;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == null || BowArrowProjectile.IsCharacterCollider(hit.collider) || hit.normal.y < .45f || hit.distance >= nearest) continue;
                nearest = hit.distance; found = true;
                local = transform.InverseTransformPoint(hit.point + hit.normal * .009f);
                localNormal = transform.InverseTransformDirection(hit.normal);
            }
            return found;
        }
        private void Build(int detail, Material material)
        {
            if (material == null) return;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var colors = new List<Color>(); var uvs = new List<Vector2>();
            void Vertex(Vector3 point, Vector2 uv, float alpha)
            { vertices.Add(point); uvs.Add(uv); colors.Add(new Color(0, 1, 1, alpha)); }
            void AddRibbon(Vector2[] path, float thickness, float strength, bool taper = true)
            {
                var points = new Vector3[path.Length]; var normals = new Vector3[path.Length];
                var valid = new bool[path.Length]; var widths = new float[path.Length];
                for (int i = 0; i < path.Length; i++)
                {
                    valid[i] = ProjectGround(path[i], out points[i], out normals[i]);
                    widths[i] = thickness * (taper ? Mathf.Lerp(1, .025f, i / (float)(path.Length - 1)) : 1) * Rand(.65f, 1.35f);
                }
                for (int i = 0; i < path.Length - 1; i++)
                {
                    if (!valid[i] || !valid[i + 1]) continue;
                    Vector3 delta = points[i + 1] - points[i];
                    // Do not bridge holes or sharp ground-height changes with a floating strip.
                    if (delta.sqrMagnitude < .000001f || Mathf.Abs(delta.y) > .65f) continue;
                    Vector3 sideA = Vector3.Cross(normals[i], delta).normalized * widths[i];
                    Vector3 sideB = Vector3.Cross(normals[i + 1], delta).normalized * widths[i + 1];
                    float a = i / (float)(path.Length - 1), b = (i + 1) / (float)(path.Length - 1);
                    int v = vertices.Count;
                    Vertex(points[i] - sideA, new Vector2(0, a), strength * (taper ? 1 - a * a : 1));
                    Vertex(points[i] + sideA, new Vector2(1, a), strength * (taper ? 1 - a * a : 1));
                    Vertex(points[i + 1] + sideB, new Vector2(1, b), strength * (taper ? 1 - b * b : 1));
                    Vertex(points[i + 1] - sideB, new Vector2(0, b), strength * (taper ? 1 - b * b : 1));
                    triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                    triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
                }
            }
            Vector2[] Path(Vector2 origin, float angle, float length, int segments)
            {
                var path = new Vector2[segments + 1]; path[0] = origin;
                var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-forward.y, forward.x);
                for (int i = 1; i <= segments; i++)
                {
                    float p = i / (float)segments;
                    path[i] = Vector2.ClampMagnitude(origin + forward * length * p +
                        side * Rand(-1, 1) * length * (.025f + roughness * .16f), radius * .98f);
                }
                return path;
            }
            // An irregular low pool at impact, projected point-by-point onto the ground.
            // Vertex red distinguishes pool shading from the surrounding narrow fracture ribbons.
            const int sectors = 40, poolRings = 5;
            var poolPoints = new Vector3[(poolRings + 1) * sectors];
            var poolValid = new bool[poolPoints.Length];
            var edgeLengths = new float[sectors];
            for (int i = 0; i < sectors; i++)
            {
                float angle = i * Mathf.PI * 2 / sectors;
                edgeLengths[i] = radius * coreRatio * (1 + .13f * Mathf.Sin(angle * 5 + seed) + .07f * Mathf.Sin(angle * 9 - seed));
                for (int ring = 0; ring <= poolRings; ring++)
                {
                    float r = edgeLengths[i] * ring / poolRings;
                    poolValid[ring * sectors + i] = ProjectGround(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r,
                        out poolPoints[ring * sectors + i], out _);
                }
            }
            void PoolVertex(int index, float fraction)
            {
                vertices.Add(poolPoints[index]); uvs.Add(new Vector2(fraction, 0));
                colors.Add(new Color(1, 1, 1, 1));
            }
            for (int ring = 0; ring < poolRings; ring++) for (int i = 0; i < sectors; i++)
            {
                int a = ring * sectors + i, b = ring * sectors + (i + 1) % sectors;
                int c = b + sectors, d = a + sectors;
                if (!poolValid[a] || !poolValid[b] || !poolValid[c] || !poolValid[d]) continue;
                if (Mathf.Abs(poolPoints[a].y - poolPoints[c].y) > .65f || Mathf.Abs(poolPoints[b].y - poolPoints[d].y) > .65f) continue;
                int v = vertices.Count;
                PoolVertex(a, ring / (float)poolRings); PoolVertex(b, ring / (float)poolRings);
                PoolVertex(c, (ring + 1f) / poolRings); PoolVertex(d, (ring + 1f) / poolRings);
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
            }
            int roots = 5 + detail;
            var trunks = new List<Vector2[]>();
            for (int root = 0; root < roots; root++)
            {
                float angle = seed + root * Mathf.PI * 2 / roots + Rand(-.4f, .4f);
                float length = radius * Rand(.5f, .98f - roughness * .2f);
                var origin = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * coreRatio * .65f;
                length = Mathf.Max(radius * .2f, length - origin.magnitude);
                var trunk = Path(origin, angle, length, 10);
                trunks.Add(trunk);
                AddRibbon(trunk, width, Rand(.75f, 1));
                int branches = detail >= 3 ? 2 : 1;
                for (int branch = 0; branch < branches; branch++)
                {
                    int joint = 3 + branch * 3;
                    float branchAngle = angle + (branch % 2 == 0 ? 1 : -1) * Rand(.5f, 1.25f);
                    var fork = Path(trunk[joint], branchAngle, length * Rand(.22f, .42f), 5);
                    AddRibbon(fork, width * .5f, .65f);
                    if (detail >= 4)
                        AddRibbon(Path(fork[2], branchAngle - .9f, length * .14f, 3), width * .23f, .42f);
                }
            }
            // Broken cross-fractures join the main cracks into large irregular ground plates.
            // The plates are the existing terrain, not raised stone meshes or a circular decal.
            for (int band = 0; band < 2; band++) for (int i = 0; i < roots; i++)
            {
                if (Rand(0, 1) < .2f) continue;
                int joint = band == 0 ? 3 : 6;
                Vector2 from = trunks[i][joint], to = trunks[(i + 1) % roots][joint];
                Vector2 delta = to - from;
                float angle = Mathf.Atan2(delta.y, delta.x);
                var connector = Path(from, angle, delta.magnitude, 5); connector[connector.Length - 1] = to;
                AddRibbon(connector, width * (band == 0 ? .75f : .5f), band == 0 ? .85f : .65f, false);
            }
            mesh = new Mesh { name = "Elemental impact pool and connected fractures" };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
            var meshNormals = new List<Vector3>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++) meshNormals.Add(Vector3.up);
            mesh.SetNormals(meshNormals);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var surface = new GameObject("Ground crack overlay"); surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderers.Add(renderer);
        }

        private void Burst(int count, Material material)
        {
            if (material == null || count <= 0) return;
            var go = new GameObject("Element particles"); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false;
            main.duration = Mathf.Max(.1f, life * speed - .5f); main.simulationSpeed = speed;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .65f); main.maxParticles = 80;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, element == ElementType.Lightning ? 2.5f : 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, element == ElementType.Earth ? .09f : .075f);
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = element == ElementType.Earth ? 1 : element == ElementType.Fire ? .35f : element == ElementType.Water ? .5f : 0;
            var shape = ps.shape;
            if (mesh != null && mesh.vertexCount > 0)
            {
                shape.shapeType = ParticleSystemShapeType.Mesh; shape.mesh = mesh; shape.normalOffset = .018f;
                // Mesh colours encode pool/crack shader masks, not particle tint or opacity.
                shape.useMeshColors = false;
            }
            else { shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius * coreRatio; shape.rotation = new Vector3(-90, 0, 0); }
            var emission = ps.emission; emission.rateOverTime = 0;
            var bursts = new List<ParticleSystem.Burst>();
            bursts.Add(new ParticleSystem.Burst(0, (short)count));
            if (element != ElementType.Normal && element != ElementType.Earth)
                for (float at = .15f; at < main.duration && bursts.Count < 32; at += Rand(.14f, .3f))
                    bursts.Add(new ParticleSystem.Burst(at, (short)Mathf.Max(1, count / 5)));
            emission.SetBursts(bursts.ToArray());
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.y = element == ElementType.Darkness ? .12f : element == ElementType.Fire ? .65f : .3f;
            if (element == ElementType.Wind || element == ElementType.Nature) velocity.orbitalY = element == ElementType.Wind ? 2 : .6f;
            var noise = ps.noise; noise.enabled = true; noise.strength = element == ElementType.Lightning ? .45f : .08f;
            noise.frequency = 2; noise.quality = ParticleSystemNoiseQuality.Low;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.9f, .12f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderers.Add(renderer);
            ps.Play();
        }
        private void BuildStrands(Material material)
        {
            if (material == null || element != ElementType.Lightning) return;
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(element + " ground strand"); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                line.useWorldSpace = false; line.positionCount = strandPoints.Length;
                line.widthMultiplier = .012f;
                line.widthCurve = AnimationCurve.Linear(0, 1, 1, .05f);
                line.startColor = line.endColor = Color.white; line.numCapVertices = 1;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                strands.Add(line); renderers.Add(line);
                var points = new Vector3[strandPoints.Length];
                float angle = seed + i * 2.39996f;
                bool valid = true;
                for (int j = 0; j < points.Length; j++)
                {
                    float r = radius * Mathf.Lerp(.08f, .78f, j / (float)(points.Length - 1));
                    valid &= ProjectGround(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, out points[j], out _);
                }
                strandBases.Add(points); strandValid.Add(valid);
            }
            UpdateStrands();
        }
        private void UpdateStrands()
        {
            for (int strand = 0; strand < strands.Count; strand++)
            {
                if (strands[strand] == null) continue;
                if (!strandValid[strand]) { strands[strand].enabled = false; continue; }
                for (int i = 0; i < strandPoints.Length; i++)
                {
                    strandPoints[i] = strandBases[strand][i] + new Vector3(Rand(-.025f, .025f), .012f, Rand(-.025f, .025f));
                }
                strands[strand].SetPositions(strandPoints);
                strands[strand].enabled = random.NextDouble() > .4;
            }
        }
        private void UpdateVisuals()
        {
            // Native Unity resources must be allocated on the main thread, never in field initializers.
            if (properties == null) properties = new MaterialPropertyBlock();
            float fade = Mathf.Clamp01((life - age) / Mathf.Min(.5f, life * .4f));
            properties.SetColor("_ElementTint", tint); properties.SetColor("_Tint", tint);
            properties.SetFloat("_Intensity", 1.4f); properties.SetFloat("_Element", (float)element);
            properties.SetFloat("_EffectTime", age * speed + seed); properties.SetFloat("_Fade", fade);
            properties.SetFloat("_Glow", glow); properties.SetFloat("_Radius", radius);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_CoreOpacity", coreOpacity);
            foreach (var renderer in renderers) if (renderer != null)
            {
                properties.SetFloat("_Fade", renderer is MeshRenderer ? fade : fade * .4f);
                renderer.SetPropertyBlock(properties);
            }
            if (age * speed >= nextArc)
            { nextArc = age * speed + (element == ElementType.Lightning ? .055f : .035f); UpdateStrands(); }
        }
        private void Update()
        {
            // Old transient effects cannot resume with missing runtime state after an Editor script reload.
            if (!initialized) { Destroy(gameObject); return; }
            age += Time.deltaTime; if (age >= life) { Destroy(gameObject); return; } UpdateVisuals();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
