using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World.Editor
{
    public static class WorldAssetLayout
    {
        public const string Props = "Assets/models/Map/Props/Region5";
        public const string Materials = "Assets/Materials";
        public const string Textures = "Assets/Textures";
        public static bool UsesURP => GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
        static readonly string[] PropNames = {"bamboo","barrel","boat","crates","crocodile_statue","dock","fallen_log","giant_crocodile_skulls","lily_pad","long_house","rainforest_tree","root_formation","ruined_walls","stilt_house","stone_arch","tree_stump","wooden_bridge","wooden_walkway"};
        public static string MaterialPath(string name) => Materials + "/" + (PropNames.Contains(name) ? "Region5_" : "Nusantara_") + name + ".mat";
        public static string TexturePath(string file) => Textures + "/Region5_" + file;
        public static string UtilityTexturePath(string file) => Textures + "/Nusantara_" + file;
        public static void SetColor(Material material, Color color) => material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : "_TintColor", color);
        public static void SetTexture(Material material, Texture texture) => material.SetTexture(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", texture);
        public static string LitShader => UsesURP ? "Universal Render Pipeline/Lit" : "Standard";
        public static string MistShader => UsesURP ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Alpha Blended";

        [MenuItem("Tools/Nusantara/Use Project Asset Folders and Rendering")]
        public static string Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before organizing assets.");
            if(!AssetDatabase.IsValidFolder(Materials)||!AssetDatabase.IsValidFolder(Textures))throw new InvalidOperationException("Use the project's existing Materials and Textures folders.");
            EnsureFolder(Props);
            int moved=0, fixedMaterials=0;
            void Move(string from,string to)
            {
                if(from==to||!File.Exists(from))return;
                if(File.Exists(to))throw new IOException("Destination already exists; preserving both assets: "+to);
                string error=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(error))throw new IOException(error);moved++;
            }
            string oldMaterials=WorldReferenceBuilder.Root+"/Materials";
            var materialGuids=AssetDatabase.FindAssets("t:Material",new[]{Materials}.Concat(AssetDatabase.IsValidFolder(oldMaterials)?new[]{oldMaterials}:Array.Empty<string>()).ToArray());
            foreach(string guid in materialGuids)
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(!path.StartsWith(oldMaterials+"/")&&!Path.GetFileName(path).StartsWith("Region5_")&&!Path.GetFileName(path).StartsWith("Nusantara_"))continue;
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null)continue;
                Color color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.HasProperty("_Color")?material.GetColor("_Color"):Color.white;
                Texture texture=material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap"):material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
                bool mist=Path.GetFileName(path).Contains("SwampMist") || (material.shader!=null&&material.shader.name.Contains("Particles"));
                if(material.shader!=null&&material.shader.name.StartsWith("Universal Render Pipeline/")&&!UsesURP)
                {
                    material.shader=Shader.Find(mist?MistShader:LitShader);material.shaderKeywords=Array.Empty<string>();
                    SetColor(material,color);SetTexture(material,texture);material.enableInstancing=!mist;
                    material.renderQueue=mist?3000:-1;
                    if(!mist) { material.SetFloat("_Glossiness",.15f);material.SetFloat("_Metallic",0);material.SetFloat("_Mode",0);material.SetFloat("_SrcBlend",(float)BlendMode.One);material.SetFloat("_DstBlend",(float)BlendMode.Zero);material.SetFloat("_ZWrite",1);material.SetOverrideTag("RenderType",""); }
                    fixedMaterials++;EditorUtility.SetDirty(material);
                }
                if(path.StartsWith(oldMaterials+"/"))Move(path,MaterialPath(Path.GetFileNameWithoutExtension(path)));
            }
            string source="Assets/EEJANAI_Team/Level 5 Props";
            foreach(string folder in new[]{source+"/Models",WorldReferenceBuilder.Root+"/Props",WorldReferenceBuilder.Root+"/Meshes"})
            {
                if(!Directory.Exists(folder))continue;
                foreach(string file in Directory.GetFiles(folder).Where(f=>!f.EndsWith(".meta")&&!Path.GetFileName(f).StartsWith(".")))Move(file.Replace('\\','/'),Props+"/"+Path.GetFileName(file));
            }
            if(Directory.Exists(source+"/Materials"))
                foreach(string file in Directory.GetFiles(source+"/Materials").Where(f=>!f.EndsWith(".meta")&&!Path.GetFileName(f).StartsWith(".")))Move(file.Replace('\\','/'),TexturePath(Path.GetFileName(file)));
            if(Directory.Exists(oldMaterials))
                foreach(string file in Directory.GetFiles(oldMaterials).Where(f=>!f.EndsWith(".meta")&&!Path.GetFileName(f).StartsWith(".")))Move(file.Replace('\\','/'),UtilityTexturePath(Path.GetFileName(file)));
            var catalog=AssetDatabase.LoadAssetAtPath<WorldPropCatalog>("Assets/NusantaraWorld/Region5PropCatalog.asset");
            if(catalog!=null) {foreach(var entry in catalog.entries)entry.assetPath=AssetDatabase.GUIDToAssetPath(entry.assetGuid);EditorUtility.SetDirty(catalog);}
            foreach(string folder in new[]{source+"/Models",source+"/Materials",WorldReferenceBuilder.Root+"/Props",WorldReferenceBuilder.Root+"/Meshes",oldMaterials,source})
                if(Directory.Exists(folder)&&Directory.GetFileSystemEntries(folder).Length==0)AssetDatabase.DeleteAsset(folder);
            AssetDatabase.SaveAssets();
            return $"Moved {moved} assets with GUIDs preserved; corrected {fixedMaterials} materials for {(UsesURP?"URP":"Built-in")} rendering. Materials use existing Assets/Materials.";
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
