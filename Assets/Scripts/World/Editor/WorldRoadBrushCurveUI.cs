using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nusantara.World.Editor
{
    public sealed partial class WorldRoadBrushWindow
    {
        [SerializeField] bool curveMode, smoothCurve=true;
        [SerializeField] bool curveErase;
        [SerializeField] List<Vector3> roadPoints=new List<Vector3>();
        [SerializeField] MeshCollider curveGround;
        VisualElement curveActions;
        Label curveCount;
        Button applyCurve;
        VisualElement brushEraseControl;

        void AddRoadModeControls(VisualElement root)
        {
            var mode=new DropdownField("Tool mode",new List<string>{"Brush","Curved Road"},curveMode?1:0){name="roadMode"};
            mode.RegisterValueChangedCallback(e=>{EndStroke();curveMode=mode.index==1;RefreshCurveControls();SceneView.RepaintAll();
                SetStatus(curveMode?"Enable the tool, click to place points, then drag handles to shape your road.":"Brush mode: drag to paint the selected surface.");});
            root.Add(mode);
        }
        void AddCurveActions(VisualElement root)
        {
            curveActions=new VisualElement();root.Add(curveActions);
            var action=new DropdownField("Road action",new List<string>{"Create road","Erase road"},curveErase?1:0){name="roadCurveAction"};
            action.RegisterValueChangedCallback(e=>{curveErase=action.index==1;RefreshCurveControls();SceneView.RepaintAll();
                SetStatus(curveErase?"Erase road: applying this curve removes surface paint along its width.":"Create road: applying this curve paints the selected surface.");});curveActions.Add(action);
            var smooth=new Toggle("Smooth curves"){value=smoothCurve,name="roadSmoothCurve"};
            smooth.RegisterValueChangedCallback(e=>{Undo.RecordObject(this,"Change road curve");smoothCurve=e.newValue;SceneView.RepaintAll();});curveActions.Add(smooth);
            curveCount=new Label();curveActions.Add(curveCount);
            applyCurve=new Button(ApplyCurve){text="Apply road",name="applyRoadCurve"};curveActions.Add(applyCurve);
            curveActions.Add(new Button(RemoveRoadPoint){text="Remove last point"});
            curveActions.Add(new Button(()=>{Undo.RecordObject(this,"Clear road preview");roadPoints.Clear();curveGround=null;RefreshCurveControls();SceneView.RepaintAll();}){text="Clear preview"});
            curveActions.Add(new HelpBox("The outline shows the road width. Create road paints the selected surface; Erase road removes paint. The brush erase setting does not affect curves. After applying, click new points to create another road. Ctrl+Z undoes the last road.",HelpBoxMessageType.Info));
        }
        void RefreshCurveControls()
        {
            if(curveActions==null)return;
            curveActions.style.display=curveMode?DisplayStyle.Flex:DisplayStyle.None;
            if(brushEraseControl!=null)brushEraseControl.style.display=curveMode?DisplayStyle.None:DisplayStyle.Flex;
            curveCount.text=$"{roadPoints.Count} control points — width {radius*2:0.#} m";
            applyCurve.text=curveErase?"Erase along curve":"Apply road";
            applyCurve.SetEnabled(roadPoints.Count>=2&&curveGround!=null&&!EditorApplication.isPlayingOrWillChangePlaymode);
        }
        void RemoveRoadPoint()
        {
            if(roadPoints.Count==0)return;Undo.RecordObject(this,"Remove road point");
            roadPoints.RemoveAt(roadPoints.Count-1);if(roadPoints.Count==0)curveGround=null;
            RefreshCurveControls();SceneView.RepaintAll();
        }
        bool CompatibleCurveGround(MeshCollider other)
        {
            if(curveGround==null)return true;
            var a=curveGround.GetComponentInParent<WorldRegionGround>();var b=other.GetComponentInParent<WorldRegionGround>();
            return a!=null&&b!=null&&(a==b||(a.region<8&&b.region<8));
        }
        Vector3 CurveOnGround(Vector3 p)
        {
            float nearest=float.MaxValue;Vector3 result=p;
            foreach(var h in Physics.RaycastAll(new Ray(p+Vector3.up*1000,Vector3.down),2000,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(h.collider is MeshCollider m)||!CompatibleCurveGround(m))continue;
                var owner=m.GetComponentInParent<WorldRegionGround>();if(owner==null||m.sharedMesh!=owner.ground)continue;
                float distance=Mathf.Abs(h.point.y-p.y);if(distance<nearest){nearest=distance;result=h.point;}
            }
            return result;
        }
        void CurveSceneGUI(SceneView view)
        {
            var e=Event.current;RefreshCurveControls();
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==KeyCode.Escape){DisablePainting();e.Use();return;}
                if(e.keyCode==KeyCode.Backspace){RemoveRoadPoint();e.Use();return;}
                if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){ApplyCurve();e.Use();return;}
            }
            if(e.alt||Tools.viewToolActive)return;
            int control=GUIUtility.GetControlID("NusantaraCurvedRoad".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            Handles.color=curveErase?new Color(1,.4f,.2f):new Color(1,.8f,.3f);
            for(int i=0;i<roadPoints.Count;i++)
            {
                var shown=roadPoints[i]+Vector3.up*.15f;
                EditorGUI.BeginChangeCheck();
                var moved=Handles.FreeMoveHandle(shown,HandleUtility.GetHandleSize(shown)*.09f,Vector3.zero,Handles.SphereHandleCap);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(this,"Move road point");roadPoints[i]=CurveOnGround(moved-Vector3.up*.15f);view.Repaint();}
                Handles.Label(shown+Vector3.up*HandleUtility.GetHandleSize(shown)*.15f,(i+1).ToString());
            }
            if(e.type==EventType.Repaint&&roadPoints.Count>1)
            {
                float length=0;for(int i=1;i<roadPoints.Count;i++)length+=Vector3.Distance(roadPoints[i-1],roadPoints[i]);
                var path=WorldRoadCurve.Sample(roadPoints,Mathf.Max(Mathf.Max(.5f,radius*.4f),length/128),smoothCurve);
                var center=new Vector3[path.Count];var left=new Vector3[path.Count];var right=new Vector3[path.Count];
                for(int i=0;i<path.Count;i++)
                {
                    var direction=path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)];direction.y=0;
                    var side=Vector3.Cross(Vector3.up,direction.normalized)*radius;
                    center[i]=CurveOnGround(path[i])+Vector3.up*.12f;
                    left[i]=CurveOnGround(path[i]+side)+Vector3.up*.12f;right[i]=CurveOnGround(path[i]-side)+Vector3.up*.12f;
                }
                Handles.DrawAAPolyLine(3,center);Handles.color=new Color(1,.85f,.45f,.8f);
                Handles.DrawAAPolyLine(2,left);Handles.DrawAAPolyLine(2,right);
                Handles.DrawWireDisc(center[0],Vector3.up,radius);Handles.DrawWireDisc(center[center.Length-1],Vector3.up,radius);
            }
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type!=EventType.MouseDown||e.button!=0||HandleUtility.nearestControl!=control)return;
            if(!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,out var region))return;
            var collider=(MeshCollider)hit.collider;
            if(!CompatibleCurveGround(collider)){SetStatus("Finish this road before starting a road on a separate floating island.");e.Use();return;}
            if(roadPoints.Count>0&&Vector3.Distance(roadPoints[roadPoints.Count-1],hit.point)<.1f){e.Use();return;}
            Undo.RecordObject(this,"Add road point");if(roadPoints.Count==0)curveGround=collider;roadPoints.Add(hit.point);
            RefreshCurveControls();SetStatus("Drag the numbered handles to reshape the preview. Press Enter or Apply road when ready.");e.Use();view.Repaint();
        }
        void ApplyCurve()
        {
            if(!curveMode||roadPoints.Count<2||curveGround==null||Current==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EndStroke();int group=-1;
            try
            {
                var path=WorldRoadCurve.Sample(roadPoints,Mathf.Max(.15f,radius*.15f),smoothCurve);
                int slot=WorldSurfaceDetails.Slot(Current);EditorUtility.SetDirty(palette);
                Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(curveErase?"Erase curved road":"Apply curved road");
                recorded.Clear();materials.Clear();
                int count=WorldRoadCurve.Apply(curveGround,path,radius,strength,hardness,Current,slot,curveErase,recorded,materials);
                if(count==0){SetStatus("No visible ground overlaps the curve. Move the points onto ground.");return;}
                Save();Undo.RecordObject(this,"Finish road preview");roadPoints.Clear();curveGround=null;
                RefreshCurveControls();SceneView.RepaintAll();SetStatus($"{(curveErase?"Erased road paint":"Created road")} across {count} ground areas and saved. Ctrl+Z undoes it. Click to start another road.");
            }
            catch(Exception error){Save();SetStatus("Road could not finish: "+error.Message+". Ctrl+Z restores any partial paint.");Debug.LogException(error);}
            finally{if(group>=0)Undo.CollapseUndoOperations(group);}
        }
    }
}
