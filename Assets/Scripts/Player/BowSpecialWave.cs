using System.Collections.Generic;
using ElementalGems;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A travelling triangular force wave. Enemy bodies never stop it; each enemy is hit once.</summary>
public sealed class BowSpecialWave : MonoBehaviour
{
    readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    Collider[] overlaps = new Collider[32];
    RaycastHit[] walls = new RaycastHit[16];
    Transform owner;
    GemManager source;
    GemAttack attack;
    BowSpecialAttackSettings settings;
    Vector3 origin, direction, right;
    float damage, knockbackDuration, travelled, fade;
    LayerMask layers;
    GemBowSpecialEffects effects;
    public int HitCount => hitEnemies.Count;
    public float Travelled => travelled;

    public void Initialize(Transform owner, GemManager source, GemAttack attack, float damage,
        float knockbackDuration, BowSpecialAttackSettings settings, Vector3 forward, LayerMask layers)
    {
        this.owner = owner; this.source = source; this.attack = attack; this.damage = damage;
        this.knockbackDuration = knockbackDuration; this.settings = settings; this.layers = layers;
        origin = transform.position;
        direction = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
        right = Vector3.Cross(Vector3.up, direction);
        transform.rotation = Quaternion.LookRotation(direction);
        effects = gameObject.AddComponent<GemBowSpecialEffects>();
        effects.Initialize(attack, settings);
        Sweep(0, 0);
    }
    void Update() => Advance(Time.deltaTime);
    public void Advance(float dt)
    {
        if (settings == null || dt <= 0) return;
        if (travelled < settings.range)
        {
            float next = Mathf.Min(settings.range, travelled + settings.waveSpeed * attack.projectileSpeed * dt);
            Sweep(travelled, next);
            travelled = next;
        }
        else fade += dt;
        effects.Draw(travelled, Mathf.Clamp01(1 - fade / .45f), dt);
        if (fade >= .45f && Application.isPlaying) Destroy(gameObject);
    }
    void Sweep(float previous, float next)
    {
        var physics = gameObject.scene.GetPhysicsScene();
        float back = Mathf.Max(0, previous - settings.length);
        Vector3 center = origin + direction * ((back + next) * .5f);
        Vector3 extents = new Vector3(settings.width * .5f, settings.height * .5f, Mathf.Max(.01f, (next - back) * .5f));
        int count;
        while ((count = physics.OverlapBox(center, extents, overlaps, transform.rotation, layers, QueryTriggerInteraction.Collide)) == overlaps.Length)
            System.Array.Resize(ref overlaps, overlaps.Length * 2);
        for (int i = 0; i < count; i++)
        {
            var collider = overlaps[i];
            if (owner != null && collider.transform.IsChildOf(owner)) continue;
            var enemy = collider.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || hitEnemies.Contains(enemy)) continue;
            var bounds = collider.bounds;
            Vector3 offset = bounds.center - origin;
            float x = Vector3.Dot(offset, right), z = Vector3.Dot(offset, direction);
            float ex = Vector3.Dot(Abs(right), bounds.extents), ez = Vector3.Dot(Abs(direction), bounds.extents);
            if (!IntersectsSweep(x, z, ex, ez, previous, next, settings.width, settings.length)) continue;
            Vector3 point = collider.ClosestPoint(origin + direction * Mathf.Clamp(z, 0, next));
            if (Blocked(physics, point)) continue;
            hitEnemies.Add(enemy);
            ElementalDamage.Hit(attack, damage, source, enemy, point, direction, knockbackDurationMultiplier: knockbackDuration);
            ElementalDamage.Impact(attack, point, -direction);
        }
    }
    // SAT against the swept triangle: back plane, sides and the two forward sloping planes.
    public static bool IntersectsSweep(float x, float z, float ex, float ez, float previous, float next, float width, float length)
    {
        float half = width * .5f;
        if (z + ez < Mathf.Max(0, previous - length) || z - ez > next || Mathf.Abs(x) - ex > half) return false;
        float slope = length / half;
        return z + slope * Mathf.Abs(x) <= next + ez + slope * ex;
    }
    bool Blocked(PhysicsScene physics, Vector3 point)
    {
        Vector3 offset = point - origin;
        if (offset.sqrMagnitude < .001f) return false;
        int count;
        while ((count = physics.Raycast(origin, offset.normalized, walls, offset.magnitude, layers, QueryTriggerInteraction.Ignore)) == walls.Length)
            System.Array.Resize(ref walls, walls.Length * 2);
        for (int i = 0; i < count; i++)
            if (!BowArrowProjectile.IsCharacterCollider(walls[i].collider) &&
                (owner == null || !walls[i].collider.transform.IsChildOf(owner))) return true;
        return false;
    }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
