using UnityEngine;
using VectorViolet.ProMovement;
using static Sastermate.Core.Utilities.HashedAnimatorParameters;

namespace Sastermate.Player
{
    /// <summary>Connects movement speed and the water hose's spraying state to a 3D Animator.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(300)] // Sample after movement and hose aiming.
    public class PlayerAnimator3D : MonoBehaviour
    {
        [SerializeField] private EntityMovement movement;
        [SerializeField] private PlayerWaterSpray waterSpray;

        [Tooltip("Time used to smooth changes between idle and running.")]
        [SerializeField, Min(0f)] private float speedDampTime = 0.1f;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;

            var body = GetComponentInParent<Rigidbody>();
            if (!movement)
            {
                // The visual and movement component are siblings under the player's physics body.
                movement = body
                    ? body.GetComponentInChildren<EntityMovement>(true)
                    : GetComponentInParent<EntityMovement>();
            }

            if (!waterSpray)
                waterSpray = body
                    ? body.GetComponentInChildren<PlayerWaterSpray>(true)
                    : GetComponentInChildren<PlayerWaterSpray>(true);

            if (!movement)
            {
                Debug.LogWarning($"[PlayerAnimator3D] Assign an EntityMovement on '{name}'.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!_animator.runtimeAnimatorController) return;

            // Sample after movement's Update. Input magnitude avoids alternating idle/run values
            // when rendering runs faster than the Rigidbody's fixed physics updates.
            var speed = movement && movement.isActiveAndEnabled
                ? Mathf.Clamp01(Mathf.Sqrt(movement.MovementSpeedSqr))
                : 0f;

            if (speedDampTime > 0f)
                _animator.SetFloat(Speed, speed, speedDampTime, Time.deltaTime);
            else
                _animator.SetFloat(Speed, speed);

            // Read the hose instead of raw input so pauses, focus loss, and disabling the
            // nozzle also end the animation, while water already in flight can continue.
            _animator.SetBool(IsSpraying, waterSpray && waterSpray.isActiveAndEnabled && waterSpray.IsSpraying);
        }

        private void OnDisable()
        {
            if (_animator && _animator.runtimeAnimatorController)
            {
                _animator.SetFloat(Speed, 0f);
                _animator.SetBool(IsSpraying, false);
            }
        }
    }
}
