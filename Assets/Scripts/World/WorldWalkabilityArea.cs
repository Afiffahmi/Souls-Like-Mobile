using UnityEngine;

namespace Nusantara.World
{
    public sealed class WorldWalkabilityArea : MonoBehaviour
    {
        [Tooltip("Axis-aligned validation volume in world metres, offset from this transform. Keep it around intended paths, not scenic cliffs.")]
        public Vector3 size = new Vector3(256, 200, 256);
        public Vector3 center;
        [Tooltip("Ground and bridge collision layers only; excludes roofs, decoration and water surfaces.")]
        public LayerMask groundMask = 1;
        public LayerMask obstacleMask = 1;
        [Min(.25f)] public float sampleSpacing = 2;
        public Transform entry;
        public Transform[] requiredDestinations = System.Array.Empty<Transform>();
        public Bounds WorldBounds => new Bounds(transform.position + center, size);
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var bounds = WorldBounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
