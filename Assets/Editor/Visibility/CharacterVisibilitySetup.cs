using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CharacterVisibilitySetup
{
    [MenuItem("Tools/Nusantara/Visibility/Enable Circular Reveal on Props")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure visibility outside Play mode.");
        var standard = Shader.Find("Nusantara/Props/Standard Circular Reveal");
        var foliage = Shader.Find("Nusantara/Props/Foliage Circular Reveal");
        if (!standard || !foliage || ShaderUtil.ShaderHasError(standard) || ShaderUtil.ShaderHasError(foliage))
            throw new InvalidOperationException("Reveal shaders must compile before configuring materials.");
        var materials = new HashSet<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Map/Props" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material) materials.Add(material);
        }
        int changed = 0;
        foreach (var material in materials)
        {
            // Trees retain their normal foliage, trunks and shadows.
            if (System.Text.RegularExpressions.Regex.IsMatch(material.name,
                @"(^|[ _+.-])(tree|trees|stump|canopy|bark)([ _+.-]|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            // Only edit project-owned prop materials, never package or other shared assets.
            if (!AssetDatabase.GetAssetPath(material).StartsWith("Assets/Materials/Map/Props/", StringComparison.Ordinal))
            { Debug.LogWarning("Skipped material outside the prop material folder: " + material.name); continue; }
            Shader replacement = material.shader.name == "Standard" ? standard :
                material.shader.name == "Nusantara/Kampung Foliage" ? foliage : null;
            if (!replacement) continue;
            Undo.RecordObject(material, "Enable circular prop reveal");
            var keywords = material.shaderKeywords;
            int queue = material.renderQueue;
            material.shader = replacement;
            material.shaderKeywords = keywords;
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            changed++;
        }
        var camera = Camera.main;
        var rig = UnityEngine.Object.FindFirstObjectByType<IsometricPlayerCamera>();
        if (camera && rig)
        {
            var reveal = camera.GetComponent<CharacterRevealCamera>();
            if (!reveal) reveal = Undo.AddComponent<CharacterRevealCamera>(camera.gameObject);
            Undo.RecordObject(reveal, "Assign reveal target");
            reveal.target = rig.player;
            EditorUtility.SetDirty(reveal);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        }
        Debug.Log("Circular reveal configured: " + changed + " prop materials updated. Save the scene to keep the camera component.");
    }
}
