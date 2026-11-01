using System.Collections.Generic;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Gameplay.Fire;
using UnityEngine;

namespace Sastermate.UI.HUD
{
    /// <summary>Shows a pooled warning at the screen edge for each off-screen fire.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(1000)]
    public sealed class FireWarningHud : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private FireWarningMarker markerPrefab;
        [Tooltip("Gap between the full marker (including its arrow) and the screen's safe area, in Canvas reference pixels.")]
        [SerializeField, Min(0f)] private float edgePadding = 16f;
        [Tooltip("Half the marker's full size, including its rotating arrow, in Canvas reference pixels.")]
        [SerializeField, Min(1f)] private float markerRadius = 48f;
        [Tooltip("Separation between nearby warnings on the same edge, in Canvas reference pixels.")]
        [SerializeField, Min(1f)] private float minimumMarkerSpacing = 88f;

        private sealed class Warning
        {
            public BurnableObject Target;
            public FireWarningMarker Marker;
            public Vector2 ScreenPoint;
            public Vector2 TargetScreenPoint;
            public int Edge; // Left, right, bottom, top.
            public float Coordinate;
        }

        private readonly Dictionary<BurnableObject, Warning> _warnings = new();
        private readonly Stack<FireWarningMarker> _pool = new();
        private readonly List<BurnableObject> _stale = new();
        private readonly List<Warning> _visible = new();
        private RectTransform _root;
        private Canvas _canvas;

        public int TrackedFireCount => _warnings.Count;
        public int VisibleMarkerCount { get; private set; }

