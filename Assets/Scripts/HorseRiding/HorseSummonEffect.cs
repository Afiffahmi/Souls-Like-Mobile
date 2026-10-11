using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Riding
{
    public sealed class HorseSummonEffect : MonoBehaviour
    {
        private LineRenderer[] rings;
        private float elapsed,duration;
        private bool reverse;
        private Color tint;
        public static void Show(Vector3 point,Color color,Material material,float seconds,bool dismiss)
        {
            if(!material)return;
            var go=new GameObject(dismiss ? "Horse Dismiss Energy" : "Horse Summon Energy");go.transform.position=point;
            var effect=go.AddComponent<HorseSummonEffect>();effect.duration=Mathf.Max(.1f,seconds);effect.reverse=dismiss;effect.tint=color;
            effect.rings=new LineRenderer[4];
            for(int i=0;i<4;i++)
            {
                var child=new GameObject("Element Ring "+i);child.transform.SetParent(go.transform,false);
                var line=child.AddComponent<LineRenderer>();effect.rings[i]=line;
                line.useWorldSpace=false;line.loop=true;line.positionCount=64;line.sharedMaterial=material;
                line.widthMultiplier=i==0?.15f:.09f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            }
        }
        private void Update()
        {
            elapsed+=Time.deltaTime;float t=Mathf.Clamp01(elapsed/duration);float expand=reverse ? 1-t : t;
            for(int r=0;r<rings.Length;r++)
            {
                var line=rings[r];float radius=.15f+Mathf.Sin(expand*Mathf.PI*.5f)*(1.5f+r*.15f);
                Color c=tint*2;c.a=Mathf.Sin(t*Mathf.PI)*(.85f-r*.12f);line.startColor=line.endColor=c;
                for(int i=0;i<64;i++)
                {
                    float angle=i*Mathf.PI*2/64;
                    float y=r==0?.08f:r*.38f+expand*1.7f;
                    line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius));
                }
            }
            if(t>=1)Destroy(gameObject);
        }
    }
}
