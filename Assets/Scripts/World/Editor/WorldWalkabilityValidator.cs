using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    [Serializable]
    public sealed class WalkabilityIssue
    {
        public string category;
        public Vector3 position;
        public string message;
    }
    [Serializable]
    public sealed class WalkabilityReport
    {
        public int sampledPoints;
        public int issueCount;
        public bool finished;
        public List<WalkabilityIssue> issues = new List<WalkabilityIssue>();
        public void Add(string category, Vector3 position, string message)
        {
            issueCount++;
            if (issues.Count < 1000) issues.Add(new WalkabilityIssue { category = category, position = position, message = message });
        }
    }
    public static class WorldWalkabilityValidator
    {
        public static IEnumerator Validate(WorldDefinition world, WorldWalkabilityArea area, WalkabilityReport report)
        {
            if (world == null || area == null) throw new ArgumentException("Choose a world definition and validation area.");
            var bounds = area.WorldBounds;
            float spacing = Mathf.Max(.25f, area.sampleSpacing);
            int nx = Mathf.CeilToInt(bounds.size.x / spacing) + 1;
            int nz = Mathf.CeilToInt(bounds.size.z / spacing) + 1;
            if ((long)nx * nz > 300000 || nx < 2 || nz < 2 || bounds.size.y <= 0)
                throw new ArgumentException("Area must be positive and contain no more than 300,000 samples. Validate large routes in sections.");
            var points = new Vector3[nx * nz];
            var walkable = new bool[points.Length];
            var physics = area.gameObject.scene.GetPhysicsScene();
            if (!physics.IsValid()) throw new InvalidOperationException("Validation area has no physics scene.");
            Physics.SyncTransforms();
            var overlaps = new Collider[32];
            float radius = world.agentRadius;
            float height = Mathf.Max(world.agentHeight, radius * 2);
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    Vector3 ray = new Vector3(Mathf.Min(bounds.min.x + x * spacing, bounds.max.x), bounds.max.y,
                        Mathf.Min(bounds.min.z + z * spacing, bounds.max.z));
                    points[i] = ray;
                    if (!physics.Raycast(ray, Vector3.down, out var hit, bounds.size.y, area.groundMask, QueryTriggerInteraction.Ignore))
                        report.Add("Missing ground", ray, "No walkable floor in the validation volume.");
                    else
                    {
                        points[i] = hit.point;
                        float slope = Vector3.Angle(hit.normal, Vector3.up);
                        if (slope > world.maxWalkableSlope + .05f)
                            report.Add("Slope", hit.point, $"Slope {slope:F1} exceeds {world.maxWalkableSlope:F1} degrees.");
                        else
                        {
                            Vector3 lower = hit.point + Vector3.up * (radius + .05f);
                            Vector3 upper = hit.point + Vector3.up * (height - radius + .05f);
                            int count = physics.OverlapCapsule(lower, upper, radius * .95f, overlaps, area.obstacleMask, QueryTriggerInteraction.Ignore);
                            bool blocked = count > 0;
                            if (blocked) report.Add("Clearance", hit.point, "Character capsule intersects an obstacle or ceiling.");
                            else walkable[i] = true;
                        }
                    }
                    report.sampledPoints++;
                    if ((i & 63) == 63) yield return null;
                }

            bool CanCross(int a, int b)
            {
                if (!walkable[a] || !walkable[b]) return false;
                var delta = points[b] - points[a];
                float planar = new Vector2(delta.x, delta.z).magnitude;
                float allowed = Mathf.Tan(world.maxWalkableSlope * Mathf.Deg2Rad) * planar + world.maxStepHeight;
                if (Mathf.Abs(delta.y) > allowed) return false;
                // Sample between grid points: a gap/step narrower than the grid must not be treated as connected.
                int steps = Mathf.Max(2, Mathf.CeilToInt(planar / Mathf.Min(.25f, radius)));
                Vector3 previous = points[a];
                for (int step = 1; step <= steps; step++)
                {
                    var p = Vector3.Lerp(points[a], points[b], (float)step / steps);
                    var top = new Vector3(p.x, bounds.max.y, p.z);
                    if (!physics.Raycast(top, Vector3.down, out var floor, bounds.size.y, area.groundMask, QueryTriggerInteraction.Ignore)) return false;
                    if (Vector3.Angle(floor.normal, Vector3.up) > world.maxWalkableSlope + .05f) return false;
                    float advance = new Vector2(floor.point.x - previous.x, floor.point.z - previous.z).magnitude;
                    if (Mathf.Abs(floor.point.y - previous.y) > Mathf.Tan(world.maxWalkableSlope * Mathf.Deg2Rad) * advance + world.maxStepHeight) return false;
                    previous = floor.point;
                }
                Vector3 from = points[a] + Vector3.up * (radius + .05f);
                Vector3 upper = points[a] + Vector3.up * (height - radius + .05f);
                return delta.sqrMagnitude < .0001f || !physics.CapsuleCast(from, upper, radius * .9f, delta.normalized,
                    out _, delta.magnitude, area.obstacleMask, QueryTriggerInteraction.Ignore);
            }

            var horizontal = new bool[points.Length];
            var vertical = new bool[points.Length];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    if (x + 1 < nx)
                    {
                        horizontal[i] = CanCross(i, i + 1);
                        if (walkable[i] && walkable[i + 1] && !horizontal[i]) report.Add("Broken connection", points[i], "Gap, step, steep edge or blocked route to the next east sample.");
                    }
                    if (z + 1 < nz)
                    {
                        vertical[i] = CanCross(i, i + nx);
                        if (walkable[i] && walkable[i + nx] && !vertical[i]) report.Add("Broken connection", points[i], "Gap, step, steep edge or blocked route to the next north sample.");
                    }
                    if ((i & 15) == 15) yield return null;
                }
            int Nearest(Vector3 point) => Mathf.Clamp(Mathf.RoundToInt((point.z - bounds.min.z) / spacing), 0, nz - 1) * nx
                + Mathf.Clamp(Mathf.RoundToInt((point.x - bounds.min.x) / spacing), 0, nx - 1);
            bool InsideXZ(Vector3 p) => p.x >= bounds.min.x && p.x <= bounds.max.x && p.z >= bounds.min.z && p.z <= bounds.max.z;
            if (area.entry != null)
            {
                int start = Nearest(area.entry.position);
                var reached = new bool[points.Length];
                var queue = new Queue<int>();
                if (InsideXZ(area.entry.position) && walkable[start]) { queue.Enqueue(start); reached[start] = true; }
                else report.Add("Entry", area.entry.position, "Entry is outside the area or not on walkable ground.");
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), x = i % nx, z = i / nx;
                    void Visit(int n, bool connected) { if (connected && !reached[n]) { reached[n] = true; queue.Enqueue(n); } }
                    if (x + 1 < nx) Visit(i + 1, horizontal[i]);
                    if (x > 0) Visit(i - 1, horizontal[i - 1]);
                    if (z + 1 < nz) Visit(i + nx, vertical[i]);
                    if (z > 0) Visit(i - nx, vertical[i - nx]);
                    if ((i & 255) == 255) yield return null;
                }
                foreach (var target in area.requiredDestinations)
                    if (target != null && (!InsideXZ(target.position) || !reached[Nearest(target.position)]))
                        report.Add("Unreachable destination", target.position, target.name + " has no continuous sampled walking route from the entry.");
            }
            else report.Add("Unverified connectivity", bounds.center, "Assign an entry and destinations to check route connectivity.");
            report.finished = true;
        }
    }
}
