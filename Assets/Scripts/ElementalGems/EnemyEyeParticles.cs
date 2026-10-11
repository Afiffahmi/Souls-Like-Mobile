using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    /// <summary>Innate character-design VFX, hidden during the link-up Normal-state lock.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ElementalEnemy))]
    public sealed class EnemyEyeParticles : MonoBehaviour
    {
        [Tooltip("Use the BowArrow prefab. Its Gem Arrow Visuals styles supply the same arrowhead prefabs and charged particle trails.")]
        public GemArrowVisuals arrowVisualsSource;
        [Tooltip("Optional explicit eye markers. Otherwise finds Trail_Particle descendants. If none exist, creates markers on this character's Left_Eye and Right_Eye bones at runtime.")]
        public Transform[] eyeMarkers = Array.Empty<Transform>();
        [Tooltip("Create missing markers on the character's eye bones when no custom markers are assigned or found.")]
        public bool useEyeBonesWhenMarkersMissing = true;
        [Min(.01f), Tooltip("World size relative to the arrowhead effect. Change this to resize both eyes together.")]
        public float eyeSize = .35f;
        public bool chargedParticleTrails = true;

        private ElementalEnemy enemy;
        private readonly List<Transform> anchors = new List<Transform>();
        private readonly List<GameObject> effects = new List<GameObject>();
        private ElementType displayedElement = ElementType.Normal;
        private GemVfxStyle displayedStyle;
        private GameObject displayedPrefab;
        private float displayedSize;
        private bool displayedTrails;

        private void Awake() => enemy = GetComponent<ElementalEnemy>();
        private void OnEnable() => FindEyeMarkers();
        // Animator initialization is complete by Start; retry bone discovery if needed.
        private void Start() { if (anchors.Count == 0) FindEyeMarkers(); }

        [ContextMenu("Refresh Eye Markers")]
        public void FindEyeMarkers()
        {
            ClearEffects();
            anchors.Clear();
            if (eyeMarkers != null && eyeMarkers.Length > 0)
            {
                foreach (var marker in eyeMarkers)
                    if (marker != null && marker != transform && marker.IsChildOf(transform) && !anchors.Contains(marker))
                        anchors.Add(marker);
                return;
            }
            foreach (var marker in GetComponentsInChildren<Transform>(true))
            {
                string markerName = marker.name;
                if (marker != transform && (markerName.Equals("Trail_Particle", StringComparison.OrdinalIgnoreCase) ||
                    markerName.StartsWith("Trail_Particle_", StringComparison.OrdinalIgnoreCase) ||
                    markerName.StartsWith("Trail_Particle (", StringComparison.OrdinalIgnoreCase)))
                    anchors.Add(marker);
            }
            if (anchors.Count == 0 && useEyeBonesWhenMarkersMissing && Application.isPlaying)
            {
                AddEyeBoneMarker(HumanBodyBones.LeftEye, "Left_Eye", "Trail_Particle_Left");
                AddEyeBoneMarker(HumanBodyBones.RightEye, "Right_Eye", "Trail_Particle_Right");
            }
            if (anchors.Count == 0 && Application.isPlaying)
                Debug.LogWarning("Enemy Eye Particles: no eye markers or eye bones found. Assign Eye Markers on " + name + ".", this);
        }

        private void AddEyeBoneMarker(HumanBodyBones bone, string boneName, string markerName)
        {
            Transform eye = null;
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
                eye = animator.GetBoneTransform(bone);
            if (eye == null)
                foreach (var candidate in GetComponentsInChildren<Transform>(true))
                    if (candidate.name.Equals(boneName, StringComparison.OrdinalIgnoreCase)) { eye = candidate; break; }
            if (eye == null) return;
            var marker = new GameObject(markerName).transform;
            marker.SetParent(eye, false);
            anchors.Add(marker);
        }

        private void LateUpdate()
        {
            // Element-born eyes turn off during the fixed Normal window, then return to their innate design.
            // Ordinary knockback never starts or refreshes that window.
            var element = enemy != null && enemy.Health != null && !enemy.Health.IsDead
                ? (enemy.IsElementBorn ? enemy.CurrentElement : enemy.element) : ElementType.Normal;
            var styles = arrowVisualsSource != null ? arrowVisualsSource.styles : null;
            var style = element == ElementType.Normal || styles == null ? null :
                Array.Find(styles, s => s != null && s.element == element);
            var prefab = style != null ? style.arrowheadAura : null;
            float size = style != null ? Mathf.Max(.01f, eyeSize) * style.size : 0;
            if (element == displayedElement && style == displayedStyle && prefab == displayedPrefab &&
                size == displayedSize && chargedParticleTrails == displayedTrails) return;

            ClearEffects();
            displayedElement = element;
            displayedStyle = style;
            displayedPrefab = prefab;
            displayedSize = size;
            displayedTrails = chargedParticleTrails;
            if (prefab == null) return;
            foreach (var marker in anchors)
            {
                if (marker == null) continue;
                var effect = Instantiate(prefab, marker, false);
                effect.name = "Eye Particle - " + element;
                effect.transform.localPosition = Vector3.zero;
                effect.transform.localRotation = Quaternion.identity;
                var scale = marker.lossyScale;
                effect.transform.localScale = new Vector3(
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.z))) * size;
                GemArrowVisuals.ConfigureTipParticles(effect, ElementalReactionVfx.Tint(element),
                    style.trailMaterial, chargedParticleTrails);
                effects.Add(effect);
            }
        }

        private void ClearEffects()
        {
            foreach (var effect in effects)
                if (effect != null) { effect.SetActive(false); Destroy(effect); }
            effects.Clear();
            displayedElement = ElementType.Normal;
            displayedStyle = null;
            displayedPrefab = null;
            displayedSize = 0;
        }

        private void OnDisable() => ClearEffects();
    }
}
