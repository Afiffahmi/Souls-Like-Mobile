using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Bow Heavy Attack Configuration")]
public sealed class BowHeavyAttackConfiguration : ScriptableObject
{
    [Min(0.01f)] public float holdDuration = 3f;
    public AnimationClip singleAnimation;
    public AnimationClip chargedAnimation;
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Min(0)] public int singleReleaseFrame = 22;
    public int[] chargedReleaseFrames = { 25, 30, 35 };

    public bool Validate()
    {
        if (float.IsNaN(holdDuration) || float.IsInfinity(holdDuration) || holdDuration <= 0f ||
            float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed) || playbackSpeed <= 0f ||
            !ValidClip(singleAnimation) || !ValidClip(chargedAnimation) || singleReleaseFrame < 0 ||
            singleReleaseFrame >= singleAnimation.length * singleAnimation.frameRate ||
            chargedReleaseFrames == null || chargedReleaseFrames.Length != 3) return false;
        int previous = -1;
        foreach (int frame in chargedReleaseFrames)
        {
            if (frame <= previous || frame >= chargedAnimation.length * chargedAnimation.frameRate) return false;
            previous = frame;
        }
        return true;
    }

    private static bool ValidClip(AnimationClip clip) => clip != null && !clip.isLooping &&
        clip.length > 0f && clip.frameRate > 0f;
}
