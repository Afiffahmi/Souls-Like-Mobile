using System;
using UnityEngine;

namespace Nusantara.World
{
    [Serializable]
    public sealed class WorldPropEntry
    {
        public string name;
        public string assetGuid;
        public string assetPath;
        public Vector3 importedSize;
        public int vertices;
        public int triangles;
        public int materials;
        public int lodGroups;
        public int colliders;
        public string authoringNotes;
    }
    [CreateAssetMenu(menuName = "Nusantara/Prop Catalog")]
    public sealed class WorldPropCatalog : ScriptableObject
    {
        [Tooltip("Metadata only: this catalog does not retain every model or texture in runtime memory.")]
        public WorldPropEntry[] entries = Array.Empty<WorldPropEntry>();
    }
}
