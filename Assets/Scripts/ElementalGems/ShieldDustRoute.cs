using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    /// <summary>Bounded, collider-tested route. Curves are sampled and collision checked before use.</summary>
    public sealed class ShieldDustRoute
    {
        public readonly List<Vector3> points = new List<Vector3>(64);
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        readonly List<Vector3> candidate = new List<Vector3>(8);
        Transform source, player, shield;
        Vector3 mouth;
        public float Length { get; private set; }
        public bool Valid { get; private set; }
        const float Clearance = .11f;

        bool Ignore(Collider c, bool terminal)
        {
            return !c || (source && c.transform.IsChildOf(source)) || (shield && c.transform.IsChildOf(shield)) ||
                (terminal && player && c.transform.IsChildOf(player));
        }
        public bool Clear(Vector3 from, Vector3 to, bool terminal = false, float radius = Clearance)
        {
            Vector3 delta = to - from; float distance = delta.magnitude;
            // Only the short, outward-facing final approach may enter the shield's collision envelope.
            terminal &= (Vector3.Distance(to, mouth) < .2f || Vector3.Distance(from, mouth) < .2f) && distance < .8f;
            int overlapCount = Physics.OverlapSphereNonAlloc(from, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) return false;
            for (int i=0;i<overlapCount;i++) if (!Ignore(overlaps[i],terminal)) return false;
            if (distance < .001f) return true;
            int count = Physics.SphereCastNonAlloc(from, radius, delta/distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for(int i=0;i<count;i++) if(!Ignore(hits[i].collider,terminal)) return false;
            return true;
        }
        public bool Build(Vector3 start, Vector3 end, Transform excludedSource, Transform playerRoot, Transform shieldRoot)
        {
            source=excludedSource;player=playerRoot;shield=shieldRoot;mouth=end;
            points.Clear(); Length=0;Valid=false;
            Vector3 away=player ? end-(player.position+Vector3.up*.3f) : start-end;
            away.y=0;
            if(away.sqrMagnitude<.01f)away=player ? -player.right : Vector3.left;
            away.Normalize();
            Vector3 approach=end+away*.65f+Vector3.up*.12f;
            // Prefer a high arch like the sketch, with a shield-side descent.
            Vector3 travel=approach-start;Vector3 side=Vector3.Cross(Vector3.up,travel).normalized;
            if(side.sqrMagnitude<.1f)side=Vector3.right;
            float playerTop=player ? player.position.y+1.35f : end.y+.5f;
            foreach(float height in new[]{.65f,1.4f,2.4f,4f,6f,9f})
            {
                foreach(float offset in new[]{0f,1.2f,-1.2f,2.7f,-2.7f,5f,-5f})
                {
                    float top=Mathf.Max(start.y,end.y,playerTop)+height;
                    candidate.Clear();candidate.Add(start);
                    candidate.Add(new Vector3(start.x,top,start.z)+side*offset);
                    candidate.Add(new Vector3(approach.x,top,approach.z)+side*offset);
                    candidate.Add(approach);candidate.Add(end);
                    bool clear=true;
                    for(int i=1;i<candidate.Count;i++)if(!Clear(candidate[i-1],candidate[i],i==candidate.Count-1)){clear=false;break;}
                    if(!clear)continue;
                    // Round only corners whose entire sampled curve is safe.
                    points.Add(start);
                    for(int i=1;i<candidate.Count-1;i++)
                    {
                        Vector3 a=Vector3.Lerp(candidate[i],candidate[i-1],.27f);
                        Vector3 b=Vector3.Lerp(candidate[i],candidate[i+1],.27f);
                        Vector3 previous=a;bool safe=Clear(points[points.Count-1],a);
                        for(int k=1;k<=8 && safe;k++)
                        {
                            float u=k/8f;Vector3 p=(1-u)*(1-u)*a+2*(1-u)*u*candidate[i]+u*u*b;
                            safe=Clear(previous,p);previous=p;
                        }
                        if(!safe)points.Add(candidate[i]);
                        else {points.Add(a);for(int k=1;k<=8;k++){float u=k/8f;points.Add((1-u)*(1-u)*a+2*(1-u)*u*candidate[i]+u*u*b);}}
                    }
                    points.Add(end);
                    // Validate rounded final connections as well; revert to safe polygon if needed.
                    for(int i=1;i<points.Count;i++)if(!Clear(points[i-1],points[i],i==points.Count-1)){points.Clear();points.AddRange(candidate);break;}
                    for(int i=1;i<points.Count;i++)Length+=Vector3.Distance(points[i-1],points[i]);
                    Valid=true;return true;
                }
            }
            return false; // Never fall back to a straight path through an obstacle.
        }
        public Vector3 Evaluate(float normalized)
        {
            if(points.Count==0)return Vector3.zero;
            float distance=Mathf.Clamp01(normalized)*Length;
            for(int i=1;i<points.Count;i++)
            {
                float segment=Vector3.Distance(points[i-1],points[i]);
                if(distance<=segment)return Vector3.Lerp(points[i-1],points[i],segment>.0001f?distance/segment:1);
                distance-=segment;
            }
            return points[points.Count-1];
        }
    }
}

