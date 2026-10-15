using UnityEngine;
namespace Nusantara.World
{
    // Optional explicit footprint for actors moved by custom scripts or animation.
    // CharacterControllers, NavMeshAgents and physics bodies work automatically.
    [DisallowMultipleComponent]
    public sealed class WorldGrassInteractor : MonoBehaviour
    {
        [Min(.1f)] public float radius = .8f;
        [Min(0)] public float strength = 1;
        public float footHeightOffset;
        public Vector3 Feet => transform.position+Vector3.up*footHeightOffset;
    }
}
