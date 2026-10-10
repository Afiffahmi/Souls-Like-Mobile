using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;
namespace ElementalGems.Editor
{
    public static class FantasyVfxReview
    {
        public static string Create()
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Review only in Play Mode.");
            var player=Object.FindAnyObjectByType<PlayerStateManager>();
            var root=new GameObject("Temporary Fantasy VFX Review");root.transform.position=new Vector3(2000,300,2000);
            var cameraGo=new GameObject("Fantasy VFX Review Camera");cameraGo.transform.SetParent(root.transform,false);cameraGo.transform.localPosition=new Vector3(0,0,-15);
            var cam=cameraGo.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=3.6f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.09f,.105f,.13f);cam.cullingMask=1<<31;
            var lightGo=new GameObject("Review Light");lightGo.transform.SetParent(root.transform,false);lightGo.transform.localRotation=Quaternion.Euler(35,-20,0);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.cullingMask=1<<31;
            void Label(string name,string text,Vector3 pos,float size,Color color)
            {
                var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=pos;
                var t=go.AddComponent<TextMeshPro>();t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(2.1f,.4f);
            }
            foreach(var style in player.GetComponent<GemWeaponEffects>().styles)
            {
                float x=((int)style.element-4)*2.1f;
                Label(style.element+" label",style.element.ToString().ToUpperInvariant(),new Vector3(x,2.9f,0),2.8f,style.trailColor);
                var sword=Object.Instantiate(player.GetComponent<GemWeaponEffects>().sword.gameObject,root.transform);sword.name=style.element+" Preview Sword";sword.SetActive(true);
                foreach(var child in sword.GetComponentsInChildren<GemVfxTuning>(true))Object.Destroy(child.gameObject);
                foreach(var child in sword.GetComponentsInChildren<TrailRenderer>(true))Object.Destroy(child.gameObject);
                sword.transform.localPosition=new Vector3(x,1.35f,0);sword.transform.localRotation=Quaternion.Euler(0,0,70);sword.transform.localScale=Vector3.one*1.4f;
                var fx=Object.Instantiate(style.swordAura,sword.transform,false);fx.transform.localPosition=new Vector3(-.04f,.1f,0);fx.transform.localScale=Vector3.one/1.4f;
                Label("Sword caption","SWORD",new Vector3(x,.32f,0),1.6f,new Color(.5f,.57f,.65f));
                var bow=Object.Instantiate(player.GetComponent<PlayerBowVisuals>().bowPrefab,root.transform);bow.name=style.element+" Preview Bow";
                foreach(var a in bow.GetComponentsInChildren<Animator>())a.enabled=false;foreach(var a in bow.GetComponentsInChildren<Animation>())a.enabled=false;
                bow.transform.localRotation=Quaternion.Euler(0,90,0);bow.transform.localScale=Vector3.Scale(bow.transform.localScale,new Vector3(.5f,.5f,.5f));
                var renderers=bow.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                bow.transform.position+=root.transform.TransformPoint(new Vector3(x,-1.1f,0))-bounds.center;
                var aura=Object.Instantiate(style.bowAura,root.transform);aura.transform.localPosition=new Vector3(x,-1.1f,0);
                Label("Bow caption","BOW",new Vector3(x,-2.25f,0),1.6f,new Color(.5f,.57f,.65f));
            }
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            return "Review scene ready; temporary objects disappear on leaving Play Mode.";
        }
    }
}
