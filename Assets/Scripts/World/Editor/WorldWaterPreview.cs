using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    [InitializeOnLoad]
    public static class WorldWaterPreview
    {
        const string Preference = "Nusantara.AnimateWaterInEditor";
        static readonly int TimeId = Shader.PropertyToID("_NusantaraWaterEditorTime");
        static readonly int EnabledId = Shader.PropertyToID("_NusantaraWaterEditorPreview");
        static double nextFrame;
        static WorldWaterPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Shader.SetGlobalFloat(EnabledId, 0);
            AssemblyReloadEvents.beforeAssemblyReload += () => Shader.SetGlobalFloat(EnabledId, 0);
        }
        [MenuItem("Tools/Nusantara/Water/Animate in Scene View")]
        static void Toggle()
        {
            EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true));
            Shader.SetGlobalFloat(EnabledId, 0); SceneView.RepaintAll();
        }
        [MenuItem("Tools/Nusantara/Water/Animate in Scene View", true)]
        static bool ToggleValidation() { Menu.SetChecked("Tools/Nusantara/Water/Animate in Scene View", EditorPrefs.GetBool(Preference, true)); return true; }
        static void Update()
        {
            bool enabled = !EditorApplication.isPlayingOrWillChangePlaymode && EditorPrefs.GetBool(Preference, true);
            if (!enabled) { Shader.SetGlobalFloat(EnabledId, 0); return; }
            double now = EditorApplication.timeSinceStartup;
            if (now < nextFrame || !UnityEditorInternal.InternalEditorUtility.isApplicationActive) return;
            nextFrame = now + .05;
            Shader.SetGlobalFloat(TimeId, (float)now); Shader.SetGlobalFloat(EnabledId, 1);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }
    }
}
