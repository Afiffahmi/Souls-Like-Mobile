using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Enemies
{
    /// <summary>Energy-only body and equipment using the same shader as the player's sword special.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArmyAura : MonoBehaviour
    {
        public Material energyMaterial;
        [ColorUsage(false, true)] public Color auraColor = new Color(.35f, .75f, 1f, 1f);
        [Range(0, 3)] public float glowIntensity = 1.25f;
        [Range(0, 1)] public float opacity = .6f;
        [Tooltip("Include the skinned body AND sword/shield mesh renderers. Empty discovers all model meshes at spawn.")]
        public Renderer[] modelRenderers = new Renderer[0];
        private sealed class Surface
        {
            public Renderer renderer;
            public Material[] original, aura;
            public MaterialPropertyBlock originalProperties;
            public MaterialPropertyBlock[] slotProperties;
            public ShadowCastingMode shadows;
            public bool receiveShadows;
        }
        private readonly List<Surface> surfaces = new List<Surface>();
        private MaterialPropertyBlock paint;
        private void Awake()
        {
            if (energyMaterial == null) energyMaterial = Resources.Load<Material>("SwordSpecialEnergy");
            if (energyMaterial == null) { Debug.LogError("Army aura needs the SwordSpecialEnergy material.", this); return; }
            paint = new MaterialPropertyBlock();
            if (modelRenderers == null || modelRenderers.Length == 0) modelRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in modelRenderers)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                var materials = renderer.sharedMaterials;
                var surface = new Surface { renderer = renderer, original = materials,
                    aura = new Material[materials.Length], originalProperties = new MaterialPropertyBlock(),
                    slotProperties = new MaterialPropertyBlock[materials.Length],
                    shadows = renderer.shadowCastingMode, receiveShadows = renderer.receiveShadows };
                renderer.GetPropertyBlock(surface.originalProperties);
                for (int i = 0; i < materials.Length; i++)
                {
                    surface.aura[i] = energyMaterial;
                    surface.slotProperties[i] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(surface.slotProperties[i], i);
                }
                surfaces.Add(surface);
            }
        }
        private void OnEnable() => Apply();
        private void Apply()
        {
            if (paint == null) return;
            paint.Clear(); paint.SetColor("_Tint", auraColor);
            paint.SetFloat("_Opacity", opacity); paint.SetFloat("_Intensity", glowIntensity); paint.SetFloat("_Ribbon", 0);
            foreach (var surface in surfaces)
            {
                if (surface.renderer == null) continue;
                surface.renderer.sharedMaterials = surface.aura;
                surface.renderer.shadowCastingMode = ShadowCastingMode.Off;
                surface.renderer.receiveShadows = false;
                surface.renderer.SetPropertyBlock(paint);
                for (int i = 0; i < surface.aura.Length; i++) surface.renderer.SetPropertyBlock(paint, i);
            }
        }
        private void OnValidate() { if (Application.isPlaying && isActiveAndEnabled) Apply(); }
        private void OnDisable()
        {
            foreach (var surface in surfaces)
            {
                if (surface.renderer == null) continue;
                surface.renderer.sharedMaterials = surface.original;
                surface.renderer.shadowCastingMode = surface.shadows;
                surface.renderer.receiveShadows = surface.receiveShadows;
                surface.renderer.SetPropertyBlock(surface.originalProperties);
                for (int i = 0; i < surface.slotProperties.Length; i++) surface.renderer.SetPropertyBlock(surface.slotProperties[i], i);
            }
        }
    }
}
