using System.Collections.Generic;
using FactorySabotage.Core.App;
using UnityEngine;
using UnityEngine.Serialization;

namespace FactorySabotage.Gameplay.Traffic
{
    /// <summary>Random ambient traffic with centralized, swept movement reservations.</summary>
    [DisallowMultipleComponent]
    public sealed class TrafficManager : GameStateListener
    {
        [SerializeField] private TrafficVehicle carPrefab;
        // Read older scenes without rewriting their prefab references during script import.
        [FormerlySerializedAs("carPrefabs"), SerializeField, HideInInspector]
        private TrafficVehicle[] legacyCarPrefabs;
        [Tooltip("Leave empty to discover the scene's enabled TrafficRoutes.")]
        [SerializeField] private TrafficRoute[] routes;
        [SerializeField, Range(1, 50)] private int maximumCars = 8;
        [Tooltip("Cars placed along the lanes during Awake, capped by Maximum Cars. Set to zero for gradual spawning only.")]
        [SerializeField, Range(0, 50)] private int initialCars = 8;
        [Tooltip("Random positions tried per lane when placing each initial car. Blocked locations are skipped.")]
        [SerializeField, Range(1, 64)] private int initialSpawnAttempts = 24;
        [SerializeField] private Vector2 spawnInterval = new(2f, 4f);
        [SerializeField] private Vector2 cruiseSpeed = new(0.45f, 0.8f);
        [Tooltip("Minimum horizontal clearance between vehicle boxes, in world units.")]
        [SerializeField, Min(0.001f)] private float safetyGap = 0.03f;
        [Tooltip("Player/building obstacle layers. Exclude road surfaces. Managed cars are always checked separately.")]
        [SerializeField] private LayerMask obstacleMask;

        private readonly List<TrafficVehicle> _cars = new();
        private readonly List<TrafficVehicle> _order = new();
        private readonly TrafficReservations _space = new();
        private readonly Stack<TrafficVehicle> _pool = new();
        private readonly Collider[] _obstacleBuffer = new Collider[32];
        private TrafficJunction[] _junctions;
        private Transform _poolRoot;
        private float _countdown = 1f;
        private int _priority;
        private TrafficVehicle _template;

        public int ActiveCarCount => _cars.Count;

        private void Awake()
        {
            _template = carPrefab;
            if (!_template && legacyCarPrefabs != null)
                foreach (var previous in legacyCarPrefabs)
                    if (previous) { _template = previous; break; }
            if (routes == null || routes.Length == 0)
                routes = FindObjectsByType<TrafficRoute>(FindObjectsSortMode.None);
            _junctions = FindObjectsByType<TrafficJunction>(FindObjectsSortMode.None);
            _poolRoot = new GameObject("Traffic Car Pool").transform;
            _poolRoot.SetParent(transform, false);
            // New prefabs initialize while inactive; no frame is rendered at an unchecked spawn.
            _poolRoot.gameObject.SetActive(false);
            // isActiveAndEnabled can still be false inside Awake, before OnEnable runs.
            if (enabled && gameObject.activeInHierarchy) PopulateInitialTraffic();
        }

        private void PopulateInitialTraffic()
        {
            if (!_template || routes == null || routes.Length == 0) return;
            Physics.SyncTransforms();
            _space.BeginStep();
            var count = Mathf.Clamp(initialCars, 0, Mathf.Max(0, maximumCars));
            var firstRoute = Random.Range(0, routes.Length);
            // Spread the first choices across lanes before placing another car on each one.
            for (var i = 0; i < count; i++)
                TrySpawn(true, (firstRoute + i) % routes.Length);
        }

        private void FixedUpdate()
        {
            if (!GameManager.IsGameplay) return;
            _space.BeginStep();
            for (var i = _cars.Count - 1; i >= 0; i--)
                if (!_cars[i] || !_cars[i].isActiveAndEnabled || !_cars[i].Route ||
                    !_cars[i].Route.isActiveAndEnabled || !_cars[i].Route.IsValid) Recycle(_cars[i]);
            foreach (var car in _cars) _space.Occupy(car.GetInstanceID(), car.Footprint(safetyGap));
            foreach (var junction in _junctions)
                if (junction && junction.isActiveAndEnabled) junction.ReleaseIfClear(safetyGap);

            _countdown -= Time.fixedDeltaTime;
            if (_countdown <= 0f)
            {
                if (_cars.Count < maximumCars) TrySpawn();
                var minimum = Mathf.Max(0.1f, spawnInterval.x);
                _countdown = Random.Range(minimum, Mathf.Max(minimum, spawnInterval.y));
            }

            _order.Clear();
            _order.AddRange(_cars);
            if (_order.Count == 0) return;
            _priority = (_priority + 1) % _order.Count;
            for (var i = 0; i < _order.Count; i++)
                Advance(_order[(i + _priority) % _order.Count]);
        }

