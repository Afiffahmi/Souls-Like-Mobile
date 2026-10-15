using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    [DisallowMultipleComponent, DefaultExecutionOrder(9000)]
    public sealed class WorldPlayerRenderRadius : MonoBehaviour
    {
        public Transform player;
        [Min(16)] public float renderRadius = 160;
        [Min(0)] public float hysteresis = 16;
        [Min(.05f)] public float updateInterval = .15f;
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly List<Renderer> removed = new List<Renderer>();
        float nextRefresh, nextUpdate;
        public int TrackedCount => hidden.Count;
        public int HiddenCount { get { int count=0; foreach(var pair in hidden) if(pair.Key!=null && pair.Value) count++; return count; } }

        void OnEnable() { nextRefresh = nextUpdate = 0; }
        void LateUpdate()
        {
            if(player == null) {
                var streamer=GetComponent<WorldChunkStreamer>();
                if(streamer!=null) player=streamer.player;
                if(player==null) { var candidate=GameObject.FindGameObjectWithTag("Player"); if(candidate!=null) player=candidate.transform; }
            }
            if(player==null) { Restore(); return; }
            if(Time.unscaledTime>=nextRefresh) { RefreshRenderers(); nextRefresh=Time.unscaledTime+1f; }
            if(Time.unscaledTime>=nextUpdate) { ApplyRadius(); nextUpdate=Time.unscaledTime+Mathf.Max(.05f,updateInterval); }
        }
        public void RefreshRenderers()
        {
            removed.Clear();
            foreach(var pair in hidden) if(pair.Key==null) removed.Add(pair.Key);
            foreach(var r in removed) hidden.Remove(r);
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Track(r);
            // Unity object searches omit DontSave render tiles; traverse their persistent owners explicitly.
            foreach(var chunks in FindObjectsByType<WorldGroundRenderChunks>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if(chunks.gameObject.scene==gameObject.scene)
                    foreach(var r in chunks.GetComponentsInChildren<Renderer>(true)) Track(r);
        }
        void Track(Renderer r)
        {
            if(r.gameObject.scene!=gameObject.scene || hidden.ContainsKey(r) || r.forceRenderingOff) return;
            var canvas=r.GetComponentInParent<Canvas>();
            if(canvas!=null && canvas.renderMode!=RenderMode.WorldSpace) return;
            hidden.Add(r,false);
        }
        public void ApplyRadius()
        {
            if(player==null) return;
            removed.Clear(); removed.AddRange(hidden.Keys);
            foreach(var r in removed)
            {
                if(r==null) { hidden.Remove(r); continue; }
                // Bounds distance keeps large objects visible until their nearest edge is outside the radius.
                float radius=Mathf.Max(16,renderRadius)+(hidden[r]?0:Mathf.Max(0,hysteresis));
                bool off=r.bounds.SqrDistance(player.position)>radius*radius;
                hidden[r]=off; r.forceRenderingOff=off;
            }
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
        void Restore() { foreach(var pair in hidden) if(pair.Key!=null) pair.Key.forceRenderingOff=false; hidden.Clear(); }
    }
}

