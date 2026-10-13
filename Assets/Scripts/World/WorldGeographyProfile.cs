using System;
using UnityEngine;

namespace Nusantara.World
{
    [Serializable] public sealed class WorldWaterway
    {
        public string name;
        public Vector2[] points = Array.Empty<Vector2>();
        public float halfWidth = 36;
        public float surface = 35;
    }
    [Serializable] public sealed class WorldWalkingRoute
    {
        public string name;
        public Vector2[] points = Array.Empty<Vector2>();
        public float halfWidth = 10;
        public float[] surfaceHeights = Array.Empty<float>();
    }
    [Serializable] public sealed class WorldScenicIsland
    {
        public string id;
        public Vector2 center;
        public float radius;
        public float waterLevel;
    }
    [Serializable] public sealed class WorldWalkingPlatform
    {
        public string id;
        public Vector2[] boundary = Array.Empty<Vector2>();
        public float surface;
    }
    [Serializable] public sealed class WorldBuildingFoundation
    {
        public string id;
        public Vector2 center;
        public float radius;
        public float blend = 20;
        public float height;
    }

    [CreateAssetMenu(menuName = "Nusantara/Geography Profile")]
    public sealed class WorldGeographyProfile : ScriptableObject
    {
        public WorldDefinition world;
        [Tooltip("Persistent hand-painted terrain height offsets.")]
        public WorldTerrainSculptData sculptData;
        public Vector2[] coast = Array.Empty<Vector2>();
        public WorldWaterway[] waterways = Array.Empty<WorldWaterway>();
        public WorldWalkingRoute[] routes = Array.Empty<WorldWalkingRoute>();
        [Range(16, 64)] public int terrainIntervals = 32;
        [Range(4, 16)] public int distantIntervals = 8;
        [Range(16, 96)] public int treesPerChunk = 48;
        public float groundLevel = 42;
        [Tooltip("Region 5 scenery only. No walking collision, bridges or gameplay destinations.")]
        public WorldScenicIsland[] scenicIslands = Array.Empty<WorldScenicIsland>();
        public WorldWalkingPlatform[] walkingPlatforms = Array.Empty<WorldWalkingPlatform>();
        public WorldBuildingFoundation[] buildingFoundations = Array.Empty<WorldBuildingFoundation>();
        [Range(0, 40)] public float region5ReliefStrength;
        public float Region5Relief(Vector2 p, float strength)
        {
            if (strength <= 0 || !InsideRegion(p, 5)) return 0;
            float boundary = 0;
            foreach (var region in world.regions)
                if (region.id == 5) boundary = Mathf.SmoothStep(0, 1, Mathf.Clamp01(LineDistance(p, region.boundary, true) / 200));
            float paths = Mathf.SmoothStep(0, 1, Mathf.Clamp01((RouteDistance(p) - 45) / 180));
            float rivers = Mathf.SmoothStep(0, 1, Mathf.Clamp01((WaterDistance(p, out _) - 70) / 160));
            float clearing = 1;
            foreach (var location in world.locations)
                clearing = Mathf.Min(clearing, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Vector2.Distance(p, new Vector2(location.position.x, location.position.z)) - location.clearRadius - 60) / 180)));
            float hills = .45f + .3f * Mathf.Sin(p.x * .0024f + 1) + .25f * Mathf.Cos(p.y * .0021f);
            return strength * hills * boundary * paths * rivers * clearing;
        }

        public static bool Inside(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
            return inside;
        }
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + t * d);
        }
        public static float LineDistance(Vector2 p, Vector2[] line, bool closed = false)
        {
            if (line.Length == 1) return Vector2.Distance(p, line[0]);
            float d = float.MaxValue;
            for (int i = 0; i < line.Length - (closed ? 0 : 1); i++) d = Mathf.Min(d, SegmentDistance(p, line[i], line[(i + 1) % line.Length]));
            return d;
        }
        public int Region(Vector2 p)
        {
            foreach (var region in world.regions) if (Inside(p, region.boundary)) return region.id;
            int nearest = 1; float distance = float.MaxValue;
            foreach (var region in world.regions)
            {
                float d = LineDistance(p, region.boundary, true);
                if (d < distance) { distance = d; nearest = region.id; }
            }
            return nearest;
        }
        // Region() deliberately assigns coastal chunks to their nearest region for streaming.
        // Content placement must use polygon containment, never that fallback assignment.
        public bool InsideRegion(Vector2 p, int id, float inset = 0)
        {
            foreach (var region in world.regions)
                if (region.id == id)
                    return Inside(p, region.boundary) && (inset <= 0 || LineDistance(p, region.boundary, true) >= inset);
            return false;
        }
        public float CoastDistance(Vector2 p) => LineDistance(p, coast, true) * (Inside(p, coast) ? 1 : -1);
        public float RouteDistance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var route in routes) d = Mathf.Min(d, LineDistance(p, route.points) - RouteHalfWidth(route, p));
            return d;
        }
        public float VillageInfluence(Vector2 p)
        {
            if(world.locations.Length==0)return 0;
            var footprint=world.locations[0].footprint;
            if(footprint==null||footprint.Length<3)return 0;
            if(Inside(p,footprint))return 1;
            return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(LineDistance(p,footprint,true)/65));
        }
        public float RouteHalfWidth(WorldWalkingRoute route,Vector2 p)
            => route.name.StartsWith("Kampung / ")?route.halfWidth:Mathf.Lerp(route.halfWidth,Mathf.Min(3,route.halfWidth),VillageInfluence(p));
        public float RouteHeight(WorldWalkingRoute route,Vector2 p)
        {
            if(route.surfaceHeights==null||route.surfaceHeights.Length!=route.points.Length)return BaseHeight(p)+.075f;
            float nearest=float.MaxValue,result=route.surfaceHeights[0];
            for(int i=1;i<route.points.Length;i++)
            {
                Vector2 a=route.points[i-1],d=route.points[i]-a;
                float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;
                float distance=(p-a-d*t).sqrMagnitude;
                if(distance<nearest){nearest=distance;result=Mathf.Lerp(route.surfaceHeights[i-1],route.surfaceHeights[i],t);}
            }
            return result;
        }
        public bool OnWalkingWaterCrossing(Vector2 p)
        {
            foreach(var route in routes)
            {
                float inset=Mathf.Lerp(2,.6f,VillageInfluence(p));
                if(LineDistance(p,route.points)<=RouteHalfWidth(route,p)-inset)return true;
            }
            return false;
        }
        public float WaterDistance(Vector2 p, out float surface)
        {
            float d = float.MaxValue; surface = 0;
            foreach (var water in waterways)
            {
                float candidate = LineDistance(p, water.points) - water.halfWidth;
                if (candidate < d) { d = candidate; surface = water.surface; }
            }
            return d;
        }
        public float BaseHeight(Vector2 p)
        {
            if (world.brushAuthoredTerrain) return groundLevel;
            // Broad continuous functions avoid height discontinuities at regional boundaries.
            var north = ReferencePoint(940, 145); var south = ReferencePoint(1060, 865);
            float n = Mathf.Exp(-(p - north).sqrMagnitude / 2100000f);
            float s = Mathf.Exp(-(p - south).sqrMagnitude / 1550000f);
            return groundLevel + 148 * n + 85 * s;
        }
        public Vector2 ReferencePoint(float x, float y) => new Vector2(
            world.origin.x + (x - 150) / 1470f * world.gridSize.x * world.chunkSize,
            world.origin.y + (1100 - y) / 1084f * world.gridSize.y * world.chunkSize);
        public float Height(float x, float z)
        {
            if (world.brushAuthoredTerrain) return groundLevel + (sculptData != null ? sculptData.Sample(x, z) : 0);
            Vector2 p = new Vector2(x, z);
            float coastD = CoastDistance(p);
            float baseY = BaseHeight(p);
            float noise = (Mathf.PerlinNoise(x * .0018f + 17, z * .0018f + 29) - .5f) * 10;
            float road = RouteDistance(p);
            float flatten = Mathf.SmoothStep(0, 1, Mathf.Clamp01((road - 8) / 48));
            float y = baseY + noise * flatten + Region5Relief(p, region5ReliefStrength);
            foreach (var location in world.locations)
            {
                float distance = Vector2.Distance(p, new Vector2(location.position.x, location.position.z));
                float t = location.footprint!=null&&location.footprint.Length>=3
                    ? Mathf.SmoothStep(0,1,Inside(p,location.footprint)?0:Mathf.Clamp01(LineDistance(p,location.footprint,true)/65))
                    : Mathf.SmoothStep(0, 1, Mathf.Clamp01((distance - location.clearRadius) / 65));
                y = Mathf.Lerp(BaseHeight(new Vector2(location.position.x, location.position.z)), y, t);
            }
            float village=VillageInfluence(p);
            if(village>0)
            {
                float routeFade=Mathf.SmoothStep(0,1,Mathf.Clamp01((RouteDistance(p)-4)/20));
                y+=village*routeFade*(Mathf.Sin((p.x-world.locations[0].position.x)*.031f)*Mathf.Cos((p.y-world.locations[0].position.z)*.027f)*1.25f);
            }
            float waterD = WaterDistance(p, out float waterY);
            // Broad banks. The innermost water is physically blocked; bridges span the channel.
            float wetBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((waterD + 8) / 55));
            y = Mathf.Lerp(waterY - 4, y, wetBlend);
            foreach(var foundation in buildingFoundations)
            {
                float influence=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Vector2.Distance(p,foundation.center)-foundation.radius)/Mathf.Max(1,foundation.blend)));
                float routeProtection=Mathf.SmoothStep(0,1,Mathf.Clamp01((RouteDistance(p)-1)/8));
                y=Mathf.Lerp(y,foundation.height,influence*routeProtection);
            }
            foreach(var route in routes)
                if(route.surfaceHeights!=null&&route.surfaceHeights.Length==route.points.Length)
                {
                    float influence=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((LineDistance(p,route.points)-route.halfWidth)/8));
                    y=Mathf.Lerp(y,Mathf.Min(y,RouteHeight(route,p)-.15f),influence);
                }
            if(village>.001f)
            {
                // The terrain lattice is 8 m. Keep all vertices around a lane below its
                // walking surface so interpolation cannot expose soil through the path.
                float nearest=float.MaxValue,routeHeight=0;
                foreach(var route in routes)
                {
                    float distance=LineDistance(p,route.points)-RouteHalfWidth(route,p);
                    if(distance<nearest){nearest=distance;routeHeight=RouteHeight(route,p);}
                }
                float cap=village*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((nearest-12)/8)));
                y=Mathf.Lerp(y,Mathf.Min(y,routeHeight-.09f),cap);
                foreach(var platform in walkingPlatforms)
                    if(Inside(p,platform.boundary)||LineDistance(p,platform.boundary,true)<10)y=Mathf.Min(y,platform.surface-.09f);
            }
            float coastalBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((coastD + 50) / 210));
            return Mathf.Lerp(-12, y, coastalBlend) + (sculptData != null ? sculptData.Sample(x, z) : 0);
        }
        public bool IsForbiddenWater(Vector2 p)
        {
            if (world.brushAuthoredTerrain) return false;
            foreach (var island in scenicIslands)
                if ((p - island.center).sqrMagnitude < (island.radius + 3) * (island.radius + 3)) return true;
            if (CoastDistance(p) < 65) return true;
            foreach(var platform in walkingPlatforms)
                if(Inside(p,platform.boundary))return false;
            foreach(var foundation in buildingFoundations)
                if(Vector2.Distance(p,foundation.center)<foundation.radius&&foundation.height>35.5f)return false;
            return WaterDistance(p, out _) < 35 && !OnWalkingWaterCrossing(p);
        }
        public bool IsBelowWalkingPlatform(Vector3 p)
        {
            foreach(var platform in walkingPlatforms)
                if(Inside(new Vector2(p.x,p.z),platform.boundary)&&p.y<platform.surface-.5f)return true;
            foreach(var route in routes)
                if(route.surfaceHeights!=null&&route.surfaceHeights.Length==route.points.Length&&LineDistance(new Vector2(p.x,p.z),route.points)<route.halfWidth&&p.y<RouteHeight(route,new Vector2(p.x,p.z))-.5f)return true;
            return false;
        }
    }
}
