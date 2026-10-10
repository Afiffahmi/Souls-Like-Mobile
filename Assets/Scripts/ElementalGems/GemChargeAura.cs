using UnityEngine;

namespace ElementalGems
{
    /// <summary>Reusable, visual-only inward charge and outward release. All sizes are in metres.</summary>
    public sealed class GemChargeAura : MonoBehaviour
    {
        public ParticleSystem inward, outward, contractingRings, releaseRings;
        public ParticleSystem milestoneRings, stageMarkers;
        public const float SecondsPerStage = 2f;
        public const int MaximumStages = 3;
        public int CompletedStages { get; private set; }
        [Min(.1f)] public float baseRadius = 1.25f;
        [Min(0)] public float radiusPerStage = .5f;
        [Range(8,120)] public int particlesPerSecond = 46;
        public bool IsCharging { get; private set; }
        public int Stage { get; private set; } = 1;
        public float Progress { get; private set; }
        public float Radius => baseRadius + radiusPerStage * (Stage - 1 + Progress);
        public Color Tint { get; private set; } = Color.white;
        float emissionDebt;
        readonly ParticleSystem.Particle[] markers = new ParticleSystem.Particle[MaximumStages];

        public static int ReachedStage(float seconds) => Mathf.Clamp(Mathf.FloorToInt(seconds / SecondsPerStage), 0, MaximumStages);
        public static void TimedSize(float seconds, out int stage, out float progress)
        {
            float level = Mathf.Clamp(seconds / SecondsPerStage, 0, MaximumStages);
            stage = Mathf.Min(MaximumStages, 1 + Mathf.FloorToInt(level));
            progress = level >= MaximumStages ? 1 : level - Mathf.Floor(level);
        }
        public void SetTimedCharge(Color color, float seconds)
        {
            TimedSize(seconds, out int stage, out float progress);
            SetCharge(color, stage, progress, true);
            int reached = ReachedStage(seconds);
            while (CompletedStages < reached) ShowMilestone(++CompletedStages);
            UpdateMarkers();
        }
        public void SetSingleCharge(Color color, float progress)
        {
            SetCharge(color, 1, progress, true);
            if (progress >= 1 && CompletedStages == 0) ShowMilestone(++CompletedStages);
            UpdateMarkers(1);
        }
        public void ReleaseTimed(Color color, float seconds)
        {
            TimedSize(seconds, out int stage, out float progress);
            Release(color, stage, progress);
        }
        void ShowMilestone(int completed)
        {
            if (milestoneRings == null) return;
            milestoneRings.Play(false);
            milestoneRings.Emit(new ParticleSystem.EmitParams {
                position = Vector3.zero, velocity = Vector3.zero,
                startSize = (baseRadius + radiusPerStage * completed) * 2.6f,
                startLifetime = .55f, startColor = Color.Lerp(Tint, Color.white, .3f)
            }, 1);
        }
        void UpdateMarkers(int count = MaximumStages)
        {
            if (stageMarkers == null) return;
            var camera = Camera.main;
            Vector3 right = camera != null ? transform.InverseTransformDirection(camera.transform.right) : Vector3.right;
            for (int i = 0; i < count; i++)
            {
                bool ready = i < CompletedStages;
                Color color = ready ? Color.Lerp(Tint, Color.white, .3f) : new Color(.3f,.35f,.42f,.45f);
                markers[i] = new ParticleSystem.Particle {
                    position = Vector3.up * 1.25f + right * ((i - (count - 1) * .5f) * .24f), velocity = Vector3.zero,
                    startSize = ready ? .19f : .1f, startColor = color, startLifetime = 1, remainingLifetime = 1
                };
            }
            stageMarkers.Play(false); stageMarkers.SetParticles(markers, count);
        }

        public void SetCharge(Color color, int stage, float progress, bool timed = false)
        {
            if (!IsCharging) { emissionDebt = 1; CompletedStages = 0; }
            IsCharging = true;
            Tint = color; Tint = new Color(Tint.r, Tint.g, Tint.b, 1);
            Stage = Mathf.Clamp(stage, 1, 8); Progress = Mathf.Clamp01(progress);
            if (!timed)
            {
                CompletedStages = 0;
                if (stageMarkers != null) stageMarkers.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsCharging || inward == null) return;
            float dt = Mathf.Clamp(deltaTime, 0, .1f);
            emissionDebt += dt * particlesPerSecond * (1 + .15f * (Stage - 1) + .3f * Progress);
            int count = Mathf.Min(24, Mathf.FloorToInt(emissionDebt)); emissionDebt -= count;
            if (!inward.isPlaying) inward.Play(false);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y *= .7f;
                Vector3 start = direction * Radius * Random.Range(.85f, 1.1f);
                float life = Random.Range(.35f, .6f);
                inward.Emit(new ParticleSystem.EmitParams {
                    position = start, velocity = -start * .93f / life,
                    startLifetime = life, startSize = Random.Range(.035f, .07f) * (1 + .12f * (Stage - 1)),
                    startColor = Color.Lerp(Tint, Color.white, Random.Range(0f,.25f))
                }, 1);
            }
        }

        public void Release(Color color, int stage, float progress = 1f)
        {
            SetCharge(color, stage, progress);
            float radius = Radius;
            StopCharge();
            if (outward != null)
            {
                outward.Play(false);
                int count = Mathf.Min(110, 48 + Stage * 14);
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = Random.onUnitSphere; direction.y *= .7f;
                    outward.Emit(new ParticleSystem.EmitParams {
                        position = direction * .18f,
                        velocity = direction * radius * Random.Range(3.2f, 5.2f),
                        startSize = Random.Range(.04f,.085f) * (1 + .1f * (Stage - 1)),
                        startLifetime = Random.Range(.28f,.52f),
                        startColor = Color.Lerp(Tint, Color.white, Random.Range(.05f,.3f))
                    }, 1);
                }
            }
            if (releaseRings != null)
            {
                releaseRings.Play(false);
                Color ringColor = Tint; ringColor.a = .65f;
                releaseRings.Emit(new ParticleSystem.EmitParams {
                    position = Vector3.zero, velocity = Vector3.zero, startSize = radius * 4,
                    startLifetime = .48f, startColor = ringColor
                }, 1);
            }
        }

        public void StopCharge()
        {
            IsCharging = false; emissionDebt = 0;
            if (inward != null) inward.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (contractingRings != null) contractingRings.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (stageMarkers != null) stageMarkers.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (milestoneRings != null) milestoneRings.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            CompletedStages = 0;
        }

        public void Cancel()
        {
            StopCharge();
            if (outward != null) outward.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (releaseRings != null) releaseRings.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (milestoneRings != null) milestoneRings.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() => Cancel();
    }
}
