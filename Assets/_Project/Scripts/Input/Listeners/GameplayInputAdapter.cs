using UnityEngine;
using UnityEngine.InputSystem;
using VectorViolet.Core.Events;

using FactorySabotage.Core.App;
using FactorySabotage.Core.Events;
using static FactorySabotage.Input.GameControls;

namespace FactorySabotage.Input.Listeners
{
    /// <summary>
    ///     Dedicated listener for player inputs. Only active during Gameplay state.
    ///     Broadcasts events blindly. It is up to the listeners to check Game State.
    /// </summary>
    public class GameplayInputAdapter : GameStateListener
    {
        private static PlayerActions Actions => InputProvider.Controls.Player;

        private void Update()
        {
            // Send absolute screen coordinates; the weapon projects them through the current camera.
            // Keep sending while stationary so listeners enabled mid-game also receive the pointer.
            if (GameManager.IsGameplay && Pointer.current != null)
                EventBus.Input.RaiseAimPositionChanged(Pointer.current.position.ReadValue());
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            Actions.Attack.performed += OnAttackPressed;
            Actions.Attack.canceled += OnAttackReleased;
            Actions.Interact.performed += OnInteractPressed;
            Actions.Move.performed += OnMovePressed;
            Actions.Move.canceled += OnMoveCanceled;
            Actions.Look.performed += OnLookPerformed;
            Actions.Look.canceled += OnLookCanceled;
            Actions.RotateCamera.performed += OnRotateCameraPerformed;
            Actions.Menu.performed += OnMenuPressed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            Actions.Attack.performed -= OnAttackPressed;
            Actions.Attack.canceled -= OnAttackReleased;
            Actions.Interact.performed -= OnInteractPressed;
            Actions.Move.performed -= OnMovePressed;
            Actions.Move.canceled -= OnMoveCanceled;
            Actions.Look.performed -= OnLookPerformed;
            Actions.Look.canceled -= OnLookCanceled;
            Actions.RotateCamera.performed -= OnRotateCameraPerformed;
            Actions.Menu.performed -= OnMenuPressed;
            EventBus.Input.RaiseAttackReleased();
        }

        protected override void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Gameplay)
                Actions.Enable();
            else
                Actions.Disable();
        }
        
        private static void OnAttackPressed(InputAction.CallbackContext ctx)   => EventBus.Input.RaiseAttackPressed();
        private static void OnAttackReleased(InputAction.CallbackContext ctx)  => EventBus.Input.RaiseAttackReleased();
        private static void OnInteractPressed(InputAction.CallbackContext ctx) => EventBus.Input.RaiseInteractPressed();
        private static void OnMovePressed(InputAction.CallbackContext ctx)     => EventBus<MoveEvent>.Raise(new MoveEvent(ctx.ReadValue<Vector2>()));
        private static void OnMoveCanceled(InputAction.CallbackContext ctx)    => EventBus<MoveEvent>.Raise(new MoveEvent(Vector2.zero));
        private static void OnLookPerformed(InputAction.CallbackContext ctx)   => EventBus<CameraRotateEvent>.Raise(new CameraRotateEvent(ctx.ReadValue<Vector2>()));
        private static void OnLookCanceled(InputAction.CallbackContext ctx)    => EventBus<CameraRotateEvent>.Raise(new CameraRotateEvent(Vector2.zero));

        // The Q/E 1D-axis composite also reports the return-to-zero on release — only raise a step on
        // the actual press (nonzero), not that release, so a step means "one press" and nothing fires
        // when a key comes back up.
        private static void OnRotateCameraPerformed(InputAction.CallbackContext ctx)
        {
            var direction = ctx.ReadValue<float>();
            if (direction != 0f) EventBus<CameraStepRotateEvent>.Raise(new CameraStepRotateEvent(direction));
        }

        private static void OnMenuPressed(InputAction.CallbackContext ctx)     => EventBus.Input.RaisePausePressed();
    }
}
