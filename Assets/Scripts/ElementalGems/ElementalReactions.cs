using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalGems
{
    public enum ElementalReaction { None, SteamBurst, ChainLightning, Wildfire, RootFracture, GroundBreak, ElementalVortex, Purification }

    /// <summary>Directional recipes: the first element belongs to the enemy, the second to the attack.</summary>
    public static class ElementalReactions
    {
        internal sealed class Chain
        {
            internal readonly HashSet<Enemy> contacted = new HashSet<Enemy>();
            internal readonly HashSet<Enemy> reacted = new HashSet<Enemy>();
            internal readonly GemAttack attack;
            internal readonly GemManager source;
            internal readonly bool control;
            internal readonly float baseDamage;
            internal Chain(GemAttack attack, GemManager source, float damage, bool control)
            { this.attack = attack; this.source = source; baseDamage = damage; this.control = control; }
        }
        public static ElementalReaction Recipe(ElementType enemy, ElementType player)
        {
            if (player == ElementType.Normal) return ElementalReaction.None;
            if (enemy == ElementType.Darkness && player != ElementType.Darkness) return ElementalReaction.Purification;
            if (enemy == ElementType.Wind && player != ElementType.Wind) return ElementalReaction.ElementalVortex;
            if (enemy == ElementType.Fire && player == ElementType.Water) return ElementalReaction.SteamBurst;
            if (enemy == ElementType.Water && player == ElementType.Lightning) return ElementalReaction.ChainLightning;
            if (enemy == ElementType.Nature && player == ElementType.Fire) return ElementalReaction.Wildfire;
            if (enemy == ElementType.Earth && player == ElementType.Nature) return ElementalReaction.RootFracture;
            if (enemy == ElementType.Lightning && player == ElementType.Earth) return ElementalReaction.GroundBreak;
            return ElementalReaction.None;
        }
        public static void OnHit(ElementalEnemy target, ElementType previous, GemAttack attack, float damage, GemManager source, bool allowControlEffects)
        {
            if (target == null || !target.isActiveAndEnabled || attack == null || attack.element == ElementType.Normal) return;
            var chain = new Chain(attack, source, damage, allowControlEffects);
            chain.contacted.Add(target.Health);
            Resolve(target, previous, ElementType.Normal, chain);
        }
        static void Resolve(ElementalEnemy target, ElementType previous, ElementType linked, Chain chain)
        {
            var reaction = Recipe(previous, chain.attack.element);
            if (reaction == ElementalReaction.None || chain.reacted.Count >= 24 || !chain.reacted.Add(target.Health)) return;
            if (reaction == ElementalReaction.Purification)
            {
                if (target.Purify(chain.attack.element, linked, chain.attack, chain.control))
                {
                    target.TakeReactionDamage(chain.baseDamage * chain.attack.damageScale, chain.attack.element, chain.control);
                    target.ApplyLinkUpElement(chain.attack.element, chain.attack);
                }
                return;
            }
            if (!target.BeginReaction()) return;
            var origin = target.transform.position;
            var scene = target.gameObject.scene;
            float radius = Mathf.Max(.1f, target.reactionRadius);
            float damage = Mathf.Max(0, chain.baseDamage * chain.attack.damageScale * target.reactionDamageScale);
            var color = ElementalReactionVfx.Tint(chain.attack.element);
            target.ConsumeInfusion(previous);
            GemFeedback.Show(origin + Vector3.up * 1.4f, Label(reaction), color);
            switch (reaction)
            {
                case ElementalReaction.SteamBurst:
                    ElementalReactionVfx.Burst(scene, origin + Vector3.up * .4f, new Color(.8f, .93f, 1), radius, chain.attack);
                    target.TakeReactionDamage(damage, chain.attack.element, chain.control);
                    if (chain.control) target.AddStatus(StatusKind.Stagger, .5f);
                    foreach (var enemy in Nearby(scene, origin, radius, chain.source))
                    {
                        if (enemy == target.Health) continue;
                        var receiver = Contact(enemy, previous, damage, chain);
                        if (receiver != null && chain.control)
                        {
                            receiver.ApplyKnockback(enemy.transform.position - origin, 7f);
                            receiver.AddStatus(StatusKind.Stagger, .5f);
                        }
                    }
                    // The original enemy also receives the steam impulse in the incoming hit direction.
                    if (chain.control && chain.source != null) target.ApplyKnockback(origin - chain.source.transform.position, 7f);
                    break;
                case ElementalReaction.ChainLightning:
                    target.TakeReactionDamage(damage, chain.attack.element, chain.control);
                    if (chain.control) target.AddStatus(StatusKind.Stun, 1.2f);
                    ElementalReactionVfx.Burst(scene, origin + Vector3.up, color, 1, chain.attack);
                    Vector3 from = origin;
                    for (int hop = 1; hop < target.maxChainTargets; hop++)
                    {
                        Enemy next = null;
                        foreach (var candidate in Nearby(scene, from, radius, chain.source))
                            if (!chain.contacted.Contains(candidate) && ElementalEnemy.GetOrAdd(candidate).Resistance(chain.attack.element) > 0) { next = candidate; break; }
                        if (next == null) break;
                        var receiver = Contact(next, previous, damage * Mathf.Pow(.85f, hop), chain);
                        if (receiver != null)
                        {
                            if (chain.control) receiver.AddStatus(StatusKind.Stun, 1.2f);
                            ElementalReactionVfx.Link(scene, from + Vector3.up, next.transform.position + Vector3.up, color, chain.attack);
                        }
                        from = next.transform.position;
                    }
                    break;
                case ElementalReaction.Wildfire:
                    target.TakeReactionDamage(damage, chain.attack.element);
                    target.AddStatus(StatusKind.Burn, 4, Mathf.Max(2, damage * .2f));
                    target.Infuse(ElementType.Fire, chain.attack);
                    var frontier = new Queue<Vector3>(); frontier.Enqueue(origin);
                    int spread = 1;
                    while (frontier.Count > 0 && spread < target.maxChainTargets)
                    {
                        Vector3 ember = frontier.Dequeue();
                        foreach (var enemy in Nearby(scene, ember, radius * .75f, chain.source))
                        {
                            var receiver = Contact(enemy, previous, damage * .65f, chain);
                            if (receiver == null) continue;
                            receiver.AddStatus(StatusKind.Burn, 4, Mathf.Max(2, damage * .2f));
                            ElementalReactionVfx.Link(scene, ember + Vector3.up, enemy.transform.position + Vector3.up, color, chain.attack);
                            frontier.Enqueue(enemy.transform.position);
                            if (++spread >= target.maxChainTargets) break;
                        }
                    }
                    ElementalReactionVfx.Burst(scene, origin + Vector3.up * .4f, color, radius * .6f, chain.attack);
                    break;
                case ElementalReaction.RootFracture:
                    target.FractureArmor(6);
                    if (chain.control) target.AddStatus(StatusKind.Root, 2.5f);
                    target.TakeReactionDamage(damage, chain.attack.element);
                    ElementalReactionVfx.Roots(target.transform, chain.attack);
                    break;
                case ElementalReaction.GroundBreak:
                    target.GroundElectricity();
                    if (chain.control) { target.AddStatus(StatusKind.Stun, 1.2f); target.AddStatus(StatusKind.Stagger, 1.8f); }
                    target.TakeReactionDamage(damage, chain.attack.element, chain.control);
                    ElementalReactionVfx.Burst(scene, origin + Vector3.up * .1f, color, 2, chain.attack);
                    break;
                case ElementalReaction.ElementalVortex:
                    target.TakeReactionDamage(damage, chain.attack.element);
                    ElementalVortexField.Spawn(scene, origin, radius, damage * .5f, chain);
                    ElementalReactionVfx.Vortex(scene, origin, radius, color, chain.attack);
                    break;
            }
            target.ApplyLinkUpElement(chain.attack.element, chain.attack);
        }
        internal static ElementalEnemy Contact(Enemy enemy, ElementType linked, float damage, Chain chain)
        {
            // Each enemy takes propagated damage at most once in the entire reaction chain.
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || chain.contacted.Count >= 24 || !chain.contacted.Add(enemy)) return null;
            var target = ElementalEnemy.GetOrAdd(enemy);
            if (!target.isActiveAndEnabled || target.Resistance(chain.attack.element) <= 0) return null;
            ElementType previous = target.CurrentElement;
            target.TakeReactionDamage(damage, chain.attack.element);
            Resolve(target, previous, linked, chain);
            // Propagated link-up damage also locks elemental-born receivers in Normal.
            target.ApplyLinkUpElement(chain.attack.element, chain.attack);
            target.Infuse(chain.attack.element, chain.attack);
            return target;
        }
        internal static List<Enemy> Nearby(Scene scene, Vector3 center, float radius, GemManager owner)
        {
            var physics = scene.GetPhysicsScene();
            var colliders = new Collider[32]; int count;
            while ((count = physics.OverlapSphere(center + Vector3.up * .8f, radius, colliders, ~0, QueryTriggerInteraction.Collide)) == colliders.Length)
                System.Array.Resize(ref colliders, colliders.Length * 2);
            var result = new List<Enemy>();
            var seen = new HashSet<Enemy>();
            for (int i = 0; i < count; i++)
            {
                var enemy = colliders[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || !seen.Add(enemy) ||
                    (owner != null && enemy.transform.IsChildOf(owner.transform))) continue;
                Vector3 delta = colliders[i].bounds.center - (center + Vector3.up * .8f);
                bool blocked = false;
                var obstacles = new RaycastHit[16]; int hitCount;
                while ((hitCount = physics.Raycast(center + Vector3.up * .8f, delta.normalized, obstacles, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)) == obstacles.Length)
                    System.Array.Resize(ref obstacles, obstacles.Length * 2);
                for (int j = 0; j < hitCount; j++)
                    if (!BowArrowProjectile.IsCharacterCollider(obstacles[j].collider)) { blocked = true; break; }
                if (!blocked) result.Add(enemy);
            }
            result.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            return result;
        }
        static string Label(ElementalReaction reaction)
        {
            switch (reaction)
            {
                case ElementalReaction.SteamBurst: return "STEAM BURST";
                case ElementalReaction.ChainLightning: return "CHAIN LIGHTNING";
                case ElementalReaction.Wildfire: return "WILDFIRE";
                case ElementalReaction.RootFracture: return "ROOT FRACTURE";
                case ElementalReaction.GroundBreak: return "GROUND BREAK";
                case ElementalReaction.ElementalVortex: return "ELEMENTAL VORTEX";
                default: return "PURIFICATION";
            }
        }
    }
}
