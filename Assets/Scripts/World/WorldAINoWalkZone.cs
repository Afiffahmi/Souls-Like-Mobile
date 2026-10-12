using UnityEngine;
using UnityEngine.AI;

namespace Nusantara.World
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(NavMeshObstacle))]
    [AddComponentMenu("Nusantara/AI No Walk Zone")]
    public sealed class WorldAINoWalkZone : MonoBehaviour
    {
        [Min(.5f), Tooltip("Radius of this painted AI exclusion area, in metres.")]
        public float radius = 8;

        public void RefreshObstacle()
        {
            radius = Mathf.Clamp(radius, .5f, 80);
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) return;
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = Vector3.zero;
            obstacle.radius = radius;
            obstacle.height = Mathf.Max(12, radius * 2 + 4);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = 0;
            obstacle.enabled = isActiveAndEnabled;
        }
        void OnEnable() => RefreshObstacle();
        void OnValidate() => RefreshObstacle();
        void OnDisable()
        {
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
        }
    }
}
