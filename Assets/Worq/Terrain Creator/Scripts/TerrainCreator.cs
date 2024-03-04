using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif

[ExecuteInEditMode]
public class TerrainCreator : MonoBehaviour
{
    public ToolType ToolType = ToolType.None;
    public float ToolSize = 5f;
    public float ToolStrength = 10f;
    public Color ToolColor = Color.white;

    public Vector3 MousePosition = Vector3.zero;

    public TerrainSystem TerrainSystem;

    //Terrain Generator
    public int Seed = 1;
    public float Scale = 5f;
    public int Octaves = 5;
    public float Persistance = 0.25f;
    public float Lacunarity = 4.0f;
    public float FalloffStrength = 1.0f;
    public float FalloffRamp = 2.0f;
    public float FalloffRange = 3.0f;
    public Vector2 Offset = Vector2.zero;
    public float HeightMultiplier = 5.0f;

    public AnimationCurve HeightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
//	public AnimationCurve HeightCurve = new AnimationCurve();

    // ReSharper disable once Unity.RedundantEventFunction
    private void OnDrawGizmosSelected()
    {
//        var color = new Color(0f, 1f, 0f, 0.5f);
//        Gizmos.color = color;
//        Gizmos.DrawSphere(MousePosition, ToolSize);
    }

    private void Awake()
    {
        if (TerrainSystem == null)
        {
            TerrainSystem = ScriptableObject.CreateInstance<TerrainSystem>().Initialize(this);
        }
    }

    void Update()
    {
        if (TerrainSystem == null)
        {
            TerrainSystem = ScriptableObject.CreateInstance<TerrainSystem>().Initialize(this);
        }

        TerrainSystem.Update();
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        if ((EditorApplication.isPlayingOrWillChangePlaymode || !Application.isPlaying) &&
            (!EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying))
        {
            DestroyImmediate(TerrainSystem.Terrain.gameObject);
        }
#endif
    }

    public void SetDefaults()
    {
        Seed = 1;
        Scale = 5f;
        Octaves = 5;
        Persistance = 0.25f;
        Lacunarity = 4.0f;
        FalloffStrength = 1.0f;
        FalloffRamp = 2.0f;
        FalloffRange = 3.0f;
        Offset = Vector2.zero;
        HeightMultiplier = 5.0f;
        HeightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        TerrainSystem.Size = new Vector2(50f, 50f);
        TerrainSystem.Resolution = new Vector2i(100, 100);
        TerrainSystem.Interpolation = 0.6f;
        TerrainSystem.FilterMode = FilterMode.Trilinear;
        TerrainSystem.Biotopes = new Biotope[0];

        SetDefaultBiotypes();
    }

    public void SetDefaultBiotypes()
    {
        var blue = new Biotope();
        var gray = new Biotope();
        var green = new Biotope();
        var white = new Biotope();

        blue.Color = new Color(0.1f, 0.4f, 0.4f, 1.0f);
        blue.StartHeight = 0.0f;
        blue.EndHeight = 0.05f;

        gray.Color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        gray.StartHeight = 0.05f;
        gray.EndHeight = 0.1f;

        green.Color = new Color(0.1f, 0.3f, 0.0f, 1.0f);
        green.StartHeight = 0.1f;
        green.EndHeight = 0.4f;

        white.Color = Color.white;
        white.StartHeight = 0.4f;
        white.EndHeight = 1.0f;

        TerrainSystem.Biotopes = new[]
        {
            blue,
            gray,
            green,
            white
        };
    }
}