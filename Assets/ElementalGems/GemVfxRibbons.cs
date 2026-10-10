using UnityEngine;
namespace ElementalGems
{
    /// <summary>Visible geometry for lightning forks, flowing water, vines and wind helices.</summary>
    public sealed class GemVfxRibbons : MonoBehaviour
    {
        public enum RibbonShape { Lightning, Helix, Vine, Orbit }
        public RibbonShape shape;
        public Material material;
        [ColorUsage(true,true)] public Color color=Color.white;
        [Min(1)] public int strands=3;
        [Range(8,48)] public int segments=24;
        public Vector3 axis=Vector3.right;
        [Min(.01f)] public float length=.75f,radius=.12f,width=.015f;
        public float turns=1.5f,speed=2;
        [Min(.02f)] public float boltRefresh=.055f;
        public bool burst;
        [Min(.05f)] public float lifetime=.65f;
        private LineRenderer[] lines;
        private Vector3[][] points;
        private float time, nextRefresh;
        private void Awake()
        {
            lines=new LineRenderer[strands];points=new Vector3[strands][];
            for(int n=0;n<strands;n++)
            {
                var child=new GameObject("Energy Ribbon "+n);child.transform.SetParent(transform,false);
                var line=child.AddComponent<LineRenderer>();lines[n]=line;points[n]=new Vector3[segments];
                line.useWorldSpace=false;line.sharedMaterial=material;line.positionCount=segments;
                line.numCapVertices=2;line.numCornerVertices=2;line.alignment=LineAlignment.View;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.1f,1),new Keyframe(.75f,.8f),new Keyframe(1,0));
                line.widthMultiplier=width*(n==0?1:.65f);
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                line.textureMode=LineTextureMode.Stretch;
            }
        }
        private void OnEnable(){time=0;nextRefresh=0;}
        private void Update()
        {
            time+=Time.deltaTime;
            float fade=burst?Mathf.Clamp01(1-time/lifetime):1;
            if(burst&&fade<=0){foreach(var l in lines)l.enabled=false;return;}
            bool refresh=shape!=RibbonShape.Lightning||time>=nextRefresh;
            Vector3 forward=axis.normalized;
            Vector3 right=Vector3.Cross(forward,Mathf.Abs(forward.y)>.9f?Vector3.forward:Vector3.up).normalized;
            Vector3 up=Vector3.Cross(forward,right);
            for(int n=0;n<strands;n++)
            {
                var line=lines[n];line.enabled=true;
                float flicker=shape==RibbonShape.Lightning?.55f+.45f*Mathf.Sin(time*67+n*3):1;
                var c=color;c.a*=fade*flicker;line.startColor=c;line.endColor=c;
                if(!refresh)continue;
                for(int i=0;i<segments;i++)
                {
                    float u=(float)i/(segments-1);
                    float phase=u*turns*Mathf.PI*2+time*speed+n*Mathf.PI*2/strands;
                    float r=radius;
                    Vector3 p;
                    if(shape==RibbonShape.Lightning)
                    {
                        float jitter=(i==0||i==segments-1)?0:radius;
                        p=forward*((u-.5f)*length)+right*(Random.Range(-1f,1f)*jitter)+up*(Random.Range(-1f,1f)*jitter);
                        if(n>0)p+=right*Mathf.Sin(u*Mathf.PI)*radius*n;
                    }
                    else if(shape==RibbonShape.Orbit)
                    {
                        r*=burst?1+time*3:1;
                        p=right*Mathf.Cos(phase)*r+up*Mathf.Sin(phase)*r+forward*((u-.5f)*length*.2f);
                    }
                    else
                    {
                        r*=shape==RibbonShape.Vine?.6f+.4f*Mathf.Sin(u*10+time):.8f+.2f*Mathf.Sin(u*3+time);
                        p=forward*((u-.5f)*length)+right*Mathf.Cos(phase)*r+up*Mathf.Sin(phase)*r;
                    }
                    points[n][i]=p;
                }
                line.SetPositions(points[n]);
            }
            if(refresh)nextRefresh=time+boltRefresh;
        }
    }
}
