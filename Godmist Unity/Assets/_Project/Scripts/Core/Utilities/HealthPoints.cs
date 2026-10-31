using UnityEngine;

namespace Godmist.Core.Utilities
{
    public static class HealthPoints
    {
        // Match the existing HP pool: nearest integer, exact halves to even.
        // A valid character's maximum HP is always at least one.
        public static int Maximum(float effectiveMaximum) => Mathf.Max(1, Mathf.RoundToInt(effectiveMaximum));
    }
}
