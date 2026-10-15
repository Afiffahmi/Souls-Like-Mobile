using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    /// <summary>Editable authoring settings and the last applied, additive height contribution.</summary>
    [Serializable]
    public sealed class WorldTerrainShape
    {
        public enum ShapeKind { Curve, Outline }
        public enum Operation { Raise, Lower, Uneven, Smooth, Flatten }

        [Serializable]
        public struct Sample
        {
            public Vector2Int coordinate;
            public float delta;
            public Sample(Vector2Int coordinate, float delta) { this.coordinate = coordinate; this.delta = delta; }
        }

        public string id, name = "Terrain shape";
        public ShapeKind kind;
        public Operation operation;
        public List<Vector3> points = new List<Vector3>();
        public bool smooth = true, protect = true;
        public float radius = 32, strength = 2, hardness = .15f, amplitude = 2, featureSize = 48, blend = 1, targetHeight;
        public int seed = 501;
        public List<Sample> samples = new List<Sample>();
    }
}
