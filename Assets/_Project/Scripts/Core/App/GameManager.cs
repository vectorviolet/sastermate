using FactorySabotage.Core.Events;
using UnityEngine;
using VectorViolet.ProAppState;
using VectorViolet.ProSaveManager;
using VectorViolet.ProSaveManager.Implementations;

namespace FactorySabotage.Core.App
{
    public class GameManager : AppStateManager<GameManager, GameState>
    {
        public static SaveManager SaveSystem { get; private set; }

        public static bool IsGameplay => IsInState(GameState.Gameplay);
        public static bool IsInteraction => IsInState(GameState.Interaction);
        public static bool IsGameOver => IsInState(GameState.GameOver);

        protected override void Awake()
        {
            base.Awake();
            if (IsDuplicate) return;

            ISerializationService serializer = new JsonSerializationService();
            IStorageService storage = new LocalFileStorageService();
            SaveSystem = new SaveManager(serializer, storage, "FactorySabotage_Slot1");
        }

        private void OnEnable()
        {
            EventBus.Input.OnPausePressed += HandlePausePressed;
            EventBus.Input.OnBackPressed += HandleBackPressed;
        }

        private void OnDisable()
        {
            EventBus.Input.OnPausePressed -= HandlePausePressed;
            EventBus.Input.OnBackPressed -= HandleBackPressed;
        }

        private void HandlePausePressed()
        {
            if (!IsGameOver) SetState(GameState.Paused);
        }

        private void HandleBackPressed()
        {
            if (!IsGameOver) SetState(GameState.Gameplay);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
