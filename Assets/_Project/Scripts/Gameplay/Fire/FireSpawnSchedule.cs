using UnityEngine;

namespace Sastermate.Gameplay.Fire
{
    internal static class FireSpawnSchedule
    {
        public static float NormalProgress(float elapsed, float warmupDuration, float rampDuration, bool enabled)
        {
            if (!enabled) return 1f;
            if (elapsed <= Mathf.Max(0f, warmupDuration)) return 0f;
            return rampDuration <= 0f ? 1f : Mathf.Clamp01((elapsed - Mathf.Max(0f, warmupDuration)) / rampDuration);
        }

        public static int FireLimit(int warmupLimit, int normalLimit, float progress)
        {
            normalLimit = Mathf.Clamp(normalLimit, 1, 10);
            warmupLimit = Mathf.Clamp(warmupLimit, 1, normalLimit);
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(warmupLimit, normalLimit, progress)), 1, normalLimit);
        }
    }
}
