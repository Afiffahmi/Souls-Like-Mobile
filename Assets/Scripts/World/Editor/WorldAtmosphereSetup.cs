using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    [InitializeOnLoad]
    public static class WorldAtmosphereSetup
    {
        const string SkyPath="Assets/Materials/Rendering/WorldCloudSky.mat";
        static WorldAtmosphereSetup(){EditorSceneManager.sceneSaving+=BeforeSave;}
        static void BeforeSave(Scene scene,string path)
        {
            if(Application.isPlaying)return;
            foreach(var cycle in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldDayNightCycle>(true)))
                cycle.RestoreLighting();
        }
        [MenuItem("Tools/Nusantara/Environment/Set Up Clouds and Day Night")]
        public static string Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Set up atmosphere in Edit mode.");
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Save the world scene first.");
            var sun=RenderSettings.sun;
            if(sun==null)sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional && l.gameObject.scene==scene);
            if(sun==null)throw new InvalidOperationException("The world needs a directional sunlight.");
            string backup="Library/Nusantara/AtmosphereBackup/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            VegetationPaintAssets.Backup(scene.path,backup);
            var cycle=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldDayNightCycle>(true)).FirstOrDefault();
            if(cycle==null)
            {
                var root=new GameObject("World Atmosphere");Undo.RegisterCreatedObjectUndo(root,"Set up world atmosphere");
                cycle=Undo.AddComponent<WorldDayNightCycle>(root);
            }
            cycle.RestoreLighting();Undo.RecordObject(cycle,"Configure atmosphere");cycle.sun=sun;
            if(cycle.GetComponent<WorldGrassInteraction>()==null)Undo.AddComponent<WorldGrassInteraction>(cycle.gameObject);
            if(cycle.moon==null)
            {
                var moon=new GameObject("Moonlight");Undo.RegisterCreatedObjectUndo(moon,"Add moonlight");moon.transform.SetParent(cycle.transform,false);
                cycle.moon=Undo.AddComponent<Light>(moon);cycle.moon.type=LightType.Directional;cycle.moon.intensity=0;
                cycle.moon.color=new Color(.57f,.68f,1);cycle.moon.shadows=LightShadows.Hard;cycle.moon.shadowStrength=.55f;
            }
            var sky=AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if(sky==null)
            {
                var shader=Shader.Find("Nusantara/Drifting Cloud Sky");if(shader==null)throw new InvalidOperationException("Import cloud sky shader first.");
                sky=new Material(shader){name="World moving cloud sky"};AssetDatabase.CreateAsset(sky,SkyPath);
            }
            sky.SetFloat("_Daylight",1);sky.SetVector("_SunDirection",-sun.transform.forward);sky.SetVector("_MoonDirection",-cycle.moon.transform.forward);
            cycle.skyMaterial=sky;RenderSettings.skybox=sky;RenderSettings.sun=sun;
            var clouds=sun.GetComponent<WorldCloudShadows>();if(clouds==null)clouds=Undo.AddComponent<WorldCloudShadows>(sun.gameObject);
            Undo.RecordObject(clouds,"Configure travelling clouds");clouds.cloudShadowShader=Shader.Find("Hidden/Nusantara/Drifting Cloud Shadows");clouds.timeCycle=cycle;
            sky.SetFloat("_CloudCoverage",clouds.cloudCoverage);sky.SetFloat("_CloudScale",clouds.cloudSize);sky.SetFloat("_CloudHeight",clouds.cloudAltitude);
            EditorUtility.SetDirty(sky);EditorUtility.SetDirty(cycle);EditorUtility.SetDirty(clouds);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject=cycle.gameObject;EditorGUIUtility.PingObject(cycle.gameObject);
            return "Atmosphere ready. World Atmosphere: Day Minutes / Night Minutes. Directional Light: cloud Wind Speed / Cloud Size / Coverage. Backup: "+backup;
        }
    }
}
