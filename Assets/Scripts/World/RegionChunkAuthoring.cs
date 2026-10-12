using UnityEngine;

namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class RegionChunkAuthoring : MonoBehaviour
    {
        [HideInInspector] public WorldChunkStreamer streamer;
        [HideInInspector] public GameObject editingRoot;
        [HideInInspector] public string assetFolder = "Assets/Region5Rework/Chunks";
        public WorldDefinition Definition => streamer != null ? streamer.definition : null;
    }
}
