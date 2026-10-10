using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalGems.Editor
{
    public static class FantasyVfxBuilder
    {
        public const string Root="Assets/ElementalGems/FantasyVFX";
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static Color C(float r,float g,float b,float a=1)=>new Color(r,g,b,a);
        static readonly Color[] primary={Color.white,C(1,.23f,.015f),C(.02f,.58f,1),C(.2f,1,.28f),C(.95f,.54f,.12f),C(1,.82f,.15f),C(.68f,.95f,1),C(.38f,.035f,.7f)};
        static readonly Color[] secondary={Color.white,C(1,.85f,.18f),C(.38f,1,1),C(.85f,1,.28f),C(.42f,.21f,.085f),C(.65f,.3f,1),Color.white,C(.8f,.16f,1)};
        [MenuItem("Tools/Elemental Gems/Build Fantasy VFX")]
        public static void BuildAllMenu()=>BuildAll();
        public static string BuildAll()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Build VFX in Edit Mode.");
            foreach(var dir in new[]{"Textures","Materials","Prefabs","Styles","Meshes"})Directory.CreateDirectory(Root+"/"+dir);
            AssetDatabase.Refresh();textures.Clear();materials.Clear();
            foreach(var shape in new[]{"Flame","Smoke","Ember","Droplet","Splash","Bubble","Leaf","Petal","Dust","Crack","Bolt","Streak","Crescent","Shadow","Halo","FireTrail","WaterTrail","NatureTrail","EarthTrail","LightningTrail","WindTrail","DarknessTrail"}) Texture(shape);
            var styles=new List<GemVfxStyle>();
            for(int e=1;e<=7;e++)
            {
                var element=(ElementType)e;
                var gem=AssetDatabase.LoadAssetAtPath<GemDefinition>($"Assets/ElementalGems/Definitions/{element}.asset");
                if(gem==null)throw new Exception("Missing gem: "+element);
                string path=$"{Root}/Styles/{element}.asset";
                var style=AssetDatabase.LoadAssetAtPath<GemVfxStyle>(path);
                if(style==null){style=ScriptableObject.CreateInstance<GemVfxStyle>();AssetDatabase.CreateAsset(style,path);}
                style.element=element;style.trailColor=Color.Lerp(primary[e],secondary[e],.35f);
                style.trailMaterial=Material(element+" Ribbon",element+"Trail",true,1.6f,element==ElementType.Fire?new Vector4(5,.035f,.1f,0):Vector4.zero);
                style.swordTrailWidth=element==ElementType.Wind?.42f:.29f;style.arrowTrailWidth=element==ElementType.Lightning?.11f:.1f;
                style.trailDuration=element==ElementType.Wind?.42f:.3f;
                style.swordAura=Prefab(style,0);style.bowAura=Prefab(style,1);style.arrowheadAura=Prefab(style,2);style.impact=Prefab(style,3);
                // Only the existing visual references are updated; gameplay fields remain untouched.
                Undo.RecordObject(gem,"Upgrade elemental VFX");
                gem.swordAura=style.swordAura;gem.bowAura=style.bowAura;gem.impact=style.impact;gem.trailMaterial=style.trailMaterial;
                gem.swordTrailWidth=style.swordTrailWidth;gem.arrowTrailWidth=style.arrowTrailWidth;gem.trailLifetime=style.trailDuration;
                EditorUtility.SetDirty(gem);EditorUtility.SetDirty(style);styles.Add(style);
            }
            var player=UnityEngine.Object.FindAnyObjectByType<PlayerStateManager>();
            var effects=player.GetComponent<GemWeaponEffects>();Undo.RecordObject(effects,"Bind fantasy VFX styles");effects.styles=styles.ToArray();
            effects.swordAuraPosition=new Vector3(-.04f,.1f,0);effects.swordTrailPosition=new Vector3(-.47f,.1f,0);
            EditorUtility.SetDirty(effects);
            var prefabPath=AssetDatabase.GetAssetPath(player.GetComponent<PlayerBowShooter>().arrowPrefab);
            var contents=PrefabUtility.LoadPrefabContents(prefabPath);
            try{var visuals=contents.GetComponent<GemArrowVisuals>()??contents.AddComponent<GemArrowVisuals>();visuals.styles=styles.ToArray();PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
            return "Created seven styles, 28 layered VFX prefabs, authored textures/materials, and visual-only arrow attachment.";
        }
        static Texture2D Texture(string name)
        {
            if(textures.TryGetValue(name,out var cached))return cached;
            const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n,v=(y+.5f)/n,px=u*2-1,py=v*2-1;
                float r=Mathf.Sqrt(px*px+py*py),angle=Mathf.Atan2(py,px);
                float noise=Noise(u*5,v*5);float alpha=0;float shade=1;
                switch(name)
                {
                    case "Flame":
                        float center=.13f*Mathf.Sin(v*10)+.09f*Mathf.Sin(v*23);
                        float width=(1-v)*.62f+.015f;
                        alpha=Mathf.Clamp01((width-Mathf.Abs(px-center))*9)*Mathf.Sin(v*Mathf.PI)*Mathf.Clamp01((noise-.2f)*3);
                        shade=.6f+.4f*(1-v);break;
                    case "Smoke": case "Dust": case "Shadow":
                        alpha=Mathf.Pow(Mathf.Clamp01(1-r),1.1f)*Mathf.Clamp01((noise-.12f)*1.8f);
                        shade=.6f+noise*.4f;if(name=="Shadow")alpha*=Mathf.Clamp01((.85f-r+Mathf.Sin(angle*5+r*14)*.12f)*3);break;
                    case "Ember":alpha=Mathf.Pow(Mathf.Clamp01(1-r),2)*.6f+Mathf.Exp(-(px*px*70+py*py*14));break;
                    case "Droplet":
                        float w=.5f*(1-v)+.04f;alpha=Mathf.Clamp01((w-Mathf.Abs(px))*35)*Mathf.Clamp01((v-.07f)*20)*Mathf.Clamp01((.94f-v)*10);
                        shade=.6f+.4f*Mathf.Exp(-Mathf.Pow((px+.1f)*12,2));break;
                    case "Splash":alpha=Mathf.Clamp01((.13f-Mathf.Abs(r-(.52f+.1f*Mathf.Sin(angle*9))))*13)*Mathf.Clamp01((noise-.2f)*4);break;
                    case "Bubble":alpha=Mathf.Clamp01((.055f-Mathf.Abs(r-.68f))*35)*.8f+Mathf.Exp(-((px+.32f)*(px+.32f)+(py-.42f)*(py-.42f))*160);break;
                    case "Leaf":
                        float leaf=1-Mathf.Pow(px*.85f+py*.55f,2)*1.35f-Mathf.Pow(py*.85f-px*.55f,2)*8;
                        alpha=Mathf.Clamp01(leaf*18);shade=.48f+.5f*noise;
                        if(Mathf.Abs(py*.85f-px*.55f)<.025f)shade=1;break;
                    case "Petal":alpha=Mathf.Clamp01((1-px*px*4-(py+.12f)*(py+.12f)*1.5f)*9);shade=.6f+.4f*v;break;
                    case "Crack":
                        for(int k=0;k<7;k++){float theta=k*6.28318f/7;float dx=Mathf.Cos(theta)*px+Mathf.Sin(theta)*py;float dy=-Mathf.Sin(theta)*px+Mathf.Cos(theta)*py;float line=Mathf.Abs(dy-.04f*Mathf.Sin(dx*35+k));if(dx>0)alpha=Mathf.Max(alpha,Mathf.Clamp01((.016f-line)*90)*Mathf.Clamp01((.85f-dx)*7));}break;
                    case "Bolt":
                        float zig=.25f*py+.17f*Mathf.Sin(Mathf.Floor((py+1)*5)*4.2f);
                        alpha=Mathf.Clamp01((.035f-Mathf.Abs(px-zig))*75)*Mathf.Clamp01((.94f-Mathf.Abs(py))*20);break;
                    case "Streak":alpha=Mathf.Exp(-px*px*180)*Mathf.Pow(Mathf.Clamp01(1-py*py),2);break;
                    case "Crescent":alpha=Mathf.Clamp01((.055f-Mathf.Abs(r-.64f))*40)*Mathf.Pow(Mathf.Clamp01((py+1)*.5f),1.5f);break;
                    case "Halo":alpha=Mathf.Exp(-r*r*7)*Mathf.Clamp01((1-r)*4);break;
                    default:
                        float edge=Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(py)),2);
                        float lineWidth=name=="LightningTrail"?.1f:.25f;
                        float wav= name=="EarthTrail"?Mathf.Sin(u*50)*.15f:Mathf.Sin(u*18)*.25f;
                        alpha=edge*(.25f+.6f*Mathf.Exp(-Mathf.Pow((py-wav)/lineWidth,2)))*Mathf.Clamp01(u*12)*Mathf.Clamp01((1-u)*12);
                        if(name=="FireTrail"||name=="DarknessTrail")alpha*=.3f+noise;
                        if(name=="NatureTrail")alpha*=.65f+.35f*Mathf.Sin(u*50+py*12);
                        shade=.6f+.4f*edge;break;
                }
                pixels[y*n+x]=new Color(shade,shade,shade,Mathf.Clamp01(alpha));
            }
            texture.SetPixels(pixels);texture.Apply();string path=$"{Root}/Textures/{name}.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            cached=AssetDatabase.LoadAssetAtPath<Texture2D>(path);textures[name]=cached;return cached;
        }
        static float Noise(float x,float y)=>Mathf.PerlinNoise(x,y)*.57f+Mathf.PerlinNoise(x*2.17f+3,y*2.17f+7)*.28f+Mathf.PerlinNoise(x*4.6f+13,y*4.6f)*.15f;
        public static string PolishRibbons()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var core=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)core.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs((y+.5f)/32-1)),.65f)));
            core.Apply();string texPath=Root+"/Textures/RibbonCore.png";File.WriteAllBytes(texPath,core.EncodeToPNG());UnityEngine.Object.DestroyImmediate(core);AssetDatabase.ImportAsset(texPath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            foreach(var id in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(id);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var ribbons in root.GetComponentsInChildren<GemVfxRibbons>(true))
                    {
                        string matPath=Root+"/Materials/"+root.name.Split(' ')[0]+" Flow Geometry.mat";
                        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if(mat==null){mat=new Material(Shader.Find("ElementalGems/Fantasy Particles"));AssetDatabase.CreateAsset(mat,matPath);}
                        mat.SetTexture("_MainTex",texture);mat.SetFloat("_Glow",ribbons.shape==GemVfxRibbons.RibbonShape.Lightning?3:1.8f);mat.SetFloat("_DstBlend",1);EditorUtility.SetDirty(mat);ribbons.material=mat;
                        if(ribbons.shape==GemVfxRibbons.RibbonShape.Lightning){ribbons.width*=1.55f;ribbons.radius*=.7f;ribbons.color=Color.Lerp(ribbons.color,Color.white,.4f);}
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();return "Polished ribbon width and electric core visibility.";
        }
        static Material Material(string name,string tex,bool additive,float glow=1,Vector4 flow=default)
        {
            if(materials.TryGetValue(name,out var cached))return cached;
            string path=$"{Root}/Materials/{name}.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("ElementalGems/Fantasy Particles"));AssetDatabase.CreateAsset(m,path);}
            m.SetTexture("_MainTex",Texture(tex));m.SetFloat("_DstBlend",additive?1:10);m.SetFloat("_Glow",glow);m.SetVector("_Flow",flow);m.SetColor("_Tint",Color.white);EditorUtility.SetDirty(m);materials[name]=m;return m;
        }
        static GameObject Prefab(GemVfxStyle style,int mode)
        {
            var e=style.element;bool burst=mode==3;bool arrow=mode==2;float size=arrow?.42f:burst?1.6f:1;
            Vector3 axis=mode==0?Vector3.right:mode==1?Vector3.up:Vector3.forward;
            string name=$"{e} {(mode==0?"Sword":mode==1?"Bow":mode==2?"Arrowhead":"Impact")}";
            var root=new GameObject(name);root.AddComponent<GemVfxTuning>().style=style;
            Color a=primary[(int)e],b=secondary[(int)e];
            ParticleSystem Layer(string label,string tex,bool additive,Color color,float rate,float lifetime,float minSize,float maxSize,float speed,float lift=0)
            {
                var go=new GameObject(label);go.transform.SetParent(root.transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=!burst;main.duration=burst?1.6f:3;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.6f,lifetime);
                main.startSize=new ParticleSystem.MinMaxCurve(minSize*size,maxSize*size);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.4f*size,speed*size);
                main.startColor=color;main.maxParticles=arrow?20:burst?65:55;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
                main.startRotation=new ParticleSystem.MinMaxCurve(-.4f,.4f);
                var emission=ps.emission;emission.rateOverTime=burst?0:rate*(arrow?.6f:1);if(burst)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)Mathf.Clamp(rate,6,45))});
                var shape=ps.shape;shape.enabled=true;
                if(burst||arrow){shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=arrow?.025f:.09f;}
                else{shape.shapeType=ParticleSystemShapeType.Box;shape.scale=mode==0?new Vector3(.75f,.025f,.025f):new Vector3(.06f,1.05f,.06f);}
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=lift;
                var colorLife=ps.colorOverLifetime;colorLife.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.75f,.55f),new GradientAlphaKey(0,1)});colorLife.color=gradient;
                var scale=ps.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.2f,1),new Keyframe(1,.1f)));
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Material(e+" "+label,tex,additive,additive?1.7f:1);
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                return ps;
            }
            void Turbulence(ParticleSystem ps,float strength,float frequency=1)
            {var noise=ps.noise;noise.enabled=true;noise.strength=strength;noise.frequency=frequency;noise.scrollSpeed=.55f;noise.quality=ParticleSystemNoiseQuality.Low;}
            void Spin(ParticleSystem ps,float speed)
            {var r=ps.rotationOverLifetime;r.enabled=true;r.z=new ParticleSystem.MinMaxCurve(-speed,speed);}
            void Ribbons(GemVfxRibbons.RibbonShape shape,int strands,float radius,float width,float speed,Color color)
            {
                var obj=new GameObject("Animated "+shape);obj.transform.SetParent(root.transform,false);var r=obj.AddComponent<GemVfxRibbons>();r.shape=shape;r.material=style.trailMaterial;r.color=color;r.strands=arrow?Mathf.Min(2,strands):strands;r.segments=shape==GemVfxRibbons.RibbonShape.Lightning?15:32;
                r.axis=burst?Vector3.forward:axis;r.length=(mode==1?1.1f:.8f)*(arrow?.27f:1);r.radius=radius*size;r.width=width*size;r.speed=speed;r.turns=e==ElementType.Wind?1.6f:.85f;r.burst=burst;r.lifetime=.7f;
            }
            switch(e)
            {
                case ElementType.Fire:
                    var flame=Layer("Flame tongues","Flame",true,a,28,.65f,.16f,.31f,.08f,.5f);Turbulence(flame,.08f,2);
                    flame.GetComponent<ParticleSystemRenderer>().sharedMaterial=Material("Fire Animated Flame","Flame",true,2,new Vector4(6,.055f,.13f,0));
                    Layer("Yellow flame cores","Flame",true,b,20,.3f,.07f,.17f,.05f,.23f);
                    var smoke=Layer("Charcoal smoke","Smoke",false,C(.09f,.065f,.045f,.45f),9,1.2f,.18f,.36f,.05f,.42f);Turbulence(smoke,.12f);Spin(smoke,.7f);
                    var embers=Layer("Rising embers","Ember",true,b,18,.9f,.018f,.04f,.2f,.65f);Turbulence(embers,.12f,2);
                    if(!arrow)
                    {
                        var heat=Layer("Heat distortion - optional quality layer","Smoke",false,Color.white,5,.5f,.2f,.4f,.03f,.35f);
                        string path=Root+"/Materials/Fire Heat Haze.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("ElementalGems/Heat Haze"));AssetDatabase.CreateAsset(mat,path);}mat.SetTexture("_MainTex",Texture("Smoke"));mat.SetFloat("_Strength",.0017f);heat.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;EditorUtility.SetDirty(mat);
                    }
                    break;
                case ElementType.Water:
                    Ribbons(GemVfxRibbons.RibbonShape.Helix,3,.09f,.038f,2.8f,C(.08f,.65f,1,.65f));
                    var drops=Layer("Liquid droplets","Droplet",false,C(.18f,.8f,1,.85f),20,.65f,.04f,.11f,burst?1.8f:.16f,-.35f);Spin(drops,1);
                    Layer("Bubble highlights","Bubble",true,b,10,.85f,.04f,.11f,.06f,.2f);
                    var splash=Layer("Splash crowns","Splash",true,C(.32f,.9f,1,.65f),burst?24:5,.65f,.13f,.23f,burst?1.2f:.03f);Spin(splash,2);
                    break;
                case ElementType.Nature:
                    Ribbons(GemVfxRibbons.RibbonShape.Vine,2,.075f,.016f,1.1f,C(.16f,.72f,.18f,.8f));
                    var leaves=Layer("Living leaves","Leaf",false,C(.34f,.9f,.13f,.95f),14,1.1f,.08f,.17f,.13f,.14f);Spin(leaves,3);Turbulence(leaves,.1f);
                    var petals=Layer("Drifting petals","Petal",false,C(1,.62f,.8f,.7f),6,1.3f,.035f,.07f,.1f,.16f);Spin(petals,1.5f);Turbulence(petals,.12f);
                    Layer("Golden pollen","Ember",true,C(.9f,1,.22f),14,.85f,.012f,.026f,.09f,.1f);
                    Layer("Emerald wisps","Flame",true,C(.16f,.8f,.27f,.38f),9,.55f,.12f,.2f,.06f,.2f);break;
                case ElementType.Earth:
                    var stones=Layer("Floating stone fragments","Dust",false,C(.52f,.3f,.12f),14,1,.1f,.18f,burst?2.2f:.06f,.05f);
                    var sr=stones.GetComponent<ParticleSystemRenderer>();sr.renderMode=ParticleSystemRenderMode.Mesh;sr.mesh=RockMesh();sr.sharedMaterial=RockMaterial();
                    var rotation=stones.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;rotation.x=1.7f;rotation.y=2.1f;rotation.z=.9f;
                    var dust=Layer("Ochre dust plumes","Dust",false,C(.46f,.29f,.12f,.28f),9,1,.15f,.29f,.1f,.04f);Turbulence(dust,.08f);
                    Layer("Molten golden fissures","Crack",true,C(1,.66f,.12f,.8f),burst?16:4,.6f,.16f,.28f,.04f);
                    Layer("Mineral sparks","Ember",true,C(1,.77f,.29f),12,.6f,.014f,.03f,.15f,.13f);break;
                case ElementType.Lightning:
                    Ribbons(GemVfxRibbons.RibbonShape.Lightning,4,.095f,.018f,1,C(1,.86f,.24f));
                    Ribbons(GemVfxRibbons.RibbonShape.Lightning,2,.15f,.01f,1,C(.7f,.4f,1));
                    var bolts=Layer("Forked bolt flashes","Bolt",true,C(.94f,.93f,1),14,.12f,.18f,.35f,.07f);Spin(bolts,8);
                    var sparks=Layer("Electric spark streaks","Streak",true,C(1,.85f,.26f),16,.2f,.03f,.09f,burst?3:.3f);Spin(sparks,9);break;
                case ElementType.Wind:
                    Ribbons(GemVfxRibbons.RibbonShape.Helix,3,.16f,.018f,5,C(.64f,.92f,1,.55f));
                    var curls=Layer("Curved air blades","Crescent",true,C(.75f,1,1,.65f),10,.45f,.18f,.35f,.13f);Spin(curls,5);
                    var streaks=Layer("Fast air streaks","Streak",true,C(.9f,1,1,.65f),16,.25f,.1f,.25f,.5f);Spin(streaks,3);
                    break;
                case ElementType.Darkness:
                    var shadows=Layer("Black shadow smoke","Shadow",false,C(.025f,.008f,.04f,.9f),18,1.1f,.2f,.36f,.08f,.18f);Turbulence(shadows,.17f);Spin(shadows,-1);
                    var violet=Layer("Violet shadow flames","Flame",true,C(.47f,.035f,.85f,.8f),16,.65f,.13f,.25f,.02f,-.1f);Turbulence(violet,.1f,1.7f);
                    Ribbons(GemVfxRibbons.RibbonShape.Helix,2,.13f,.02f,-2,C(.65f,.08f,1,.7f));
                    var fragments=Layer("Shadow fragments","Petal",false,C(.035f,.006f,.08f),10,.9f,.045f,.09f,.13f);Spin(fragments,4);
                    Layer("Void motes","Ember",true,C(.85f,.22f,1),8,.7f,.017f,.033f,.1f);break;
            }
            // Soft painted glow provides visibility without a bloom or post-processing dependency.
            Layer("Soft elemental glow","Halo",true,new Color(a.r,a.g,a.b,e==ElementType.Darkness?.16f:.2f),arrow?5:8,.35f,.14f,.27f,0);
            if(burst)
            {
                var flash=Layer("Impact flash","Halo",true,new Color(b.r,b.g,b.b,.6f),6,.24f,.25f,.45f,.05f);
                var ring=Layer("Expanding elemental shockwave",e==ElementType.Earth?"Crack":e==ElementType.Water?"Splash":"Crescent",true,new Color(b.r,b.g,b.b,.75f),8,.65f,.2f,.4f,.02f);
                var sz=ring.sizeOverLifetime;sz.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,5));
                var renderer=ring.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            }
            var result=PrefabUtility.SaveAsPrefabAsset(root,$"{Root}/Prefabs/{name}.prefab");UnityEngine.Object.DestroyImmediate(root);return result;
        }
        static Mesh RockMesh()
        {
            string path=Root+"/Meshes/Faceted Stone.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var points=new[]{new Vector3(0,.65f,0),new Vector3(0,-.5f,0),new Vector3(-.5f,0,0),new Vector3(.6f,.05f,0),new Vector3(0,0,-.45f),new Vector3(0,.1f,.5f)};
            int[] face={0,3,5,0,5,2,0,2,4,0,4,3,1,5,3,1,2,5,1,4,2,1,3,4};
            var vertices=new Vector3[face.Length];var tris=new int[face.Length];for(int i=0;i<face.Length;i++){vertices[i]=points[face[i]];tris[i]=i;}
            var m=new Mesh{name="Faceted Stone"};m.vertices=vertices;m.triangles=tris;m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,path);return m;
        }
        static Material RockMaterial()
        {
            string path=Root+"/Materials/Earth Solid Stone.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
            m=new Material(Shader.Find("Standard"));m.color=C(.29f,.17f,.07f);m.SetFloat("_Glossiness",.17f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",C(.09f,.035f,.003f));AssetDatabase.CreateAsset(m,path);return m;
        }
    }
}
