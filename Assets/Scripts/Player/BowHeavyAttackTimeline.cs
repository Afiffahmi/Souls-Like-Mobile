using System;

/// <summary>Single-use input cycle. Completion never rearms a still-held button.</summary>
public sealed class BowHeavyHoldCycle
{
    public bool Held { get; private set; }
    public bool Decided { get; private set; }
    private double started;
    public bool Begin(double now)
    {
        if (Held) return false;
        Held = true;
        Decided = false;
        started = now;
        return true;
    }
    // null means no attack, false means single, true means charged.
    public bool? Advance(double now, bool held, double duration)
    {
        if (!Held) return null;
        Held = held;
        if (Decided) return null;
        bool charged = now - started >= duration;
        if (held && !charged) return null;
        Decided = true;
        return charged;
    }
    public void Cancel() { Held = false; Decided = true; }
}

/// <summary>
/// Shares the character's sampled frame clock. A release frame is presented for
/// one update before progressing, even during a hitch; time debt is carried over.
/// This prevents skipped poses and never merges several releases into one volley.
/// </summary>
public sealed class BowHeavyAttackTimeline
{
    public float Frame { get; private set; }
    public int Shots { get; private set; }
    public bool Finished { get; private set; }
    private int[] frames;
    private float lastFrame;
    private double target;

    public void Begin(int[] releaseFrames, float endFrame)
    {
        frames = (int[])releaseFrames.Clone();
        lastFrame = endFrame;
        Frame = 0f;
        target = 0;
        Shots = 0;
        Finished = false;
    }

    public bool Advance(float deltaFrames)
    {
        if (frames == null || Finished) return false;
        target = Math.Min(lastFrame, target + Math.Max(0f, deltaFrames));
        if (Shots < frames.Length && target + 0.00001 >= frames[Shots])
        {
            Frame = frames[Shots++];
            return true;
        }
        Frame = (float)target;
        Finished = Shots == frames.Length && Frame >= lastFrame;
        return false;
    }
    public void Reset() { frames = null; Frame = 0; Shots = 0; Finished = false; target = 0; }
}
