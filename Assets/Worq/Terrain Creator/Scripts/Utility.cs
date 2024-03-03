using System;
using UnityEngine;

public static class Utility
{
    public static float Normalise(float value, float valueMin, float valueMax, float resultMin, float resultMax)
    {
        if (Math.Abs(valueMax - valueMin) > 0.0f)
        {
            return (value - valueMin) / (valueMax - valueMin) * (resultMax - resultMin) + resultMin;
        }
        else
        {
            return 0f;
        }
    }

    public static void FlatShading(ref Vector3[] vertices, ref int[] triangles, ref Vector2[] uvs)
    {
        var flatVertices = new Vector3[triangles.Length];
        var flatUVs = new Vector2[triangles.Length];
        for (var i = 0; i < triangles.Length; i++)
        {
            flatVertices[i] = vertices[triangles[i]];
            flatUVs[i] = uvs[triangles[i]];
            triangles[i] = i;
        }

        vertices = flatVertices;
        uvs = flatUVs;
    }

    public static Mesh[] CreateMeshes(Vector3[] vertices, int[] triangles, Vector2[] uvs, int chunkSize)
    {
        var meshes = new Mesh[Mathf.CeilToInt((float) vertices.Length / (float) chunkSize)];
        for (var i = 0; i < meshes.Length; i++)
        {
            var elements = Mathf.Min(chunkSize, vertices.Length - i * chunkSize);
            var mesh = new Mesh();
            var meshVertices = new Vector3[elements];
            var meshTriangles = new int[elements];
            var meshUVs = new Vector2[elements];
            for (var j = 0; j < elements; j++)
            {
                meshVertices[j] = vertices[i * chunkSize + j];
                meshTriangles[j] = triangles[i * chunkSize + j] % chunkSize;
                meshUVs[j] = uvs[i * chunkSize + j];
            }

            mesh.vertices = meshVertices;
            mesh.triangles = meshTriangles;
            mesh.uv = meshUVs;
            mesh.RecalculateNormals();
            meshes[i] = mesh;
        }

        return meshes;
    }

    public static T[] RangeSubset<T>(this T[] array, int startIndex, int length)
    {
        T[] subset = new T[length];
        Array.Copy(array, startIndex, subset, 0, length);
        return subset;
    }

    public static Texture2D CreateTexture(Color[] colorMap, int dimX, int dimY, FilterMode filterMode)
    {
        Texture2D texture = new Texture2D(dimX, dimY);
        texture.filterMode = filterMode;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colorMap);
        texture.Apply();
        return texture;
    }
}