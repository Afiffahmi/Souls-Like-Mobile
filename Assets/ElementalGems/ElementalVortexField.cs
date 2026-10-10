using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalGems
{
    public sealed class ElementalVortexField : MonoBehaviour
    {
        ElementalReactions.Chain chain;
        float radius, damage, remaining = 1.8f, nextPull;
        internal static void Spawn(Scene scene, Vector3 point, float radius, float damage, ElementalReactions.Chain chain)
        {
            var go = new GameObject("Elemental Vortex Pull");
            SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = point;
            var field = go.AddComponent<ElementalVortexField>();
            field.radius = radius; field.damage = damage; field.chain = chain;
        }
        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            if (Time.time < nextPull) return;
            nextPull = Time.time + .15f;
            foreach (var enemy in ElementalReactions.Nearby(gameObject.scene, transform.position, radius, chain.source))
            {
                var target = ElementalEnemy.GetOrAdd(enemy);
                if (!target.isActiveAndEnabled || target.Resistance(chain.attack.element) <= 0) continue;
                ElementalReactions.Contact(enemy, ElementType.Wind, damage, chain);
                Vector3 inward = transform.position - enemy.transform.position;
                if (chain.control && inward.sqrMagnitude > .25f) target.ApplyKnockback(inward, 3.5f, .6f);
            }
        }
    }
}
