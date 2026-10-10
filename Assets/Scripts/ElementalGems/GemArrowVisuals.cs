using UnityEngine;

namespace ElementalGems
{
    /// <summary>Tip effects use the gem captured at release and follow lodged charged arrows.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class GemArrowVisuals : MonoBehaviour
    {
        public GemVfxStyle[] styles;
        private BowArrowProjectile arrow;
        private GemArrowPayload payload;
        private GameObject aura;
        private ParticleSystem[] particles;
        private bool initialized, stopped, attachedParticles;

        private void Awake() => arrow = GetComponent<BowArrowProjectile>();

        private void LateUpdate()
        {
            if (arrow == null) return;
            if (!initialized)
            {
                payload = GetComponent<GemArrowPayload>();
                if (payload == null || payload.Attack == null) return;
                initialized = true;
                var style = styles == null ? null : System.Array.Find(styles,
                    s => s != null && s.element == payload.Element);
                // A close-range arrow can hit before its first LateUpdate.
                if (payload.Element == ElementType.Normal ||
                    (!arrow.IsFlying && !payload.KeepTipEffectsOnImpact)) return;
                if (style != null && style.arrowheadAura != null)
                {
                    aura = Instantiate(style.arrowheadAura, transform, false);
                    aura.transform.localPosition = Vector3.forward * arrow.tipOffset;
                    var scale = transform.lossyScale;
                    aura.transform.localScale = new Vector3(
                        1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                        1 / Mathf.Max(.001f, Mathf.Abs(scale.y)),
                        1 / Mathf.Max(.001f, Mathf.Abs(scale.z))) * style.size;
                    particles = aura.GetComponentsInChildren<ParticleSystem>(true);
                    ConfigureTipParticles(aura, payload.Attack.color, payload.Attack.trailMaterial, payload.IsChargedLight);
                }
            }

            if (!arrow.IsFlying && payload.KeepTipEffectsOnImpact && !attachedParticles)
            {
                attachedParticles = true;
                // Flight particles leave a wake in world space. After impact, new particles
                // move with the embedded tip instead of being left behind by knockback.
                if (particles != null)
                {
                    foreach (var particle in particles)
                    {
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main;
                        main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        particle.Play(false);
                    }
                }
            }
            if (!stopped && !arrow.IsFlying && !payload.KeepTipEffectsOnImpact)
            {
                stopped = true;
                if (aura != null) { aura.SetActive(false); Destroy(aura); }
            }
        }


        /// <summary>Shared arrowhead tint and charged trails, also used by enemy eye effects.</summary>
        public static void ConfigureTipParticles(GameObject aura, Color color, Material trailMaterial, bool charged)
        {
            var particles = aura.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles)
            {
                var main = particle.main;
                Color tint = color;
                tint.a *= main.startColor.color.a;
                main.startColor = tint;
                if (charged && trailMaterial != null)
                {
                    // Individual tip particles carry small tails as well as the main wake.
                    var tails = particle.trails;
                    tails.enabled = true;
                    tails.mode = ParticleSystemTrailMode.PerParticle;
                    tails.ratio = 0.7f;
                    tails.lifetime = new ParticleSystem.MinMaxCurve(0.45f);
                    tails.minVertexDistance = 0.025f;
                    tails.sizeAffectsWidth = true;
                    tails.sizeAffectsLifetime = false;
                    tails.inheritParticleColor = true;
                    tails.dieWithParticles = true;
                    // Follow the particle simulation space, including the local tip after impact.
                    tails.worldSpace = false;
                    tails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.65f,
                        AnimationCurve.Linear(0f, 1f, 1f, 0f));
                    var fade = new Gradient();
                    fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
                    tails.colorOverTrail = new ParticleSystem.MinMaxGradient(fade);
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null) renderer.trailMaterial = trailMaterial;
                }
            }
            foreach (var ribbon in aura.GetComponentsInChildren<GemVfxRibbons>(true))
            {
                Color tint = color;
                tint.a *= ribbon.color.a;
                ribbon.color = tint;
            }
        }
        private void OnDisable() { if (aura != null) aura.SetActive(false); }
    }
}
