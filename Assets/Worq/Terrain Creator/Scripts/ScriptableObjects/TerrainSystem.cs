using System;
using UnityEngine;
using Random = UnityEngine.Random;

//[CreateAssetMenu(fileName = "Terrain Data", menuName = "Terrain Data", order = 1)]
public class TerrainSystem : ScriptableObject
{
    public TerrainCreator Landscaper;
    public Transform Terrain;

    public Vector2 Size = new Vector2(50f, 50f);
    public Vector2i Resolution = new Vector2i(100, 100);

    public Mesh[] Meshes;
    public Vertex[] Vertices;

    public Vector2 VertexDistance;
    public Vector3[] VertexData;
    public int[] TriangleData;
    public Vector2[] UvData;
    public float[] HeightMap;
    public Color[] ColorMap;
    public Material Material;
    public Texture2D Texture;

    public float Interpolation = 0.5f;
    public FilterMode FilterMode = FilterMode.Trilinear;
    public Biotope[] Biotopes = new Biotope[0];

    public int ChunkSize = 6000;

    public TerrainSystem Initialize(TerrainCreator landscaper)
    {
        Landscaper = landscaper;
        Terrain = new GameObject("Terrain").transform;
        Terrain.SetParent(landscaper.transform);
        CreateTerrain();
        return this;
    }

    public void Reinitialise()
    {
        CreateTerrain();
    }

    public void SetSize(Vector2 size)
    {
        if (Size != size)
        {
            Size = size;
            for (int y = 0; y < Resolution.y; y++)
            {
                for (int x = 0; x < Resolution.x; x++)
                {
                    Vector2 position = GridToWorld(x, y);
                    Vertices[GridToArray(x, y)].SetPosition(position.x, position.y, this);
                }
            }

            for (int i = 0; i < Meshes.Length; i++)
            {
                Meshes[i].vertices = VertexData.RangeSubset(i * ChunkSize,
                    Mathf.Min(ChunkSize, VertexData.Length - i * ChunkSize));
                Meshes[i].RecalculateNormals();
                //Terrain.GetChild(i).GetComponent<MeshCollider>().sharedMesh = Meshes[i];
            }
        }
    }

    public void SetResolution(Vector2i resolution)
    {
        resolution.x = Mathf.Max(resolution.x, 2);
        resolution.y = Mathf.Max(resolution.y, 2);
        if (Resolution.x != resolution.x || Resolution.y != resolution.y)
        {
            Resolution = resolution;
            CreateTerrain();
        }
    }

    public void Update()
    {
        Terrain.localPosition = Vector3.zero;
        Terrain.localRotation = Quaternion.identity;
        for (int i = 0; i < Terrain.childCount; i++)
        {
            Terrain.GetChild(i).localPosition = Vector3.zero;
            Terrain.GetChild(i).localRotation = Quaternion.identity;
        }

        //Debug.Log(Biotopes[0].Color);
    }

    public void Record()
    {
        //TODO
    }

    public void Undo()
    {
        //TODO
    }

    public void Redo()
    {
        //TODO
    }

    public void ModifyTerrain(Vector2 world, float size, float strength, ToolType tool)
    {
        var meshUpdates = new bool[Meshes.Length];

        var sqrSize = size * size;
        for (var y = world.y - size; y <= world.y + size; y += VertexDistance.y)
        {
            for (var x = world.x - size; x <= world.x + size; x += VertexDistance.x)
            {
                var sqrDist = (world.x - x) * (world.x - x) + (world.y - y) * (world.y - y);
                if (sqrDist <= sqrSize)
                {
                    var vertex = GetVertex(x, y);
                    if (vertex != null)
                    {
                        var weight = (size - Mathf.Sqrt(sqrDist)) / size;

                        switch (tool)
                        {
                            case ToolType.None:
                                break;

                            case ToolType.Bump:
                                vertex.UpdateHeight(weight * Random.Range(0f, strength), this);
                                break;

                            case ToolType.Noise:
                                vertex.UpdateHeight(weight * Random.Range(-strength, strength), this);
                                break;

                            case ToolType.Smooth:
                                var neighbours = 0f;
                                var height = 0f;
                                for (var i = -1; i <= 1; i++)
                                {
                                    for (var j = -1; j <= 1; j++)
                                    {
                                        var neighbour = GetVertex(x + i, y + j);
                                        if (neighbour != null)
                                        {
                                            neighbours += 1f;
                                            height += neighbour.GetHeight(this);
                                        }
                                    }
                                }

                                var avg = height / neighbours;
                                vertex.SetHeight((1f - weight) * vertex.GetHeight(this) + weight * avg, this);
                                break;
                            case ToolType.PaintBrush:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(tool), tool, null);
                        }

                        foreach (var index in vertex.MeshIndices)
                        {
                            meshUpdates[index] = true;
                        }
                    }
                }
            }
        }

