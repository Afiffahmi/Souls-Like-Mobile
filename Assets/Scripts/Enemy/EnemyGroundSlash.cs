using System.Collections.Generic;
using ElementalGems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsLike.Enemies
{
    /// <summary>A grounded, swept damage wave; its fading wake is visual only.</summary>
    public sealed class EnemyGroundSlash : MonoBehaviour
    {
        readonly HashSet<EnemyTarget> hit = new HashSet<EnemyTarget>();
        readonly List<LineRenderer> cracks = new List<LineRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> rubble = new List<Transform>();
        readonly List<Vector3> rubbleOrigins = new List<Vector3>();
        readonly List<float> rubbleTimes = new List<float>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        MaterialPropertyBlock properties;
        EnemyBrain owner;
        EnemyGroundSlashAction settings;
        PhysicsScene physics;
        Transform blade;
        MeshRenderer bladeRenderer;
        ParticleSystem sparks;
        ParticleSystemRenderer sparkRenderer;
        MaterialPropertyBlock sparkProperties;
        Mesh shardMesh;
        Vector3 direction, start, previousCrack;
        Color tint;
        ElementType element;
        float traveled, age, stoppedAt = -1, nextCrack;
        int damage;
        bool elementalDamage;
        public float TravelledDistance => traveled;
        public ElementType Element => element;
        public bool IsTravelling => stoppedAt < 0;

        public static EnemyGroundSlash Spawn(EnemyAttackContext context, EnemyGroundSlashAction definition,
            Vector3 origin, Vector3 heading, ElementType element, Color tint)
        {
            var go = new GameObject("Enemy Ground Slash - " + element);
            SceneManager.MoveGameObjectToScene(go, context.source.gameObject.scene);
            var wave = go.AddComponent<EnemyGroundSlash>();
            wave.owner = context.source; wave.settings = definition; wave.element = element; wave.tint = tint;
            wave.damage = context.attack.damage; wave.elementalDamage = context.attack.applyElementToPlayer;
            wave.physics = go.scene.GetPhysicsScene(); wave.direction = heading.normalized;
            if (!wave.Ground(origin, out origin)) { Destroy(go); return null; }
            wave.start = wave.previousCrack = origin; go.transform.position = origin;
            wave.BuildVisuals(); wave.AddWake(origin); return wave;
        }

        bool Ground(Vector3 point, out Vector3 grounded)
        {
            grounded = point;
            int count = physics.Raycast(point + Vector3.up * 2, Vector3.down, hits, 5,
                settings.groundAndObstacles, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (!BowArrowProjectile.IsCharacterCollider(hits[i].collider) && hits[i].normal.y >= .45f && hits[i].distance < nearest)
                { grounded = hits[i].point + Vector3.up * .035f; nearest = hits[i].distance; }
            return nearest < float.MaxValue;
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            int count = physics.Raycast(from + Vector3.up * .45f, delta.normalized, hits, delta.magnitude,
                settings.groundAndObstacles, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++)
                if (!BowArrowProjectile.IsCharacterCollider(hits[i].collider) && hits[i].normal.y < .45f) return true;
            return false;
        }

        void Update() => Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            if (settings == null || seconds <= 0) return;
            age += seconds;
            if (IsTravelling)
            {
                float remaining = Mathf.Min(settings.distance - traveled, settings.speed * seconds);
                // Small ground samples keep both damage and visuals on slopes and prevent tunnelling.
                while (remaining > .0001f)
                {
                    float step = Mathf.Min(.2f, remaining);
                    Vector3 from = blade.position;
                    Vector3 candidate = start + direction * (traveled + step); candidate.y = from.y;
                    if (!Ground(candidate, out var to) || Mathf.Abs(to.y - from.y) > .6f || Blocked(from, to))
                    { Stop(); break; }
                    DamageSegment(from, to); traveled += step; remaining -= step;
                    blade.position = to;
                    if (traveled >= nextCrack) { AddWake(to); nextCrack = traveled + .35f; }
                }
                if (traveled >= settings.distance - .001f) Stop();
                if (sparks != null) sparks.transform.position = blade.position + Vector3.up * .15f;
            }
            float fade = stoppedAt < 0 ? 1 : Mathf.Clamp01(1 - (age - stoppedAt) / settings.trailLifetime);
            if (sparkRenderer != null)
            {
                sparkProperties.SetFloat("_EffectTime", age); sparkProperties.SetFloat("_Fade", fade);
                sparkRenderer.SetPropertyBlock(sparkProperties);
            }
            foreach (var line in cracks) { var c = tint; c.a = fade; line.startColor = line.endColor = c; }
            for (int i = 0; i < rubble.Count; i++)
            {
                float t = age - rubbleTimes[i];
                rubble[i].position = rubbleOrigins[i] + Vector3.up * (Mathf.Max(0, 1.7f * t - 3.4f * t * t) - (1 - fade) * .25f);
                rubble[i].localScale = Vector3.one * (.12f + (i % 3) * .035f) * fade;
            }
            if (fade <= 0) Destroy(gameObject);
        }

        void DamageSegment(Vector3 from, Vector3 to)
        {
            Vector3 step = to - from; float length2 = step.sqrMagnitude;
            foreach (var target in EnemyTarget.Active)
            {
                if (target == null || !target.IsAlive || target.gameObject.scene != gameObject.scene || hit.Contains(target)) continue;
                float u = length2 > 0 ? Mathf.Clamp01(Vector3.Dot(target.GroundPosition - from, step) / length2) : 0;
                Vector3 point = Vector3.Lerp(from, to, u), delta = target.GroundPosition - point;
                if (Mathf.Abs(delta.y) > settings.height || Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > settings.hitHalfWidth * settings.hitHalfWidth) continue;
                // A target beside a wall is protected even if it overlaps the wave's width.
                if (Blocked(point, target.GroundPosition)) continue;
                hit.Add(target);
                if (elementalDamage) target.ReceiveElementalDamage(damage, owner, element, false);
                else target.ReceiveDamage(damage, owner, false);
            }
        }

        void Stop()
        {
            if (!IsTravelling) return;
            stoppedAt = age; bladeRenderer.enabled = false;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void BuildVisuals()
        {
            properties = new MaterialPropertyBlock();
            var go = new GameObject("Upright travelling crescent", typeof(MeshFilter), typeof(MeshRenderer));
            blade = go.transform; blade.SetParent(transform, false); blade.rotation = Quaternion.LookRotation(direction);
            const int segments = 40;
            // A thin solid crescent stays readable when the attack approaches the camera head-on.
            var vertices = new Vector3[(segments + 1) * 4]; var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
            float thickness = settings.hitHalfWidth * .12f;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments, a = Mathf.Lerp(-90, 90, u) * Mathf.Deg2Rad;
                float y = (Mathf.Sin(a) + 1) * settings.height * .5f;
                float z = Mathf.Cos(a) * settings.height * .34f;
                float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), .75f);
                float width = taper * settings.height * .23f;
                int v = i * 4;
                vertices[v] = new Vector3(-thickness * taper, y, z - width);
                vertices[v+1] = new Vector3(-thickness * taper, y, z);
                vertices[v+2] = new Vector3(thickness * taper, y, z);
                vertices[v+3] = new Vector3(thickness * taper, y, z - width);
                uv[v]=uv[v+3]=new Vector2(u,.04f); uv[v+1]=uv[v+2]=new Vector2(u,.88f);
                if (i == segments) continue;
                for (int side=0;side<4;side++)
                {
                    int next=(side+1)%4;
                    triangles.Add(v+side);triangles.Add(v+next);triangles.Add(v+next+4);
                    triangles.Add(v+side);triangles.Add(v+next+4);triangles.Add(v+side+4);
                }
            }
            var mesh = new Mesh { name = "Ground crescent" }; mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles.ToArray(); mesh.RecalculateBounds(); meshes.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; bladeRenderer = go.GetComponent<MeshRenderer>();
            bladeRenderer.sharedMaterial = settings.crescentMaterial; bladeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            properties.SetColor("_Tint", tint); bladeRenderer.SetPropertyBlock(properties);
            if (settings.particleMaterial == null) return;
            var particles = new GameObject("Gem sparks"); particles.transform.SetParent(transform, false);
            sparks = particles.AddComponent<ParticleSystem>(); sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = .5f;
            main.startSpeed = 1.8f; main.startSize = .07f; main.startColor = Color.white; main.gravityModifier = .35f; main.maxParticles = 80;
            var emission = sparks.emission; emission.rateOverTime = 45;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 50; shape.radius = .15f;
            particles.transform.rotation = Quaternion.Euler(-90, 0, 0);
            sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.sharedMaterial = settings.particleMaterial;
            sparkProperties = new MaterialPropertyBlock(); sparkProperties.SetColor("_ElementTint", tint);
            sparkProperties.SetFloat("_Element", (float)element); sparkProperties.SetFloat("_Fade", 1);
            sparkRenderer.SetPropertyBlock(sparkProperties); sparks.Play();
        }

        void AddWake(Vector3 point)
        {
            if (sparks != null) { sparks.transform.position = point + Vector3.up * .15f; sparks.Emit(3); }
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            var go = new GameObject("Glowing ground fissure"); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = settings.trailMaterial;
            line.useWorldSpace = true; line.positionCount = 3; line.widthMultiplier = .14f;
            line.SetPositions(new[] { previousCrack, Vector3.Lerp(previousCrack, point, .5f) + right * Mathf.Sin(cracks.Count * 2.4f) * .16f, point });
            line.startColor = line.endColor = tint; line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cracks.Add(line); previousCrack = point;
            if (settings.debrisMaterial == null || cracks.Count % 2 != 0) return;
            for (int side = -1; side <= 1; side += 2)
            {
                var rock = new GameObject("Lifted ground shard", typeof(MeshFilter), typeof(MeshRenderer)); rock.transform.SetParent(transform, false);
                if (shardMesh == null)
                {
                    shardMesh = new Mesh { name = "Ground shard" };
                    shardMesh.vertices = new[] { new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(0,0,1),new Vector3(0,1.7f,0) };
                    shardMesh.triangles = new[] {0,2,1,0,1,3,1,2,3,2,0,3};shardMesh.RecalculateNormals();meshes.Add(shardMesh);
                }
                rock.GetComponent<MeshFilter>().sharedMesh=shardMesh;rock.GetComponent<MeshRenderer>().sharedMaterial=settings.debrisMaterial;
                rock.transform.position = point + right * side * .28f; rock.transform.rotation = Quaternion.Euler(0, cracks.Count * 71, side * 18);
                rubble.Add(rock.transform); rubbleOrigins.Add(rock.transform.position); rubbleTimes.Add(age);
            }
        }
        void OnDestroy() { foreach (var mesh in meshes) if (mesh != null) Destroy(mesh); }
    }
}
