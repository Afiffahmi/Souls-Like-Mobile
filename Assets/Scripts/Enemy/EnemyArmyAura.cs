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
        public ElementalGems.GemDefinition[] elementPalette = new ElementalGems.GemDefinition[0];
        [Range(0,.2f)] public float flameShellWidth = .065f;
        [Range(0,100)] public float bodyWispsPerSecond = 45;
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
            public Renderer shell;
            public ElementalGems.GemSurfaceFlames wisps;
        }
        private readonly List<Surface> surfaces = new List<Surface>();
        private MaterialPropertyBlock paint;
        private MaterialPropertyBlock outerPaint;
        private ElementalGems.ElementalEnemy ownElement;
        private float nextTintCheck;
        private void RefreshTint()
        {
            if (ownElement == null) ownElement = GetComponent<ElementalGems.ElementalEnemy>();
            if (ownElement == null) return;
            var element = ownElement.CurrentElement;
            Color next = ElementalGems.ElementalReactionVfx.Tint(element);
            if (elementPalette != null) foreach (var gem in elementPalette)
                if (gem != null && gem.element == element) { next = gem.color; break; }
            next.a = 1;
            if (auraColor != next) { auraColor = next; if (isActiveAndEnabled) Apply(); }
        }
        private void Update()
        {
            if (Time.time < nextTintCheck) return;
            nextTintCheck = Time.time + .1f; RefreshTint();
        }
        private void Awake()
        {
            ownElement = GetComponent<ElementalGems.ElementalEnemy>();
            if (energyMaterial == null) energyMaterial = Resources.Load<Material>("ArmyElementalEnergy");
            if (energyMaterial == null) { Debug.LogError("Army aura needs the SwordSpecialEnergy material.", this); return; }
            paint = new MaterialPropertyBlock();
            outerPaint = new MaterialPropertyBlock();
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
                var go = new GameObject("Army Outer Energy"); go.layer = renderer.gameObject.layer;
                go.transform.SetParent(renderer.transform, false);
                if (renderer is SkinnedMeshRenderer skin)
                {
                    var shell = go.AddComponent<SkinnedMeshRenderer>();
                    shell.sharedMesh = skin.sharedMesh; shell.bones = skin.bones; shell.rootBone = skin.rootBone;
                    shell.localBounds = skin.localBounds; shell.quality = skin.quality;
                    surface.shell = shell;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    go.AddComponent<MeshFilter>().sharedMesh = filter != null ? filter.sharedMesh : null;
                    surface.shell = go.AddComponent<MeshRenderer>();
                }
                surface.shell.sharedMaterials = surface.aura;
                surface.shell.shadowCastingMode = ShadowCastingMode.Off; surface.shell.receiveShadows = false;
                surface.wisps = ElementalGems.GemSurfaceFlames.Create(renderer,
                    renderer is SkinnedMeshRenderer ? bodyWispsPerSecond : bodyWispsPerSecond * .3f, .32f, .6f);
                surfaces.Add(surface);
            }
        }
        private void OnEnable() { RefreshTint(); Apply(); }
        private void Apply()
        {
            if (paint == null) return;
            paint.Clear(); paint.SetColor("_Tint", auraColor);
            paint.SetFloat("_Opacity", opacity); paint.SetFloat("_Intensity", glowIntensity); paint.SetFloat("_Ribbon", 0);
            paint.SetFloat("_OuterShell", 0);
            outerPaint.Clear(); outerPaint.SetColor("_Tint", auraColor); outerPaint.SetFloat("_Opacity", opacity);
            outerPaint.SetFloat("_Intensity", glowIntensity); outerPaint.SetFloat("_OuterShell", 1);
            outerPaint.SetFloat("_ShellWidth", flameShellWidth);
            foreach (var surface in surfaces)
            {
                if (surface.renderer == null) continue;
                surface.renderer.sharedMaterials = surface.aura;
                surface.renderer.shadowCastingMode = ShadowCastingMode.Off;
                surface.renderer.receiveShadows = false;
                surface.renderer.SetPropertyBlock(paint);
                for (int i = 0; i < surface.aura.Length; i++) surface.renderer.SetPropertyBlock(paint, i);
                if (surface.shell != null)
                {
                    surface.shell.enabled = surface.renderer.enabled;
                    surface.shell.SetPropertyBlock(outerPaint);
                }
                if (surface.wisps != null) { surface.wisps.SetTint(auraColor, glowIntensity); surface.wisps.SetActive(surface.renderer.enabled); }
            }
        }
        private void OnValidate() { if (Application.isPlaying && isActiveAndEnabled) Apply(); }
        private void OnDisable()
        {
            foreach (var surface in surfaces)
            {
                if (surface.renderer == null) continue;
                if (surface.shell != null) surface.shell.enabled = false;
                if (surface.wisps != null) surface.wisps.SetActive(false);
                surface.renderer.sharedMaterials = surface.original;
                surface.renderer.shadowCastingMode = surface.shadows;
                surface.renderer.receiveShadows = surface.receiveShadows;
                surface.renderer.SetPropertyBlock(surface.originalProperties);
                for (int i = 0; i < surface.slotProperties.Length; i++) surface.renderer.SetPropertyBlock(surface.slotProperties[i], i);
            }
        }
        private void OnDestroy()
        {
            foreach (var surface in surfaces)
            {
                if (surface.shell != null) Destroy(surface.shell.gameObject);
                if (surface.wisps != null) Destroy(surface.wisps.gameObject);
            }
        }
    }
}
