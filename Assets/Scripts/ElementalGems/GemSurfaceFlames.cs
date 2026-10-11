using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalGems
{
    /// <summary>Bounded mesh-surface wisps. Uses the animated body/equipment directly; no per-frame mesh baking.</summary>
    public sealed class GemSurfaceFlames : MonoBehaviour
    {
        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private MaterialPropertyBlock tint;
        public static GemSurfaceFlames Create(Renderer surface, float rate, float size, float lifetime)
        {
            var material = Resources.Load<Material>("ElementalAuraFlame");
            if (surface == null || material == null) return null;
            var go = new GameObject("Elemental Surface Wisps");
            go.layer = surface.gameObject.layer; go.transform.SetParent(surface.transform, false);
            var result = go.AddComponent<GemSurfaceFlames>();
            result.particles = go.AddComponent<ParticleSystem>();
            var ps = result.particles;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * lifetime * 2), 12, 100);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.015f, .08f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startColor = Color.white;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.enabled = true;
            if (surface is SkinnedMeshRenderer skin)
            { shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer; shape.skinnedMeshRenderer = skin; }
            else if (surface is MeshRenderer mesh)
            { shape.shapeType = ParticleSystemShapeType.MeshRenderer; shape.meshRenderer = mesh; }
            else { Destroy(go); return null; }
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.08f, .08f);
            velocity.y = new ParticleSystem.MinMaxCurve(.35f, .75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.08f, .08f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .08f; noise.frequency = 1.6f;
            noise.scrollSpeed = .45f; noise.quality = ParticleSystemNoiseQuality.Low;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.7f, .15f), new GradientAlphaKey(.45f, .5f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .25f), new Keyframe(.3f, 1), new Keyframe(1, .1f)));
            result.particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            result.particleRenderer.sharedMaterial = material;
            result.particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            result.particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            result.particleRenderer.receiveShadows = false;
            result.tint = new MaterialPropertyBlock();
            return result;
        }
        public void SetTint(Color color, float intensity)
        {
            if (particleRenderer == null) return;
            tint.SetColor("_Tint", color); tint.SetFloat("_Intensity", intensity);
            particleRenderer.SetPropertyBlock(tint);
        }
        public void SetActive(bool active)
        {
            if (particles == null) return;
            if (active) { if (!particles.isPlaying) particles.Play(); }
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
