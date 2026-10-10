using System.Collections.Generic;
using UnityEngine;

public sealed partial class PlayerBowShooter
{
    private readonly List<Enemy> volleyEnemies = new List<Enemy>();
    private readonly List<Enemy> volleyCandidates = new List<Enemy>();
    private readonly List<Enemy> volleyPreferred = new List<Enemy>();
    private readonly List<GameObject> volleyRoots = new List<GameObject>();
    private readonly List<Enemy> volleyRootEnemies = new List<Enemy>();
    private readonly List<Collider> volleyColliders = new List<Collider>();
    private readonly List<Vector3> volleyPoints = new List<Vector3>(3);
    private readonly Enemy[] volleyAssignments = new Enemy[3];
    private readonly Enemy[] volleyReleasedTargets = new Enemy[3];

    private static float PlanarDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x-b.x, z = a.z-b.z;
        return x*x+z*z;
    }

    private bool EligibleVolleyEnemy(Enemy enemy) => enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead &&
        enemy.gameObject.scene == gameObject.scene && !enemy.transform.IsChildOf(transform) &&
        PlanarDistanceSquared(enemy.transform.position, transform.position) <= Mathf.Pow(Mathf.Max(0.1f, heavyGroundDistance), 2);

    private void CollectVolleyEnemies()
    {
        volleyEnemies.Clear(); volleyCandidates.Clear(); volleyRoots.Clear();
        gameObject.scene.GetRootGameObjects(volleyRoots);
        foreach (var root in volleyRoots)
        {
            volleyRootEnemies.Clear();
            root.GetComponentsInChildren(false, volleyRootEnemies);
            foreach (var enemy in volleyRootEnemies)
            {
                if (!enemy.isActiveAndEnabled || enemy.IsDead || enemy.transform.IsChildOf(transform)) continue;
                volleyEnemies.Add(enemy);
                if (EligibleVolleyEnemy(enemy)) volleyCandidates.Add(enemy);
            }
        }
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (int i=values.Count-1; i>0; i--)
        {
            int j = Random.Range(0,i+1);
            T value=values[i]; values[i]=values[j]; values[j]=value;
        }
    }

    // First arrow snapshots the assignment, not the points: each release samples around
    // the enemy's current position and rechecks range/death without homing arrows in flight.
    private void BeginChargedVolley()
    {
        volleyPoints.Clear();
        System.Array.Clear(volleyReleasedTargets,0,3);
        System.Array.Clear(volleyAssignments,0,3);
        Shuffle(volleyCandidates);
        int count=volleyCandidates.Count;
        if (count==0) return;
        for (int i=0;i<3;i++) volleyAssignments[i]=volleyCandidates[i % Mathf.Min(count,3)];
        Shuffle(volleyAssignments);
    }

    private bool TryGetChargedVolleyPoint(int shotIndex, out Vector3 point)
    {
        point=default;
        if (shotIndex<0 || shotIndex>=3) return false;
        CollectVolleyEnemies();
        if (shotIndex==0) BeginChargedVolley();
        var selected=volleyAssignments[shotIndex];
        if (!EligibleVolleyEnemy(selected)) selected=SelectReplacement();
        if (selected != null)
        {
            // Do not silently switch to a direct hit if terrain/clearance leaves no room.
            if (!TrySampleVolleyPoint(selected, out point)) return false;
            volleyReleasedTargets[shotIndex]=selected;
        }
        else if (!TrySampleVolleyPoint(null, out point)) return false;
        volleyPoints.Add(point);
        return true;
    }

    private Enemy SelectReplacement()
    {
        volleyPreferred.Clear();
        int leastUses=int.MaxValue;
        foreach(var enemy in volleyCandidates)
        {
            int uses=0;
            foreach(var released in volleyReleasedTargets) if(released==enemy) uses++;
            if(uses<leastUses) { leastUses=uses; volleyPreferred.Clear(); }
            if(uses==leastUses) volleyPreferred.Add(enemy);
        }
        return volleyPreferred.Count==0 ? null : volleyPreferred[Random.Range(0,volleyPreferred.Count)];
    }

    private bool TrySampleVolleyPoint(Enemy selected, out Vector3 point)
    {
        point=default;
        float range=Mathf.Max(0.1f,heavyGroundDistance);
        float min=Mathf.Max(0.1f,volleyMinOffset);
        float max=Mathf.Max(min+0.05f,volleyMaxOffset);
        Vector3 center=selected != null ? selected.transform.position : transform.position;
        // Uniform area sampling of the annulus; never clamp a rejected point onto a target.
        for(int attempt=0;attempt<256;attempt++)
        {
            float angle=selected != null ? Random.Range(0f,Mathf.PI*2f) :
                Mathf.Atan2(transform.forward.z,transform.forward.x)+Random.Range(-Mathf.PI/3f,Mathf.PI/3f);
            float radius=selected != null ? Mathf.Sqrt(Random.Range(min*min,max*max)) : Random.Range(range*0.5f,range);
            Vector3 desired=center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(PlanarDistanceSquared(desired,transform.position)>range*range || !ClearOfEnemiesAndPoints(desired)) continue;
            if(!TryProjectHeavyGround(desired,Mathf.Max(transform.position.y,center.y),out var ground)) continue;
            if(!ClearOfEnemiesAndPoints(ground)) continue;
            point=ground;
            return true;
        }
        return false;
    }

    private bool ClearOfEnemiesAndPoints(Vector3 point)
    {
        float separation=Mathf.Max(0.05f,volleyPointSeparation);
        foreach(var previous in volleyPoints)
            if(PlanarDistanceSquared(point,previous)<separation*separation) return false;
        float min=Mathf.Max(0.1f,volleyMinOffset), clearance=Mathf.Max(0.05f,volleyEnemyClearance);
        foreach(var enemy in volleyEnemies)
        {
            if(PlanarDistanceSquared(point,enemy.transform.position)<min*min) return false;
            volleyColliders.Clear(); enemy.GetComponentsInChildren(false,volleyColliders);
            foreach(var collider in volleyColliders)
            {
                if(!collider.enabled || collider.isTrigger) continue;
                // Conservative XZ bounds exclude the whole body footprint on slopes as well.
                var bounds=collider.bounds;
                float x=Mathf.Max(bounds.min.x-point.x,0,point.x-bounds.max.x);
                float z=Mathf.Max(bounds.min.z-point.z,0,point.z-bounds.max.z);
                if(x*x+z*z<clearance*clearance) return false;
            }
        }
        return true;
    }
}