        private void Awake()
        {
            _root = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (!_canvas || !markerPrefab)
            {
                Debug.LogWarning("[FireWarningHud] Assign a marker prefab and place this HUD inside a Canvas.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            EventBus.Game.OnFireIgnited += Track;
            EventBus.Game.OnFireExtinguished += Untrack;
            // Recover existing fires when the HUD is enabled after gameplay has started.
            foreach (var target in FindObjectsByType<BurnableObject>(FindObjectsSortMode.None))
                if (target.IsBurning) Track(target);
        }

        private void OnDisable()
        {
            EventBus.Game.OnFireIgnited -= Track;
            EventBus.Game.OnFireExtinguished -= Untrack;
            foreach (var warning in _warnings.Values) Release(warning.Marker);
            _warnings.Clear();
            _visible.Clear();
            VisibleMarkerCount = 0;
        }

        private void Track(BurnableObject target)
        {
            if (!target || !target.IsBurning || !markerPrefab || _warnings.ContainsKey(target)) return;
            var marker = _pool.Count > 0 ? _pool.Pop() : Instantiate(markerPrefab, transform, false);
            marker.name = "Fire Warning - " + target.name;
            marker.Bind(target);
            _warnings.Add(target, new Warning { Target = target, Marker = marker });
        }

        private void Untrack(BurnableObject target)
        {
            if (ReferenceEquals(target, null) || !_warnings.Remove(target, out var warning)) return;
            Release(warning.Marker);
        }

        private void Release(FireWarningMarker marker)
        {
            if (!marker) return;
            marker.Bind(null);
            // Keep markers under the HUD, including when a burning object is disabled/destroyed.
            _pool.Push(marker);
        }

        private void LateUpdate()
        {
            _visible.Clear();
            _stale.Clear();
            VisibleMarkerCount = 0;
            if (!worldCamera) worldCamera = Camera.main;
            var show = GameManager.IsGameplay && worldCamera && _canvas;
            var screenRect = show ? GetVisibleScreenRect(worldCamera) : default;
            var scale = _canvas ? Mathf.Max(0.001f, _canvas.rootCanvas.scaleFactor) : 1f;
            var inset = (edgePadding + markerRadius) * scale;
            var bounds = Rect.MinMaxRect(
                screenRect.xMin + Mathf.Min(inset, screenRect.width * 0.49f),
                screenRect.yMin + Mathf.Min(inset, screenRect.height * 0.49f),
                screenRect.xMax - Mathf.Min(inset, screenRect.width * 0.49f),
                screenRect.yMax - Mathf.Min(inset, screenRect.height * 0.49f));

            foreach (var warning in _warnings.Values)
            {
                if (!warning.Target || !warning.Target.isActiveAndEnabled || !warning.Target.IsBurning)
                {
                    _stale.Add(warning.Target);
                    continue;
                }
                if (!show || screenRect.width <= 0f || screenRect.height <= 0f ||
                    !Project(warning, screenRect, bounds))
                {
                    warning.Marker.SetVisible(false);
                    continue;
                }
                _visible.Add(warning);
            }
            foreach (var target in _stale) Untrack(target);
            if (_visible.Count == 0) return;

            SeparateMarkers(bounds, minimumMarkerSpacing * scale);
            var uiCamera = _canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.rootCanvas.worldCamera;
            foreach (var warning in _visible)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,
                        warning.ScreenPoint, uiCamera, out var local)) continue;
                warning.Marker.Place(local, warning.TargetScreenPoint - warning.ScreenPoint);
                VisibleMarkerCount++;
            }
        }

        private static Rect GetVisibleScreenRect(Camera camera)
        {
            var viewport = camera.pixelRect;
            var safe = Screen.safeArea;
            return Rect.MinMaxRect(Mathf.Max(viewport.xMin, safe.xMin),
                Mathf.Max(viewport.yMin, safe.yMin), Mathf.Min(viewport.xMax, safe.xMax),
                Mathf.Min(viewport.yMax, safe.yMax));
        }

        private bool Project(Warning warning, Rect screenRect, Rect bounds)
        {
            var projected = worldCamera.WorldToScreenPoint(warning.Target.FirePosition);
            var point = new Vector2(projected.x, projected.y);
            if (projected.z >= worldCamera.nearClipPlane && projected.z <= worldCamera.farClipPlane &&
                screenRect.Contains(point)) return false;

            var center = screenRect.center;
            var direction = point - center;
            // Perspective projection mirrors points behind the camera. Orthographic projection does not.
            if (projected.z < 0f && !worldCamera.orthographic) direction = -direction;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;

            var horizontal = Mathf.Abs(direction.x) > 0.001f
                ? bounds.width * 0.5f / Mathf.Abs(direction.x) : float.PositiveInfinity;
            var vertical = Mathf.Abs(direction.y) > 0.001f
                ? bounds.height * 0.5f / Mathf.Abs(direction.y) : float.PositiveInfinity;
            warning.ScreenPoint = center + direction * Mathf.Min(horizontal, vertical);
            warning.Edge = horizontal < vertical ? (direction.x < 0f ? 0 : 1) : (direction.y < 0f ? 2 : 3);
            warning.Coordinate = warning.Edge < 2 ? warning.ScreenPoint.y : warning.ScreenPoint.x;
            // Keep a useful outward direction even for targets outside the near/far clipping planes.
            warning.TargetScreenPoint = projected.z >= worldCamera.nearClipPlane &&
                                        projected.z <= worldCamera.farClipPlane
                ? point
                : center + direction.normalized * Mathf.Max(direction.magnitude,
                    Mathf.Max(screenRect.width, screenRect.height) * 2f);
            return true;
        }

        private void SeparateMarkers(Rect bounds, float preferredSpacing)
        {
            _visible.Sort((a, b) =>
            {
                var edge = a.Edge.CompareTo(b.Edge);
                if (edge != 0) return edge;
                var position = a.Coordinate.CompareTo(b.Coordinate);
                return position != 0 ? position : a.Target.GetInstanceID().CompareTo(b.Target.GetInstanceID());
            });
            for (var start = 0; start < _visible.Count;)
            {
                var edge = _visible[start].Edge;
                var end = start + 1;
                while (end < _visible.Count && _visible[end].Edge == edge) end++;
                var min = edge < 2 ? bounds.yMin : bounds.xMin;
                var max = edge < 2 ? bounds.yMax : bounds.xMax;
                var spacing = end - start > 1 ? Mathf.Min(preferredSpacing, (max - min) / (end - start - 1)) : 0f;
                var cursor = min - spacing;
                for (var i = start; i < end; i++)
                {
                    _visible[i].Coordinate = Mathf.Max(_visible[i].Coordinate, cursor + spacing);
                    cursor = _visible[i].Coordinate;
                }
                cursor = max + spacing;
                for (var i = end - 1; i >= start; i--)
                {
                    var warning = _visible[i];
                    warning.Coordinate = Mathf.Min(warning.Coordinate, cursor - spacing);
                    cursor = warning.Coordinate;
                    warning.ScreenPoint = edge < 2
                        ? new Vector2(warning.ScreenPoint.x, warning.Coordinate)
                        : new Vector2(warning.Coordinate, warning.ScreenPoint.y);
                }
                start = end;
            }
        }
    }
}
