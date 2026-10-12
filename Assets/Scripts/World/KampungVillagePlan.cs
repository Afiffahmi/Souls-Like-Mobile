using System;
using UnityEngine;
namespace Nusantara.World
{
    [Serializable] public sealed class KampungBuilding
    {
        public string id;
        public string asset;
        public Vector2 offset;
        public float yaw;
        public float height=5.5f;
        public Vector2 entrance;
    }
    [CreateAssetMenu(menuName="Nusantara/Kampung Village Plan")]
    public sealed class KampungVillagePlan:ScriptableObject
    {
        public Vector2 center;
        public Vector2[] footprint=Array.Empty<Vector2>();
        public KampungBuilding[] buildings=Array.Empty<KampungBuilding>();
        public WorldWalkingRoute[] paths=Array.Empty<WorldWalkingRoute>();
        public WorldWalkingPlatform[] platforms=Array.Empty<WorldWalkingPlatform>();
        public WorldWaterway[] channels=Array.Empty<WorldWaterway>();
        public int seed=5102026;
        [Range(.1f,2)]public float grassDensity=.85f;
    }
}
