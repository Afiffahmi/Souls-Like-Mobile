using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalGems
{
    /// <summary>One frozen surface snapshot plus staggered dust motes; no per-frame skin baking.</summary>
    [DefaultExecutionOrder(350)]
    public sealed class ShieldDustTransfer : MonoBehaviour
    {
        sealed class Surface { public Renderer original, snapshot; public bool wasHidden; public Mesh mesh; public Material[] materials; }
        struct Mote { public Vector3 surface, previous; public float release, size, phase, travel, spread; public bool started; }
        readonly List<Surface> surfaces = new List<Surface>();
        readonly List<Renderer> hiddenExtras = new List<Renderer>();
        readonly List<Vector3> samples = new List<Vector3>(1200);
        readonly ShieldDustRoute route = new ShieldDustRoute();
        Mote[] motes;
        ParticleSystem.Particle[] particles;
        ParticleSystem dust;
        Transform subject, player;
        GemShieldEffects shield;
        Color tint;
        Vector3 gate, lastMouth, lastPlayer;
        bool forming, finished;
        float age, duration, nextRoute;
        MaterialPropertyBlock properties;
        static int activeTransfers;
        bool counted;
        public bool Finished => finished;
        public bool RouteValid => route.Valid;
        public float Progress => Mathf.Clamp01(age / duration);
        public Color DustColor => tint;
        public int VisibleMotes { get; private set; }
        public IReadOnlyList<Vector3> RoutePoints => route.points;

        public static GemShieldEffects FindShield(Transform owner, Vector3 near)
        {
            if(owner){var local=owner.GetComponentInChildren<GemShieldEffects>(true);if(local)return local;}
            GemShieldEffects best=null;float distance=float.PositiveInfinity;
            foreach(var item in Object.FindObjectsByType<GemShieldEffects>(FindObjectsInactive.Exclude))
            {
                var root=item.GetComponentInParent<PlayerStateManager>();if(!root || !root.isActiveAndEnabled)continue;
                float d=(root.transform.position-near).sqrMagnitude;if(d<distance){distance=d;best=item;}
            }
            return best;
        }
        public static ShieldDustTransfer Begin(Transform model, GemShieldEffects destination, Color color, bool materialize, float seconds=1.65f)
        {
            if(!model || !destination)return null;
            var surfaceMaterial=Resources.Load<Material>("DustTransformation");
            var moteMaterial=Resources.Load<Material>("ShieldDustMotes");
            if(!surfaceMaterial || !moteMaterial){Debug.LogWarning("Dust transformation materials are missing.",model);return null;}
            var go=new GameObject(materialize?"Shield Dust - Form Horse":"Shield Dust - Absorb") {hideFlags=HideFlags.DontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,model.gameObject.scene);
            var fx=go.AddComponent<ShieldDustTransfer>();
            fx.subject=model;fx.shield=destination;var state=destination.GetComponentInParent<PlayerStateManager>();
            fx.player=state?state.transform:destination.transform.root;
            fx.tint=color;fx.tint.a=1;fx.forming=materialize;fx.duration=Mathf.Clamp(seconds,1.2f,4f);
            fx.properties=new MaterialPropertyBlock();
            try
            {
                fx.Capture(surfaceMaterial);
                if(fx.samples.Count==0 || !fx.BuildRoute()){fx.Cancel();return null;}
                fx.BuildDust(moteMaterial);
                foreach(var surface in fx.surfaces)surface.original.forceRenderingOff=true;
                // Keep pre-existing trails, flames and eye particles out of the dissolving silhouette.
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if(!renderer.enabled || renderer.forceRenderingOff)continue;
                    renderer.forceRenderingOff=true;fx.hiddenExtras.Add(renderer);
                }
                activeTransfers++;fx.counted=true;
                var deploy=destination.GetComponent<PlayerShieldAnimation>();if(deploy)deploy.OpenForDust(fx.duration+.25f);
                fx.RenderAt(0,0);
                return fx;
            }
            catch(System.Exception e){Debug.LogException(e,model);fx.Cancel();return null;}
        }
        static bool VisualMesh(Renderer r)
        {
            if(!r.enabled || r.forceRenderingOff || !(r is SkinnedMeshRenderer || r is MeshRenderer))return false;
            if((r.gameObject.hideFlags & HideFlags.DontSave)!=0)return false;
            if(r.GetComponentInParent<Canvas>())return false;
            return r.name!="Army Outer Energy" && !r.name.StartsWith("Horse Moving") && !r.name.StartsWith("Horse Running");
        }
        void Capture(Material template)
        {
            bool first=true;Bounds bounds=default;
            foreach(var renderer in subject.GetComponentsInChildren<Renderer>(true))
            {
                if(!VisualMesh(renderer))continue;
                Mesh mesh=null;
                if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                {mesh=new Mesh {name="Dust frozen skin",hideFlags=HideFlags.DontSave};skin.BakeMesh(mesh,true);}
                else {var filter=renderer.GetComponent<MeshFilter>();if(filter && filter.sharedMesh)mesh=Instantiate(filter.sharedMesh);}
                if(!mesh)continue;
                var go=new GameObject("Dissolving "+renderer.name) {layer=renderer.gameObject.layer};go.transform.SetParent(transform,false);
                go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var copy=go.AddComponent<MeshRenderer>();
                copy.shadowCastingMode=ShadowCastingMode.Off;copy.receiveShadows=false;
                var original=renderer.sharedMaterials;var materials=new Material[original.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var m=new Material(template) {hideFlags=HideFlags.DontSave};materials[i]=m;
                    if(original[i])
                    {
                        string tex=original[i].HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                        if(original[i].HasProperty(tex)){m.SetTexture("_MainTex",original[i].GetTexture(tex));m.SetTextureScale("_MainTex",original[i].GetTextureScale(tex));m.SetTextureOffset("_MainTex",original[i].GetTextureOffset(tex));}
                        Color baseColor=original[i].HasProperty("_BaseColor")?original[i].GetColor("_BaseColor"):original[i].HasProperty("_Color")?original[i].GetColor("_Color"):Color.white;
                        m.SetColor("_Color",baseColor);
                    }
                    m.SetColor("_DustTint",tint);m.SetFloat("_Progress",forming?1:0);
                }
                copy.sharedMaterials=materials;
                surfaces.Add(new Surface {original=renderer,snapshot=copy,wasHidden=renderer.forceRenderingOff,mesh=mesh,materials=materials});
                // Some imported equipment meshes are GPU-only in builds. Their renderer bounds
                // remain readable; preserve the mesh for drawing and sample its envelope for dust.
                if(mesh.isReadable)
                {
                    var vertices=mesh.vertices;
                    int stride=Mathf.Max(1,vertices.Length/400);
                    for(int i=0;i<vertices.Length;i+=stride)
                    {
                        Vector3 point=go.transform.TransformPoint(vertices[i]);samples.Add(point);
                        if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                    }
                }
                else
                {
                    var envelope=renderer.bounds;
                    for(int i=0;i<48;i++)
                    {
                        float u=(i%12)/11f;
                        Vector3 edge=new Vector3((i/12)%2==0?-1:1,Mathf.Lerp(-1,1,u),i/24==0?-1:1);
                        Vector3 point=envelope.center+Vector3.Scale(envelope.extents,edge);samples.Add(point);
                        if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                    }
                }
            }
            gate=bounds.center;
        }
        Vector3 Mouth()
        {
            if(!shield)return lastMouth;
            return shield.transform.TransformPoint(shield.core);
        }
        bool BuildRoute()
        {
            lastMouth=Mouth();lastPlayer=player?player.position:Vector3.zero;
            return route.Build(gate,lastMouth,subject,player,shield?shield.transform:null);
        }
        void BuildDust(Material material)
        {
            int count=Mathf.Clamp(300/(1+activeTransfers/3),90,300);
            motes=new Mote[count];particles=new ParticleSystem.Particle[count];
            var random=new System.Random(713+samples.Count);
            for(int i=0;i<count;i++)
            {
                motes[i]=new Mote {surface=samples[random.Next(samples.Count)],release=(float)random.NextDouble()*.46f,
                    phase=(float)random.NextDouble()*Mathf.PI*2,travel=Mathf.Lerp(.4f,.54f,(float)random.NextDouble()),spread=Mathf.Lerp(.35f,1.1f,(float)random.NextDouble()),size=Mathf.Lerp(.025f,.065f,(float)random.NextDouble())};
            }
            var go=new GameObject("Staggered Dust Stream");go.transform.SetParent(transform,false);
            dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main;main.playOnAwake=false;main.loop=true;main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSpeed=0;main.startLifetime=100;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=dust.emission;emission.enabled=false;var shape=dust.shape;shape.enabled=false;
            var renderer=dust.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            dust.Play();
        }
        void Update()
        {
            if(finished)return;
            if(!shield || !player || (forming && !subject)){Cancel();return;}
            float dt=Mathf.Min(Time.deltaTime,.08f);age+=Time.deltaTime;
            if(age>=nextRoute && (!route.Valid || (Mouth()-lastMouth).sqrMagnitude>.015f || (player.position-lastPlayer).sqrMagnitude>.01f))
            {nextRoute=age+.12f;BuildRoute();}
            RenderAt(age,dt);
            if(age>=duration)Complete();
        }
        void RenderAt(float seconds,float dt)
        {
            float t=Mathf.Clamp01(seconds/duration);
            float dissolved=forming?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.94f,t)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.55f,t));
            properties.SetFloat("_Progress",dissolved);
            foreach(var surface in surfaces)if(surface.snapshot)surface.snapshot.SetPropertyBlock(properties);
            if(!dust || motes==null)return;
            int live=0;
            for(int i=0;i<motes.Length;i++)
            {
                var m=motes[i];float u=(t-m.release)/m.travel;
                if(u<0 || u>=1)continue;
                float smooth=Mathf.SmoothStep(0,1,u);
                float pathT=forming?1-smooth:smooth;
                Vector3 center;
                if(pathT<.16f)center=Vector3.Lerp(m.surface,gate,pathT/.16f);
                else center=route.Evaluate((pathT-.16f)/.84f);
                // Independent 3D eddies fill a broad cloud, rather than lining up on the route.
                // The source shape blends into the cloud without a pinch at the shared gate.
                float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.14f,pathT));
                float close=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,pathT));
                float cloud=open*close;
                float orbit=m.phase+pathT*(4.5f+2.3f*Mathf.Sin(m.phase*3));
                Vector3 eddy=new Vector3(Mathf.Sin(orbit),Mathf.Cos(orbit*1.17f+m.phase),Mathf.Cos(orbit*.83f-m.phase));
                Vector3 turbulence=new Vector3(Mathf.Sin(pathT*19+m.phase*2),Mathf.Cos(pathT*15+m.phase*3),Mathf.Sin(pathT*23-m.phase));
                Vector3 inherited=Vector3.ClampMagnitude(m.surface-gate,1.25f)*(1-Mathf.SmoothStep(0,1,pathT));
                Vector3 scatter=(eddy*m.spread*.72f+turbulence*.16f+inherited*.5f)*cloud;
                Vector3 desired=center+scatter;
                Vector3 start=forming?Mouth():m.surface;
                Vector3 previous=m.started?m.previous:start;
                // Per-mote sweeps keep a moving player/obstacle from crossing the already planned stream.
                bool terminal=((desired-Mouth()).sqrMagnitude<.04f || (previous-Mouth()).sqrMagnitude<.04f) && (desired-previous).sqrMagnitude<.64f;
                if(!route.Valid)desired=previous;
                else if(!route.Clear(previous,desired,terminal,.022f))
                {
                    // Narrow the cloud only where solid geometry demands it. Test every alternative.
                    bool clear=false;
                    for(int attempt=1;attempt<=4;attempt++)
                    {
                        Vector3 alternative=center+scatter*(1-attempt*.25f);
                        if(route.Clear(previous,alternative,terminal,.022f)){desired=alternative;clear=true;break;}
                    }
                    if(!clear)desired=previous;
                }
                m.previous=desired;m.started=true;motes[i]=m;
                float alpha=Mathf.Clamp01(u*12)*Mathf.Clamp01((1-u)*9);
                Color color=Color.Lerp(tint,Color.white,.08f+((i%7)/7f)*.16f);color.a=alpha;
                particles[live++]=new ParticleSystem.Particle {position=desired,startColor=color,startSize=m.size*(.65f+.35f*Mathf.Sin(u*Mathf.PI)),startLifetime=100,remainingLifetime=100};
            }
            dust.SetParticles(particles,live);VisibleMotes=live;
        }
        void Restore()
        {
            foreach(var surface in surfaces)if(surface.original)surface.original.forceRenderingOff=surface.wasHidden;
            foreach(var renderer in hiddenExtras)if(renderer)renderer.forceRenderingOff=false;
        }
        public void Complete()
        {
            if(finished)return;finished=true;
            if(forming)Restore();
            if(dust)dust.Clear();
            gameObject.SetActive(false);ReleaseObject(gameObject);
        }
        public void Cancel()
        {
            if(finished)return;finished=true;Restore();gameObject.SetActive(false);ReleaseObject(gameObject);
        }
        static void ReleaseObject(Object obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy()
        {
            if(!finished)Restore();
            foreach(var surface in surfaces){if(surface.mesh)ReleaseObject(surface.mesh);foreach(var material in surface.materials)if(material)ReleaseObject(material);}
            if(counted){activeTransfers=Mathf.Max(0,activeTransfers-1);counted=false;}
        }
        public static Color EnemyOriginColor(Enemy enemy)
        {
            var identity=enemy.GetComponent<ElementalEnemy>();
            var element=identity?identity.element:ElementType.Normal;
            if(element==ElementType.Normal)return new Color(.62f,.66f,.7f);
            var army=enemy.GetComponent<SoulsLike.Enemies.EnemyArmyAura>();
            if(army && army.elementPalette!=null)foreach(var gem in army.elementPalette)if(gem && gem.element==element)return gem.color;
            var palette=Resources.Load<ShieldDustPalette>("ShieldDustPalette");
            if(palette && palette.gems!=null)foreach(var gem in palette.gems)if(gem && gem.element==element)return gem.color;
            return ElementalReactionVfx.Tint(element);
        }
        public static void AbsorbEnemy(Enemy enemy)
        {
            if(!enemy || !Application.isPlaying)return;
            var shield=FindShield(null,enemy.transform.position);if(!shield)return;
            var effect=Begin(enemy.transform,shield,EnemyOriginColor(enemy),false,1.85f);
            if(!effect)return;
            foreach(var c in enemy.GetComponentsInChildren<Collider>())c.enabled=false;
            var agent=enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent)agent.enabled=false;
            // Existing death events/rewards have already run; an independent snapshot survives corpse cleanup.
            Destroy(enemy.gameObject,2.1f);
        }
    }
}
