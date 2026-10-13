using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed class WorldWaterBrushWindow : EditorWindow
    {
        [SerializeField] WorldPaintedWater water;
        [SerializeField] float radius = 12;
        [SerializeField] bool erase;
        bool painting, picking, stroke, hasLast, meshPending;
        double nextMeshBuild;
        int control, undoGroup;
        Vector3 last;
        ObjectField area;
        FloatField level;
        Toggle brush;
        HelpBox status;
        [MenuItem("Tools/Nusantara/Water Brush")]
        public static void Open()
        {
            var window=GetWindow<WorldWaterBrushWindow>("Water Brush");
            window.minSize=new Vector2(350,500);window.Show();
        }
        public void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();
            root.Add(new HelpBox("Draw your own ponds, lakes and rivers. The brush creates moving water automatically.",HelpBoxMessageType.Info));
            root.Add(new Button(NewArea){text="1. New Water Area"});
            area=new ObjectField("Water area"){objectType=typeof(WorldPaintedWater),allowSceneObjects=true,value=water};
            area.RegisterValueChangedCallback(e=>{EndStroke();water=e.newValue as WorldPaintedWater;SyncLevel();});root.Add(area);
            root.Add(new Button(()=>{if(!Ready())return;DisablePainting();picking=true;StopOtherBrushes();status.text="Click the bank in Scene view. Water will sit just below that point.";}){text="2. Pick Water Level from Ground"});
            level=new FloatField("Water level (metres)");SyncLevel();
            level.RegisterValueChangedCallback(e=>
            {
                if(!Ready() || float.IsNaN(e.newValue)||float.IsInfinity(e.newValue))return;
                Undo.RecordObject(water.transform,"Change water level");var p=water.transform.position;p.y=e.newValue;water.transform.position=p;
                EditorSceneManager.MarkSceneDirty(water.gameObject.scene);SceneView.RepaintAll();
            });root.Add(level);
            brush=new Toggle("3. Enable water brush"){value=painting};
            brush.RegisterValueChangedCallback(e=>
            {
                EndStroke();picking=false;painting=e.newValue&&Ready();brush.SetValueWithoutNotify(painting);
                if(painting)StopOtherBrushes();SceneView.RepaintAll();
            });root.Add(brush);
            var mode=new DropdownField("Action",new System.Collections.Generic.List<string>{"Paint water","Erase water"},erase?1:0);
            mode.RegisterValueChangedCallback(e=>{EndStroke();erase=mode.index==1;});root.Add(mode);
            var size=new Slider("Brush radius",1,48){value=radius,showInputField=true};
            size.RegisterValueChangedCallback(e=>radius=Mathf.Clamp(e.newValue,1,48));root.Add(size);
            root.Add(new HelpBox("Drag in Scene view to paint. Shift erases. Ctrl+Z undoes a stroke. Esc stops painting. Alt + mouse moves the camera.\nLower the ground first with Terrain Brush if you need a pond or riverbed. Water below the ground is hidden by land.",HelpBoxMessageType.None));
            root.Add(new Button(()=>{EndStroke();if(Ready())status.text=EditorSceneManager.SaveScene(water.gameObject.scene)?"Water saved with the scene.":"Save cancelled.";}){text="4. Save Water and Scene"});
            status=new HelpBox("Create a water area, pick its height, then paint its shape. Each area can have its own water level.",HelpBoxMessageType.None);root.Add(status);
        }
        bool Ready()
        {
            bool ready=water!=null&&water.gameObject.scene.IsValid()&&!EditorApplication.isPlayingOrWillChangePlaymode;
            if(!ready&&status!=null)status.text="Exit Play mode and create or select a water area first.";return ready;
        }
        void NewArea()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            DisablePainting();var scene=SceneManager.GetActiveScene();
            var go=new GameObject("Painted Water");SceneManager.MoveGameObjectToScene(go,scene);
            var pivot=SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.pivot:Vector3.zero;
            go.transform.position=new Vector3(Mathf.Round(pivot.x),pivot.y,Mathf.Round(pivot.z));
            water=go.AddComponent<WorldPaintedWater>();
            water.waterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_River.mat");water.Rebuild();
            Undo.RegisterCreatedObjectUndo(go,"Create water area");Selection.activeGameObject=go;area.SetValueWithoutNotify(water);SyncLevel();
            EditorSceneManager.MarkSceneDirty(scene);status.text="Click Pick Water Level, then click the bank. Enable the brush to paint.";
        }
        void SyncLevel(){if(level!=null&&water!=null)level.SetValueWithoutNotify(water.transform.position.y);}
        void StopOtherBrushes()
        {
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>())tool.DisablePainting();
            foreach(var tool in Resources.FindObjectsOfTypeAll<WorldAINoWalkBrushWindow>())tool.DisablePainting();
        }
        public void DisablePainting(){EndStroke();painting=false;picking=false;brush?.SetValueWithoutNotify(false);SceneView.RepaintAll();}
        void OnEnable(){EditorApplication.update+=FlushPendingMesh;OnSelectionChange();SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=OnUndo;EditorApplication.playModeStateChanged+=OnPlay;}
        void OnSelectionChange()
        {
            var selected=Selection.activeGameObject!=null?Selection.activeGameObject.GetComponent<WorldPaintedWater>():null;
            if(selected==null||selected==water)return;
            EndStroke();water=selected;area?.SetValueWithoutNotify(water);SyncLevel();
        }
        void OnDisable(){DisablePainting();EditorApplication.update-=FlushPendingMesh;SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=OnUndo;EditorApplication.playModeStateChanged-=OnPlay;}
        void OnPlay(PlayModeStateChange state)=>DisablePainting();
        void OnUndo(){EndStroke();foreach(var body in Object.FindObjectsByType<WorldPaintedWater>())body.Rebuild();SyncLevel();SceneView.RepaintAll();}
        void FlushPendingMesh()
        {
            if(!meshPending||water==null||EditorApplication.timeSinceStartup<nextMeshBuild)return;
            water.Rebuild(false);meshPending=false;nextMeshBuild=EditorApplication.timeSinceStartup+.1;SceneView.RepaintAll();
        }
        void EndStroke()
        {
            if(water!=null&&meshPending){water.Rebuild(false);meshPending=false;}
            if(stroke&&water!=null){water.FinishEditing();EditorUtility.SetDirty(water);}
            if(stroke)Undo.CollapseUndoOperations(undoGroup);
            if(control!=0&&GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            control=0;stroke=false;hasLast=false;
        }
        void SceneGUI(SceneView view)
        {
            if((!painting&&!picking)||water==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){DisablePainting();e.Use();return;}
            if(e.type==EventType.MouseUp&&e.button==0&&stroke){EndStroke();e.Use();return;}
            if(e.alt){if(stroke)EndStroke();return;}
            int id=GUIUtility.GetControlID("NusantaraWaterBrush".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(id);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if(picking)
            {
                var hits=Physics.RaycastAll(ray,100000,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.gameObject.scene==water.gameObject.scene&&!SceneVisibilityManager.instance.IsHidden(h.collider.gameObject)&&WorldAINavigation.IsGround(h.collider)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0&&e.type==EventType.MouseDown&&e.button==0)
                {
                    Undo.RecordObject(water.transform,"Pick water level");var p=water.transform.position;p.y=hits[0].point.y-.3f;water.transform.position=p;
                    picking=false;SyncLevel();EditorSceneManager.MarkSceneDirty(water.gameObject.scene);status.text="Level picked. Enable the brush and paint inside the bank.";e.Use();
                }
                return;
            }
            var plane=new Plane(Vector3.up,water.transform.position);
            if(!plane.Raycast(ray,out float distance)){hasLast=false;return;}
            var point=ray.GetPoint(distance);bool erasing=erase||e.shift;
            Handles.color=erasing?new Color(1,.5f,.3f):new Color(.1f,1,1);
            Handles.DrawWireDisc(point+Vector3.up*.05f,Vector3.up,radius);
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type==EventType.MouseDown&&e.button==0&&!e.control&&!e.command)
            {
                Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(erasing?"Erase water":"Paint water");
                Undo.RegisterCompleteObjectUndo(water,"Paint water");stroke=true;control=id;GUIUtility.hotControl=id;hasLast=false;
            }
            else if(e.type!=EventType.MouseDrag||!stroke||e.button!=0)return;
            float spacing=Mathf.Max(.5f,radius*.3f);
            if(!hasLast||Vector3.Distance(point,last)>=spacing)
            {
                int count=hasLast?Mathf.Min(128,Mathf.CeilToInt(Vector3.Distance(point,last)/spacing)):1;
                var points=new Vector3[count];for(int i=0;i<count;i++)points[i]=hasLast?Vector3.Lerp(last,point,(float)(i+1)/count):point;
                if(water.PaintStroke(points,radius,erasing,false))
                {meshPending=true;FlushPendingMesh();EditorUtility.SetDirty(water);EditorSceneManager.MarkSceneDirty(water.gameObject.scene);}
                last=point;hasLast=true;status.text=$"Painted area: {water.CellCount:N0} m². Save Water and Scene when finished.";
            }
            e.Use();view.Repaint();
        }
    }
    [CustomEditor(typeof(WorldPaintedWater))]
    public sealed class WorldPaintedWaterEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root=new VisualElement();root.Add(new HelpBox("Water drawn with the Water Brush. Its surface and material are created automatically.",HelpBoxMessageType.Info));
            root.Add(new Button(WorldWaterBrushWindow.Open){text="Open Water Brush"});return root;
        }
    }
}
