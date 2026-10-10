using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace ElementalGems.Editor
{
    public static class GemEarthGroundFieldPolish
    {
        const string Root="Assets/ElementalGems/FantasyVFX";
        static ParticleSystem.MinMaxCurve Range(float a,float b)=>new ParticleSystem.MinMaxCurve(a,b);
        sealed class Geometry
        {
            public List<Vector3> v=new List<Vector3>(); public List<Vector2> uv=new List<Vector2>(), data=new List<Vector2>();
            public List<Color> colors=new List<Color>(); public List<int> t=new List<int>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 detail,Color tint)
            {int n=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{ua,ub,uc});data.AddRange(new[]{detail,detail,detail});colors.AddRange(new[]{tint,tint,tint});t.AddRange(new[]{n,n+1,n+2});}
            public Mesh Save(string name)
            {
                string path=Root+"/Meshes/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(m==null){m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);}
                m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,data);m.SetColors(colors);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(.8f);m.bounds=b;EditorUtility.SetDirty(m);return m;
            }
        }
        static void Stone(Geometry g,Vector3 center,float radius,float height,float phase,bool crystal=false)
        {
            const int sides=5;var rows=new Vector3[3,sides];float ux=crystal?4:2;
            for(int j=0;j<3;j++)for(int k=0;k<sides;k++)
            {
                float a=k*Mathf.PI*2/sides+phase;
                float r=radius*(j==0?1:j==1?.86f:.27f)*(1+.17f*Mathf.Sin(k*3.7f+phase));
                float h=j==0?-.06f:j==1?height*.66f:height*(.87f+.13f*Mathf.Sin(k*2+phase));
                rows[j,k]=center+new Vector3(Mathf.Cos(a)*r+height*.022f*(j/2f)*Mathf.Cos(phase),h,Mathf.Sin(a)*r+height*.022f*(j/2f)*Mathf.Sin(phase));
            }
            var detail=new Vector2(.025f+Mathf.Repeat(phase*.037f,.17f),height);
            for(int j=0;j<2;j++)for(int k=0;k<sides;k++)
            {
                int next=(k+1)%sides;float u=(float)k/sides,un=(float)(k+1)/sides;
                Color tint=Color.Lerp(new Color(.62f,.57f,.47f),Color.white,.35f+.65f*Mathf.Abs(Mathf.Sin(phase+k*1.3f+j)));
                g.Tri(rows[j,k],rows[j+1,k],rows[j,next],new Vector2(ux+u,j*.5f),new Vector2(ux+u,(j+1)*.5f),new Vector2(ux+un,j*.5f),detail,tint);
                g.Tri(rows[j,next],rows[j+1,k],rows[j+1,next],new Vector2(ux+un,j*.5f),new Vector2(ux+u,(j+1)*.5f),new Vector2(ux+un,(j+1)*.5f),detail,tint);
            }
            Vector3 tip=center+new Vector3(height*.028f*Mathf.Cos(phase),height,height*.028f*Mathf.Sin(phase));
            for(int k=0;k<sides;k++)g.Tri(rows[2,k],tip,rows[2,(k+1)%sides],new Vector2(ux+(float)k/sides,1),new Vector2(ux+.5f,1.15f),new Vector2(ux+(float)(k+1)/sides,1),detail,Color.white);
        }
        static Mesh Surface()
        {
            var g=new Geometry();const int n=96;
            Vector3 Edge(int i){float a=i*Mathf.PI*2/n;float r=.88f+.062f*Mathf.Sin(a*5+1)+.045f*Mathf.Sin(a*11)+.025f*Mathf.Sin(a*19+2);return new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
            for(int i=0;i<n;i++){var a=Edge(i);var b=Edge(i+1);g.Tri(Vector3.zero,b,a,Vector2.one*.5f,new Vector2(b.x,b.z)*.5f+Vector2.one*.5f,new Vector2(a.x,a.z)*.5f+Vector2.one*.5f,Vector2.zero,Color.white);}
            // Uneven clusters leave gaps for enemies and preserve the open central area.
            for(int i=0;i<18;i++)
            {
                float a=i*2.399963f,r=.34f+.43f*Mathf.Repeat(i*.618f,1),h=i%5==0?2.05f:i%3==0?1.35f:.55f+.55f*Mathf.Repeat(i*.37f,1);
                Stone(g,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),.052f+.028f*Mathf.Repeat(i*.73f,1),h,i*1.71f);
            }
            for(int i=0;i<32;i++){float a=i*2.399963f,r=.50f+.33f*Mathf.Repeat(i*.43f,1);Stone(g,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),.026f+.026f*Mathf.Repeat(i*.71f,1),.10f+.22f*Mathf.Repeat(i*.37f,1),i*.9f);}
            for(int cluster=0;cluster<4;cluster++)for(int j=0;j<3;j++)
            {float a=cluster*1.57f+.5f+j*.11f,r=.61f+j*.032f;Stone(g,new Vector3(Mathf.Cos(a)*r,.05f,Mathf.Sin(a)*r),.018f+j*.003f,.32f+j*.15f,cluster+j*2,true);}
            return g.Save("Earth Fractured Bed and Erupting Spires");
        }
        static Material Mat(string name,string shader)
        {string p=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,p);}else m.shader=Shader.Find(shader);m.SetFloat("_Fade",1);EditorUtility.SetDirty(m);return m;}
        [MenuItem("Tools/Elemental Gems/Polish Earth Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path=Root+"/Prefabs/Earth Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.firstHitKnockback=5;field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=.9f;field.particleBudgetPerLayer=48;
                field.surface.name="Fractured earth and erupting stone clusters";field.surface.GetComponent<MeshFilter>().sharedMesh=Surface();var renderer=field.surface.GetComponent<MeshRenderer>();
                var ground=Mat("Earth Ground Field","ElementalGems/Earth Field Surface");ground.SetFloat("_ParticleMode",0);renderer.sharedMaterial=ground;
                var stones=Mat("Earth Field Flying Stone","ElementalGems/Earth Field Surface");stones.SetFloat("_ParticleMode",1);
                var g=new Geometry();Stone(g,Vector3.zero,.35f,1,1.7f);var chip=g.Save("Earth Airborne Rock Chip");
                Material Particle(string name,string texture,Color tint,float blend)
                {var m=Mat(name,"ElementalGems/Fantasy Particles");m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+".png"));m.SetColor("_Tint",tint);m.SetFloat("_DstBlend",blend);m.SetFloat("_Glow",1.1f);return m;}
                var dust=Particle("Earth Field Settling Dust","Dust",new Color(.65f,.45f,.24f,.36f),10);
                var sparks=Particle("Earth Field Mineral Glints","Ember",new Color(1,.68f,.23f,.85f),1);
                foreach(var ps in field.particles)
                {
                    bool rock=ps.name.Contains("stone"),smoke=ps.name.Contains("dust");ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*.04f;ps.transform.localRotation=Quaternion.identity;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.maxParticles=rock?22:smoke?18:28;main.startLifetime=rock?Range(.65f,1.05f):smoke?Range(.8f,1.4f):Range(.4f,.85f);main.startSize3D=false;main.startSize=rock?Range(.10f,.24f):smoke?Range(.6f,1.25f):Range(.025f,.055f);main.startColor=Color.white;main.gravityModifier=rock?.38f:0;
                    main.startRotation3D=rock;if(rock){main.startRotationX=Range(-2,2);main.startRotationY=Range(-3,3);main.startRotationZ=Range(-2,2);}else main.startRotation=Range(-3,3);
                    var emission=ps.emission;emission.enabled=true;emission.rateOverTime=rock?11:smoke?10:16;emission.SetBursts(new[]{new ParticleSystem.Burst(.03f,(short)(rock?10:smoke?8:12))});
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.85f;shape.radiusThickness=1;shape.rotation=new Vector3(90,0,0);shape.position=Vector3.zero;shape.scale=Vector3.one*.8f;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.25f,.25f);velocity.y=rock?Range(1.3f,2.3f):smoke?Range(.18f,.45f):Range(.3f,.7f);velocity.z=Range(-.25f,.25f);velocity.orbitalX=Range(0,0);velocity.orbitalY=Range(0,0);velocity.orbitalZ=Range(0,0);velocity.radial=Range(.2f,.7f);
                    var noise=ps.noise;noise.enabled=!rock;noise.strength=smoke?.15f:.07f;noise.frequency=1.1f;noise.scrollSpeed=.5f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.8f,.7f,.5f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(.65f,.65f),new GradientAlphaKey(0,1)});var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=false;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.2f,1),new Keyframe(1,smoke?1.6f:.4f)));
                    var rotation=ps.rotationOverLifetime;rotation.enabled=rock;rotation.separateAxes=true;rotation.x=Range(-3,3);rotation.y=Range(-4,4);rotation.z=Range(-3,3);
                    var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=rock?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;r.sharedMaterial=rock?stones:smoke?dust:sparks;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.sortMode=ParticleSystemSortMode.Distance;r.maxParticleSize=1;if(rock){r.mesh=chip;r.alignment=ParticleSystemRenderSpace.Local;}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Earth field: 18 erupting spires, 32 broken slabs, 12 mineral crystals, cracked bed, dust/chips/amber sparks; 68 particle cap; first-hit outward push 5.";
        }
    }
}
