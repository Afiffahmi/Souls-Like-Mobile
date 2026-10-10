using System.Collections.Generic;
using UnityEngine;

// Per-player, per-configuration deadlines; independent of Animator progress.
// Mode changes and rejected presses never reset these deadlines.
public sealed class CombatSpecialCooldowns
{
    private readonly Dictionary<CombatAttackConfiguration, double> readyAt = new Dictionary<CombatAttackConfiguration, double>();

    public float Remaining(CombatAttackConfiguration configuration, double now) =>
        configuration != null && readyAt.TryGetValue(configuration, out double deadline)
            ? (float)System.Math.Max(0, deadline - now) : 0f;

    public bool TryStart(CombatAttackConfiguration configuration, double now, float? defaultCooldown = null)
    {
        if (configuration == null || configuration.specialAttack == null || Remaining(configuration, now) > 0f) return false;
        readyAt[configuration] = now + Mathf.Max(0f, defaultCooldown ?? configuration.specialAttack.cooldown);
        return true;
    }
}
