using System;
using UnityEngine;

namespace Nusantara.World
{
    // One paused billboard-particle canopy per chunk, using the project's leaf sprite.
    // Trunk models are untouched. Particles have no collision, physics, lights or shadows.
    [ExecuteAlways, RequireComponent(typeof(ParticleSystem))]
    public sealed class WorldCanopyVfx : MonoBehaviour
    {
        public Vector3[] crowns = Array.Empty<Vector3>();
        public float[] radii = Array.Empty<float>();
        public int seed;
        [Range(2, 12)] public int leavesPerCrown = 8;
        [Range(16, 1024)] public int particleBudget = 384;
        [Min(20)] public float visibleDistance = 700;
        public bool useQualitySettings = true;
        [Range(.5f, 4)] public float canopyRadiusMultiplier = 2.2f;
        public float minimumCrownRadius = 5;
        public float maximumCrownRadius = 14;
        ParticleSystem system;
        ParticleSystemRenderer rendererCache;
        ParticleSystem.Particle[] particles;
        Color32[] colors;
        Camera view;
        Transform player;
        float nextUpdate;
        int quality = -1;
        void OnEnable() { system=GetComponent<ParticleSystem>();rendererCache=GetComponent<ParticleSystemRenderer>();Rebuild(); }
        public void Rebuild()
        {
            if(system==null)system=GetComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            quality=QualitySettings.GetQualityLevel();
            int leaves=useQualitySettings&&quality<=1?Mathf.Max(2,leavesPerCrown/2):leavesPerCrown;
            int count=Mathf.Min(particleBudget,crowns.Length*leaves);
            particles=new ParticleSystem.Particle[count];colors=new Color32[count];var random=new System.Random(seed);
            // Distribute the cap over every crown, rather than dropping later trees in a dense chunk.
            for(int i=0;i<count;i++)
            {
                int crown=i%crowns.Length;float radius=crown<radii.Length?radii[crown]:5;
                radius=Mathf.Clamp(radius*canopyRadiusMultiplier,minimumCrownRadius,maximumCrownRadius);
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float reach=Mathf.Sqrt((float)random.NextDouble())*radius;
                float h=(float)random.NextDouble()*radius*.5f;
                particles[i]=new ParticleSystem.Particle {
                    position=crowns[crown]+new Vector3(Mathf.Cos(angle)*reach,h,Mathf.Sin(angle)*reach),
                    startLifetime=1000000,remainingLifetime=1000000,startSize=radius*(.7f+(float)random.NextDouble()*.35f),
                    rotation=(float)random.NextDouble()*360,randomSeed=(uint)(seed+i+1),
                    startColor=Color.Lerp(new Color(.65f,.75f,.55f,1),new Color(1,1,.85f,1),(float)random.NextDouble())
                };
                colors[i]=particles[i].startColor;
            }
            // Play initializes the native particle buffers after Stop/Clear. Setting particles
            // on a stopped system is discarded; pause only after seeding the live buffers.
            system.Play(true);system.SetParticles(particles,count);system.Pause(true);
        }
        void Update()
        {
            if(!Application.isPlaying)return;
            if(Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.5f;
            if(useQualitySettings&&QualitySettings.GetQualityLevel()!=quality)Rebuild();
            if(view==null)view=Camera.main;
            if(view==null||rendererCache==null)return;
            // Renderer supplies normal frustum culling; this bounds distant transparency cost.
            rendererCache.enabled=(rendererCache.bounds.ClosestPoint(view.transform.position)-view.transform.position).sqrMagnitude<visibleDistance*visibleDistance;
            if(!rendererCache.enabled)return;
            if(player==null){var streamer=FindAnyObjectByType<WorldChunkStreamer>();if(streamer!=null)player=streamer.player;}
            if(player==null)return;
            Vector3 cameraPosition=view.transform.position;Vector3 line=player.position+Vector3.up-cameraPosition;
            float length=line.sqrMagnitude;
            if(length<.01f)return;
            for(int i=0;i<particles.Length;i++)
            {
                Vector3 p=transform.TransformPoint(particles[i].position);float t=Vector3.Dot(p-cameraPosition,line)/length;
                var color=colors[i];float radius=particles[i].startSize*.5f+1.5f;
                if(t>0&&t<1&&(p-(cameraPosition+line*t)).sqrMagnitude<radius*radius)color.a=0;
                particles[i].startColor=color;
            }
            system.SetParticles(particles,particles.Length);
        }
    }
}
