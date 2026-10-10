#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ElementalGems
{
    public sealed class GemGroundFieldValidation : MonoBehaviour
    {
        const string Folder="Temp/GroundFieldReview";
        public static string Report="Not run";
        readonly List<string> checks=new List<string>();
        readonly List<UnityEngine.Object> cleanup=new List<UnityEngine.Object>();
        PlayerStateManager player;GemManager manager;ElementType original;PlayerBowShooter shooter;
        Action<BowArrowProjectile> onArrow;
        public static string Begin(){if(!Application.isPlaying)return "Play Mode required";Report="Running";new GameObject("Temporary ground field verification").AddComponent<GemGroundFieldValidation>();return Report;}
        void Start(){Directory.CreateDirectory(Folder);player=FindAnyObjectByType<PlayerStateManager>();manager=player.GetComponent<GemManager>();original=manager.EquippedElement;shooter=player.GetComponent<PlayerBowShooter>();StartCoroutine(Guard(Run()));}
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);File.WriteAllLines(Folder+"/Report.txt",checks);}
        IEnumerator Guard(IEnumerator steps)
        {
            while(true)
            {
                bool more;object result=null;
                try{more=steps.MoveNext();if(more)result=steps.Current;}
                catch(Exception e){Report="FAIL "+e.Message;break;}
                if(!more){Report="COMPLETE: "+checks.Count+" checks passed";break;}
                yield return result;
            }
            checks.Add(Report);File.WriteAllLines(Folder+"/Report.txt",checks);Destroy(gameObject);
        }
        GameObject Box(string label,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=label;go.transform.position=position;go.transform.localScale=scale;cleanup.Add(go);return go;
        }
        Enemy Target(string name,Vector3 feet,bool compound=false)
        {
            var root=new GameObject(name);root.transform.position=feet;cleanup.Add(root);
            var body=root.AddComponent<BoxCollider>();body.center=Vector3.up*.75f;body.size=new Vector3(.5f,1.5f,.5f);
            if(compound){var child=new GameObject("Second collider");child.transform.SetParent(root.transform,false);var c=child.AddComponent<SphereCollider>();c.center=Vector3.up;c.radius=.35f;}
            var enemy=root.AddComponent<Enemy>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(Enemy).GetField("debugLog",flags).SetValue(enemy,false);
            root.AddComponent<ElementalEnemy>();return enemy;
        }
        IEnumerator Run()
        {
            var config=UnityEngine.Object.Instantiate(shooter.heavyGroundField);cleanup.Add(config);
            config.duration=1.2f;config.tickInterval=.2f;config.damagePerSecond=10;
            Vector3 center=new Vector3(2000,50,2000);
            Box("Test floor",center-Vector3.up*.5f,new Vector3(20,1,20));
            var inside=Target("Inside compound enemy",center+Vector3.right,true);
            var outside=Target("Outside enemy",center+Vector3.right*5);
            var blocked=Target("Enemy behind wall",center-Vector3.right*2);
            var above=Target("Enemy above damage height",center+Vector3.up*4+Vector3.forward);
            Box("Solid wall",center-Vector3.right+Vector3.up,new Vector3(.2f,2,2));Physics.SyncTransforms();
            var data=config.Capture(ElementType.Normal,4);
            var field=GemGroundField.Spawn(data,new GemAttack(null),null,center,Vector3.up,gameObject.scene);cleanup.Add(field.gameObject);
            yield return new WaitForSeconds(.25f);
            Check(inside.CurrentHealth==98,"Inside enemy takes one damage tick even with multiple colliders");
            Check(outside.CurrentHealth==100,"Enemy outside radius takes no field damage");
            Check(blocked.CurrentHealth==100,"Wall blocks lingering damage");
            Check(above.CurrentHealth==100,"Enemy above configured damage height is excluded");
            inside.transform.position=center+Vector3.right*8;outside.transform.position=center+Vector3.forward*2;Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            Check(inside.CurrentHealth==98,"Leaving the field stops its periodic damage");
            Check(outside.CurrentHealth<100,"Entering an active field starts taking damage");
            var same=GemGroundField.Spawn(data,new GemAttack(null),null,center+Vector3.forward*.2f,Vector3.up,gameObject.scene);
            Check(same==field,"Nearby same-element impacts refresh one field instead of stacking ticks");
            Check(GemGroundField.Spawn(data,new GemAttack(null),null,center,Vector3.right,gameObject.scene)==null,"Wall impacts do not create ground fields");
            yield return new WaitForSeconds(1.4f);int remaining=outside.CurrentHealth;
            yield return new WaitForSeconds(.3f);
            Check(!field.IsDamaging&&outside.CurrentHealth==remaining,"Expired field stops damage");
            config.duration=2;config.damagePerSecond=4;
            var fire=UnityEditor.AssetDatabase.LoadAssetAtPath<GemDefinition>("Assets/Data/ElementalGems/Definitions/Fire.asset");
            var fireAttack=new GemAttack(fire);
            var fireData=config.Capture(ElementType.Fire,3);
            var fireField=GemGroundField.Spawn(fireData,fireAttack,null,center,Vector3.up,gameObject.scene);cleanup.Add(fireField.gameObject);
            yield return new WaitForSeconds(.25f);
            Check(outside.GetComponent<ElementalEnemy>().HasStatus(StatusKind.Burn),"Fire field applies the existing burn status");
            Check(fireField.particles.Sum(p=>p.particleCount)>0,"Ground flames and embers are emitting");
            config.damagePerSecond=100;config.duration=100;
            Check(fireData.radius==3&&fireData.damagePerSecond==4&&fireData.duration==2,"In-flight ground configuration is a stable snapshot");
            foreach(var obj in cleanup.ToArray())if(obj is GameObject go)Destroy(go);
            yield return null;

            // Exercise the actual equipped bow and its existing animation-driven release.
            yield return new WaitForSeconds(1);
            if(player.CombatMode!=PlayerCombatMode.Bow)player.TrySetCombatMode(PlayerCombatMode.Bow);
            yield return new WaitForSeconds(2);
            Check(player.CombatMode==PlayerCombatMode.Bow,"Actual player enters Bow State");
            manager.Equip(ElementType.Fire,false);
            BowArrowProjectile released=null;
            onArrow=a=>{released=a;manager.Equip(ElementType.Water,false);};shooter.ArrowSpawned+=onArrow;
            player.BeginHeavyAttackHold();yield return new WaitForSeconds(.1f);player.EndHeavyAttackHold();
            float timeout=Time.time+6;GemGroundField landed=null;
            while(Time.time<timeout)
            {
                landed=FindObjectsByType<GemGroundField>(FindObjectsSortMode.None).FirstOrDefault(f=>f.IsDamaging&&f.Element==ElementType.Fire);
                if(landed!=null)break;yield return null;
            }
            shooter.ArrowSpawned-=onArrow;onArrow=null;
            Check(released!=null&&released.IsGroundShot,"Actual heavy animation releases a ground-targeted arrow");
            Check(landed!=null&&Mathf.Approximately(landed.Radius,4),"Actual heavy-arrow impact spawns configured 4m ground field");
            Check(manager.EquippedElement==ElementType.Water&&landed.Element==ElementType.Fire,"Switching gems after release preserves original Fire field");
            yield return new WaitForSeconds(.55f);
            Capture(landed.transform.position);
            yield return new WaitForSeconds(4);
            Check(!FindObjectsByType<GemGroundField>(FindObjectsSortMode.None).Any(f=>f.IsDamaging),"Actual ground field expires on schedule");
        }
        void Capture(Vector3 point)
        {
            var go=new GameObject("Temporary Ground Field Camera");cleanup.Add(go);var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.orthographic=false;camera.fieldOfView=50;
            Vector3 target=Vector3.Lerp(point,player.transform.position,.25f)+Vector3.up*.3f;
            camera.transform.position=target-player.transform.forward*9+player.transform.right*5+Vector3.up*9;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1440,960,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1440,960,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,960),0,0);texture.Apply();File.WriteAllBytes(Folder+"/Fire-Ground-Field.png",texture.EncodeToPNG());Destroy(texture);
            camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
        }
        void OnDestroy(){if(onArrow!=null&&shooter!=null)shooter.ArrowSpawned-=onArrow;if(manager!=null)manager.Equip(original,false);foreach(var obj in cleanup)if(obj!=null)Destroy(obj);}
    }
}
#endif
