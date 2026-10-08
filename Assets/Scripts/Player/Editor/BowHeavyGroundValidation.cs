using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BowHeavyGroundValidation
{
    [MenuItem("Tools/Combat/Validate Bow Heavy Ground Targeting")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject Create(string name)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj,scene);
            return obj;
        }
        GameObject playerObject = null;
        Transform testMovementFrame = null;
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        bool Near(Vector3 a, Vector3 b, float tolerance = 0.05f) => Vector3.Distance(a,b) < tolerance;
        try
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(floor,scene);
            floor.name = "Validation ground";
            floor.transform.position = new Vector3(0,-0.5f,0);
            floor.transform.localScale = new Vector3(30,1,30);
            playerObject = Create("Ground test player");
            playerObject.AddComponent<PlayerStateManager>();
            var shooter = playerObject.AddComponent<PlayerBowShooter>();
            var lockOn = playerObject.AddComponent<PlayerLockOn>();
            Call(lockOn,"Awake"); Call(shooter,"Awake");
            testMovementFrame = (Transform)typeof(PlayerLockOn).GetField("movementFrame",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lockOn);
            SceneManager.MoveGameObjectToScene(testMovementFrame.gameObject,scene);
            var targetObject = Create("Enemy root");
            var target = targetObject.AddComponent<LockOnTarget>();
            var body = targetObject.AddComponent<CapsuleCollider>();
            body.center = Vector3.up; body.height=2; body.radius=0.4f;
            target.localAimOffset = new Vector3(3,10,3); // Deliberately misleading camera aim.
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out var point) && Near(point,new Vector3(0,0,5)),"Default ground point must be 5 metres ahead.");
            shooter.heavyGroundDistance=7;
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,0,7)),"Configurable range failed.");
            shooter.heavyGroundDistance=5;
            targetObject.transform.position=new Vector3(1.2f,0,1.6f);
            Physics.SyncTransforms();
            Check(lockOn.TryLockOn(target),"Could not establish existing lock-on.");
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(1.2f,0,1.6f)),"A target at 2 metres must use ground beneath its root, not its body/aim point.");
            targetObject.transform.position=new Vector3(0,0,5);
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,0,5)),"Exact range boundary failed.");
            targetObject.transform.position=new Vector3(0,0,8);
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,0,5)),"Far enemy must be clamped to 5 metres.");
            targetObject.transform.position=new Vector3(0,0,2);
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,0,2)),"Moving target must update before next release.");

            var arrowObject = Create("Ground arrow");
            arrowObject.transform.position=new Vector3(0,1.5f,0);
            var arrow=arrowObject.AddComponent<BowArrowProjectile>();
            arrow.LaunchAtGround(playerObject.transform,point,true,30,5,~0);
            Call(arrow,"Advance",100f);
            var direction=(point-new Vector3(0,1.5f,0)).normalized;
            Check(!arrow.IsFlying && arrow.IsGroundShot && Near(arrow.transform.position+direction*arrow.tipOffset,point,0.08f),"Heavy arrow must land at enemy ground despite its capsule; large step must not overshoot.");

            var lightObject=Create("Light arrow"); lightObject.transform.position=new Vector3(0,1,0);
            var light=lightObject.AddComponent<BowArrowProjectile>(); light.Launch(playerObject.transform,Vector3.forward,30,5,~0);
            Call(light,"Advance",5f);
            Check(!light.IsFlying && !light.IsGroundShot && light.transform.position.z<2f,"Light projectile must still collide with enemy body.");

            target.SetTargetable(false);
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,0,5)),"Unavailable target should fall back to forward range.");
            lockOn.Unlock();
            floor.transform.position=new Vector3(0,-2.5f,0);
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out point) && Near(point,new Vector3(0,-2,5)),"Lower terrain height was not detected.");
            floor.transform.rotation=Quaternion.Euler(10,0,0);
            Physics.SyncTransforms();
            Check(shooter.TryGetHeavyGroundPoint(out point) && Mathf.Abs(point.z-5)<0.01f && point.y < -2.2f,"Sloping ground must retain horizontal range and use raycast height.");
            floor.SetActive(false); Physics.SyncTransforms();
            Check(!shooter.TryGetHeavyGroundPoint(out point),"Missing floor should not report a ground hit.");
            var pitObject=Create("Pit arrow"); pitObject.transform.position=Vector3.up*1.5f;
            var pit=pitObject.AddComponent<BowArrowProjectile>(); pit.LaunchAtGround(playerObject.transform,point,false,30,5,0);
            Call(pit,"Advance",100f);
            Check(!pit.IsFlying && pit.name=="Arrow (Range End)","No-floor shot should stop at range without inventing an impact.");
            Debug.Log("[Bow Heavy Ground Validation] PASS: "+checks+" checks; configurable 5m cap; 2m/5m/8m locked targets; enemy collider filtering; moving/dead target; ground impact without overshoot; light collision regression; lower/sloping ground; no-floor fallback.");
        }
        finally
        {
            if(testMovementFrame != null) UnityEngine.Object.DestroyImmediate(testMovementFrame.gameObject);
            if(playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            EditorSceneManager.ClosePreviewScene(scene);
            if(previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    private static void Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
}
