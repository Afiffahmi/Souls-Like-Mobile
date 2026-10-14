using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed partial class WorldRoadBrushWindow : EditorWindow
    {
        [SerializeField] WorldSurfacePalette palette;
        [SerializeField] int selected;
        [SerializeField] int selectedRegion=1;
        readonly List<int> visibleStyles=new List<int>();
        [SerializeField] float radius=5, strength=.6f, hardness=.45f;
        [SerializeField] bool erase;
        bool painting, stroke, segment;
        int undoGroup, hotControl, detailSlot;
        Vector2 last;
        MeshCollider lastGround;
        readonly HashSet<Texture2D> recorded = new HashSet<Texture2D>();
        readonly HashSet<Material> materials = new HashSet<Material>();
        Toggle paintToggle;
        DropdownField preset;
        HelpBox status;
        VisualElement settings;
        WorldSurfaceStyle Current => palette != null && selected >= 0 && selected < palette.styles.Count ? palette.styles[selected] : null;

        [MenuItem("Tools/Nusantara/Road & Ground Brush")]
        public static void Open()
        {
            var window=GetWindow<WorldRoadBrushWindow>("Road & Ground Brush");
            window.minSize=new Vector2(380,540); window.Show();
        }
        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;
            Undo.undoRedoPerformed+=OnUndo;
            EditorApplication.playModeStateChanged+=OnPlay;
            AssemblyReloadEvents.beforeAssemblyReload+=DisablePainting;
        }
        void OnDisable()
        {
            DisablePainting();
            SceneView.duringSceneGui-=SceneGUI;
            Undo.undoRedoPerformed-=OnUndo;
            EditorApplication.playModeStateChanged-=OnPlay;
            AssemblyReloadEvents.beforeAssemblyReload-=DisablePainting;
        }
        void OnPlay(PlayModeStateChange state) => DisablePainting();
        public void DisablePainting()
        {
            EndStroke(); painting=false; paintToggle?.SetValueWithoutNotify(false);
            paintToggle?.RemoveFromClassList("paint-enabled"); SceneView.RepaintAll();
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/World/Editor/WorldRoadBrushWindow.uss");
            if(sheet!=null)rootVisualElement.styleSheets.Add(sheet);
            var root=new ScrollView();root.AddToClassList("road-panel");rootVisualElement.Add(root);
            var title=new Label("ROAD & GROUND");title.AddToClassList("road-title");root.Add(title);
            var note=new Label("Draw curved roads or brush natural surfaces onto your Nusantara regional ground.");note.AddToClassList("road-note");root.Add(note);
            if(palette==null)palette=WorldSurfacePaint.EnsurePalette();
            var field=new ObjectField("Surface palette"){objectType=typeof(WorldSurfacePalette),allowSceneObjects=false,value=palette};
            field.RegisterValueChangedCallback(e=>{DisablePainting();palette=e.newValue as WorldSurfacePalette;selected=0;RefreshPresets();});root.Add(field);
            selectedRegion=Mathf.Clamp(selectedRegion,0,9);
            var regionPicker=new DropdownField("Region",Enumerable.Range(0,10).Select(WorldRegionalRoadAssets.RegionLabel).ToList(),selectedRegion){name="roadRegion"};
            regionPicker.RegisterValueChangedCallback(e=>{DisablePainting();selectedRegion=regionPicker.index;RefreshPresets();SetStatus("Showing only "+WorldRegionalRoadAssets.RegionLabel(selectedRegion)+" surfaces.");});root.Add(regionPicker);
            preset=new DropdownField("Surface"){name="roadSurface"};root.Add(preset);
            preset.RegisterValueChangedCallback(e=>{EndStroke();selected=preset.index>=0&&preset.index<visibleStyles.Count?visibleStyles[preset.index]:-1;RefreshSettings();});
            AddRoadModeControls(root);
            settings=new VisualElement();root.Add(settings);
            root.Add(new Button(AddCustom){text="Duplicate as custom surface"});
            root.Add(new Button(()=>{if(palette!=null)Selection.activeObject=palette;}){text="Edit palette and add more surfaces"});
            root.Add(new Button(()=>{var folder=AssetDatabase.LoadAssetAtPath<DefaultAsset>(WorldRegionalRoadAssets.Folder(selectedRegion));if(folder!=null){Selection.activeObject=folder;EditorGUIUtility.PingObject(folder);}}){text="Show this region's road assets"});
            var heading=new Label("ROAD SIZE & EDGES");heading.AddToClassList("road-section");root.Add(heading);
            AddSlider(root,"Width (metres)",1,128,radius*2,v=>radius=v*.5f);
            AddSlider(root,"Strength",.02f,1,strength,v=>strength=v);
            AddSlider(root,"Edge hardness",0,.95f,hardness,v=>hardness=v);
            var eraseToggle=new Toggle("Erase back to original ground"){value=erase};
            brushEraseControl=eraseToggle;
            eraseToggle.RegisterValueChangedCallback(e=>{EndStroke();erase=e.newValue;});root.Add(eraseToggle);
            paintToggle=new Toggle("Enable road tool");
            paintToggle.RegisterValueChangedCallback(e=>
            {
                EndStroke();painting=e.newValue&&!EditorApplication.isPlayingOrWillChangePlaymode&&Current!=null;
                paintToggle.SetValueWithoutNotify(painting);paintToggle.EnableInClassList("paint-enabled",painting);
                if(painting)
                {
                    foreach(var regionTool in Resources.FindObjectsOfTypeAll<WorldRegionBrushWindow>())regionTool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldAINoWalkBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())tool.DisablePainting();
                    SetStatus(curveMode?"Click ground to place points; drag the point handles to shape the road. Apply when the preview is ready.":"Drag on the visible regional ground in Scene view. Shift erases. Each completed stroke saves automatically.");
                }
                else SetStatus("Road tool off. Exit Play mode and choose a surface to paint.");
                SceneView.RepaintAll();
            });root.Add(paintToggle);
            AddCurveActions(root);
            root.Add(new Button(()=>{EndStroke();Save();SetStatus("Surface paint and palette saved.");}){text="Save painted surfaces"});
            root.Add(new HelpBox("Brush: drag to paint, Shift to erase.\nCurved Road: click to add points, drag handles to reshape, Enter to apply, Backspace to remove the last point.\nCtrl+Z: undo  |  Esc: stop  |  Alt + mouse: orbit\nRoads follow the ground. They do not change heights, AI routes, trees or grass.",HelpBoxMessageType.Info));
            status=new HelpBox("Choose a surface, enable the brush, then paint in Scene view. Width = twice the radius.",HelpBoxMessageType.None);root.Add(status);
            RefreshPresets();RefreshCurveControls();
        }
        void AddSlider(VisualElement root,string label,float min,float max,float value,Action<float> setter)
        {
            var slider=new Slider(label,min,max){value=value,showInputField=true};
            slider.RegisterValueChangedCallback(e=>{if(float.IsNaN(e.newValue)||float.IsInfinity(e.newValue))return;EndStroke();setter(Mathf.Clamp(e.newValue,min,max));SceneView.RepaintAll();});root.Add(slider);
        }
        void RefreshPresets()
        {
            if(preset==null)return;
            visibleStyles.Clear();
            if(palette!=null)for(int i=0;i<palette.styles.Count;i++)if(palette.styles[i]!=null&&palette.styles[i].region==selectedRegion)visibleStyles.Add(i);
            if(!visibleStyles.Contains(selected))selected=visibleStyles.Count>0?visibleStyles[0]:-1;
            preset.choices=visibleStyles.Select((index,i)=>$"{i+1}. {palette.styles[index].name}").ToList();
            preset.SetValueWithoutNotify(selected>=0?preset.choices[visibleStyles.IndexOf(selected)]:"");
            preset.SetEnabled(visibleStyles.Count>0);RefreshSettings();
        }
        public void RefreshRegionalAssets(){DisablePainting();RefreshPresets();}
        void RefreshSettings()
        {
            if(settings==null)return;settings.Clear();var style=Current;if(style==null)return;
            if(style.texture!=null){var preview=new Image{image=style.texture,scaleMode=ScaleMode.ScaleToFit};preview.AddToClassList("road-preview");settings.Add(preview);}
            void Change(Action action){EndStroke();Undo.RecordObject(palette,"Edit surface preset");action();EditorUtility.SetDirty(palette);}
            var color=new ColorField("Colour"){value=style.color,showAlpha=false};
            color.RegisterValueChangedCallback(e=>Change(()=>style.color=e.newValue));settings.Add(color);
            var texture=new ObjectField("Optional texture"){objectType=typeof(Texture2D),allowSceneObjects=false,value=style.texture};
            texture.RegisterValueChangedCallback(e=>Change(()=>style.texture=e.newValue as Texture2D));settings.Add(texture);
            AddSlider(settings,"Pattern scale (m)",.5f,32,style.patternSize,v=>Change(()=>style.patternSize=v));
            AddSlider(settings,"Colour variation",0,.6f,style.variation,v=>Change(()=>style.variation=v));
            var hint=new Label("Fine textures repeat in world metres, independently of brush size. Preset edits update that style when you next paint. Custom textures need Read/Write enabled.");hint.AddToClassList("road-note");settings.Add(hint);
        }
        void AddCustom()
        {
            if(palette==null)return;EndStroke();var source=Current??new WorldSurfaceStyle();
            Undo.RecordObject(palette,"Add surface preset");
            palette.styles.Add(new WorldSurfaceStyle{region=selectedRegion,name="Custom "+(palette.styles.Count+1),color=source.color,variation=source.variation,patternSize=source.patternSize,texture=source.texture});
            selected=palette.styles.Count-1;EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);RefreshPresets();
        }
        void SetStatus(string message){if(status!=null)status.text=message;}
        bool Ground(Ray ray,out RaycastHit hit,out WorldRegionGround region)
        {
            hit=default;region=null;float nearest=float.MaxValue;
            foreach(var candidate in Physics.RaycastAll(ray,20000,~0,QueryTriggerInteraction.Ignore))
            {
                if(candidate.distance>=nearest||!(candidate.collider is MeshCollider mesh)||SceneVisibilityManager.instance.IsHidden(mesh.gameObject))continue;
                var marker=mesh.GetComponentInParent<WorldRegionGround>();
                if(marker==null||mesh.sharedMesh!=marker.ground)continue;
                hit=candidate;region=marker;nearest=candidate.distance;
            }
            return region!=null;
        }
        void SceneGUI(SceneView view)
        {
            var e=Event.current;
            if(stroke&&(e.rawType==EventType.MouseUp||e.type==EventType.Ignore||e.type==EventType.MouseLeaveWindow))EndStroke();
            if(!painting||EditorApplication.isPlayingOrWillChangePlaymode||Current==null)return;
            if(curveMode){CurveSceneGUI(view);return;}
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==KeyCode.Escape){DisablePainting();e.Use();return;}
                if(e.keyCode==KeyCode.LeftBracket||e.keyCode==KeyCode.RightBracket)
                {EndStroke();radius=Mathf.Clamp(radius*(e.keyCode==KeyCode.LeftBracket?.85f:1.15f),1.5f,64);CreateGUI();paintToggle.SetValueWithoutNotify(painting);e.Use();view.Repaint();return;}
            }
            if(e.alt||Tools.viewToolActive){EndStroke();return;}
            int control=GUIUtility.GetControlID("NusantaraRoadGroundBrush".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            if(!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,out var region)){segment=false;return;}
            bool erasing=erase||e.shift;
            Handles.color=erasing?new Color(1,.55f,.25f):new Color(.95f,.85f,.5f);
            Handles.DrawWireDisc(hit.point+hit.normal*.08f,hit.normal,radius);
            Handles.DrawWireDisc(hit.point+hit.normal*.08f,hit.normal,radius*hardness);
            Handles.Label(hit.point+Vector3.up,$"{(erasing?"Erase":Current.name)} | {radius*2:0.#} m wide | Region {region.region}");
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.button!=0||(e.type!=EventType.MouseDown&&e.type!=EventType.MouseDrag))return;
            if(e.type==EventType.MouseDown&&HandleUtility.nearestControl!=control)return;
            if(!stroke&&e.type!=EventType.MouseDown)return;
            try
            {
                if(!erasing&&Current.texture!=null&&!Current.texture.isReadable)throw new InvalidOperationException("Enable Read/Write on the custom surface texture before painting.");
                var collider=(MeshCollider)hit.collider;
                var renderer=collider.GetComponent<MeshRenderer>();
                var material=renderer!=null?renderer.sharedMaterial:null;
                if(material==null)throw new InvalidOperationException("No regional ground material found.");
                if(erasing&&(material.shader.name!=WorldSurfacePaint.ShaderName||material.GetTexture("_SurfacePaint")==null)){e.Use();return;}
                if(!stroke)
                {
                    detailSlot=WorldSurfaceDetails.Slot(Current);EditorUtility.SetDirty(palette);
                    Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(erasing?"Erase ground surface":"Paint "+Current.name);
                    recorded.Clear();materials.Clear();stroke=true;segment=false;hotControl=control;GUIUtility.hotControl=control;
                }
                Rect bounds=WorldSurfacePaint.Bounds(collider);
                var sourceMap=material.GetTexture("_SurfacePaint") as Texture2D;
                var point=new Vector2(hit.point.x,hit.point.z);
                void Dab(Vector2 p) => WorldSurfaceBrush.StampAcrossGrounds(collider,
                    new Vector3(p.x,hit.point.y,p.y),radius,strength,hardness,Current,detailSlot,erasing,recorded,materials);
                float spacing=Mathf.Max(bounds.width/(sourceMap!=null?sourceMap.width:1024)*.5f,radius*.16f);
                bool adjoining=lastGround!=null&&lastGround.GetComponentInParent<WorldRegionGround>()!=null
                    &&lastGround.GetComponentInParent<WorldRegionGround>().region<8&&region.region<8;
                if(!segment||(lastGround!=collider&&!adjoining)){Dab(point);last=point;segment=true;}
                else
                {
                    float distance=Vector2.Distance(last,point);int count=Mathf.FloorToInt(distance/spacing);
                    var direction=distance>0?(point-last)/distance:Vector2.zero;
                    for(int i=0;i<count;i++){last+=direction*spacing;Dab(last);}
                }
                lastGround=collider;
                SetStatus($"{(erasing?"Erasing":"Painting")} Region {region.region}. Release to save; Ctrl+Z undoes the stroke.");
                e.Use();view.Repaint();
            }
            catch(Exception error){DisablePainting();SetStatus(error.Message);Debug.LogException(error);}
        }
        void EndStroke()
        {
            if(!stroke)return;stroke=false;segment=false;
            if(GUIUtility.hotControl==hotControl)GUIUtility.hotControl=0;hotControl=0;
            Undo.CollapseUndoOperations(undoGroup);Save();SceneView.RepaintAll();
        }
        void Save()
        {
            foreach(var map in recorded)if(map!=null)AssetDatabase.SaveAssetIfDirty(map);
            foreach(var material in materials)if(material!=null)AssetDatabase.SaveAssetIfDirty(material);
            if(palette!=null)AssetDatabase.SaveAssetIfDirty(palette);
        }
        void OnUndo()
        {
            EndStroke();
            if(AssetDatabase.IsValidFolder(WorldSurfacePaint.Root))
                foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{WorldSurfacePaint.Root}))
                {
                    var map=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if(map==null||!map.isReadable)continue;
                    if(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(map)).Replace('\\','/')!=WorldSurfacePaint.Root)continue;
                    map.Apply();EditorUtility.SetDirty(map);AssetDatabase.SaveAssetIfDirty(map);
                }
            Save();RefreshPresets();RefreshCurveControls();SetStatus("Undo / redo applied and saved.");SceneView.RepaintAll();
        }
    }
}

