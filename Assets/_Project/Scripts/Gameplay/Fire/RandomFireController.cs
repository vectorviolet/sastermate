using System.Collections.Generic;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Gameplay.Tasks;
using Sastermate.Player;
using UnityEngine;
using VectorViolet.Utilities;

namespace Sastermate.Gameplay.Fire
{
    [DisallowMultipleComponent]
    public sealed class RandomFireController : GameStateListener
    {
        [SerializeField] private PooledFireEffect firePrefab;
        [SerializeField] private BurnableObject[] targets;
        [SerializeField, Min(0f)] private float initialDelay = 2f;
        [SerializeField] private Vector2 ignitionInterval = new(8f, 14f);
        [SerializeField, Range(1, 10)] private int maxSimultaneousFires = 3;

        [Header("Beginner Warmup")]
        [SerializeField] private bool enableWarmup = true;
        [SerializeField, Min(0f)] private float warmupDuration = 60f;
        [SerializeField, Min(0f)] private float warmupInitialDelay = 12f;
        [SerializeField] private Vector2 warmupIgnitionInterval = new(20f, 30f);
        [SerializeField, Range(1, 10)] private int warmupMaxFires = 1;
        [Tooltip("Seconds after warmup to blend back to the normal fire schedule.")]
        [SerializeField, Min(0f)] private float difficultyRampDuration = 30f;
        [Tooltip("Prefer fires within this XZ distance of the player during warmup. Zero allows the whole map.")]
        [SerializeField, Min(0f)] private float warmupSpawnRadius = 12f;
        [Tooltip("Optional. Uses the player's water nozzle transform when empty.")]
        [SerializeField] private Transform playerTarget;

        [Header("Task Rewards")]
        [Tooltip("Reward for fully extinguishing a fire with water. Disabling objects or clearing fires never awards points.")]
        [SerializeField] private TaskDefinition extinguishTask;

        private readonly Dictionary<BurnableObject, PooledFireEffect> _active = new();
        private readonly HashSet<BurnableObject> _targets = new();
        private ComponentPool<PooledFireEffect> _pool;
        private float _countdown;
        private float _gameplayTime;

        public int ActiveFireCount => _active.Count;
        public int PooledEffectCount => _pool?.TotalCount ?? 0;
        public float SecondsUntilNextIgnition => _countdown;
        private float NormalProgress => FireSpawnSchedule.NormalProgress(_gameplayTime, warmupDuration,
            difficultyRampDuration, enableWarmup);
        public int CurrentFireLimit => FireSpawnSchedule.FireLimit(warmupMaxFires, maxSimultaneousFires, NormalProgress);

        private void Awake()
        {
            if (!firePrefab)
            {
                Debug.LogWarning("[RandomFireController] Assign the pooled fire prefab.", this);
                enabled = false;
                return;
            }
            if (!playerTarget)
            {
                var water = FindFirstObjectByType<PlayerWaterSpray>();
                if (water) playerTarget = water.transform;
            }
            if (targets != null)
                foreach (var target in targets)
                    if (target) _targets.Add(target);
            // Deleted scene objects leave a non-empty array of missing references. Treat it
            // like an empty list so replacing the environment does not disable all fires.
            if (_targets.Count == 0)
                foreach (var target in FindObjectsByType<BurnableObject>(FindObjectsSortMode.None))
                    _targets.Add(target);

            var container = new GameObject("Fire VFX Pool").transform;
            container.SetParent(transform, false);
            _pool = new ComponentPool<PooledFireEffect>(firePrefab,
                maxSimultaneousFires, maxSimultaneousFires, container);
        }

        protected override void OnEnable()
        {
            _gameplayTime = 0f;
            _countdown = enableWarmup ? Mathf.Max(initialDelay, warmupInitialDelay) : initialDelay;
            EventBus.Game.OnWaterHit += HandleWaterHit;
            EventBus.Game.OnFireExtinguished += HandleFireExtinguished;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            EventBus.Game.OnWaterHit -= HandleWaterHit;
            EventBus.Game.OnFireExtinguished -= HandleFireExtinguished;
            ExtinguishAll();
            base.OnDisable();
        }

        private void OnDestroy() => _pool?.Dispose();

        protected override void OnGameStateChanged(GameState state)
        {
            foreach (var effect in _active.Values)
                if (effect) effect.SetPaused(state != GameState.Gameplay);
        }

        private void Update()
        {
            if (!GameManager.IsGameplay || _pool == null) return;
            _gameplayTime += Time.deltaTime;
            foreach (var effect in _active.Values)
                if (effect) effect.UpdateIntensity();

            _countdown -= Time.deltaTime;
            if (_countdown > 0f) return;
            TryIgniteRandom();
            var interval = Vector2.Lerp(warmupIgnitionInterval, ignitionInterval, NormalProgress);
            var minimum = Mathf.Max(0.1f, interval.x);
            _countdown = Random.Range(minimum, Mathf.Max(minimum, interval.y));
        }

        public bool TryIgnite(BurnableObject target)
        {
            if (!isActiveAndEnabled || !GameManager.IsGameplay || _pool == null || !target ||
                !_targets.Contains(target) || !target.CanIgnite || _active.Count >= CurrentFireLimit)
                return false;

            var effect = _pool.Get();
            target.Ignite(extinguishTask);
            effect.Show(target);
            _active.Add(target, effect);
            EventBus.Game.RaiseFireIgnited(target);
            return true;
        }

        public bool TryIgniteRandom()
        {
            // Reservoir sampling chooses uniformly without creating a candidate list every spawn.
            BurnableObject chosen = null;
            BurnableObject nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var eligible = 0;
            var nearbyOnly = enableWarmup && _gameplayTime < warmupDuration && playerTarget && warmupSpawnRadius > 0f;
            foreach (var target in _targets)
            {
                if (!target || !target.CanIgnite) continue;
                if (nearbyOnly)
                {
                    var delta = target.FirePosition - playerTarget.position;
                    var distance = delta.x * delta.x + delta.z * delta.z;
                    if (distance < nearestDistance) { nearest = target; nearestDistance = distance; }
                    if (distance > warmupSpawnRadius * warmupSpawnRadius) continue;
                }
                if (Random.Range(0, ++eligible) == 0) chosen = target;
            }
            // If the spawn area has no nearby burnables, use the closest eligible one.
            if (!chosen && nearbyOnly) chosen = nearest;
            return TryIgnite(chosen);
        }

        private void HandleWaterHit(RaycastHit hit, float waterAmount)
        {
            if (!GameManager.IsGameplay || !hit.collider) return;
            var target = hit.collider.GetComponentInParent<BurnableObject>();
            if (target && _active.ContainsKey(target)) target.ApplyWater(waterAmount);
        }

        private void HandleFireExtinguished(BurnableObject target)
        {
            if (!_active.Remove(target, out var effect)) return;
            if (effect) effect.ReturnToPool();
        }

        [ContextMenu("Extinguish all fires")]
        public void ExtinguishAll()
        {
            // Snapshot only for this infrequent operation; events may remove dictionary entries.
            var burning = new List<BurnableObject>(_active.Keys);
            foreach (var target in burning)
            {
                if (target) target.Extinguish();
                // OnDisable unsubscribes first, so also return any effects not handled by the event.
                if (_active.Remove(target, out var effect) && effect) effect.ReturnToPool();
            }
        }
    }
}
