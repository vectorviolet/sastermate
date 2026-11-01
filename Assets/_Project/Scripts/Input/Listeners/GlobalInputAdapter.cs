using FactorySabotage.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using static FactorySabotage.Input.GameControls;

namespace FactorySabotage.Input.Listeners
{
    /// <summary>
    ///     Dedicated listener for global inputs that should be available regardless of Game State.
    /// </summary>
    public class GlobalInputAdapter : MonoBehaviour
    {
        private static GlobalActions Actions => InputProvider.Controls.Global;

        private void OnEnable()
        {
            Actions.Enable();
            
            Actions.DebugConsole.performed += OnDebugConsoleToggleRequested;
            Actions.FPSCounter.performed += OnFPSCounterToggleRequested;
            Actions.Logger.performed += OnLoggerToggleRequested;
        }

        private void OnDisable()
        {
            Actions.Disable();
            
            Actions.DebugConsole.performed -= OnDebugConsoleToggleRequested;
            Actions.FPSCounter.performed -= OnFPSCounterToggleRequested;
            Actions.Logger.performed -= OnLoggerToggleRequested;
        }

        private static void OnDebugConsoleToggleRequested(InputAction.CallbackContext ctx) => EventBus.Input.RaiseDebugConsoleTogglePressed();
        private static void OnFPSCounterToggleRequested(InputAction.CallbackContext ctx)   => EventBus.Input.RaiseFPSCounterTogglePressed();
        private static void OnLoggerToggleRequested(InputAction.CallbackContext ctx)       => EventBus.Input.RaiseLoggerTogglePressed();
    }
}