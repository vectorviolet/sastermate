using System;
using System.Collections.Generic;
using System.Globalization;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Gameplay.Fire;
using Sastermate.Gameplay.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sastermate.Gameplay.Climate
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

        [Header("Eco Upgrades")]
        [Tooltip("City-wide limit on the fraction of fire warming prevented by purchased infrastructure.")]
        [SerializeField, Range(0f, 1f)] private float maximumWarmingReduction = 0.8f;

        [Header("Survival Goal")]
        [SerializeField] private bool enableSurvivalGoal = true;
        [Tooltip("Win by keeping temperature below +2 degrees for this many Gameplay seconds.")]
        [SerializeField, Min(1f)] private float survivalDuration = 180f;

        [Header("UI")]
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private TextMeshProUGUI temperatureText;
        [SerializeField] private TextMeshProUGUI cpText;
        [SerializeField] private TextMeshProUGUI warmingRateText;
        [SerializeField] private TextMeshProUGUI roundTimerText;

        [Header("Existing Game Systems")]
        [Tooltip("Uses the shared CP balance when assigned. Finds the scene's TaskRewardManager if empty; otherwise uses currentCP directly.")]
        [SerializeField] private TaskRewardManager rewards;
        [Tooltip("Count the project's burning objects automatically. Turn off for manual AddFire/RemoveFire control.")]
        [SerializeField] private bool trackSceneFires = true;

        private readonly HashSet<BurnableObject> _trackedFires = new();
        private GameManager _gameManager;
        private bool _listeningForFires;
        private bool _gameOverTriggered;
        private bool _buyingUpgrade;
        private float _warmingReduction;
        private int _lastNotifiedCP = -1;
        private float _lastNotifiedReduction = -1f;
        private float _elapsedGameplayTime;

        public bool IsGameOver => _gameOverTriggered || GameManager.IsGameOver;
        public int CarbonPoints => rewards ? rewards.CarbonPoints : Mathf.Max(0, currentCP);
        public float WarmingReduction => _warmingReduction;
        public float MaximumWarmingReduction => maximumWarmingReduction;
        public float WarmingMultiplier => 1f - _warmingReduction;
        public float WarmingPerSecond => activeFiresCount * temperatureIncreasePerFire * WarmingMultiplier;
        public float ElapsedGameplayTime => _elapsedGameplayTime;
        public float SecondsRemaining => Mathf.Max(0f, survivalDuration - _elapsedGameplayTime);
        public bool CanPurchaseUpgrades => isActiveAndEnabled && !IsGameOver &&
            currentTemperature < MaximumTemperature && (!_gameManager || GameManager.IsGameplay);

        public event Action EconomyChanged;

        private void Awake()
        {
            if (!rewards) rewards = FindFirstObjectByType<TaskRewardManager>();
            _gameManager = GameManager.Instance ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            ClampState();
            ConfigureSlider();
        }

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
            if (!IsGameOver && (!_gameManager || GameManager.IsGameplay))
            {
                _elapsedGameplayTime += Time.deltaTime;
                currentTemperature = Mathf.Clamp(currentTemperature +
                    WarmingPerSecond * Time.deltaTime,
                    0f, MaximumTemperature);
            }
            RefreshUI();
            // Loss takes priority if the temperature limit and timer are reached in the same frame.
            if (!IsGameOver && currentTemperature >= MaximumTemperature) GameOver();
            else if (!IsGameOver && enableSurvivalGoal && _gameManager && GameManager.IsGameplay &&
                     _elapsedGameplayTime >= survivalDuration) _gameManager.WinGame();
        }

        public void SpendCPForEcoBuilding(int cost, float temperatureReduction)
        {
            if (IsGameOver) return;
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

        /// <summary>Purchases permanent protection for this run, without lowering the current temperature.</summary>
        public bool TryPurchaseWarmingReduction(int cost, float reduction)
        {
            if (_buyingUpgrade || !CanPurchaseUpgrades || cost < 0 || reduction <= 0f ||
                float.IsNaN(reduction) || float.IsInfinity(reduction)) return false;
            ClampState();
            SyncCP();
            if (_warmingReduction >= maximumWarmingReduction || currentCP < cost) return false;

            _buyingUpgrade = true;
            try
            {
                if (rewards)
                {
                    if (!rewards.TrySpendCarbonPoints(cost)) return false;
                    currentCP = rewards.CarbonPoints;
                }
                else currentCP -= cost;

                _warmingReduction = Mathf.Min(maximumWarmingReduction, _warmingReduction + reduction);
                RefreshUI();
                return true;
            }
            finally { _buyingUpgrade = false; }
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
            if (warmingRateText)
                warmingRateText.text = "+" + WarmingPerSecond.ToString("0.000", CultureInfo.InvariantCulture) +
                    "\u00B0C/s | " + (_warmingReduction * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "% protection";
            if (roundTimerText)
            {
                var seconds = Mathf.CeilToInt(SecondsRemaining);
                roundTimerText.text = enableSurvivalGoal ? $"Survive {seconds / 60:00}:{seconds % 60:00}" : "";
            }
            if (_lastNotifiedCP == currentCP && _lastNotifiedReduction == _warmingReduction) return;
            _lastNotifiedCP = currentCP;
            _lastNotifiedReduction = _warmingReduction;
            EconomyChanged?.Invoke();
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
            maximumWarmingReduction = float.IsNaN(maximumWarmingReduction) ? 0.8f :
                Mathf.Clamp01(maximumWarmingReduction);
            _warmingReduction = Mathf.Clamp(_warmingReduction, 0f, maximumWarmingReduction);
            survivalDuration = float.IsNaN(survivalDuration) || float.IsInfinity(survivalDuration)
                ? 180f : Mathf.Max(1f, survivalDuration);
        }

        private void GameOver()
        {
            if (IsGameOver) return;
            _gameOverTriggered = true;
            if (_gameManager) _gameManager.LoseGame();
            Debug.Log("[ClimateManager] Game Over: global warming has reached +2.00\u00B0C.", this);
        }

        private void OnValidate()
        {
            ClampState();
        }
    }
}
