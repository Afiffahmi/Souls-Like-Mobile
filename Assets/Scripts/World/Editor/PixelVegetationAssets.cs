using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    // Only the vegetation palette and its assets are changed. Terrain brushes stay independent.
    public static class PixelVegetationAssets
    {
        const string Root = VegetationPaintAssets.Root;
        public const string TreePath = Root + "/PixelPaintTree.prefab";

        [MenuItem("Tools/Nusantara/Vegetation/Apply Reference Pixel Vegetation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply vegetation style in Edit mode.");
            var palette = VegetationPaintAssets.Prepare();
            string backup = "Library/Nusantara/PixelVegetationBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            foreach (string path in new[] { VegetationPaintAssets.PalettePath, Root + "/GrassTuft.asset", "Assets/Materials/KampungGrass.mat", TreePath })
                VegetationPaintAssets.Backup(path, backup);

            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KampungGrass.mat");
            grass.shader = Shader.Find("Nusantara/Kampung Living Grass");
            // Muted source colours compensate for the game's existing bright grading.
            grass.SetColor("_RootColor", new Color(.30f,.38f,.28f));
            grass.SetColor("_TipColor", new Color(.46f,.54f,.34f));
            grass.SetColor("_PatchColor", new Color(.35f,.46f,.32f));
            grass.SetFloat("_Wind", .09f); grass.SetFloat("_WindSpeed", 1.1f);
            grass.SetFloat("_PatchSize", 4); grass.SetFloat("_ShadeSteps", 3);
            grass.SetFloat("_Density", 1); grass.enableInstancing = true;
            EditorUtility.SetDirty(grass);
            StoreMesh(VegetationPaintAssets.GrassTuft(), Root + "/GrassTuft.asset");

            var leaves = Material(Root + "/PixelTreeLeaves.mat", "Nusantara/Pixel Leaf Canopy");
            leaves.mainTexture = StoreTexture(LeafClusters(), Root + "/PixelLeafClusters.asset");
            leaves.SetColor("_Color", new Color(.46f,.54f,.32f));
            leaves.SetColor("_ShadowColor", new Color(.25f,.39f,.34f));
            leaves.SetFloat("_Wind", .18f); leaves.SetFloat("_WindSpeed", .8f);
            leaves.SetFloat("_Cutoff", .5f); leaves.SetFloat("_ShadeSteps", 3);
            EditorUtility.SetDirty(leaves);
            // A clean branching trunk replaces the source tree's oversized root mound.
            var bark = Material(Root + "/PixelTreeBark.mat", "Nusantara/Pixel Leaf Canopy");
            bark.mainTexture = Texture2D.whiteTexture;
            bark.SetColor("_Color", new Color(.37f,.35f,.32f));
            bark.SetColor("_ShadowColor", new Color(.25f,.30f,.29f));
            bark.SetFloat("_Wind", 0); bark.SetFloat("_ShadeSteps", 3);
            EditorUtility.SetDirty(bark);
            var crown = StoreMesh(Crown(72), Root + "/PixelTreeCrown.asset");
            var lowCrown = StoreMesh(Crown(28), Root + "/PixelTreeCrownLOD.asset");
            var trunkMesh = StoreMesh(Trunk(), Root + "/PixelTreeTrunk.asset");
            var tree = new GameObject("Pixel paint tree");
            try
            {
                tree.name = "Pixel paint tree";
                var trunkRenderer = Child(tree, "Branching pixel trunk", trunkMesh, bark);
                var near = Child(tree, "Pixel leaf crown", crown, leaves);
                var far = Child(tree, "Pixel leaf crown LOD", lowCrown, leaves);
                var lod = tree.GetComponent<LODGroup>();
                if (lod == null) lod = tree.AddComponent<LODGroup>();
                lod.SetLODs(new[] {
                    new LOD(.10f, new Renderer[] { trunkRenderer, near }),
                    new LOD(.015f, new Renderer[] { trunkRenderer, far })
                });
                lod.RecalculateBounds();
                Undo.RecordObject(palette, "Apply pixel vegetation palette");
                palette.treePrefab = PrefabUtility.SaveAsPrefabAsset(tree, TreePath);
            }
            finally { Object.DestroyImmediate(tree); }
            palette.grassHeight = new Vector2(.32f,.65f);
            palette.grassSpacing = .32f;
            palette.treeCollisionRadius = .8f;
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log("Reference grass and leafy tree applied to Tree & Grass Painter. Terrain brushes are unchanged. Backup: " + backup);
        }

        static MeshRenderer Child(GameObject root, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided; renderer.receiveShadows = true;
            return renderer;
        }
        static Material Material(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(shaderName)); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find(shaderName); material.enableInstancing = true;
            return material;
        }
        static Mesh StoreMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        static Texture2D StoreTexture(Texture2D texture, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null) { AssetDatabase.CreateAsset(texture, path); return texture; }
            EditorUtility.CopySerialized(texture, existing); Object.DestroyImmediate(texture); EditorUtility.SetDirty(existing); return existing;
        }
        static Texture2D LeafClusters()
        {
            // Small, point-filtered cutouts give the crown a broken leafy edge,
            // with actual gaps through to the branches rather than transparent blending.
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "Pixel leaf clusters", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size*size]; var random = new System.Random(1407);
            for (int leaf = 0; leaf < 18; leaf++)
            {
                float angle = leaf * 2.39996f, radius = leaf == 0 ? 0 : 5 + (float)random.NextDouble()*17;
                float cx = 31 + Mathf.Cos(angle)*radius, cy = 31 + Mathf.Sin(angle)*radius;
                float rx = 7 + (float)random.NextDouble()*5, ry = 4 + (float)random.NextDouble()*3;
                byte shade = (byte)(190 + random.Next(65));
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float dx = x-cx, dy = y-cy;
                    float a = (dx*Mathf.Cos(angle)+dy*Mathf.Sin(angle))/rx;
                    float b = (-dx*Mathf.Sin(angle)+dy*Mathf.Cos(angle))/ry;
                    if (a*a+b*b < 1) pixels[y*size+x] = new Color32(shade,shade,shade,255);
                }
            }
            texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
        }
        public static Mesh Crown(int leavesPerLobe,int shape=0)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            var random = new System.Random(5261);
            Vector3[] centers = {
                new Vector3(0,18.2f,0), new Vector3(-4.2f,16.8f,-1), new Vector3(3.8f,16.4f,1),
                new Vector3(-1,16,-4.5f), new Vector3(1.5f,17,4.3f), new Vector3(-3.8f,15.1f,3.6f),
                new Vector3(4.2f,15.2f,-3.4f), new Vector3(-.8f,14.8f,1), new Vector3(2.2f,18.1f,-2)
            };
            if(shape==1)centers=new[]{new Vector3(0,9,0),new Vector3(.2f,11,0),new Vector3(-.2f,13,.2f),new Vector3(.1f,15,-.1f),new Vector3(0,17,0),new Vector3(.1f,19,0),new Vector3(0,21,0),new Vector3(0,22,0)};
            if(shape==2)centers=new[]{new Vector3(-4,14,0),new Vector3(-5,16,1),new Vector3(-2,18,-1),new Vector3(4,14,-1),new Vector3(5,17,1),new Vector3(2,19,0),new Vector3(0,15,3)};
            foreach (var center in centers)
            {
                float lobeSize = shape==1?Mathf.Max(.75f,(24-center.y)*.25f):2.8f+(float)random.NextDouble()*.9f;
                for (int leaf = 0; leaf < leavesPerLobe; leaf++)
                {
                    float y = 1-2*(leaf+.5f)/leavesPerLobe, angle = leaf*2.39996f;
                    var radial = new Vector3(Mathf.Cos(angle)*Mathf.Sqrt(1-y*y), y, Mathf.Sin(angle)*Mathf.Sqrt(1-y*y));
                    var point = center+Vector3.Scale(radial,new Vector3(lobeSize,lobeSize*.8f,lobeSize));
                    var orientation = Quaternion.LookRotation(radial)*Quaternion.Euler(0,0,(float)random.NextDouble()*360);
                    float size = 1.8f+(float)random.NextDouble()*.9f;
                    var right = orientation*Vector3.right*size*.5f; var up = orientation*Vector3.up*size*.5f;
                    int n = vertices.Count;
                    vertices.AddRange(new[] { point-right-up, point+right-up, point+right+up, point-right+up });
                    // Cloud-shaped lighting instead of each flat card flashing independently.
                    var normal = (radial*.45f+Vector3.up*.8f).normalized;
                    normals.AddRange(new[] { normal,normal,normal,normal });
                    float light = Mathf.Clamp01(.12f+(point.y-13)/11+(float)random.NextDouble()*.18f);
                    var color = new Color(light,light,light,.65f+(float)random.NextDouble()*.35f);
                    colors.AddRange(new[] { color,color,color,color });
                    uv.AddRange(new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up });
                    triangles.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
                }
            }
            var mesh = new Mesh { name = "Layered pixel leaf crown" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds(); var bounds=mesh.bounds; bounds.Expand(1.5f); mesh.bounds=bounds;
            return mesh;
        }
        public static Mesh Trunk()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            void Branch(Vector3[] points,float[] radii)
            {
                const int sides=7;int start=vertices.Count;
                for(int ring=0;ring<points.Length;ring++)
                {
                    var direction=ring+1<points.Length?points[ring+1]-points[ring]:points[ring]-points[ring-1];
                    var rotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
                    for(int side=0;side<sides;side++)
                    {
                        float angle=side*Mathf.PI*2/sides;
                        vertices.Add(points[ring]+rotation*new Vector3(Mathf.Cos(angle)*radii[ring],0,Mathf.Sin(angle)*radii[ring]));
                        float shade=.40f+.3f*(.5f+.5f*Mathf.Cos(angle+.8f));colors.Add(new Color(shade,shade,shade,0));
                        if(ring==0)continue;
                        int a=start+(ring-1)*sides+side,b=start+(ring-1)*sides+(side+1)%sides,c=start+ring*sides+side,d=start+ring*sides+(side+1)%sides;
                        triangles.AddRange(new[]{a,c,b,b,c,d});
                    }
                }
            }
            Branch(new[]{Vector3.zero,new Vector3(.08f,.8f,0),new Vector3(.2f,6,.1f),new Vector3(-.15f,11,.2f),new Vector3(.3f,16,0)},new[]{1.05f,.8f,.65f,.48f,.12f});
            Branch(new[]{new Vector3(.1f,8,0),new Vector3(-1.4f,11.5f,.5f),new Vector3(-3.6f,15.5f,1.3f)},new[]{.42f,.29f,.08f});
            Branch(new[]{new Vector3(0,10,.1f),new Vector3(2,12.5f,-.6f),new Vector3(4,16,-1.8f)},new[]{.35f,.24f,.07f});
            Branch(new[]{new Vector3(0,11,0),new Vector3(.4f,13,2),new Vector3(1,16,3.5f)},new[]{.3f,.2f,.06f});
            var mesh=new Mesh{name="Tapered branching pixel trunk"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
