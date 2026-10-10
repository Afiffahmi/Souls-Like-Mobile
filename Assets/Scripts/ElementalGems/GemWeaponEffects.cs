using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemManager)), DefaultExecutionOrder(200)]
    public sealed class GemWeaponEffects : MonoBehaviour
    {
        public Transform sword;
        public Vector3 swordAuraPosition = new Vector3(0.12f, 0.1f, 0);
        public Vector3 swordTrailPosition = new Vector3(0.48f, 0.1f, 0);
        [Header("Bow string effect")]
        public Vector3 bowAuraPosition;
        [Range(.05f, 2f), Tooltip("Thickness and particle size of the bow string effect. 1 = original size. Independent of length. Adjustable during Play Mode; existing particles finish at their previous size.")]
        public float bowStringSize = .2f;
        [Range(.1f, 1f), Tooltip("Visible length of each string half, from the center nock toward the bow tip. 1 = full length; lower values shorten both ends while keeping the center connected. Adjustable during Play Mode.")]
        public float bowStringLength = .75f;
        [Header("Visual-only style overrides")]
        public GemVfxStyle[] styles;
        private GemManager manager;
        private PlayerBowVisuals bow;
        private PlayerSwordVisuals swordVisuals;
        private GemSwordCombat melee;
        private GemLightSlashEffects slashTiming;
        private GameObject swordEffect, bowEffect;
        private TrailRenderer slash;
        private GemDefinition gem;
        private Transform bowRoot;
        private BowStringSegment upperString, lowerString;
        private GemVfxStyle style;
        private void Awake()
        {
            manager = GetComponent<GemManager>(); bow = GetComponent<PlayerBowVisuals>();
            swordVisuals = GetComponent<PlayerSwordVisuals>(); melee = GetComponent<GemSwordCombat>();
        }
        private void OnEnable() { manager.GemChanged += Refresh; Refresh(manager.Equipped); }
        private void OnDisable() { manager.GemChanged -= Refresh; Clear(); }
        private static void Dispose(ref GameObject obj) { if (obj != null) { obj.SetActive(false); Object.Destroy(obj); obj = null; } }
        private void Clear()
        {
            Dispose(ref swordEffect); Dispose(ref bowEffect);
            upperString = lowerString = null;
            if (slash != null) { slash.emitting = false; slash.Clear(); Destroy(slash.gameObject); slash = null; }
        }
        private void Refresh(GemDefinition next)
        {
            Clear(); gem = next; bowRoot = null;
            if (gem == null || gem.element == ElementType.Normal) return;
            style = styles == null ? null : System.Array.Find(styles, s => s != null && s.element == gem.element);
            if (sword != null)
            {
                swordEffect = Aura(style != null ? style.swordAura : gem.swordAura, sword, swordAuraPosition);
                var go = new GameObject("Elemental Slash Trail"); go.transform.SetParent(sword, false); go.transform.localPosition = swordTrailPosition;
                slash = go.AddComponent<TrailRenderer>(); slash.sharedMaterial = style != null ? style.trailMaterial : gem.trailMaterial;
                slash.time = style != null ? style.trailDuration : gem.trailLifetime;
                slash.startWidth = style != null ? style.swordTrailWidth : gem.swordTrailWidth; slash.endWidth = 0;
                var color = style != null ? style.trailColor : gem.color;
                slash.startColor = color; slash.endColor = new Color(color.r,color.g,color.b,0);
                slash.numCornerVertices=3;slash.numCapVertices=2;slash.textureMode=LineTextureMode.Stretch;
                slash.emitting = false; slash.minVertexDistance = 0.02f;
                slash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; slash.receiveShadows = false;
            }
            RefreshActive();
        }
        private GameObject Aura(GameObject prefab, Transform parent, Vector3 offset)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false); go.transform.localPosition = offset;
            // Imported bow has a large FBX scale. Keep particle sizes in world metres.
            var scale=parent.lossyScale;
            go.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z))) * (style != null ? style.size : 1);
            return go;
        }
        private void LateUpdate() => RefreshActive();
        // The bow placement inspector calls this after sampling a paused bow pose.
        public void RefreshBowPlacement()
        {
            if (Application.isPlaying && isActiveAndEnabled) RefreshActive();
        }
        private void RefreshActive()
        {
            if (gem == null || gem.element == ElementType.Normal) return;
            if (bow != null && bow.BowModel != bowRoot)
            {
                Dispose(ref bowEffect); bowRoot = bow.BowModel;
                upperString = lowerString = null;
                if (bowRoot != null)
                {
                    var prefab = style != null ? style.bowAura : gem.bowAura;
                    if (!CreateStringAura(prefab))
                    {
                        var renderers=bowRoot.GetComponentsInChildren<Renderer>(true);
                        Vector3 center=Vector3.zero;
                        if(renderers.Length>0)
                        {
                            var bounds=renderers[0].bounds;
                            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                            center=bowRoot.InverseTransformPoint(bounds.center);
                        }
                        bowEffect = Aura(prefab, bowRoot, center+bowAuraPosition);
                    }
                }
            }
            bool swordOn = swordVisuals != null && swordVisuals.IsSwordDrawn;
            bool bowOn = bowRoot != null && bowRoot.parent == bow.handSocket && bow.isActiveAndEnabled;
            // PlayerBowVisuals samples the bow first; fit both halves to that same pose.
            if (upperString != null && bowRoot != null)
            {
                Vector3 offset = bowRoot.TransformVector(bowAuraPosition);
                upperString.Fit(offset, bowStringSize, bowStringLength);
                lowerString.Fit(offset, bowStringSize, bowStringLength);
            }
            SetAuraActive(swordEffect,swordOn);
            SetAuraActive(bowEffect,bowOn);
            if (slashTiming == null) slashTiming = GetComponent<GemLightSlashEffects>();
            if (slash != null)
            {
                bool configured = slashTiming != null && slashTiming.isActiveAndEnabled && slashTiming.HasConfiguredSlash;
                slash.emitting = swordOn && (configured ? slashTiming.TrailActive : melee != null && melee.TrailActive);
                if (!swordOn || (configured && !slash.emitting)) slash.Clear();
            }
        }
        private bool CreateStringAura(GameObject prefab)
        {
            if (prefab == null) return false;
            Transform upper = null, upperEnd = null, lower = null, lowerEnd = null;
            foreach (var bone in bowRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "String_Upper": upper = bone; break;
                    case "String_Upper_end": upperEnd = bone; break;
                    case "String_Lower": lower = bone; break;
                    case "String_Lower_end": lowerEnd = bone; break;
                }
            }
            // Keep the existing aura placement for replacement bows without this string rig.
            if (upper == null || upperEnd == null || lower == null || lowerEnd == null) return false;
            bowEffect = new GameObject("Elemental Bow String");
            bowEffect.transform.SetParent(bowRoot, false);
            float size = style != null ? style.size : 1f;
            upperString = new BowStringSegment(Aura(prefab, bowEffect.transform, Vector3.zero), upper, upperEnd, size);
            lowerString = new BowStringSegment(Aura(prefab, bowEffect.transform, Vector3.zero), lower, lowerEnd, size);
            return true;
        }

        private sealed class BowStringSegment
        {
            readonly Transform effect, start, end;
            readonly float widthScale, authoredLength;
            readonly LineRenderer[] lines;
            readonly float[] lineWidths;
            readonly ParticleSystem[] particleSystems;
            readonly Vector3[] particleSizes;
            float appliedSize = -1f;

            public BowStringSegment(GameObject instance, Transform start, Transform end, float widthScale)
            {
                effect = instance.transform;
                this.start = start; this.end = end; this.widthScale = widthScale;
                instance.name = start.name + " Elemental Aura";
                // Bow effects are authored vertically. Measure their original span before fitting
                // it to the animated string, so style size changes thickness, not attachment points.
                float span = 0f;
                foreach (var ribbon in instance.GetComponentsInChildren<GemVfxRibbons>(true))
                    span = Mathf.Max(span, Mathf.Abs(ribbon.axis.normalized.y) * ribbon.length);
                foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var shape = particles.shape;
                    if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box)
                        span = Mathf.Max(span, shape.scale.y);
                    // A string must move with the bow, rather than leaving its old particles behind.
                    var main = particles.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.scalingMode = ParticleSystemScalingMode.Shape;
                    // Two halves share the original emission budget.
                    var emission = particles.emission;
                    emission.rateOverTimeMultiplier *= .5f;
                    emission.rateOverDistanceMultiplier *= .5f;
                    particles.Play(false);
                }
                authoredLength = Mathf.Max(.001f, span > 0f ? span : 1.05f);
                // Keep authored values so adjusting the slider never compounds the scale.
                lines = instance.GetComponentsInChildren<LineRenderer>(true);
                lineWidths = new float[lines.Length];
                for (int i = 0; i < lines.Length; i++) lineWidths[i] = lines[i].widthMultiplier;
                particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                particleSizes = new Vector3[particleSystems.Length];
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    var main = particleSystems[i].main;
                    particleSizes[i] = main.startSize3D
                        ? new Vector3(main.startSizeXMultiplier, main.startSizeYMultiplier, main.startSizeZMultiplier)
                        : Vector3.one * main.startSizeMultiplier;
                }
            }

            public void Fit(Vector3 offset, float size, float lengthScale)
            {
                if (effect == null || start == null || end == null) return;
                size = Mathf.Clamp(size, .05f, 2f);
                lengthScale = Mathf.Clamp(lengthScale, .1f, 1f);
                if (!Mathf.Approximately(appliedSize, size))
                {
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i] != null) lines[i].widthMultiplier = lineWidths[i] * size;
                    for (int i = 0; i < particleSystems.Length; i++)
                    {
                        if (particleSystems[i] == null) continue;
                        var main = particleSystems[i].main;
                        if (main.startSize3D)
                        {
                            main.startSizeXMultiplier = particleSizes[i].x * size;
                            main.startSizeYMultiplier = particleSizes[i].y * size;
                            main.startSizeZMultiplier = particleSizes[i].z * size;
                        }
                        else main.startSizeMultiplier = particleSizes[i].x * size;
                    }
                    appliedSize = size;
                }
                Vector3 direction = end.position - start.position;
                float length = direction.magnitude;
                if (length < .0001f) return;
                // Both halves start at the nock. Shorten toward it without opening a center gap.
                effect.position = start.position + direction * (.5f * lengthScale) + offset;
                effect.localRotation = Quaternion.FromToRotation(Vector3.up,
                    effect.parent.InverseTransformVector(direction).normalized);
                // Measure each rotated local axis through the parent matrix. This also handles
                // an imported bow or player with nonuniform scale without stretching the string.
                Matrix4x4 parent = effect.parent.localToWorldMatrix;
                Quaternion rotation = effect.localRotation;
                Vector3 scale = new Vector3(
                    parent.MultiplyVector(rotation * Vector3.right).magnitude,
                    parent.MultiplyVector(rotation * Vector3.up).magnitude,
                    parent.MultiplyVector(rotation * Vector3.forward).magnitude);
                effect.localScale = new Vector3(widthScale * size / Mathf.Max(.001f, scale.x),
                    length * lengthScale / authoredLength / Mathf.Max(.001f, scale.y),
                    widthScale * size / Mathf.Max(.001f, scale.z));
            }
        }
        private static void SetAuraActive(GameObject effect,bool active)
        {
            if(effect==null||effect.activeSelf==active)return;
            if(!active)foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>())ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.SetActive(active);
            if(active)foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>())ps.Play(false);
        }
    }
}
