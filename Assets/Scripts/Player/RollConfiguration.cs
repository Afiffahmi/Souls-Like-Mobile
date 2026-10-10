using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class RollDirectionalAnimations
{
    public AnimationClip forward, forwardRight, right, backwardRight, backward, backwardLeft, left, forwardLeft;
    public AnimationClip[] Clips => new[] { forward, forwardRight, right, backwardRight, backward, backwardLeft, left, forwardLeft };
    public static readonly string[] Names = { "Forward", "ForwardRight", "Right", "BackwardRight", "Backward", "BackwardLeft", "Left", "ForwardLeft" };
    public static readonly Vector2[] Positions = { new Vector2(0,1), new Vector2(1,1), new Vector2(1,0), new Vector2(1,-1), new Vector2(0,-1), new Vector2(-1,-1), new Vector2(-1,0), new Vector2(-1,1) };
    public bool HasAny
    {
        get { foreach (var clip in Clips) if (clip != null) return true; return false; }
    }
    public bool IsValid
    {
        get { foreach (var clip in Clips) if (!RollConfiguration.ValidClip(clip)) return false; return true; }
    }
}

[Serializable]
public sealed class RollDefinition
{
    public PlayerCombatMode mode;
    [HideInInspector] public AnimationClip animation; // Retained for existing assets and migration.
    public RollDirectionalAnimations directionalAnimations = new RollDirectionalAnimations();
    public bool UsesDirectionalAnimations => directionalAnimations != null && directionalAnimations.HasAny;
    [Min(0f)] public float distance = 3f;
    [Min(0.05f)] public float duration = 0.65f;
    [Tooltip("Animation timeline end frame. Used to scale movement timing with Duration, not game frame rate.")]
    [Min(1)] public int animationFrames = 53;
    [Tooltip("Horizontal travel stops at this animation frame. The full Distance is covered before this point; the animation continues.")]
    [Min(1)] public int movementEndFrame = 32;
    [Tooltip("Delay after the roll finishes before another roll can start.")]
    [Min(0f)] public float recovery = 0.2f;
    public bool requireGrounded = true;
}

[CreateAssetMenu(menuName = "Combat/Roll Configuration", fileName = "RollConfiguration")]
public sealed class RollConfiguration : ScriptableObject
{
    public RuntimeAnimatorController animatorController;
    public List<RollDefinition> rolls = new List<RollDefinition>();
    public RollDefinition ForMode(PlayerCombatMode mode) => rolls?.Find(r => r != null && r.mode == mode);
    public static string StatePath(PlayerCombatMode mode) => mode == PlayerCombatMode.Normal
        ? "Base Layer.Normal.Normal_Roll" : "Base Layer.Attack." + mode + "_Roll";
    public static string LocomotionPath(PlayerCombatMode mode) => mode == PlayerCombatMode.Normal
        ? "Base Layer.Normal.Locomotion" : "Base Layer.Attack." + mode;
    public static bool ValidClip(AnimationClip clip) => clip != null && clip.length > 0f && !clip.isLooping;
    public static bool Valid(RollDefinition r) => r != null &&
        (r.UsesDirectionalAnimations ? r.directionalAnimations.IsValid : ValidClip(r.animation)) && Finite(r.distance) && r.distance >= 0f &&
        Finite(r.duration) && r.duration >= 0.05f && Finite(r.recovery) && r.recovery >= 0f &&
        r.animationFrames >= 1 && r.movementEndFrame >= 1 && r.movementEndFrame <= r.animationFrames;
    public bool Validate(out string error)
    {
        error = null;
        if (animatorController == null || rolls == null || rolls.Count != 4)
            error = "Assign a controller and one roll for each of Normal, Sword, Magic, and Bow.";
        else
        {
            var modes = new HashSet<PlayerCombatMode>();
            foreach (var r in rolls)
                if (!Valid(r) || !Enum.IsDefined(typeof(PlayerCombatMode), r.mode) || !modes.Add(r.mode))
                { error = "Each mode needs eight non-looping directional clips, nonnegative distance/recovery, duration >= 0.05 seconds, and 1 <= Movement End Frame <= Animation Frames."; break; }
        }
        return error == null;
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
