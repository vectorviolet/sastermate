using System.Collections.Generic;
using System.Globalization;
using FactorySabotage.Core.App;
using FactorySabotage.Core.Events;
using FactorySabotage.Gameplay.Fire;
using FactorySabotage.Gameplay.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VectorViolet.ProPanelManager;

namespace FactorySabotage.Gameplay.Climate
{
    [DisallowMultipleComponent]
    public sealed class ClimateManager : MonoBehaviour
    {
        private const float MaximumTemperature = 2f;

        [Header("Climate State")]
        [Range(0f, MaximumTemperature)] public float currentTemperature;
        [Min(0)] public int currentCP;
        [Min(0)] public int activeFiresCount;
        [SerializeField, Min(0f)] private float temperatureIncreasePerFire = 0.01f;

        [Header("UI")]
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private TextMeshProUGUI temperatureText;
        [SerializeField] private TextMeshProUGUI cpText;

        [Header("Game Over")]
        [SerializeField] private SetActivePanel gameOverPanel;
        [SerializeField] private string gameOverPanelId = "GameOver";

        [Header("Existing Game Systems")]
        [Tooltip("Uses the shared CP balance when assigned. Finds the scene's TaskRewardManager if empty; otherwise uses currentCP directly.")]
        [SerializeField] private TaskRewardManager rewards;
        [Tooltip("Count the project's burning objects automatically. Turn off for manual AddFire/RemoveFire control.")]
        [SerializeField] private bool trackSceneFires = true;

        private readonly HashSet<BurnableObject> _trackedFires = new();
        private GameManager _gameManager;
        private bool _listeningForFires;
        private bool _gameOverTriggered;
        private IPanelService _panels;

        public bool IsGameOver => _gameOverTriggered;

        private void Awake()
        {
            if (!rewards) rewards = FindFirstObjectByType<TaskRewardManager>();
            _gameManager = GameManager.Instance ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            ClampState();
            ConfigureSlider();
            // SetActivePanel remains registered while its GameObject is hidden.
            _panels = PanelService.Current;
            if (gameOverPanel && _panels != null)
            {
                _panels.Register(gameOverPanelId, gameOverPanel);
                _panels.Hide(gameOverPanelId);
            }
        }

        private void OnDestroy() => _panels?.Unregister(gameOverPanelId, gameOverPanel);

        private void OnEnable()
        {
            if (trackSceneFires)
            {
                EventBus.Game.OnFireIgnited += HandleFireIgnited;
                EventBus.Game.OnFireExtinguished += HandleFireExtinguished;
                _listeningForFires = true;
                _trackedFires.Clear();
                foreach (var fire in FindObjectsByType<BurnableObject>(FindObjectsSortMode.None))
                    if (fire.IsBurning) _trackedFires.Add(fire);
                activeFiresCount = _trackedFires.Count;
            }
            RefreshUI();
        }

        private void OnDisable()
        {
            if (!_listeningForFires) return;
            EventBus.Game.OnFireIgnited -= HandleFireIgnited;
            EventBus.Game.OnFireExtinguished -= HandleFireExtinguished;
            _listeningForFires = false;
            _trackedFires.Clear();
        }

        private void Update()
        {
            ClampState();
            // Respect the project's pause/menu states, while also working in a simple test
            // scene without a GameManager. Time.deltaTime respects a paused timescale.
            if (!_gameOverTriggered && (!_gameManager || GameManager.IsGameplay))
            {
                currentTemperature = Mathf.Clamp(currentTemperature +
                    activeFiresCount * temperatureIncreasePerFire * Time.deltaTime,
                    0f, MaximumTemperature);
            }
            RefreshUI();
            if (currentTemperature >= MaximumTemperature) GameOver();
        }

        public void SpendCPForEcoBuilding(int cost, float temperatureReduction)
        {
            if (_gameOverTriggered) return;
            if (cost < 0 || temperatureReduction < 0f || float.IsNaN(temperatureReduction) ||
                float.IsInfinity(temperatureReduction))
            {
                Debug.LogWarning("[ClimateManager] Cost and temperature reduction must be nonnegative, finite values.", this);
                return;
            }
            ClampState();
            SyncCP();
            if (currentTemperature >= MaximumTemperature) { RefreshUI(); GameOver(); return; }
            if (currentCP < cost) return;

            if (rewards)
            {
                if (!rewards.TrySpendCarbonPoints(cost)) return;
                currentCP = rewards.CarbonPoints;
            }
            else currentCP -= cost;

            currentTemperature = Mathf.Clamp(currentTemperature - temperatureReduction,
                0f, MaximumTemperature);
            RefreshUI();
        }

        public void AddFire()
        {
            if (activeFiresCount < int.MaxValue) activeFiresCount = Mathf.Max(0, activeFiresCount) + 1;
        }

        public void RemoveFire() => activeFiresCount = activeFiresCount > 0 ? activeFiresCount - 1 : 0;

        private void HandleFireIgnited(BurnableObject fire)
        {
            if (fire && fire.IsBurning && _trackedFires.Add(fire)) AddFire();
        }

        private void HandleFireExtinguished(BurnableObject fire)
        {
            if (!ReferenceEquals(fire, null) && _trackedFires.Remove(fire)) RemoveFire();
        }

        private void SyncCP() => currentCP = rewards ? rewards.CarbonPoints : Mathf.Max(0, currentCP);

        private void RefreshUI()
        {
            SyncCP();
            if (temperatureSlider) temperatureSlider.SetValueWithoutNotify(currentTemperature);
            if (temperatureText)
                temperatureText.text = "+" + currentTemperature.ToString("0.00", CultureInfo.InvariantCulture) + "\u00B0C";
            if (cpText) cpText.text = currentCP.ToString("N0") + " CP";
        }

        private void ConfigureSlider()
        {
            if (!temperatureSlider) return;
            temperatureSlider.minValue = 0f;
            temperatureSlider.maxValue = MaximumTemperature;
            temperatureSlider.wholeNumbers = false;
            temperatureSlider.interactable = false;
        }

        private void ClampState()
        {
            currentTemperature = float.IsNaN(currentTemperature) ? 0f :
                Mathf.Clamp(currentTemperature, 0f, MaximumTemperature);
            currentCP = Mathf.Max(0, currentCP);
            activeFiresCount = Mathf.Max(0, activeFiresCount);
        }

        private void GameOver()
        {
            if (_gameOverTriggered) return;
            _gameOverTriggered = true;
            if (_gameManager) _gameManager.SetState(GameState.GameOver);
            if (gameOverPanel)
            {
                _panels ??= PanelService.Current;
                if (_panels != null)
                {
                    _panels.Register(gameOverPanelId, gameOverPanel);
                    _panels.Show(gameOverPanelId);
                }
            }
            Debug.Log("[ClimateManager] Game Over: global warming has reached +2.00\u00B0C.", this);
        }

        private void OnValidate()
        {
            ClampState();
        }
    }
}
