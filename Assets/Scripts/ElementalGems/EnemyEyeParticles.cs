using System;
using System.Collections.Generic;
using UnityEngine;
using Nusantara.World;

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
        public float eyeSize = .2f;
        [InspectorName("Show Ribbon Tail")]
        public bool chargedParticleTrails = true;
        [HideInInspector]
        public float trailLifetime = 1.2f;
        [Header("Visible eye ribbon")]
        public Material ribbonMaterial;
        [Min(.1f), Tooltip("Maximum world length of the trail left by movement. No tail is drawn while idle.")]
        public float tailLength = 1.2f;
        [Min(.05f), Tooltip("Seconds before the movement tail fades away after stopping.")]
        public float tailFadeSeconds = .25f;
        [Min(.01f), Tooltip("World width of the bright ribbon. The core and soft glow scale with this.")]
        public float tailWidth = .035f;
        [Min(1), Tooltip("Brightness of the core and ribbon; remains visible without camera bloom.")]
        public float brightness = 3f;
        [Tooltip("Optional clock override. Otherwise uses the active world's day/night clock. Eyes are hidden without an active clock and during 06:00–18:00.")]
        public WorldDayNightCycle timeCycle;
        private static WorldDayNightCycle sharedTimeCycle;
        private static float nextClockLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClock() { sharedTimeCycle = null; nextClockLookup = 0; }
        private bool IsNight()
        {
            if (timeCycle != null) return timeCycle.isActiveAndEnabled && timeCycle.IsNight;
            if ((sharedTimeCycle == null || !sharedTimeCycle.isActiveAndEnabled) && Time.unscaledTime >= nextClockLookup)
            {
                sharedTimeCycle = FindAnyObjectByType<WorldDayNightCycle>();
                nextClockLookup = Time.unscaledTime + 1;
            }
            return sharedTimeCycle != null && sharedTimeCycle.isActiveAndEnabled && sharedTimeCycle.IsNight;
        }

        private ElementalEnemy enemy;
        private readonly List<Transform> anchors = new List<Transform>();
        private readonly List<GameObject> effects = new List<GameObject>();
        private ElementType displayedElement = ElementType.Normal;
        private GemVfxStyle displayedStyle;
        private GameObject displayedPrefab;
        private float displayedSize;
        private bool displayedTrails;
        private float displayedTrailLifetime, displayedLength, displayedWidth, displayedBrightness, displayedFadeSeconds;
        private Material displayedRibbonMaterial;

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
            var element = IsNight() && enemy != null && enemy.Health != null && !enemy.Health.IsDead
                ? (enemy.IsElementBorn ? enemy.CurrentElement : enemy.element) : ElementType.Normal;
            var styles = arrowVisualsSource != null ? arrowVisualsSource.styles : null;
            var style = element == ElementType.Normal || styles == null ? null :
                Array.Find(styles, s => s != null && s.element == element);
            var prefab = style != null ? style.arrowheadAura : null;
            float size = style != null ? Mathf.Max(.01f, eyeSize) * style.size : 0;
            if (element == displayedElement && style == displayedStyle && prefab == displayedPrefab &&
                size == displayedSize && chargedParticleTrails == displayedTrails && trailLifetime == displayedTrailLifetime &&
                tailFadeSeconds == displayedFadeSeconds && tailLength == displayedLength && tailWidth == displayedWidth && brightness == displayedBrightness && ribbonMaterial == displayedRibbonMaterial) return;

            ClearEffects();
            displayedElement = element;
            displayedStyle = style;
            displayedPrefab = prefab;
            displayedSize = size;
            displayedTrails = chargedParticleTrails;
            displayedTrailLifetime = trailLifetime;
            displayedFadeSeconds = tailFadeSeconds; displayedLength = tailLength; displayedWidth = tailWidth; displayedBrightness = brightness; displayedRibbonMaterial = ribbonMaterial;
            if (prefab == null) return;
            Vector3 center = Vector3.zero;
            foreach (var marker in anchors) if (marker != null) center += marker.position;
            center /= Mathf.Max(1, anchors.Count);
            for (int index = 0; index < anchors.Count; index++)
            {
                var marker = anchors[index]; if (marker == null) continue;
                var effect = new GameObject("Eye Particle - " + element); effect.layer = gameObject.layer;
                effect.transform.SetParent(marker, false);
                var scale = marker.lossyScale;
                effect.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                var accent = Instantiate(prefab, effect.transform, false);
                accent.name = "Elemental eye motes"; accent.transform.localScale = Vector3.one * size;
                var tint = ElementalReactionVfx.Tint(element);
                GemArrowVisuals.ConfigureTipParticles(accent, tint, style.trailMaterial, false);
                foreach (var particle in accent.GetComponentsInChildren<ParticleSystem>(true))
                { var trails = particle.trails; trails.enabled = false; }
                foreach (var ribbon in accent.GetComponentsInChildren<GemVfxRibbons>(true)) ribbon.enabled = false;
                foreach (var line in accent.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
                if (chargedParticleTrails && ribbonMaterial != null)
                {
                    float secondary = anchors.Count > 2 && index >= 2 ? .45f : 1;
                    float side = Vector3.Dot(marker.position - center, transform.right) >= 0 ? 1 : -1;
                    effect.AddComponent<EnemyEyeRibbon>().Configure(transform, ribbonMaterial, tint,
                        tailLength * (secondary < 1 ? .7f : 1), tailWidth * secondary, brightness, side, tailFadeSeconds);
                }
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
