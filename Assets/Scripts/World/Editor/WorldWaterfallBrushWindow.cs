using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed class WorldWaterfallBrushWindow : EditorWindow
    {
        const string MaterialPath = "Assets/NusantaraWorld/Water/Waterfall Brush.mat";
        [SerializeField] float width = 5, bow = 1, speed = 1.2f, splash = 1, foam = .75f, offset = .12f;
        [SerializeField] Color tint = new Color(.04f,.69f,.78f,1);
        [SerializeField] bool matchRiverColours = true;
        [SerializeField] WorldWaterfall selected;
        bool painting, dragging;
        int control;
        Vector3 top, bottom, forward;
        Plane dragPlane;
        Toggle toggle;
        HelpBox status;
        ObjectField selection;

        [MenuItem("Tools/Water/Waterfall Brush", false, 20)]
        [MenuItem("Tools/Nusantara/Water/Waterfall Brush", false, 20)]
        public static void Open()
        {
            var window=GetWindow<WorldWaterfallBrushWindow>("Waterfall Brush");
            window.minSize=new Vector2(360,540);window.Show();
        }
        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;
            Undo.undoRedoPerformed+=OnUndo;
            EditorApplication.playModeStateChanged+=OnPlay;
            EditorApplication.update+=CheckOtherTools;
        }
        void OnDisable()
        {
            DisablePainting();SceneView.duringSceneGui-=SceneGUI;
            Undo.undoRedoPerformed-=OnUndo;EditorApplication.playModeStateChanged-=OnPlay;
            EditorApplication.update-=CheckOtherTools;
        }
        void CheckOtherTools()
        {
            if(!painting)return;
            var window=focusedWindow;
            if(window is WorldWaterBrushWindow||window is WorldRoadBrushWindow||window is WorldTerrainBrushWindow||window is WorldRegionBrushWindow||window is WorldAINoWalkBrushWindow||window is VegetationPainterWindow)DisablePainting();
        }
        void OnPlay(PlayModeStateChange state)=>DisablePainting();
        void OnUndo()
        {
            foreach(var fall in UnityEngine.Object.FindObjectsByType<WorldWaterfall>(FindObjectsSortMode.None))fall.Rebuild();
            SceneView.RepaintAll();
        }
        void OnSelectionChange()
        {
            var go=Selection.activeGameObject;
            var fall=go!=null?go.GetComponentInParent<WorldWaterfall>():null;
            if(fall!=null){selected=fall;selection?.SetValueWithoutNotify(fall);}
        }
        public void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();
            var styles=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/World/Editor/WorldWaterfallBrushWindow.uss");
            if(styles!=null)root.styleSheets.Add(styles);
            var body=new ScrollView();body.AddToClassList("waterfall-panel");root.Add(body);
            var title=new Label("WATERFALL BRUSH");title.AddToClassList("waterfall-title");body.Add(title);
            var note=new Label("Turquoise curtains • flowing streaks • foaming impact");note.AddToClassList("waterfall-note");body.Add(note);
            body.Add(new HelpBox("Drag from the cliff lip down to the landing point in Scene view, then release. Each drag makes one waterfall. Ground needs a collider; painted water is picked automatically.",HelpBoxMessageType.Info));
            toggle=new Toggle("Enable waterfall brush"){value=painting};
            toggle.RegisterValueChangedCallback(e=>
            {
                DisablePainting();painting=e.newValue&&!EditorApplication.isPlayingOrWillChangePlaymode;
                if(painting)StopOtherBrushes();toggle.SetValueWithoutNotify(painting);
                SetStatus(painting?"Drag from the lip to the base. Shift-click a waterfall to erase. Esc stops the brush.":"Brush stopped. Select a waterfall to adjust it.");SceneView.RepaintAll();
            });body.Add(toggle);
            AddSlider(body,"Width (m)",.25f,40,width,v=>width=v);
            AddSlider(body,"Outward curve (m)",0,8,bow,v=>bow=v);
            AddSlider(body,"Flow speed",.1f,5,speed,v=>speed=v);
            AddSlider(body,"Splash size",.25f,3,splash,v=>splash=v);
            AddSlider(body,"Foam amount",0,1,foam,v=>foam=v);
            AddSlider(body,"Surface offset (m)",.02f,2,offset,v=>offset=v);
            var riverToggle=new Toggle("Match existing river colours"){value=matchRiverColours};body.Add(riverToggle);
            var color=new ColorField("Custom water colour"){value=tint,showAlpha=false};color.SetEnabled(!matchRiverColours);color.RegisterValueChangedCallback(e=>tint=e.newValue);body.Add(color);
            riverToggle.RegisterValueChangedCallback(e=>{matchRiverColours=e.newValue;color.SetEnabled(!matchRiverColours);});
            var heading=new Label("EDIT A WATERFALL");heading.AddToClassList("waterfall-section");body.Add(heading);
            selection=new ObjectField("Selected waterfall"){objectType=typeof(WorldWaterfall),allowSceneObjects=true,value=selected};
            selection.RegisterValueChangedCallback(e=>selected=e.newValue as WorldWaterfall);body.Add(selection);
            body.Add(new Button(LoadSelected){text="Load selected settings"});
            body.Add(new Button(ApplySelected){text="Apply settings to selected"});
            body.Add(new HelpBox("Turn the brush off to select waterfalls. Move the TOP and BASE handles to reshape the selected waterfall. Settings above affect new strokes until you click Apply. Ctrl+Z undoes placement, edits and erasing. Alt + mouse or right-drag navigates.",HelpBoxMessageType.None));
            body.Add(new Button(()=>{DisablePainting();if(EditorApplication.isPlayingOrWillChangePlaymode)return;var scene=selected!=null?selected.gameObject.scene:SceneManager.GetActiveScene();SetStatus(EditorSceneManager.SaveScene(scene)?"Scene saved with its waterfalls.":"Save cancelled.");}){text="Save Waterfalls and Scene"});
            status=new HelpBox("Enable the brush, then drag down a cliff. The waterfall animates in Scene view and during gameplay.",HelpBoxMessageType.None);body.Add(status);
        }
        static void AddSlider(VisualElement root,string label,float min,float max,float value,Action<float> set)
        {
            var slider=new Slider(label,min,max){value=value,showInputField=true};
            slider.RegisterValueChangedCallback(e=>{var v=float.IsFinite(e.newValue)?Mathf.Clamp(e.newValue,min,max):min;slider.SetValueWithoutNotify(v);set(v);});root.Add(slider);
        }
        void SetStatus(string text){if(status!=null)status.text=text;}
        bool CanEdit()=>selected!=null&&selected.gameObject.scene.IsValid()&&!EditorApplication.isPlayingOrWillChangePlaymode;
        void LoadSelected()
        {
            if(!CanEdit()){SetStatus("Select a waterfall in the scene first.");return;}
            width=selected.width;bow=selected.bow;speed=selected.flowSpeed;splash=selected.splashSize;foam=selected.foam;tint=selected.waterColor;matchRiverColours=selected.matchRiverColours;CreateGUI();
        }
        void ApplySelected()
        {
            if(!CanEdit()){SetStatus("Select a waterfall in the scene first.");return;}
            Undo.RecordObject(selected,"Adjust waterfall");Configure(selected);selected.Rebuild();Dirty(selected);SetStatus("Settings applied to "+selected.name+".");
        }
        void Configure(WorldWaterfall fall)
        {
            fall.width=width;fall.bow=bow;fall.flowSpeed=speed;fall.splashSize=splash;fall.foam=foam;fall.waterColor=tint;
            fall.matchRiverColours=matchRiverColours;
            if(fall.riverMaterial==null)fall.riverMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_River.mat");
        }
        public void DisablePainting()
        {
            if(control!=0&&GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            control=0;dragging=false;painting=false;toggle?.SetValueWithoutNotify(false);SceneView.RepaintAll();
        }
        void StopOtherBrushes()
        {
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldRoadBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldRegionBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldAINoWalkBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())tool.DisablePainting();
        }
        public static Material EnsureMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material!=null)return material;
            var shader=Shader.Find("Nusantara/Stylized Waterfall");
            if(shader==null)throw new InvalidOperationException("Waterfall shader has not finished importing.");
            if(!AssetDatabase.IsValidFolder("Assets/NusantaraWorld/Water"))AssetDatabase.CreateFolder("Assets/NusantaraWorld","Water");
            material=new Material(shader){name="Waterfall Brush"};AssetDatabase.CreateAsset(material,MaterialPath);return material;
        }
        public WorldWaterfall CreateWaterfall(Vector3 start,Vector3 end,Vector3 outward)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||end.y>=start.y-.25f)return null;
            var material=EnsureMaterial();
            var go=new GameObject("Painted Waterfall");SceneManager.MoveGameObjectToScene(go,SceneManager.GetActiveScene());
            outward.y=0;if(outward.sqrMagnitude<.001f)outward=Vector3.forward;
            go.transform.SetPositionAndRotation(start,Quaternion.LookRotation(outward.normalized,Vector3.up));
            var fall=go.AddComponent<WorldWaterfall>();fall.landing=go.transform.InverseTransformPoint(end);fall.waterMaterial=material;
            Configure(fall);fall.Rebuild();Undo.RegisterCreatedObjectUndo(go,"Paint waterfall");Dirty(fall);
            selected=fall;selection?.SetValueWithoutNotify(fall);Selection.activeGameObject=go;
            return fall;
        }
        static void Dirty(WorldWaterfall fall)
        {
            EditorUtility.SetDirty(fall);PrefabUtility.RecordPrefabInstancePropertyModifications(fall);
            EditorSceneManager.MarkSceneDirty(fall.gameObject.scene);SceneView.RepaintAll();
        }
        bool Pick(Ray ray,out Vector3 point,out Vector3 normal)
        {
            point=normal=Vector3.zero;float nearest=float.MaxValue;var scene=SceneManager.GetActiveScene();
            foreach(var hit in Physics.RaycastAll(ray,100000,~0,QueryTriggerInteraction.Ignore))
            {
                var go=hit.collider.gameObject;
                if(go.scene!=scene||hit.distance>=nearest||go.GetComponentInParent<WorldWaterfall>()!=null||SceneVisibilityManager.instance.IsHidden(go))continue;
                nearest=hit.distance;point=hit.point;normal=hit.normal;
            }
            foreach(var water in UnityEngine.Object.FindObjectsByType<WorldPaintedWater>(FindObjectsSortMode.None))
            {
                if(water.gameObject.scene!=scene||SceneVisibilityManager.instance.IsHidden(water.gameObject))continue;
                if(!new Plane(water.transform.up,water.transform.position).Raycast(ray,out float distance)||distance>=nearest)continue;
                var local=water.transform.InverseTransformPoint(ray.GetPoint(distance));
                if(!water.cells.Contains(new Vector2Int(Mathf.FloorToInt(local.x),Mathf.FloorToInt(local.z))))continue;
                nearest=distance;point=ray.GetPoint(distance);normal=water.transform.up;
            }
            return nearest<float.MaxValue;
        }
        void SceneGUI(SceneView view)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!painting)return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){DisablePainting();e.Use();return;}
            if(e.alt||e.button==1||e.button==2)
            {
                if(dragging){if(GUIUtility.hotControl==control)GUIUtility.hotControl=0;dragging=false;control=0;}
                return;
            }
            int id=GUIUtility.GetControlID("NusantaraWaterfallBrush".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(id);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hit=Pick(ray,out var point,out var normal);
            if(dragging)
            {
                if(hit)bottom=point+normal*offset;
                else if(dragPlane.Raycast(ray,out float distance))bottom=ray.GetPoint(distance);
                DrawPreview();
                if(e.type==EventType.MouseUp&&e.button==0)
                {
                    if(GUIUtility.hotControl==control)GUIUtility.hotControl=0;dragging=false;control=0;
                    if(CreateWaterfall(top,bottom,forward)!=null)SetStatus("Waterfall placed. Drag again for another, or turn off the brush to edit.");
                    else SetStatus("The landing must be at least 0.25 m below the lip. Drag downward to make a fall.");
                    e.Use();view.Repaint();return;
                }
                if(e.type==EventType.MouseDrag){e.Use();view.Repaint();}return;
            }
            if(hit){Handles.color=e.shift?new Color(1,.4f,.2f):Color.cyan;Handles.DrawWireDisc(point+normal*.02f,normal,width*.5f);}
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type!=EventType.MouseDown||e.button!=0||e.control||e.command)return;
            if(e.shift)
            {
                var go=HandleUtility.PickGameObject(e.mousePosition,false);
                var fall=go!=null?go.GetComponentInParent<WorldWaterfall>():null;
                if(fall!=null&&fall.gameObject.scene==SceneManager.GetActiveScene())
                {var scene=fall.gameObject.scene;Undo.DestroyObjectImmediate(fall.gameObject);EditorSceneManager.MarkSceneDirty(scene);SetStatus("Waterfall erased. Ctrl+Z restores it.");}
                e.Use();return;
            }
            if(!hit){SetStatus("Start on terrain or a cliff with a collider.");return;}
            top=point+normal*offset;bottom=top;
            forward=Vector3.ProjectOnPlane(normal,Vector3.up);
            if(forward.sqrMagnitude<.05f)forward=Vector3.ProjectOnPlane(view.camera.transform.position-top,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            forward.Normalize();dragPlane=new Plane(forward,top);dragging=true;control=id;GUIUtility.hotControl=id;e.Use();
        }
        void DrawPreview()
        {
            bool valid=bottom.y<top.y-.25f;Handles.color=valid?Color.cyan:new Color(1,.5f,.2f);
            var right=Vector3.Cross(Vector3.up,forward).normalized;var leftPoints=new Vector3[25];var rightPoints=new Vector3[25];
            for(int i=0;i<25;i++)
            {
                float t=i/24f;var c=Vector3.Lerp(top,bottom,t);c.y=Mathf.Lerp(top.y,bottom.y,t*t*(3-2*t));c+=forward*(bow*Mathf.Sin(t*Mathf.PI));
                leftPoints[i]=c-right*width*.5f*(1+.18f*t*t);rightPoints[i]=c+right*width*.5f*(1+.18f*t*t);
                if(i%4==0)Handles.DrawLine(leftPoints[i],rightPoints[i]);
            }
            Handles.DrawAAPolyLine(3,leftPoints);Handles.DrawAAPolyLine(3,rightPoints);Handles.DrawWireDisc(bottom,Vector3.up,width*.8f*splash);
            Handles.Label(top,"TOP");Handles.Label(bottom,valid?$"BASE  |  {top.y-bottom.y:0.0} m drop":"Drag below the lip");
        }
        public static bool IsPainting=>Resources.FindObjectsOfTypeAll<WorldWaterfallBrushWindow>().Any(w=>w.painting);
    }

    [CustomEditor(typeof(WorldWaterfall))]
    public sealed class WorldWaterfallEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root=new VisualElement();
            if(target==null)return root;
            root.Add(new HelpBox("Animated waterfall. Drag TOP and BASE in Scene view to reshape it.",HelpBoxMessageType.Info));
            root.Add(new Button(WorldWaterfallBrushWindow.Open){text="Open Waterfall Brush"});
            foreach(string name in new[]{"width","bow","flowSpeed","splashSize","foam","matchRiverColours","riverMaterial","waterColor","landing"})root.Add(new PropertyField(serializedObject.FindProperty(name)));
            return root;
        }
        void OnSceneGUI()
        {
            if(target==null||WorldWaterfallBrushWindow.IsPainting||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var fall=(WorldWaterfall)target;var top=fall.Top;var bottom=fall.Bottom;
            Handles.color=Color.cyan;Handles.Label(top,"TOP");Handles.Label(bottom,"BASE");Handles.DrawDottedLine(top,bottom,5);
            EditorGUI.BeginChangeCheck();var nextTop=Handles.PositionHandle(top,fall.transform.rotation);var nextBottom=Handles.PositionHandle(bottom,Quaternion.identity);
            if(EditorGUI.EndChangeCheck()&&nextBottom.y<nextTop.y-.25f)
            {
                Undo.RecordObjects(new UnityEngine.Object[]{fall,fall.transform},"Reshape waterfall");
                fall.transform.position=nextTop;fall.landing=fall.transform.InverseTransformPoint(nextBottom);fall.Rebuild();
                PrefabUtility.RecordPrefabInstancePropertyModifications(fall);PrefabUtility.RecordPrefabInstancePropertyModifications(fall.transform);
                EditorUtility.SetDirty(fall);EditorSceneManager.MarkSceneDirty(fall.gameObject.scene);
            }
        }
    }
}
