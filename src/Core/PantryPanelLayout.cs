using System;

namespace Baiye.CryogenicPantry.Core
{
    // Canvas-space policy. The caller supplies the actual space remaining below
    // this panel, after visible sibling panels and the screen gutter are removed.
    public static class PantryPanelLayout
    {
        public const float NavigationHeight = 34;
        public const float Gap = 6;
        public const float ChromeHeight = NavigationHeight + Gap;
        public const float MaxHeight = 360;

        public static float Height(float available, float contentPreferred)
        {
            if (float.IsNaN(available) || available <= 0) return 0;
            if (float.IsNaN(contentPreferred) || contentPreferred < 0) contentPreferred = 0;
            float wanted = Math.Max(ChromeHeight, contentPreferred + ChromeHeight);
            return Math.Min(available, Math.Min(MaxHeight, wanted));
        }
    }
}
