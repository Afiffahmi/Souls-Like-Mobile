using UnityEngine;

namespace Nusantara.World
{
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class WorldFireLight : MonoBehaviour
    {
        [Min(0)] public float intensity = 2.8f;
        [Range(0, .5f)] public float flicker = .12f;
        [Min(0)] public float speed = 3;
        public bool followParticles = true;
        [Min(0)] public float worldHeight = 2;
        Light source;
        ParticleSystem[] flames;
        float phase;

        void OnEnable()
        {
            source = GetComponent<Light>();
            flames = transform.parent != null ? transform.parent.GetComponentsInChildren<ParticleSystem>() : new ParticleSystem[0];
            phase = transform.position.x * .17f + transform.position.z * .31f;
        }

        void LateUpdate()
        {
            // Particle prefabs may be scaled down heavily; light range and height
            // remain in world metres rather than ending up inside the ground.
            if (transform.parent != null)
                transform.position = transform.parent.position + Vector3.up * worldHeight;
            bool alive = !followParticles || flames.Length == 0;
            foreach (var flame in flames)
                if (flame != null && flame.gameObject.activeInHierarchy && (flame.isPlaying || flame.particleCount > 0)) { alive = true; break; }
            source.enabled = alive;
            float noise = Mathf.PerlinNoise(phase, Time.time * speed) * 2 - 1;
            source.intensity = intensity * (1 + noise * flicker);
        }

        void OnDisable() { if (source != null) source.enabled = false; }
    }
}
