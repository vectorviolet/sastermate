using UnityEngine;
using VectorViolet.Core.Events;
using VectorViolet.Utilities;

namespace Sastermate.Core.App
{
    public class CursorControllerBinder : GameStateListener
    {
        [Tooltip("Leave off for mouse aiming in the isometric game. Enable for a centered look cursor.")]
        [SerializeField] private bool lockCursorDuringGameplay;

        protected override void OnGameStateChanged(GameState newState)
        {
            EventBus<CursorLockEvent>.Raise(new CursorLockEvent(
                lockCursorDuringGameplay && newState == GameState.Gameplay));
        }
    }
}
