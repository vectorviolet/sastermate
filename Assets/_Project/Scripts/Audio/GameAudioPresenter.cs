using Sastermate.Core.App;
using Sastermate.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using VectorViolet.ProSoundManager;

namespace Sastermate.Audio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/Audio/Game Audio Presenter")]
    public sealed class GameAudioPresenter : GameStateListener
    {
        [Header("Music")]
        [SerializeField] private SoundData menuMusic;
        [SerializeField] private SoundData gameplayMusic;

        [Header("UI")]
        [SerializeField] private SoundData clickSound;

        [Header("End Results")]
        [SerializeField] private SoundData winSound;
        [SerializeField] private SoundData loseSound;

        [Header("Water Spray")]
        [SerializeField] private PlayerWaterSpray waterSpray;
        [SerializeField] private SoundData waterLoop;

        private SoundData _requestedMusic;
        private SoundData _startedMusic;
        private SoundData _activeWaterLoop;
        private ISoundService _waterService;
        private bool _inGameplay;
        private GameState? _pendingResult;
        private bool _resultSoundPlayed;

        protected override void OnEnable()
        {
            // A scene reload destroys the duplicate GameManager root at the end of the
            // frame. Its presenter must not briefly replay the persistent manager's result.
            var owner = GetComponent<GameManager>();
            if (owner && GameManager.Instance && owner != GameManager.Instance)
            {
                enabled = false;
                return;
            }
            SceneManager.sceneLoaded += HandleSceneLoaded;
            base.OnEnable();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopWaterLoop();
            // A presenter on the persistent GameManager must follow the newly spawned player after Replay.
            if (!waterSpray) waterSpray = FindFirstObjectByType<PlayerWaterSpray>();
        }

        protected override void OnGameStateChanged(GameState state)
        {
            _inGameplay = state == GameState.Gameplay;
            if (state == GameState.MainMenu) _requestedMusic = menuMusic;
            else if (_inGameplay) _requestedMusic = gameplayMusic;
            // Pausing and overlays retain the current music without restarting it.
            TryPlayMusic();
            if (!_inGameplay) StopWaterLoop();
            if (state == GameState.GameWon || state == GameState.GameOver) _pendingResult = state;
            else
            {
                _pendingResult = null;
                if (_inGameplay || state == GameState.MainMenu) _resultSoundPlayed = false;
            }
            TryPlayResultSound();
        }

        private void Update()
        {
            // The sound manager can become available after this listener first binds.
            TryPlayMusic();
            TryPlayResultSound();
            if (!_inGameplay || !waterSpray || !waterSpray.IsSpraying || !waterLoop || !waterLoop.clip)
            {
                StopWaterLoop();
                return;
            }
            if (_waterService != null && _activeWaterLoop == waterLoop) return;
            StopWaterLoop();
            var service = SoundService.Current;
            if (service == null) return;
            service.PlaySfx(waterLoop);
            _waterService = service;
            _activeWaterLoop = waterLoop;
        }

        private void TryPlayMusic()
        {
            if (!_requestedMusic || !_requestedMusic.clip || _requestedMusic == _startedMusic) return;
            var service = SoundService.Current;
            if (service == null) return;
            service.PlayMusic(_requestedMusic);
            _startedMusic = _requestedMusic;
        }

        private void TryPlayResultSound()
        {
            if (!_pendingResult.HasValue || _resultSoundPlayed || GameManager.IsReplaying) return;
            var sound = _pendingResult.Value == GameState.GameWon ? winSound : loseSound;
            if (!sound || !sound.clip) return;
            var service = SoundService.Current;
            if (service == null) return;
            _resultSoundPlayed = true;
            service.PlaySfx(sound);
        }

        // Add this to Button.onClick. The 2D overload is independent of the overhead camera position.
        public void PlayClick()
        {
            if (clickSound && clickSound.clip) SoundService.Current?.PlaySfx(clickSound);
        }

        private void StopWaterLoop()
        {
            if (_waterService != null && _activeWaterLoop) _waterService.StopSfx(_activeWaterLoop);
            _waterService = null;
            _activeWaterLoop = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StopWaterLoop();
        }

        protected override void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopWaterLoop();
            _startedMusic = null;
            base.OnDisable();
        }
    }
}
