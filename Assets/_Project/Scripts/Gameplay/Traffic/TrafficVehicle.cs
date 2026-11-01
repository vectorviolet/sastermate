using System.Collections.Generic;
using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class TrafficVehicle : MonoBehaviour
    {
        [Header("Random Visual Model")]
        [SerializeField] private TrafficModelCatalog modelCatalog;
        [Tooltip("Container owned by the model picker. An empty Visuals child is created at runtime when left unassigned.")]
        [SerializeField] private Transform visualRoot;

        [Header("Driving")]
        [Tooltip("Fit a preplaced visual on initialization. Catalog models are always refitted when selected.")]
        [SerializeField] private bool fitColliderOnAwake = true;
        [SerializeField, Min(0.01f)] private float acceleration = 0.6f;
        [SerializeField, Min(0.01f)] private float braking = 1.5f;
        [SerializeField, Min(1f)] private float turnSpeed = 180f;

        private Rigidbody _body;
        private BoxCollider _box;
        private Vector3 _center;
        private Vector3 _halfSize;
        private GameObject _selectedVisual;
        private bool _warnedMissingModels;
        private readonly Dictionary<GameObject, CachedVisual> _visuals = new();

        private sealed class CachedVisual
        {
            public GameObject Instance;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        internal TrafficManager Controller { get; private set; }
        internal TrafficRoute Route { get; private set; }
        internal TrafficPose Pose { get; private set; }
        internal float Distance { get; private set; }
        internal float CruiseSpeed { get; private set; }
        public float CurrentSpeed { get; internal set; }
        internal float Acceleration => acceleration;
        internal float Braking => braking;
        internal Rigidbody Body => _body;

        private void Awake() => Initialize();

        internal void Initialize()
        {
            if (_body) return;
            _body = GetComponent<Rigidbody>();
            _box = GetComponent<BoxCollider>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _box.isTrigger = false;
            if (fitColliderOnAwake) FitColliderToVisuals();
            CacheFootprint();
        }

        internal bool PrepareForSpawn()
        {
            Initialize();
            if (!modelCatalog)
            {
                if (GetComponentInChildren<Renderer>(true)) return true;
                if (!_warnedMissingModels)
                {
                    Debug.LogWarning("[TrafficVehicle] Assign a Model Catalog or add a preplaced visual model.", this);
                    _warnedMissingModels = true;
                }
                return false;
            }
            if (!modelCatalog.TryPick(out var model))
            {
                if (!_warnedMissingModels)
                {
                    Debug.LogWarning("[TrafficVehicle] The model catalog needs a visual model with a Renderer and no TrafficVehicle/Rigidbody.", this);
                    _warnedMissingModels = true;
                }
                return false;
            }
            if (visualRoot && visualRoot != transform && !visualRoot.IsChildOf(transform))
            {
                Debug.LogWarning("[TrafficVehicle] Visual Root must belong to this car.", this);
                return false;
            }
            if (!visualRoot)
            {
                visualRoot = new GameObject("Visuals").transform;
                visualRoot.SetParent(transform, false);
            }
            // Hide the old choice before instantiating or fitting another model. The car itself
            // is still inactive here; it is activated only after its new footprint is accepted.
            for (var i = 0; i < visualRoot.childCount; i++)
                visualRoot.GetChild(i).gameObject.SetActive(false);

            if (!_visuals.TryGetValue(model, out var visual) || !visual.Instance)
            {
                var instance = Instantiate(model, visualRoot, false);
                visual = new CachedVisual
                {
                    Instance = instance,
                    Position = instance.transform.localPosition,
                    Rotation = instance.transform.localRotation,
                    Scale = instance.transform.localScale
                };
                // Imported visual colliders must not become extra, untracked traffic shapes.
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                _visuals[model] = visual;
            }
            _selectedVisual = visual.Instance;
            var modelTransform = _selectedVisual.transform;
            modelTransform.localPosition = visual.Position;
            modelTransform.localRotation = modelCatalog.ModelRotation * visual.Rotation;
            modelTransform.localScale = Vector3.Scale(visual.Scale, modelCatalog.ModelScale);
            _selectedVisual.SetActive(true);
            if (modelCatalog.CenterAndGround && TryGetVisualBounds(out var bounds, selectedModelOnly: true))
            {
                var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                modelTransform.position += transform.TransformVector(offset);
            }
            // Fit the chosen model and common attachments, excluding inactive cached choices.
            FitColliderToVisuals();
            CacheFootprint();
            return true;
        }

        [ContextMenu("Fit Box Collider To Visuals")]
        private void FitColliderToVisuals()
        {
            if (!TryGetVisualBounds(out var bounds)) return;
            var box = GetComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = new Vector3(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y),
                Mathf.Max(0.01f, bounds.size.z));
            if (Application.isPlaying) CacheFootprint();
        }

        private bool TryGetVisualBounds(out Bounds bounds, bool selectedModelOnly = false)
        {
            var renderers = selectedModelOnly && _selectedVisual
                ? _selectedVisual.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds();
            var initialized = false;
            foreach (var renderer in renderers)
            {
                if (_selectedVisual && !renderer.transform.IsChildOf(_selectedVisual.transform) &&
                    !IsActiveVisual(renderer.transform)) continue;
                var local = renderer.localBounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                    var point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return initialized;
        }

        private bool IsActiveVisual(Transform visual)
        {
            // The car root is inactive while checking a spawn; only child activeSelf states
            // identify which cached model is selected. Common wheels/attachments still count.
            for (var node = visual; node && node != transform; node = node.parent)
                if (!node.gameObject.activeSelf) return false;
            return true;
        }

        private void CacheFootprint()
        {
            var scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            _center = Vector3.Scale(_box.center, scale);
            _halfSize = Vector3.Scale(_box.size * 0.5f, scale);
        }

        internal void Begin(TrafficManager controller, TrafficRoute route, float speed, float startingDistance = 0f)
        {
            Controller = controller;
            Route = route;
            Distance = route.Path.NormalizeDistance(startingDistance);
            CurrentSpeed = 0f;
            CruiseSpeed = speed;
            var direction = route.Path.Direction(Distance);
            Pose = new TrafficPose(route.Path.Position(Distance), Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            transform.SetPositionAndRotation(Pose.Position, Pose.Rotation);
            _body.position = Pose.Position;
            _body.rotation = Pose.Rotation;
        }

        internal TrafficPose PoseAt(float distance, float seconds)
        {
            var direction = Route.Path.Direction(distance);
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            return new TrafficPose(Route.Path.Position(distance),
                Mathf.MoveTowardsAngle(Pose.Yaw, yaw, turnSpeed * seconds));
        }

        internal TrafficFootprint Footprint(float gap) => TrafficFootprint.At(Pose, _center, _halfSize, gap);
        internal TrafficFootprint Sweep(TrafficPose target, float gap) =>
            TrafficFootprint.Swept(Pose, target, _center, _halfSize, gap);

        internal void Commit(TrafficPose pose, float distance)
        {
            Pose = pose;
            Distance = Route.Path.NormalizeDistance(distance);
            _body.MovePosition(pose.Position);
            _body.MoveRotation(pose.Rotation);
        }

        internal void ResetForPool()
        {
            Controller = null;
            Route = null;
            CurrentSpeed = 0f;
            gameObject.SetActive(false);
        }
    }
}
