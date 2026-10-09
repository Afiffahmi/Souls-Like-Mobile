#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ElementalGems
{
    public sealed class GemCrescentValidation : MonoBehaviour
    {
        PlayerStateManager player;
        GemManager manager;
        ElementType original;
        Camera reviewCamera;
        readonly List<string> report = new List<string>();
        const string Folder = "Temp/ElementalSlashReview";
        public static string Begin()
        {
            if (!Application.isPlaying) return "Enter Play Mode first.";
            var p = Object.FindAnyObjectByType<PlayerStateManager>();
            if (p.GetComponent<GemCrescentValidation>() != null) return "Already running.";
            p.gameObject.AddComponent<GemCrescentValidation>(); return "Visual validation running.";
        }
        void Check(bool pass, string detail) { report.Add((pass?"PASS ":"FAIL ")+detail); File.WriteAllLines(Folder+"/Report.txt",report); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            player=GetComponent<PlayerStateManager>();manager=GetComponent<GemManager>();original=manager.EquippedElement;
            yield return new WaitForSeconds(1);
            if(player.CombatMode!=PlayerCombatMode.Sword)player.TrySetCombatMode(PlayerCombatMode.Sword);
            yield return new WaitForSeconds(2);
            Check(player.CombatMode==PlayerCombatMode.Sword,"Sword mode ready");
            var shaders=GetComponent<GemLightSlashEffects>().elementalSlashes;
            Check(shaders.Length==7 && shaders.All(s=>s!=null&&s.GetComponent<MeshFilter>().sharedMesh.vertexCount>1000),"Seven assigned, fully authored crescent prefabs");
            var cameraGo=new GameObject("Temporary Crescent Review Camera");reviewCamera=cameraGo.AddComponent<Camera>();reviewCamera.CopyFrom(Camera.main);reviewCamera.enabled=false;
            reviewCamera.orthographic=false;reviewCamera.fieldOfView=48;reviewCamera.nearClipPlane=.1f;reviewCamera.farClipPlane=150;
            reviewCamera.cullingMask &= ~(1<<LayerMask.NameToLayer("UI"));
            foreach(var element in new[]{ElementType.Nature,ElementType.Water,ElementType.Fire,ElementType.Earth,ElementType.Lightning,ElementType.Wind,ElementType.Darkness})
            {
                manager.Equip(element,false);
                yield return null;
                bool accepted=player.TryAttack(CombatAttackInput.LightAttack);
                Check(accepted,element+" light attack accepted");
                float timeout=Time.time+4;GemCrescentSlash fx=null;
                while(Time.time<timeout)
                {
                    fx=FindObjectsByType<GemCrescentSlash>(FindObjectsSortMode.None).FirstOrDefault(f=>f.element==element&&f.NormalizedAge>.19f&&f.NormalizedAge<.6f);
                    if(fx!=null)break;
                    yield return null;
                }
                Check(fx!=null,element+" crescent appears inside actual light-attack animation");
                if(fx!=null)
                {
                    Check(fx.GetComponent<MeshRenderer>().bounds.size.magnitude>4,element+" broad sweep exceeds four metres across bounds");
                    Check(fx.accents.Any(p=>p.particleCount>0),element+" accent particles emitted");
                    Vector3 target=player.transform.position+Vector3.up*.9f+player.transform.forward*.7f;
                    reviewCamera.transform.position=target-player.transform.forward*5.5f+player.transform.right*3.1f+Vector3.up*5.0f;
                    reviewCamera.transform.LookAt(target);
                    Capture(element.ToString());
                }
                yield return new WaitForSeconds(2.1f);
            }
            manager.Equip(ElementType.Normal,false);yield return null;
            player.TryAttack(CombatAttackInput.LightAttack);
            yield return CheckAttackSlash(ElementType.Normal, "Plain sword shows a neutral slash for its hit shape");
            yield return new WaitForSeconds(1.8f);
            manager.Equip(ElementType.Water,false);yield return null;
            player.TryAttack(CombatAttackInput.HeavyAttack);
            yield return CheckAttackSlash(ElementType.Water, "Heavy attack shows the slash used for its hit shape");
            yield return new WaitForSeconds(2);
            player.TryAttack(CombatAttackInput.LightAttack);
            float end=Time.time+3;
            while(Time.time<end&&FindObjectsByType<GemCrescentSlash>(FindObjectsSortMode.None).Length==0)yield return null;
            manager.Equip(ElementType.Normal,false);yield return null;
            Check(FindObjectsByType<GemCrescentSlash>(FindObjectsSortMode.None).Length==0,"Gem change clears previous crescent immediately");
            yield return new WaitForSeconds(2);
            manager.Equip(original,false);
            report.Add("COMPLETE");File.WriteAllLines(Folder+"/Report.txt",report);
            Destroy(this);
        }
        IEnumerator CheckAttackSlash(ElementType element, string label)
        {
            var effects = GetComponent<GemLightSlashEffects>();
            float timeout = Time.time + 4;
            while (Time.time < timeout && effects.ActiveSlash == null) yield return null;
            var slash = effects.ActiveSlash;
            Check(slash != null && slash.element == element && slash.HasDrivenGeometry, label);
            if (slash != null && element == ElementType.Normal)
                Check(slash.accents.Length == 0, "Neutral slash has no elemental accent particles");
        }
        void Capture(string element)
        {
            var rt=RenderTexture.GetTemporary(1440,960,24,RenderTextureFormat.ARGB32);
            var old=RenderTexture.active;
            reviewCamera.targetTexture=rt;reviewCamera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,960),0,0);image.Apply();
            File.WriteAllBytes(Folder+"/"+element+".png",image.EncodeToPNG());
            Destroy(image);reviewCamera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
        }
        void OnDestroy() { if(reviewCamera!=null)Destroy(reviewCamera.gameObject);if(manager!=null)manager.Equip(original,false); }
    }
}
#endif
