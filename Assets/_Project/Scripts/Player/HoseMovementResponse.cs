using UnityEngine;

namespace Sastermate.Player
{
    /// <summary>Combines intended movement with a bounded, horizontal hose reaction.</summary>
    internal sealed class HoseMovementResponse
    {
        public Vector3 RecoilVelocity { get; private set; }

        public Vector3 Apply(Vector3 movementVelocity, Vector3 launchVelocity, bool spraying,
            float movementMultiplier, float acceleration, float maximumSpeed, float recovery, float deltaTime)
        {
            var dt = Mathf.Max(0f, deltaTime);
            if (spraying)
            {
                // Project the actual jet direction onto the ground. Nearly vertical jets have
                // little horizontal reaction, and no shot can lift or push the player downward.
                var reaction = -launchVelocity.normalized;
                reaction.y = 0f;
                RecoilVelocity = Vector3.ClampMagnitude(
                    RecoilVelocity + reaction * (Mathf.Max(0f, acceleration) * dt),
                    Mathf.Max(0f, maximumSpeed));
            }
            else
            {
                RecoilVelocity = Vector3.MoveTowards(RecoilVelocity, Vector3.zero,
                    Mathf.Max(0f, recovery) * dt);
            }

            var multiplier = spraying ? Mathf.Clamp01(movementMultiplier) : 1f;
            return new Vector3(
                movementVelocity.x * multiplier + RecoilVelocity.x,
                movementVelocity.y,
                movementVelocity.z * multiplier + RecoilVelocity.z);
        }

        public void Reset() => RecoilVelocity = Vector3.zero;
    }
}
