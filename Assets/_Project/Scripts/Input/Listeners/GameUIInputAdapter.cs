using Sastermate.Core.App;
using Sastermate.Core.Events;
using UnityEngine.InputSystem;
using static Sastermate.Input.GameControls;

namespace Sastermate.Input.Listeners
{
    /// <summary>
    ///     Dedicated listener for generic menu inputs.
    ///     Broadcasts events blindly. It is up to the listeners to check Game State.
    /// </summary>
    public class GameUIInputAdapter : GameStateListener
    {
        private static UIActions Actions => InputProvider.Controls.UI;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            Actions.Interact.performed += OnInteract;
            Actions.Cancel.performed += OnCancel;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            Actions.Interact.performed -= OnInteract;
            Actions.Cancel.performed -= OnCancel;
        }

        protected override void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Gameplay)
                Actions.Disable();
            else
                Actions.Enable();
        }
        
        private static void OnInteract(InputAction.CallbackContext ctx) => EventBus.Input.RaiseInteractPressed();
        private static void OnCancel(InputAction.CallbackContext ctx)   => EventBus.Input.RaiseBackPressed();
    }
}