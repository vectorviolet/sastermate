using System.Collections.Generic;
using FactorySabotage.Core.App;
using FactorySabotage.Core.Events;
using FactorySabotage.Gameplay.Tasks;
using UnityEngine;
using VectorViolet.Utilities;

namespace FactorySabotage.Gameplay.Fire
{
    [DisallowMultipleComponent]
    public sealed class RandomFireController : GameStateListener
    {
        [SerializeField] private PooledFireEffect firePrefab;
        [SerializeField] private BurnableObject[] targets;
        [SerializeField, Min(0f)] private float initialDelay = 2f;
        [SerializeField] private Vector2 ignitionInterval = new(8f, 14f);
        [SerializeField, Range(1, 10)] private int maxSimultaneousFires = 3;

        [Header("Task Rewards")]
        [Tooltip("Reward for fully extinguishing a fire with water. Disabling objects or clearing fires never awards points.")]
        [SerializeField] private TaskDefinition extinguishTask;

        private readonly Dictionary<BurnableObject, PooledFireEffect> _active = new();
        private readonly HashSet<BurnableObject> _targets = new();
        private ComponentPool<PooledFireEffect> _pool;
        private float _countdown;

        public int ActiveFireCount => _active.Count;
        public int PooledEffectCount => _pool?.TotalCount ?? 0;
        public float SecondsUntilNextIgnition => _countdown;

        private void Awake()
        {
            if (!firePrefab)
            {
                Debug.LogWarning("[RandomFireController] Assign the pooled fire prefab.", this);
                enabled = false;
                return;
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
            _countdown = initialDelay;
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
            foreach (var effect in _active.Values)
                if (effect) effect.UpdateIntensity();

            _countdown -= Time.deltaTime;
            if (_countdown > 0f) return;
            TryIgniteRandom();
            var minimum = Mathf.Max(0.1f, ignitionInterval.x);
            _countdown = Random.Range(minimum, Mathf.Max(minimum, ignitionInterval.y));
        }

        public bool TryIgnite(BurnableObject target)
        {
            if (!isActiveAndEnabled || !GameManager.IsGameplay || _pool == null || !target ||
                !_targets.Contains(target) || !target.CanIgnite || _active.Count >= maxSimultaneousFires)
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
            var eligible = 0;
            foreach (var target in _targets)
                if (target && target.CanIgnite && Random.Range(0, ++eligible) == 0) chosen = target;
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
