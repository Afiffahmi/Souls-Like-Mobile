using UnityEngine;

namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class RegionChunkTile : MonoBehaviour
    {
        [HideInInspector] public Vector2Int coordinate;
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, 1f, .7f, .8f);
            var ground = transform.Find("Ground and Structures/Ground");
            if (ground != null && ground.TryGetComponent<Renderer>(out var renderer)) Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size + Vector3.up * .1f);
        }
    }
}
