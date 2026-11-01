using System;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Gameplay.Tasks;
using UnityEngine;

namespace Sastermate.Gameplay.Fire
{
    [DisallowMultipleComponent]
    public sealed class BurnableObject : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float waterToExtinguish = 3f;
        [SerializeField, Min(0f)] private float reigniteCooldown = 20f;
        [SerializeField, Min(0.01f)] private float fireScale = 0.5f;
        [Tooltip("Optional custom flame origin. Otherwise uses the object's rendered bounds.")]
        [SerializeField] private Transform fireAnchor;
        [SerializeField, Range(0f, 1f)] private float fireHeight = 1f;

        [Header("Fire Visibility")]
        [Tooltip("Place the visible fire on the camera-facing surface of the rendered bounds. Useful for dense tree canopies.")]
        [SerializeField] private bool cameraFacingFire;
        [SerializeField, Min(0f)] private float cameraFacingFirePadding = 0.05f;

        private MeshRenderer[] _fireRenderers;
        private Vector3 _localFirePoint;
        private float _waterRemaining;
        private float _cooldownRemaining;
        private TaskDefinition _extinguishTask;
        private Guid _taskInstanceId;

        public bool IsBurning { get; private set; }
        public bool CanIgnite => isActiveAndEnabled && !IsBurning && _cooldownRemaining <= 0f;
        public float FireIntensity => IsBurning ? Mathf.Clamp01(_waterRemaining / Mathf.Max(0.1f, waterToExtinguish)) : 0f;
        public float FireScale => fireScale;
        public Vector3 FirePosition => fireAnchor ? fireAnchor.position : transform.TransformPoint(_localFirePoint);

        internal Vector3 GetFireEffectPosition(Camera worldCamera)
        {
            var origin = FirePosition;
            if (!cameraFacingFire || !worldCamera || _fireRenderers == null) return origin;
            var initialized = false;
            var bounds = new Bounds();
            foreach (var renderer in _fireRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!initialized) return origin;

            // Moving along the viewing ray preserves the flame's screen position while
            // bringing it out of the opaque canopy. Normal depth testing still applies.
            var direction = worldCamera.orthographic
                ? -worldCamera.transform.forward
                : (worldCamera.transform.position - origin).normalized;
            var radius = Vector3.Dot(bounds.extents,
                new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
            var distance = radius - Vector3.Dot(origin - bounds.center, direction) +
                           Mathf.Max(0f, cameraFacingFirePadding);
            return origin + direction * Mathf.Max(0f, distance);
        }

        // A model picker can update the visuals without replacing the burnable target itself.
        internal void ConfigureFireVisuals(Transform anchor, float scale)
        {
            fireAnchor = anchor;
            fireScale = Mathf.Max(0.01f, scale);
        }

        private void Awake()
        {
            var renderers = _fireRenderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var point = bounds.center;
            point.y = Mathf.Lerp(bounds.min.y, bounds.max.y, fireHeight);
            _localFirePoint = transform.InverseTransformPoint(point);
        }

        private void Update()
        {
            if (GameManager.IsGameplay && _cooldownRemaining > 0f)
                _cooldownRemaining -= Time.deltaTime;
        }

        internal void Ignite(TaskDefinition extinguishTask = null)
        {
            IsBurning = true;
            _waterRemaining = Mathf.Max(0.1f, waterToExtinguish);
            _extinguishTask = extinguishTask;
            _taskInstanceId = Guid.NewGuid();
        }

        public void ApplyWater(float amount)
        {
            if (!IsBurning || !GameManager.IsGameplay || amount <= 0f) return;
            _waterRemaining = Mathf.Max(0f, _waterRemaining - amount);
            if (_waterRemaining <= 0f) ResolveFire(successfullyExtinguished: true);
        }

        /// <summary>Clears a fire for cleanup without awarding a completed-task reward.</summary>
        public void Extinguish() => ResolveFire(successfullyExtinguished: false);

        private void ResolveFire(bool successfullyExtinguished)
        {
            if (!IsBurning) return;
            var completedTask = _extinguishTask;
            var completedTaskId = _taskInstanceId;
            var completedPosition = FirePosition;
            IsBurning = false;
            _waterRemaining = 0f;
            _cooldownRemaining = reigniteCooldown;
            _extinguishTask = null;
            _taskInstanceId = Guid.Empty;
            EventBus.Game.RaiseFireExtinguished(this);
            if (successfullyExtinguished)
            {
                EventBus.Game.RaiseFireSuccessfullyExtinguished(completedPosition);
                if (completedTask)
                    EventBus.Game.RaiseTaskCompleted(completedTask, completedTaskId, completedPosition);
            }
        }

        private void OnDisable() => Extinguish();
    }
}
