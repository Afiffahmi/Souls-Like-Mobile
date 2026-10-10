using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    /// <summary>Independent decaying impulses. Duration changes neither initial push speed nor other hits' timers.</summary>
    public sealed class KnockbackMotion
    {
        sealed class Impulse { public Vector3 velocity; public float duration, remaining; }
        readonly List<Impulse> impulses = new List<Impulse>();
        public Vector3 Velocity { get; private set; }
        public float Remaining { get; private set; }
        public void Add(Vector3 direction, float effectiveStrength, float durationMultiplier = 1)
        {
            float strength = Mathf.Max(0, WeaponStatModifier.Finite(effectiveStrength));
            float multiplier = Mathf.Clamp(WeaponStatModifier.Finite(durationMultiplier, 1), 0, 4);
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (strength <= 0 || multiplier <= 0 || direction.sqrMagnitude < .000001f) return;
            float duration = strength / 12f * multiplier;
            impulses.Add(new Impulse { velocity = direction.normalized * strength, duration = duration, remaining = duration });
            Refresh();
        }
        public Vector3 Advance(float seconds)
        {
            float dt = Mathf.Max(0, WeaponStatModifier.Finite(seconds));
            Vector3 displacement = Vector3.zero;
            for (int i = impulses.Count - 1; i >= 0; i--)
            {
                var p = impulses[i];
                float elapsed = Mathf.Min(dt, p.remaining);
                float after = Mathf.Max(0, p.remaining - elapsed);
                // Integrate the linear decay exactly, including hitches across its endpoint.
                displacement += p.velocity * ((p.remaining + after) * .5f / p.duration) * elapsed;
                p.remaining = after;
                if (p.remaining <= .000001f) impulses.RemoveAt(i);
            }
            Refresh();
            return displacement;
        }
        void Refresh()
        {
            Velocity = Vector3.zero; Remaining = 0;
            foreach (var p in impulses)
            { Velocity += p.velocity * (p.remaining / p.duration); Remaining = Mathf.Max(Remaining, p.remaining); }
        }
        public void Clear() { impulses.Clear(); Refresh(); }
    }
}
