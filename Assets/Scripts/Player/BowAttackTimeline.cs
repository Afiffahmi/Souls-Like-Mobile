using UnityEngine;

public enum BowAttackPhase { None, Draw, Hold, Release, Finished }

// Character frames are the clock for both the character and the bow model.
public sealed class BowAttackTimeline
{
    public const float DrawFrame = 11f;
    public const float HoldFrame = 34f;
    public const float ReleaseFrame = 35f;
    public BowAttackPhase Phase { get; private set; }
    public float Frame { get; private set; }
    private bool releaseRequested;

    public void Begin()
    {
        Frame = DrawFrame;
        Phase = BowAttackPhase.Draw;
        releaseRequested = false;
    }

    public void RequestRelease() => releaseRequested = true;

    public void Advance(float frames, float lastFrame, float drawSpeed = 1f)
    {
        frames = Mathf.Max(0f, frames);
        if (Phase == BowAttackPhase.Draw)
        {
            drawSpeed = Mathf.Max(0.01f, drawSpeed);
            float remaining = HoldFrame - Frame;
            Frame += Mathf.Min(frames * drawSpeed, remaining);
            // Carry remaining time into hold/release at their normal speed.
            frames = Mathf.Max(0f, frames - remaining / drawSpeed);
            if (Frame < HoldFrame) return;
            Phase = BowAttackPhase.Hold;
        }
        if (Phase == BowAttackPhase.Hold)
        {
            if (!releaseRequested)
            {
                Frame = HoldFrame + Mathf.Repeat(Frame - HoldFrame + frames, ReleaseFrame - HoldFrame);
                return;
            }
            Frame = ReleaseFrame;
            Phase = BowAttackPhase.Release;
        }
        if (Phase == BowAttackPhase.Release)
        {
            Frame = Mathf.Min(Frame + frames, lastFrame);
            if (Frame >= lastFrame) Phase = BowAttackPhase.Finished;
        }
    }

    public void Reset()
    {
        Frame = DrawFrame;
        Phase = BowAttackPhase.None;
        releaseRequested = false;
    }
}
