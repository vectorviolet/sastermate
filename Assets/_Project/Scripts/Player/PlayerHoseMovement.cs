using FactorySabotage.Core.App;
using UnityEngine;
using VectorViolet.ProMovement;

namespace FactorySabotage.Player
{
    /// <summary>Applies hose handling after EntityMovement has written this physics step's velocity.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class PlayerHoseMovement : GameStateListener
    {
        [Header("References")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private EntityMovement movement;
        [SerializeField] private PlayerWaterSpray waterSpray;

        [Header("While Spraying")]
        [Tooltip("Fraction of normal movement speed while spraying. 0.5 means half speed.")]
        [SerializeField, Range(0f, 1f)] private float movementMultiplier = 0.5f;
        [Tooltip("Horizontal acceleration opposite the jet, in world units per second squared.")]
        [SerializeField, Min(0f)] private float recoilAcceleration = 2f;
        [Tooltip("Maximum extra backward speed, in world units per second.")]
        [SerializeField, Min(0f)] private float maximumRecoilSpeed = 0.4f;
        [Tooltip("How quickly residual recoil stops after release, in world units per second squared.")]
        [SerializeField, Min(0.01f)] private float recoilRecovery = 4f;

        private readonly HoseMovementResponse _response = new();

        public Vector3 RecoilVelocity => _response.RecoilVelocity;

        private void Awake()
        {
            if (!body) body = GetComponentInParent<Rigidbody>();
            if (body)
            {
                if (!movement) movement = body.GetComponentInChildren<EntityMovement>(true);
                if (!waterSpray) waterSpray = body.GetComponentInChildren<PlayerWaterSpray>(true);
            }
            if (!body || !movement || !waterSpray)
            {
                Debug.LogWarning("[PlayerHoseMovement] Assign the player's Rigidbody, EntityMovement, and PlayerWaterSpray.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!GameManager.IsGameplay || !body || body.isKinematic || !movement ||
                !movement.isActiveAndEnabled || !movement.usePhysicsMovement)
            {
                _response.Reset();
                return;
            }

            // EntityMovement (order 0) has just supplied fresh movement velocity. Compose once,
            // preserving its stat-derived speed and the body's vertical/gravity velocity.
            var spraying = waterSpray && waterSpray.isActiveAndEnabled && waterSpray.IsSpraying;
            body.linearVelocity = _response.Apply(body.linearVelocity,
                waterSpray ? waterSpray.LaunchVelocity : Vector3.zero, spraying,
                movementMultiplier, recoilAcceleration, maximumRecoilSpeed, recoilRecovery,
                Time.fixedDeltaTime);
        }

        protected override void OnGameStateChanged(GameState newState)
        {
            if (newState != GameState.Gameplay) _response.Reset();
        }

        protected override void OnDisable()
        {
            _response.Reset();
            base.OnDisable();
        }
    }
}
