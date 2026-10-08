using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemManager)), DefaultExecutionOrder(180)]
    public sealed class GemSwordCombat : MonoBehaviour
    {
        public Transform blade;
        public Vector3 bladeStart = new Vector3(-0.35f, 0.1f, 0);
        public Vector3 bladeEnd = new Vector3(0.48f, 0.1f, 0);
        [Min(0.01f)] public float hitRadius = 0.18f;
        public LayerMask enemyLayers = ~0;
        [Header("Damage and hit window; animation clips remain unchanged")]
        [Min(0)] public float lightDamage = 18, heavyDamage = 30, specialDamage = 45;
        [Range(0, 1)] public float hitStart = 0.2f, hitEnd = 0.72f;
        private GemManager manager;
        private PlayerStateManager player;
        private readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        private int stateHash;
        private float normalized, previousNormalized;
        private bool tracking, sampled;
        private CombatAttackInput input;
        private GemAttack attack;
        private Vector3 previousStart, previousEnd;
        public bool TrailActive => tracking && normalized >= hitStart && normalized <= hitEnd;
        private void Awake() { manager = GetComponent<GemManager>(); player = GetComponent<PlayerStateManager>(); }
        public void Begin(int hash, CombatAttackInput kind)
        {
            if (manager == null) manager = GetComponent<GemManager>();
            stateHash = hash; input = kind; normalized = previousNormalized = 0; tracking = true; sampled = false; hit.Clear();
            attack = manager.Capture();
        }
        public void Tick(int hash, float time) { if (tracking && stateHash == hash) normalized = time; }
        public void End(int hash) { if (stateHash == hash) { tracking = false; sampled = false; } }
        private void LateUpdate()
        {
            if (!tracking || blade == null || !blade.gameObject.activeInHierarchy || !player.IsAttacking) { sampled = false; return; }
            Vector3 a = blade.TransformPoint(bladeStart), b = blade.TransformPoint(bladeEnd);
            // Detect a window crossed in one low-framerate step too.
            bool window = normalized >= hitStart && previousNormalized <= hitEnd;
            if (window)
            {
                var current = manager.Capture(); // Switching gems updates the remaining contacts immediately.
                if (current.element != attack.element) attack = current;
                int steps = sampled ? Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a, previousStart), Vector3.Distance(b, previousEnd)) / Mathf.Max(0.05f, hitRadius)), 1, 24) : 1;
                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Query(sampled ? Vector3.Lerp(previousStart, a, t) : a, sampled ? Vector3.Lerp(previousEnd, b, t) : b);
                }
            }
            previousStart = a; previousEnd = b; previousNormalized = normalized; sampled = true;
        }
        private void Query(Vector3 a, Vector3 b)
        {
            foreach (var collider in Physics.OverlapCapsule(a, b, hitRadius, enemyLayers, QueryTriggerInteraction.Collide))
            {
                var enemy = collider.GetComponentInParent<Enemy>();
                if (enemy == null || enemy.transform.IsChildOf(transform) || enemy.IsDead || !hit.Add(enemy)) continue;
                Vector3 point = collider.ClosestPoint((a + b) * 0.5f);
                float damage = input == CombatAttackInput.HeavyAttack ? heavyDamage : input == CombatAttackInput.SpecialAttack ? specialDamage : lightDamage;
                ElementalDamage.Hit(attack, damage, manager, enemy, point, enemy.transform.position - transform.position);
                ElementalDamage.Impact(attack, point, transform.forward);
            }
        }
        private void OnDisable() { tracking = false; sampled = false; hit.Clear(); }
        private void OnDrawGizmosSelected()
        {
            if (blade == null) return;
            Gizmos.color = Color.cyan;
            var a = blade.TransformPoint(bladeStart); var b = blade.TransformPoint(bladeEnd);
            Gizmos.DrawWireSphere(a, hitRadius); Gizmos.DrawWireSphere(b, hitRadius); Gizmos.DrawLine(a, b);
        }
    }
}
