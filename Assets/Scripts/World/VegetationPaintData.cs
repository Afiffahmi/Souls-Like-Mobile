using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    public enum VegetationPaintKind { Tree, Grass }
    [Serializable] public sealed class VegetationStamp
    {
        public VegetationPaintKind kind;
        public VegetationBrushStyle brushStyle;
        public Vector3 position;
        public float yaw;
        public float scale = 1;
        public int seed;
    }
    public sealed class VegetationPaintData : ScriptableObject
    {
        public Vector2Int coordinate;
        public List<VegetationStamp> stamps = new List<VegetationStamp>();
    }
}
