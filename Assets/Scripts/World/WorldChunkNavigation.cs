using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class WorldChunkNavigation : MonoBehaviour
    {
        [Tooltip("Bake one data asset per agent type and chunk. Include overlapping border geometry. No runtime whole-world bake.")]
        public NavMeshData[] bakedData = System.Array.Empty<NavMeshData>();
        public bool connectEdges;
        public float chunkSize = 256;
        public int agentTypeId;
        public Vector3[] edgeStarts = System.Array.Empty<Vector3>();
        public Vector3[] edgeEnds = System.Array.Empty<Vector3>();
        NavMeshDataInstance[] instances;
        NavMeshLinkInstance[] links;
        void OnEnable()
        {
            instances = new NavMeshDataInstance[bakedData.Length];
            for (int i = 0; i < bakedData.Length; i++)
                if (bakedData[i] != null) instances[i] = NavMesh.AddNavMeshData(bakedData[i], transform.position, transform.rotation);
            links = new NavMeshLinkInstance[edgeStarts.Length];
            if (Application.isPlaying && connectEdges) StartCoroutine(ConnectLoadedNeighbors());
        }
        IEnumerator ConnectLoadedNeighbors()
        {
            var wait = new WaitForSeconds(2);
            var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            while (enabled)
            {
                for (int i = 0; i < edgeStarts.Length && i < edgeEnds.Length; i++)
                {
                    if (links[i].valid) continue;
                    Vector3 a = transform.TransformPoint(edgeStarts[i]), b = transform.TransformPoint(edgeEnds[i]);
                    if (!NavMesh.SamplePosition(a, out var from, 2.5f, filter) || !NavMesh.SamplePosition(b, out var to, 2.5f, filter)) continue;
                    if (Mathf.Abs(from.position.y - to.position.y) > .6f || Vector3.Distance(from.position,to.position) < 1) continue;
                    links[i] = NavMesh.AddLink(new NavMeshLinkData { agentTypeID = agentTypeId, startPosition = from.position, endPosition = to.position, width = 6, bidirectional = true, area = 0, costModifier = 1 });
                }
                yield return wait;
            }
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if (links != null) foreach (var link in links) if (link.valid) link.Remove();
            links = null;
            if (instances != null) foreach (var instance in instances) if (instance.valid) instance.Remove();
            instances = null;
        }
    }
}