        private bool TrySpawn(bool randomPosition = false, int preferredRoute = -1)
        {
            if (!_template || routes == null || routes.Length == 0) return false;
            var firstRoute = preferredRoute >= 0 ? preferredRoute % routes.Length : Random.Range(0, routes.Length);
            var car = TakeFromPool();
            // Choose and refit exactly once before trying any route's spawn clearance.
            if (!car.PrepareForSpawn()) { ReturnToPool(car); return false; }
            var speed = Random.Range(Mathf.Max(0.01f, cruiseSpeed.x),
                Mathf.Max(Mathf.Max(0.01f, cruiseSpeed.x), cruiseSpeed.y));
            for (var i = 0; i < routes.Length; i++)
            {
                var route = routes[(firstRoute + i) % routes.Length];
                if (!route || !route.enabled || !route.gameObject.activeInHierarchy || !route.IsValid) continue;
                var attempts = randomPosition ? Mathf.Clamp(initialSpawnAttempts, 1, 64) : 1;
                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var end = route.IsLoop ? route.Length : Mathf.Max(0f, route.Length - 0.01f);
                    var distance = randomPosition ? Random.Range(0f, end) : 0f;
                    car.Begin(this, route, speed, distance);
                    var footprint = car.Footprint(safetyGap);
                    // Spawn outside intersections; let moving cars request crossings normally.
                    var inJunction = false;
                    foreach (var junction in _junctions)
                        if (junction && junction.enabled && junction.gameObject.activeInHierarchy &&
                            junction.Area.Overlaps(footprint))
                        { inJunction = true; break; }
                    if (inJunction || !CanOccupy(car, footprint)) continue;
                    if (randomPosition) car.CurrentSpeed = car.CruiseSpeed;
                    car.transform.SetParent(transform, true);
                    car.gameObject.SetActive(true);
                    _cars.Add(car);
                    _space.Occupy(car.GetInstanceID(), footprint);
                    return true;
                }
            }
            ReturnToPool(car);
            return false;
        }

        private TrafficVehicle TakeFromPool()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled) return pooled;
            }
            var car = Instantiate(_template, _poolRoot);
            // Initialize explicitly under the inactive parent, before validating a spawn.
            car.Initialize();
            car.gameObject.SetActive(false);
            return car;
        }

        private void Advance(TrafficVehicle car)
        {
            var path = car.Route.Path;
            var dt = Time.fixedDeltaTime;
            var toCorner = path.DistanceToCorner(car.Distance);
            var lookDistance = Mathf.Min(toCorner,
                car.CurrentSpeed * car.CurrentSpeed / (2f * car.Braking) + safetyGap);
            var lookPose = car.PoseAt(car.Distance + lookDistance,
                Mathf.Max(dt, lookDistance / Mathf.Max(0.01f, car.CruiseSpeed)));
            var clearAhead = CanMove(car, car.Sweep(lookPose, safetyGap));
            car.CurrentSpeed = Mathf.MoveTowards(car.CurrentSpeed, clearAhead ? car.CruiseSpeed : 0f,
                (clearAhead ? car.Acceleration : car.Braking) * dt);
            // Do not cut across multiple route corners in one physics tick, even at high speed.
            var advance = Mathf.Min(car.CurrentSpeed * dt, toCorner);
            var distance = car.Distance + advance;
            var pose = car.PoseAt(distance, dt);
            var sweep = car.Sweep(pose, safetyGap);
            if (!CanMove(car, sweep)) { car.CurrentSpeed = 0f; return; }
            foreach (var junction in _junctions)
                if (junction && junction.isActiveAndEnabled && junction.Area.Overlaps(sweep)) junction.Reserve(car);
            _space.Occupy(car.GetInstanceID(), sweep);
            car.Commit(pose, distance);
            if (!car.Route.IsLoop && distance >= path.Length - 0.00001f) Recycle(car);
        }

        private bool CanMove(TrafficVehicle car, TrafficFootprint sweep)
        {
            foreach (var junction in _junctions)
                if (junction && junction.isActiveAndEnabled && junction.Area.Overlaps(sweep) && !junction.Allows(car))
                    return false;
            return CanOccupy(car, sweep);
        }

        private bool CanOccupy(TrafficVehicle car, TrafficFootprint area)
        {
            if (!_space.IsClear(car.GetInstanceID(), area)) return false;
            if (obstacleMask.value == 0) return true;
            var count = Physics.OverlapBoxNonAlloc(area.Center, area.HalfSize, _obstacleBuffer,
                area.Rotation, obstacleMask, QueryTriggerInteraction.Ignore);
            var hits = count == _obstacleBuffer.Length
                ? Physics.OverlapBox(area.Center, area.HalfSize, area.Rotation, obstacleMask, QueryTriggerInteraction.Ignore)
                : _obstacleBuffer;
            if (hits != _obstacleBuffer) count = hits.Length;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit || hit.attachedRigidbody == car.Body) continue;
                var otherCar = hit.GetComponentInParent<TrafficVehicle>();
                if (otherCar && otherCar.Controller == this) continue;
                return false;
            }
            return true;
        }

        private void Recycle(TrafficVehicle car)
        {
            _cars.Remove(car);
            if (!car) return;
            foreach (var junction in _junctions)
                if (junction) junction.Release(car);
            _space.Release(car.GetInstanceID());
            ReturnToPool(car);
        }

        private void ReturnToPool(TrafficVehicle car)
        {
            car.ResetForPool();
            _pool.Push(car);
        }

        protected override void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Gameplay) return;
            foreach (var car in _cars) if (car) car.CurrentSpeed = 0f;
        }

        protected override void OnDisable()
        {
            for (var i = _cars.Count - 1; i >= 0; i--) Recycle(_cars[i]);
            base.OnDisable();
        }
    }
}
