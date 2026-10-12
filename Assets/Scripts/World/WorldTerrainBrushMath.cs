using System;
using UnityEngine;

namespace Nusantara.World
{
    public static class WorldTerrainBrushMath
    {
        /// <summary>Unit strength at the centre, with a smooth fade outside the hardness plateau.</summary>
        public static float Falloff(float distance, float radius, float hardness)
        {
            if (!IsFinite(distance) || !IsFinite(radius) || radius <= 0) return 0;
            distance = Mathf.Max(0, distance);
            if (distance >= radius) return 0;
            hardness = IsFinite(hardness) ? Mathf.Clamp01(hardness) : 0;
            float normalized = distance / radius;
            if (normalized <= hardness || hardness >= 1) return 1;
            float t = (normalized - hardness) / (1 - hardness);
            return 1 - t * t * (3 - 2 * t);
        }

        /// <summary>Deterministic three-octave coherent noise, zero-centred and bounded to [-1, 1].</summary>
        public static float Noise(Vector2 worldPosition, float featureSize, int seed)
        {
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y)) return 0;
            featureSize = IsFinite(featureSize) && featureSize > 0 ? Mathf.Max(.01f, featureSize) : 1;
            double x = (double)worldPosition.x / featureSize;
            double z = (double)worldPosition.y / featureSize;
            double result = 0, amplitude = 1, total = 0;
            for (int octave = 0; octave < 3; octave++)
            {
                result += ValueNoise(x, z, unchecked(seed + octave * 1013)) * amplitude;
                total += amplitude;
                amplitude *= .5;
                x *= 2;
                z *= 2;
            }
            return Mathf.Clamp((float)(result / total), -1, 1);
        }

        static double ValueNoise(double x, double z, int seed)
        {
            // Periodic integer coordinates avoid conversion overflow for extreme world positions.
            // Hash the wrapped neighbours as well, keeping the wrap itself continuous.
            const int period = 1048576;
            x %= period; z %= period;
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            double tx = x - ix, tz = z - iz;
            tx = tx * tx * (3 - 2 * tx);
            tz = tz * tz * (3 - 2 * tz);
            double a = Hash(ix & (period - 1), iz & (period - 1), seed);
            double b = Hash((ix + 1) & (period - 1), iz & (period - 1), seed);
            double c = Hash(ix & (period - 1), (iz + 1) & (period - 1), seed);
            double d = Hash((ix + 1) & (period - 1), (iz + 1) & (period - 1), seed);
            return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
        }

        static double Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint hash = (uint)x * 374761393u + (uint)z * 668265263u + (uint)seed * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 8388607.5 - 1;
            }
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
