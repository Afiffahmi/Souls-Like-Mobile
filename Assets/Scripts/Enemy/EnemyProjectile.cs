using UnityEngine;

namespace SoulsLike.Enemies
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [Min(.001f)] public float radius = .08f;
        public LayerMask collisionLayers = ~0;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private EnemyBrain owner;
        private Vector3 velocity;
        private int damage;
        private float expires;
        public void Launch(EnemyBrain source, Vector3 speed, int amount, float lifetime)
        { owner = source; velocity = speed; damage = amount; expires = Time.time + lifetime; }
        private void Update()
        {
            if (Time.time >= expires) { Destroy(gameObject); return; }
            Vector3 step = velocity * Time.deltaTime;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, step.normalized, hits, step.magnitude, collisionLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) { Destroy(gameObject); return; }
            int closest = -1; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].transform;
                if (t.IsChildOf(transform) || (owner != null && t.IsChildOf(owner.transform))) continue;
                if (hits[i].distance < distance) { closest = i; distance = hits[i].distance; }
            }
            if (closest >= 0)
            {
                var target = hits[closest].collider.GetComponentInParent<EnemyTarget>();
                if (target != null && target.IsAlive) target.ReceiveDamage(damage, owner);
                Destroy(gameObject); return;
            }
            transform.position += step;
        }
    }
}
