using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nusantara.World.Editor
{
    // Traced from the supplied 1802 x 1140 sketch. North is +Z. Both region
    // edges use the same vertices, rather than six disconnected rectangular maps.
    public static class WorldReferenceLayout
    {
        public static void Apply(WorldGeographyProfile profile)
        {
            var w = profile.world;
            Vector2[] P(params float[] xy) { var a = new Vector2[xy.Length / 2]; for (int i = 0; i < a.Length; i++) a[i] = profile.ReferencePoint(xy[i * 2], xy[i * 2 + 1]); return a; }
            profile.coast = P(150,594, 163,325, 210,235, 247,196, 263,124, 280,91, 307,71, 646,71, 678,55, 759,19, 804,16, 877,24, 943,42, 1002,66, 1070,98, 1118,129, 1165,131, 1196,128, 1231,162, 1269,204, 1310,245, 1371,278, 1420,308, 1450,360, 1490,408, 1521,440, 1560,479, 1620,546, 1598,598, 1548,650, 1490,699, 1501,729, 1498,776, 1344,871, 1288,895, 1240,924, 1200,964, 1175,980, 1090,1000, 1040,1030, 980,1060, 870,1084, 780,1100, 744,1100, 717,1090, 681,1053, 647,1011, 629,987, 550,988, 476,983, 414,970, 352,954, 302,954, 265,941, 237,916, 217,881, 207,835, 197,817, 199,800, 217,776, 211,751, 198,708, 180,632, 176,610);
            var centralNorth = P(700,285, 750,285, 795,315, 850,315, 900,325, 970,333, 1020,344, 1060,363, 1090,375);
            var centralEast = P(1090,375, 1090,550, 1060,557, 1020,578, 1005,608, 982,642, 982,713, 920,724, 875,731, 845,732);
            var centralSouth = P(845,732, 827,721, 799,708, 761,704, 735,689, 718,656, 700,638, 669,619, 634,612, 611,573, 594,545, 586,523, 565,496);
            var centralWest = P(565,496, 600,438, 650,401, 665,378, 665,347, 685,327, 700,285);
            var northWest = P(678,55, 679,207, 702,260, 700,285);
            var westSouth = P(565,496, 536,511, 500,517, 518,537, 518,565, 508,596, 486,618, 394,639, 350,663, 328,697, 282,750, 256,770);
            var southSplit = P(845,732, 842,755, 815,780, 817,816, 838,855, 859,879, 865,942, 866,1083);
            var eastSouth = P(1090,550, 1105,557, 1130,552, 1141,560, 1160,556, 1186,557, 1206,572, 1230,605, 1255,622, 1292,639, 1312,657, 1325,705, 1322,803, 1344,871);
            var eastNorth = P(1090,375, 1085,354, 1120,325, 1155,303, 1200,287, 1240,273, 1263,271, 1295,246, 1310,245);
            Vector2[] Join(params Vector2[][] lines) => lines.SelectMany(x => x).Distinct().ToArray();
            Vector2[] Rev(Vector2[] x) => x.Reverse().ToArray();
            w.regions[0].boundary = Join(centralNorth, centralEast, centralSouth, centralWest);
            w.regions[1].boundary = Join(P(256,770, 217,776, 198,708, 176,610, 150,594, 163,325, 210,235, 247,196, 263,124, 280,91, 307,71, 646,71, 678,55), northWest, Rev(centralWest), westSouth);
            w.regions[2].boundary = Join(Rev(westSouth), Rev(centralSouth), southSplit, P(866,1083, 780,1100, 744,1100, 717,1090, 681,1053, 647,1011, 629,987, 550,988, 476,983, 414,970, 352,954, 302,954, 265,941, 237,916, 217,881, 197,817, 217,776, 256,770));
            w.regions[3].boundary = Join(Rev(southSplit), Rev(centralEast), eastSouth, P(1344,871, 1288,895, 1240,924, 1200,964, 1175,980, 1090,1000, 1040,1030, 980,1060, 866,1083));
            w.regions[4].boundary = Join(eastNorth, P(1310,245, 1371,278, 1420,308, 1450,360, 1490,408, 1521,440, 1560,479, 1620,546, 1598,598, 1548,650, 1490,699, 1501,729, 1498,776, 1344,871), Rev(eastSouth), P(1090,550, 1090,375));
            w.regions[5].boundary = Join(P(678,55, 759,19, 804,16, 877,24, 943,42, 1002,66, 1070,98, 1118,129, 1165,131, 1196,128, 1231,162, 1269,204, 1310,245), Rev(eastNorth), Rev(centralNorth), Rev(northWest));
            var landmarks = P(1265,470, 1190,360, 1200,525, 1330,580, 1290,310, 1460,510, 1400,380, 1450,650, 1460,725);
            for (int i = 0; i < w.locations.Length; i++)
            {
                var p = landmarks[i]; w.locations[i].position = new Vector3(p.x, profile.BaseHeight(p), p.y);
                w.locations[i].positionConfirmed = true; w.locations[i].clearRadius = i == 8 ? 100 : i == 0 ? 160 : i == 7 ? 80 : 65;
            }
            WorldWaterway Water(string name, float radius, params float[] xy) => new WorldWaterway { name = name, halfWidth = radius, points = P(xy), surface = 35 };
            profile.waterways = new[] {
                Water("Sungai Bujang main river", 48, 1290,310, 1270,355, 1290,392, 1260,435, 1285,480, 1240,505, 1220,541),
                Water("Delta western channel", 27, 1260,435, 1215,455, 1205,485, 1180,508, 1145,528),
                Water("Delta eastern channel", 31, 1285,480, 1290,525, 1330,548, 1365,610, 1405,660, 1425,725, 1400,780),
                Water("Swamp branch", 22, 1290,525, 1355,530, 1390,563, 1370,605),
                Water("Delta island channel", 24, 1240,505, 1260,525, 1285,542),
                Water("Sacred waterfall pool", 105, 1290,310),
                Water("Sarang river basin", 135, 1430,727),
                Water("Gua Sunyi hidden pool", 20, 1406,373),
                Water("Region 1 freshwater basin", 185, 785,485, 801,474, 820,470),
                Water("Region 1 southern lake", 95, 753,560, 770,565)
            };
            WorldWalkingRoute Route(string name, params float[] xy) => new WorldWalkingRoute { name = name, points = P(xy), halfWidth = 10 };
            profile.routes = new[] {
                Route("Region 1 to western forest", 900,470, 810,410, 670,400, 520,350, 415,350),
                Route("Region 1 to ancient forest", 900,470, 840,590, 735,650, 660,790, 530,820),
                Route("Region 1 to stone foothills", 900,470, 950,580, 955,695, 1030,795, 1060,865),
                Route("Region 1 to northern kingdom", 900,470, 910,360, 920,285, 940,145),
                Route("Region 1 to Kampung Sungai", 900,470, 1020,460, 1125,465, 1200,470, 1265,470),
                Route("Village to Hutan Rimba", 1265,470, 1230,435, 1190,400, 1190,360),
                Route("Forest to waterfall", 1190,360, 1230,325, 1268,310),
                Route("Waterfall to hidden cave", 1268,310, 1320,325, 1380,350, 1400,365, 1400,380),
                Route("Village to delta", 1265,470, 1220,493, 1200,525),
                Route("Village to swamp", 1265,470, 1310,505, 1330,580),
                Route("Village to ruins", 1265,470, 1360,470, 1410,490, 1460,510),
                Route("Ruins to cave", 1460,510, 1435,445, 1400,395, 1400,380),
                Route("Ruins to Pulau Buaya", 1460,510, 1460,590, 1450,650),
                Route("Swamp to elite territory", 1330,580, 1380,635, 1450,650),
                Route("Elite territory to boss arena", 1450,650, 1470,685, 1460,725),
                Route("Region 4 eastern connection", 1060,865, 1200,815, 1290,775, 1380,745, 1460,725)
            };
            // The sketch governs regional adjacency. The concept sheet's Map 3 arrow
            // therefore connects west through Region 1 rather than inventing a 3–5 border.
            Vector2 start = profile.ReferencePoint(900, 470);
            w.playerStart = new Vector3(start.x, profile.Height(start.x, start.y) + .15f, start.y);
            w.playerStartConfirmed = true; w.layoutApproved = false; // Enabled only after generation finishes.
        }
    }
}
