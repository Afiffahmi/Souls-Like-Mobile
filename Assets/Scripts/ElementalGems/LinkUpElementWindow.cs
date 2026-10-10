namespace ElementalGems
{
    /// <summary>A locked Normal state with a fixed deadline in scaled game time.</summary>
    public sealed class LinkUpElementWindow
    {
        public const double Duration = 10;
        private bool active;
        private double expiresAt;

        public bool IsActive(double now) => active && now < expiresAt;
        public double Remaining(double now) => IsActive(now) ? expiresAt - now : 0;
        public ElementType Resolve(ElementType innate, double now) => IsActive(now) ? ElementType.Normal : innate;

        public bool TryBegin(ElementType innate, ElementType incoming, double now)
        {
            if (innate == ElementType.Normal || incoming == ElementType.Normal || incoming == innate || IsActive(now))
                return false;
            active = true;
            expiresAt = now + Duration;
            return true;
        }

        public bool Expire(double now)
        {
            if (!active || now < expiresAt) return false;
            Clear();
            return true;
        }

        public void Clear() { active = false; expiresAt = 0; }
    }
}
