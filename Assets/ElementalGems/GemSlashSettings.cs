using System;
using UnityEngine;

namespace ElementalGems
{
    [Serializable]
    public sealed class GemSlashSettings
    {
        public bool enabled;
        [Tooltip("Frame numbers in Unity's Animation window (zero-based), using this step's clip frame rate. Visible from start up to end; hidden at end.")]
        [Min(0)] public float startFrame = 11;
        [Min(0)] public float endFrame = 16;
        [Tooltip("Player-local offset in metres: X right, Y up, Z forward.")]
        public Vector3 localOffset = new Vector3(0, 1, .05f);
        [Tooltip("Rotate the slash plane relative to player facing. Z: 0 left-to-right, -90 top-to-bottom, +45 bottom-left to top-right. Reverse Sweep swaps the endpoints.")]
        public Vector3 localEulerAngles = new Vector3(0, 0, -90);
        public bool reverseSweep;
        [Tooltip("Additional rotation about the slash plane's local Y axis, from start to end. Use values outside +/-180 for larger arcs.")]
        public float startSweepAngle;
        public float endSweepAngle;
        [Tooltip("Sweep and reveal progression. Keep endpoints at (0,0) and (1,1).")]
        public AnimationCurve progression = AnimationCurve.Linear(0, 0, 1, 1);
        [Min(.01f)] public float size = 1;
        [Min(0)] public float forwardDrift;
        [Tooltip("Follow player position/facing during the swing; otherwise stay at the spawn pose.")]
        public bool followPlayer;

        public bool IsValid(AnimationClip clip) => clip != null && clip.frameRate > 0 &&
            Finite(startFrame) && Finite(endFrame) && startFrame >= 0 && endFrame > startFrame &&
            endFrame <= clip.length * clip.frameRate + .01f && Finite(size) && size > 0 &&
            Finite(startSweepAngle) && Finite(endSweepAngle) && Finite(forwardDrift) && forwardDrift >= 0 &&
            Finite(localOffset.x) && Finite(localOffset.y) && Finite(localOffset.z) &&
            Finite(localEulerAngles.x) && Finite(localEulerAngles.y) && Finite(localEulerAngles.z);
        public float FrameAt(AnimationClip clip, float normalizedTime) => normalizedTime * clip.length * clip.frameRate;
        public bool ContainsFrame(float frame) => frame >= startFrame && frame < endFrame;
        public float ProgressAt(float frame) => Mathf.Clamp01((frame - startFrame) / Mathf.Max(.001f, endFrame - startFrame));
        public float Evaluate(float progress) => Mathf.Clamp01(progression == null ? progress : progression.Evaluate(progress));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
