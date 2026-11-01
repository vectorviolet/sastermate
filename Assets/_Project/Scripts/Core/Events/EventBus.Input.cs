using System;
using UnityEngine;

namespace Sastermate.Core.Events
{
    public static partial class EventBus
    {
        public static class Input
        {
            public static event Action<Vector2> OnAimPositionChanged;

            public static event Action
                OnAttackPressed,
                OnAttackReleased,
                OnInteractPressed,
                OnBackPressed,
                OnPausePressed,
                OnMainMenuPressed;
            
            public static event Action
                OnDebugConsoleToggleRequested,
                OnFPSCounterToggleRequested,
                OnLoggerToggleRequested;
            
            public static void RaiseAttackPressed() => OnAttackPressed?.Invoke();
            public static void RaiseAimPositionChanged(Vector2 screenPosition) =>
                OnAimPositionChanged?.Invoke(screenPosition);
            public static void RaiseAttackReleased() => OnAttackReleased?.Invoke();
            public static void RaiseInteractPressed() => OnInteractPressed?.Invoke();
            public static void RaiseBackPressed() => OnBackPressed?.Invoke();
            public static void RaisePausePressed() => OnPausePressed?.Invoke();
            public static void RaiseMainMenuPressed() => OnMainMenuPressed?.Invoke();
            public static void RaiseDebugConsoleTogglePressed() => OnDebugConsoleToggleRequested?.Invoke();
            public static void RaiseFPSCounterTogglePressed() => OnFPSCounterToggleRequested?.Invoke();
            public static void RaiseLoggerTogglePressed() => OnLoggerToggleRequested?.Invoke();
        }
    }
}
