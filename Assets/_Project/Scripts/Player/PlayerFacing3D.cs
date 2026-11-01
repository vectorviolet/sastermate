using UnityEngine;

namespace Sastermate.Player
{
    /// <summary>Faces the water stream while spraying, otherwise planar movement; leaves the physics body and camera alone.</summary>
    [DisallowMultipleComponent]
    public class PlayerFacing3D : MonoBehaviour
    {
        [Tooltip("The player's physics body. Resolved from parents when left empty.")]
        [SerializeField] private Rigidbody body;

        [Tooltip("Maximum visual turning speed in degrees per second.")]
        [SerializeField, Min(0f)] private float turnSpeed = 720f;

        [Tooltip("Use 180 if the model faces backward relative to Unity's forward (+Z) direction.")]
        [SerializeField] private float modelYawOffset;

        private PlayerWaterSpray _waterSpray;
        private PlayerHoseMovement _hoseMovement;

        private const float MinimumSpeed = 0.05f;

        private void Awake()
        {
            if (!body) body = GetComponentInParent<Rigidbody>();

            if (!body)
            {
                Debug.LogWarning($"[PlayerFacing3D] Assign the player's Rigidbody on '{name}'.", this);
                enabled = false;
                return;
            }

            _waterSpray = body.GetComponentInChildren<PlayerWaterSpray>(true);
            _hoseMovement = body.GetComponentInChildren<PlayerHoseMovement>(true);
        }

        private void LateUpdate()
        {
            if (!body) return;

            // Velocity is already in world space, including camera-relative movement.
            // Ignore vertical motion so jumping or falling cannot pitch the model.
            var direction = body.linearVelocity;
            // A backward hose reaction must not turn the firing pose away from its target.
            if (_waterSpray && _waterSpray.IsSpraying)
                direction = _waterSpray.LaunchVelocity;
            else if (_hoseMovement && _hoseMovement.isActiveAndEnabled)
                direction -= _hoseMovement.RecoilVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude < MinimumSpeed * MinimumSpeed) return;

            var target = Quaternion.LookRotation(direction, Vector3.up)
                * Quaternion.Euler(0f, modelYawOffset, 0f);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
