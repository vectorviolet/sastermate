using Sastermate.Core.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using VectorViolet.ProAppState;
using VectorViolet.ProSaveManager;
using VectorViolet.ProSaveManager.Implementations;

namespace Sastermate.Core.App
{
    public class GameManager : AppStateManager<GameManager, GameState>
    {
        [SerializeField] private GameState startupState = GameState.Gameplay;
        private static string _replayScenePath;

        protected override GameState InitialState =>
            !string.IsNullOrEmpty(_replayScenePath) && gameObject.scene.path == _replayScenePath
                ? GameState.Gameplay : startupState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReplayState() => _replayScenePath = null;

        public static SaveManager SaveSystem { get; private set; }

        public static bool IsGameplay => IsInState(GameState.Gameplay);
        public static bool IsInteraction => IsInState(GameState.Interaction);
        public static bool HasWon => IsInState(GameState.GameWon);
        public static bool HasLost => IsInState(GameState.GameOver);
        public static bool IsGameOver => HasWon || HasLost;

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
            if (IsDuplicate) return;
            EventBus.Input.OnPausePressed += HandlePausePressed;
            EventBus.Input.OnBackPressed += HandleBackPressed;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            EventBus.Input.OnPausePressed -= HandlePausePressed;
            EventBus.Input.OnBackPressed -= HandleBackPressed;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandlePausePressed()
        {
            if (!IsGameOver && CurrentState != GameState.MainMenu) SetState(GameState.Paused);
        }

        private void HandleBackPressed()
        {
            if (!IsGameOver && CurrentState != GameState.MainMenu) SetState(GameState.Gameplay);
        }

        public void StartGame()
        {
            if (CurrentState == GameState.MainMenu) SetState(GameState.Gameplay);
        }

        public void WinGame() => FinishGame(GameState.GameWon);
        public void LoseGame() => FinishGame(GameState.GameOver);

        private void FinishGame(GameState outcome)
        {
            if (IsDuplicate || IsGameOver || CurrentState == GameState.MainMenu) return;
            SetState(outcome);
        }

        // Wire the game-over Replay button to this method. A fresh scene resets the climate,
        // task wallet, fires, traffic, and purchased infrastructure together.
        public void Replay()
        {
            if (IsDuplicate || !IsGameOver || _replayScenePath != null) return;
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[GameManager] Save the active scene and add it to the build's Scene List before replaying.", this);
                return;
            }

            _replayScenePath = scene.path;
            try
            {
                if (SceneManager.LoadSceneAsync(scene.path, LoadSceneMode.Single) == null)
                {
                    _replayScenePath = null;
                    return;
                }
                Time.timeScale = 1f;
            }
            catch (System.Exception exception)
            {
                _replayScenePath = null;
                Debug.LogException(exception, this);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_replayScenePath == null || scene.path != _replayScenePath) return;
            _replayScenePath = null;
            // The existing manager may persist through the reload. The newly loaded scene's
            // listeners have now enabled, so reconcile all of them with the fresh game's state.
            SetState(GameState.Gameplay);
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
