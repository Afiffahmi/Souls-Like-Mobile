using System;
using UnityEngine;

namespace SoulsLike.Riding
{
    public enum HorseMotion { Idle, Walk, WalkLeft, WalkRight, Run, SpecialAttack, Death1, Death2, Mount, Dismount, Ejected }

    [Serializable]
    public sealed class MountedAnimation
    {
        public HorseMotion motion;
        public string horseState;
        public string playerState;
        public AnimationClip horseClip;
        public AnimationClip playerClip;
        // Original motions in the base controllers; retained when the Inspector
        // clip fields are replaced so overrides do not depend on placeholder names.
        [HideInInspector] public AnimationClip horseControllerClip;
        [HideInInspector] public AnimationClip playerControllerClip;
        [Tooltip("Shared cycle duration. Zero uses the longer of the two clips.")]
        [Min(0)] public float duration;
        public float Seconds => Mathf.Max(.05f, duration > 0 ? duration : Mathf.Max(horseClip ? horseClip.length : 0, playerClip ? playerClip.length : 0));
    }

    [CreateAssetMenu(menuName = "Riding/Horse Animation Set")]
    public sealed class HorseAnimationSet : ScriptableObject
    {
        public RuntimeAnimatorController horseController;
        public RuntimeAnimatorController playerController;
        public string timeParameter = "RideTime";
        [Min(0)] public float transitionSeconds = .1f;
        public MountedAnimation[] animations = Array.Empty<MountedAnimation>();
        public MountedAnimation Get(HorseMotion motion) => Array.Find(animations, a => a.motion == motion);
        public float Duration(HorseMotion motion) => Get(motion)?.Seconds ?? 1f;
        public static bool Loops(HorseMotion motion) => motion <= HorseMotion.Run;

        public AnimatorOverrideController CreateController(bool player)
        {
            var source = player ? playerController : horseController;
            if (!source) return null;
            var result = new AnimatorOverrideController(source);
            var clips = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            result.GetOverrides(clips);
            foreach (var entry in animations)
            {
                var controllerClip = player ? entry.playerControllerClip : entry.horseControllerClip;
                string key = (player ? "Player_" : "Horse_") + entry.motion + "_Placeholder";
                for (int i = 0; i < clips.Count; i++)
                    if ((controllerClip ? clips[i].Key == controllerClip : clips[i].Key.name == key) && (player ? entry.playerClip : entry.horseClip))
                        clips[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(clips[i].Key, player ? entry.playerClip : entry.horseClip);
            }
            result.ApplyOverrides(clips);
            return result;
        }
    }
}
