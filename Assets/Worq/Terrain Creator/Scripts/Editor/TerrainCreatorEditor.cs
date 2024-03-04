using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainCreator))]
public class LowPolyLandscaperEditor : Editor
{
    private TerrainCreator _target;

    private bool _leftMouseDown;
    private RaycastHit _hit;

    private DateTime _time;
    private float _deltaTime;

    private Vector2i _resolution;

    private void Awake()
    {
        _target = (TerrainCreator) target;
        _resolution = _target.TerrainSystem.Resolution;
    }

    private void OnEnable()
    {
        Tools.hidden = true;
    }

    private void OnDisable()
    {
        Tools.hidden = false;
    }

    private void OnSceneGUI()
    {
        if (Event.current.type == EventType.MouseMove) SceneView.RepaintAll();

        _deltaTime = Mathf.Min(0.01f, (float) (DateTime.Now - _time).Duration().TotalSeconds); //100Hz Baseline
        _time = DateTime.Now;
        if (Event.current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(0);
        }

        DrawCursor();
        HandleInteraction();
    }

    public override void OnInspectorGUI()
    {
        Undo.RecordObject(_target, _target.name);
        Inspect();
        if (GUI.changed)
        {
            RegenerateTerrain();
        }
    }

    private void Inspect()
    {
        using (new EditorGUILayout.VerticalScope("Button"))
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            {
                //EditorGUILayout.Space();
                EditorGUILayout.LabelField("Terrain Creator", EditorStyles.largeLabel);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            InspectWorld(_target.TerrainSystem);
            InspectTerrain();
            InspectTools();

            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope("Button"))
            {
                GUILayout.Space(5);

                EditorGUILayout.HelpBox("Settings", MessageType.None);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Force Re-Generate"))
                    {
                        RegenerateTerrain();
                    }

                    GUI.backgroundColor = Color.white;
                    GUI.backgroundColor = Color.red;

                    if (GUILayout.Button("Reset To Defaults") &&
                        EditorUtility.DisplayDialog("Confirm Reset", "Reset Terrain Defaults?", "Reset", "Cancel"))
                    {
                        _target.SetDefaults();
                        _target.TerrainSystem.Reinitialise();
                    }
                }
                EditorGUILayout.EndVertical();

                GUI.backgroundColor = Color.white;
                GUILayout.Space(5);
            }
        }
    }

    private void InspectWorld(TerrainSystem terrainSystem)
    {
        if (terrainSystem == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope("Button"))
        {
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("World", MessageType.None);

            terrainSystem.SetSize(EditorGUILayout.Vector2Field("Size", terrainSystem.Size));
            var resolution = EditorGUILayout.Vector2Field("Resolution", new Vector2(_resolution.x, _resolution.y));
            _resolution = new Vector2i((int) resolution.x, (int) resolution.y);
            // ReSharper disable once InvertIf
            if (_resolution.x != terrainSystem.Resolution.x || _resolution.y != terrainSystem.Resolution.y)
            {
                EditorGUILayout.HelpBox("Changing the resolution will reset the world.", MessageType.Warning);
                if (GUILayout.Button("Apply"))
                {
                    terrainSystem.SetResolution(_resolution);
                }
            }
        }
    }

    private void InspectTerrain()
    {
        GUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope("Button"))
        {
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Terrain Values", MessageType.None);

            _target.Seed = EditorGUILayout.IntField("Seed", _target.Seed);
            _target.Scale = EditorGUILayout.FloatField("Scale", _target.Scale);

            _target.Octaves = EditorGUILayout.IntSlider("Octaves", _target.Octaves, 0, 10);

            _target.Persistance = EditorGUILayout.FloatField("Pointiness", _target.Persistance);
            _target.Lacunarity = EditorGUILayout.FloatField("Noise", _target.Lacunarity);
            _target.FalloffStrength = EditorGUILayout.FloatField("Falloff", _target.FalloffStrength);
            _target.FalloffRamp = EditorGUILayout.FloatField("Falloff Ramp", _target.FalloffRamp);

            _target.FalloffRange = EditorGUILayout.Slider("Falloff Range", _target.FalloffRange, 0, 200);

            _target.Offset = EditorGUILayout.Vector2Field("Offset", _target.Offset);
            _target.HeightMultiplier = EditorGUILayout.FloatField("Height Multiplier", _target.HeightMultiplier);
            _target.HeightCurve = EditorGUILayout.CurveField("Height Curve", _target.HeightCurve);
        }

        GUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope("Button"))
        {
            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Painting", MessageType.None);

            _target.TerrainSystem.Interpolation =
                EditorGUILayout.Slider("Interpolation", _target.TerrainSystem.Interpolation, 0f, 1f);
            _target.TerrainSystem.FilterMode =
                (FilterMode) EditorGUILayout.EnumPopup("Filter Mode", _target.TerrainSystem.FilterMode);
            for (int i = 0; i < _target.TerrainSystem.Biotopes.Length; i++)
            {
                _target.TerrainSystem.Biotopes[i].Color =
                    EditorGUILayout.ColorField(_target.TerrainSystem.Biotopes[i].Color);
                float start = _target.TerrainSystem.Biotopes[i].StartHeight;
                float end = _target.TerrainSystem.Biotopes[i].EndHeight;
                EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, 1f);
                _target.TerrainSystem.SetBiotopeStartHeight(i, start);
                _target.TerrainSystem.SetBiotopeEndHeight(i, end);
            }

            if (GUILayout.Button("Add Color Layer"))
            {
                Array.Resize(ref _target.TerrainSystem.Biotopes, _target.TerrainSystem.Biotopes.Length + 1);
                _target.TerrainSystem.Biotopes[_target.TerrainSystem.Biotopes.Length - 1] = new Biotope();
            }

            if (GUILayout.Button("Remove Color Layer"))
            {
                if (_target.TerrainSystem.Biotopes.Length > 0)
                {
                    Array.Resize(ref _target.TerrainSystem.Biotopes,
                        _target.TerrainSystem.Biotopes.Length - 1);
                }
            }
        }
    }

    private void RegenerateTerrain()
    {
        EditorUtility.SetDirty(_target);

        _target.TerrainSystem.SetHeightMap(_target.TerrainSystem.CreateHeightMap(
            _target.Seed, _target.Scale, _target.Octaves, _target.Persistance, _target.Lacunarity,
            _target.FalloffStrength, _target.FalloffRamp, _target.FalloffRange, _target.Offset,
            _target.HeightMultiplier, _target.HeightCurve
        ));

        _target.TerrainSystem.SetColorMap(_target.TerrainSystem.CreateColorMap());
    }

    private void InspectTools()
    {
//        GUILayout.Space(10);
//        using (new EditorGUILayout.VerticalScope("Button"))
//        {
//            GUI.backgroundColor = Color.white;
//            EditorGUILayout.HelpBox("Terrain Brush", MessageType.None);
//
//            _target.ToolType = (ToolType) EditorGUILayout.EnumPopup("Brush Type", _target.ToolType);
//            _target.ToolSize = EditorGUILayout.FloatField("Size", _target.ToolSize);
//            _target.ToolStrength = EditorGUILayout.FloatField("Strength", _target.ToolStrength);
//            _target.ToolColor = EditorGUILayout.ColorField("Color", _target.ToolColor);
//        }
    }

    private void DrawCursor()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(new Vector2(Event.current.mousePosition.x,
            Event.current.mousePosition.y));
        Physics.Raycast(ray.origin, ray.direction, out _hit);

//        if (_hit.Equals(null))
//        {
        _target.MousePosition = _hit.point;
        //}
    }

    private void HandleInteraction()
    {
//        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
//        {
//            _leftMouseDown = true;
//        }
//
//        if (Event.current.type == EventType.mouseUp && Event.current.button == 0)
//        {
//            _leftMouseDown = false;
//        }
//
//        if (_leftMouseDown)
//        {
//            if (_target.ToolType == ToolType.PaintBrush)
//            {
//                _target.TerrainSystem.ModifyTexture(new Vector2(_target.MousePosition.x, _target.MousePosition.z),
//                    _target.ToolSize, _target.ToolStrength * _deltaTime, _target.ToolColor);
//
//                EditorUtility.SetDirty(_target.TerrainSystem);
//            }
//            else
//            {
//                _target.TerrainSystem.ModifyTerrain(new Vector2(_target.MousePosition.x, _target.MousePosition.z),
//                    _target.ToolSize, _target.ToolStrength * _deltaTime, _target.ToolType);
//
//                EditorUtility.SetDirty(_target.TerrainSystem);
//            }
//        }
    }
}