        for (var i = 0; i < Meshes.Length; i++)
        {
            if (meshUpdates[i])
            {
                Meshes[i].vertices = VertexData.RangeSubset(i * ChunkSize,
                    Mathf.Min(ChunkSize, VertexData.Length - i * ChunkSize));
                Meshes[i].RecalculateNormals();

                Terrain.GetChild(i).GetComponent<MeshCollider>().sharedMesh = Meshes[i];
            }
        }
    }

    public void ModifyTexture(Vector2 world, float size, float strength, Color color)
    {
        var sqrSize = size * size;
        for (var y = world.y - size; y <= world.y + size; y += VertexDistance.y)
        {
            for (var x = world.x - size; x <= world.x + size; x += VertexDistance.x)
            {
                var sqrDist = (world.x - x) * (world.x - x) + (world.y - y) * (world.y - y);
                if (sqrDist <= sqrSize)
                {
                    var vertex = GetVertex(x, y);
                    if (vertex != null)
                    {
                        vertex.SetColor(Color.Lerp(vertex.GetColor(this), color,
                            (size - Mathf.Sqrt(sqrDist)) / size), this);
                    }
                }
            }
        }

        Texture.SetPixels(ColorMap);
        Texture.Apply();
    }

    public Vertex GetVertex(float worldX, float worldY)
    {
        return GetVertex(GetCoordinates(worldX, worldY));
    }

    public Vertex GetVertex(Vector2i grid)
    {
        return GetVertex(grid.x, grid.y);
    }

    public Vertex GetVertex(int gridX, int gridY)
    {
        if (gridX >= 0 && gridY >= 0 && gridX < Resolution.x && gridY < Resolution.y)
        {
            return Vertices[GridToArray(gridX, gridY)];
        }
        else
        {
            return null;
        }
    }

    public Vector2i GetCoordinates(float worldX, float worldY)
    {
        int x = Mathf.RoundToInt((worldX + Size.x / 2f) / Size.x * Resolution.x);
        int y = Mathf.RoundToInt((worldY + Size.y / 2f) / Size.y * Resolution.y);
        return new Vector2i(x, y);
    }

    public Vector2 GridToWorld(int gridX, int gridY)
    {
        return new Vector2(Size.x * (float) gridX / ((float) Resolution.x - 1f) - Size.x / 2f,
            Size.y * (float) gridY / ((float) Resolution.y - 1) - Size.y / 2f);
    }

    public int GridToArray(int gridX, int gridY)
    {
        return gridY * Resolution.x + gridX;
    }

    public void SetHeightMap(float[] heightMap)
    {
        for (int i = 0; i < Vertices.Length; i++)
        {
            Vertices[i].SetHeight(heightMap[i], this);
        }

        for (int i = 0; i < Meshes.Length; i++)
        {
            Meshes[i].vertices =
                VertexData.RangeSubset(i * ChunkSize, Mathf.Min(ChunkSize, VertexData.Length - i * ChunkSize));
            Meshes[i].RecalculateNormals();
            //Terrain.GetChild(i).GetComponent<MeshCollider>().sharedMesh = Meshes[i];
        }
    }

    public void SetColorMap(Color[] colorMap)
    {
        for (int i = 0; i < Vertices.Length; i++)
        {
            Vertices[i].SetColor(colorMap[i], this);
        }

        Texture.SetPixels(ColorMap);
        Texture.filterMode = FilterMode;
        Texture.Apply();
    }

    public float[] CreateHeightMap(int seed, float scale, int octaves, float persistance, float lacunarity,
        float falloffStrength, float falloffRamp, float falloffRange, Vector2 offset, float heightMultiplier,
        AnimationCurve heightCurve)
    {
        float[] heightMap = new float[Resolution.x * Resolution.y];

        Vector2[] octaveOffsets = new Vector2[octaves];

        Random.InitState(seed);

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = Random.Range(-100f, 100f);
            float offsetY = Random.Range(-100f, 100f);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;
        for (int y = 0; y < Resolution.y; y++)
        {
            for (int x = 0; x < Resolution.x; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float xPos = (((float) x + offset.x) - (float) Resolution.x / 2f) / (float) Resolution.x;
                    float yPos = (((float) y + offset.y) - (float) Resolution.y / 2f) / (float) Resolution.y;
                    float sampleX = frequency * scale * xPos + octaveOffsets[i].y;
                    float sampleY = frequency * scale * yPos + octaveOffsets[i].y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                maxLocalNoiseHeight = Mathf.Max(maxLocalNoiseHeight, noiseHeight);
                minLocalNoiseHeight = Mathf.Min(minLocalNoiseHeight, noiseHeight);

                heightMap[GridToArray(x, y)] = noiseHeight;
            }
        }

        for (var y = 0; y < Resolution.y; y++)
        {
            for (var x = 0; x < Resolution.x; x++)
            {
                var value = Mathf.Max(Mathf.Abs(x / (float) Resolution.x * 2f - 1f),
                    Mathf.Abs(y / (float) Resolution.y * 2f - 1f));
                var a = Mathf.Pow(value, falloffRamp);
                var b = Mathf.Pow(falloffRange - falloffRange * value, falloffRamp);
                var falloff = 1f - (a + b != 0f ? falloffStrength * a / (a + b) : 0f);

                try
                {
                    heightMap[GridToArray(x, y)] = heightMultiplier * heightCurve.Evaluate(
                                                       falloff * Utility.Normalise(heightMap[GridToArray(x, y)],
                                                           minLocalNoiseHeight, maxLocalNoiseHeight, 0f, 1f));
                }
                catch (Exception)
                {
                    //Ignored
                }
            }
        }

        return heightMap;
    }

    public Color[] CreateColorMap()
    {
        var colorMap = new Color[Resolution.x * Resolution.y];
        for (var y = 0; y < Resolution.y; y++)
        {
            for (var x = 0; x < Resolution.x; x++)
            {
                var height = GetVertex(x, y).GetHeight(this) / Landscaper.HeightMultiplier;
                var index = GetBiotopeIndex(height);
                if (index != -1)
                {
                    var color = Biotopes[index].Color;

                    var colorPrevious = index > 0 ? Biotopes[index - 1].Color : Biotopes[index].Color;

                    var colorNext = index < Biotopes.Length - 1 ? Biotopes[index + 1].Color : Biotopes[index].Color;

                    var distPrevious = Interpolation * (1f - (height - Biotopes[index].StartHeight) /
                                                        (Biotopes[index].EndHeight - Biotopes[index].StartHeight));
                    var distNext = Interpolation * (1f - (Biotopes[index].EndHeight - height) /
                                                    (Biotopes[index].EndHeight - Biotopes[index].StartHeight));
                    color = Color.Lerp(Color.Lerp(color, colorPrevious, distPrevious),
                        Color.Lerp(color, colorNext, distNext), 0.5f);
                    colorMap[GridToArray(x, y)] = color;
                }
                else
                {
                    colorMap[GridToArray(x, y)] = Color.white;
                }
            }
        }

        return colorMap;
    }

    public void SetBiotopeStartHeight(int index, float value)
    {
        if (index > 0)
        {
            Biotopes[index].StartHeight = Mathf.Max(Biotopes[index - 1].StartHeight, value);
            Biotopes[index - 1].EndHeight = Biotopes[index].StartHeight;
        }
        else
        {
            Biotopes[index].StartHeight = 0f;
        }
    }

    public void SetBiotopeEndHeight(int index, float value)
    {
        if (index < Biotopes.Length - 1)
        {
            Biotopes[index].EndHeight = Mathf.Min(Biotopes[index + 1].EndHeight, value);
            Biotopes[index + 1].StartHeight = Biotopes[index].EndHeight;
        }
        else
        {
            Biotopes[index].EndHeight = 1f;
        }
    }

    public void SetBiotopeColor(int index, Color color)
    {
        Biotopes[index].Color = color;
    }

    public int GetBiotopeIndex(float height)
    {
        for (int i = 0; i < Biotopes.Length; i++)
        {
            if (Biotopes[i].StartHeight <= height && Biotopes[i].EndHeight >= height)
            {
                return i;
            }
        }

        return -1;
    }

    private void CreateTerrain()
    {
        float[] heightMap = new float[Resolution.x * Resolution.y];
        Color[] colorMap = new Color[Resolution.x * Resolution.y];
        for (int i = 0; i < colorMap.Length; i++)
        {
            colorMap[i] = Color.grey;
        }

        CreateTerrain(heightMap, colorMap);
    }

    private void CreateTerrain(float[] heightMap)
    {
        Color[] colorMap = new Color[Resolution.x * Resolution.y];
        for (int i = 0; i < colorMap.Length; i++)
        {
            colorMap[i] = Color.grey;
        }

        CreateTerrain(heightMap, colorMap);
    }

    private void CreateTerrain(Color[] colorMap)
    {
        float[] heightMap = new float[Resolution.x * Resolution.y];
        CreateTerrain(heightMap, colorMap);
    }


    private void CreateTerrain(float[] heightMap, Color[] colorMap)
    {
        //Clean up
        while (Terrain.childCount > 0)
        {
            DestroyImmediate(Terrain.GetChild(0).gameObject);
        }

        DestroyImmediate(Material);
        Resources.UnloadUnusedAssets();

        //Allocate memory
        HeightMap = heightMap;
        ColorMap = colorMap;
        Vertices = new Vertex[Resolution.x * Resolution.y];
        VertexData = new Vector3[Resolution.x * Resolution.y];
        TriangleData = new int[6 * Resolution.x * Resolution.y];
        UvData = new Vector2[Resolution.x * Resolution.y];

        //Calculate vertex distance
        VertexDistance = new Vector2(Size.x / (float) Resolution.x, Size.y / (float) Resolution.y);

        //Create vertices
        for (int y = 0; y < Resolution.y; y++)
        {
            for (int x = 0; x < Resolution.x; x++)
            {
                int index = GridToArray(x, y);
                Vector2 position = GridToWorld(x, y);
                Vertices[index] = new Vertex(index);
                VertexData[index] = new Vector3(position.x, heightMap[index], position.y);
            }
        }

        //Create triangles
        int triangleIndex = 0;
        for (int y = 0; y < Resolution.y - 1; y++)
        {
            for (int x = 0; x < Resolution.x - 1; x++)
            {
                int a = GridToArray(x, y);
                int b = GridToArray(x, y + 1);
                int c = GridToArray(x + 1, y);
                int d = GridToArray(x + 1, y + 1);

                TriangleData[triangleIndex] = a;
                Vertices[a].AddVertexIndex(triangleIndex);
                Vertices[a].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;

                TriangleData[triangleIndex] = d;
                Vertices[d].AddVertexIndex(triangleIndex);
                Vertices[d].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;

                TriangleData[triangleIndex] = c;
                Vertices[c].AddVertexIndex(triangleIndex);
                Vertices[c].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;

                TriangleData[triangleIndex] = d;
                Vertices[d].AddVertexIndex(triangleIndex);
                Vertices[d].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;

                TriangleData[triangleIndex] = a;
                Vertices[a].AddVertexIndex(triangleIndex);
                Vertices[a].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;

                TriangleData[triangleIndex] = b;
                Vertices[b].AddVertexIndex(triangleIndex);
                Vertices[b].AddMeshIndex(Mathf.FloorToInt((float) triangleIndex / (float) ChunkSize));
                triangleIndex += 1;
            }
        }

        //Create UVs
        for (var y = 0; y < Resolution.y; y++)
        {
            for (var x = 0; x < Resolution.x; x++)
            {
                var percentX = (float) x / ((float) Resolution.x);
                var percentY = (float) y / ((float) Resolution.y);
                UvData[GridToArray(x, y)] = new Vector2(percentX, percentY);
            }
        }

        //Apply flat shading
        Utility.FlatShading(ref VertexData, ref TriangleData, ref UvData);
        
        //Create meshes
        Meshes = Utility.CreateMeshes(VertexData, TriangleData, UvData, ChunkSize);

        //Create material, texture, colormap
        Material = new Material(Shader.Find("Standard"));
        Texture = Utility.CreateTexture(ColorMap, Resolution.x, Resolution.y, FilterMode);
        Material.mainTexture = Texture;

        //Instantiate
        foreach (var mesh in Meshes)
        {
            var instance = new GameObject("Mesh");
            instance.transform.SetParent(Terrain);
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            var filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            instance.AddComponent<MeshCollider>();
        }
    }
}