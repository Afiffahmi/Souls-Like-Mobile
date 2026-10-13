using UnityEngine;
namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class WorldRegionGround : MonoBehaviour
    {
        public int region;
        public Vector2Int cells = new Vector2Int(6,6);
        public int nearIntervals = 32;
        public int distantIntervals = 8;
        public Mesh ground;
        public Mesh distantGround;
    }
}
