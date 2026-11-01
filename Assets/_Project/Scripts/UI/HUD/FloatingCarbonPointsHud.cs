using System.Collections.Generic;
using FactorySabotage.Core.App;
using FactorySabotage.Core.Events;
using TMPro;
using UnityEngine;

namespace FactorySabotage.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(1100)]
    public sealed class FloatingCarbonPointsHud : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TextMeshProUGUI textPrefab;
        [SerializeField, Min(0.1f)] private float lifetime = 1.3f;
        [Tooltip("Upward travel in Canvas reference pixels.")]
        [SerializeField, Min(0f)] private float riseDistance = 64f;
        [SerializeField, Min(0f)] private float worldHeightOffset = 0.2f;
        [SerializeField, Range(0f, 0.95f)] private float fadeStart = 0.35f;
        [SerializeField, Min(0)] private int prewarmCount = 3;
        [SerializeField, Min(1)] private int maximumVisible = 12;

        private sealed class Popup
        {
            public TextMeshProUGUI Label;
            public Vector3 WorldPosition;
            public float Age;
        }

        private readonly List<Popup> _active = new();
        private readonly Stack<Popup> _pool = new();
        private RectTransform _root;
        private Canvas _canvas;

        public int ActivePopupCount => _active.Count;
        public int PooledPopupCount => _pool.Count;

        private void Awake()
        {
            _root = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (!_canvas || !textPrefab)
            {
                Debug.LogWarning("[FloatingCarbonPointsHud] Assign a text prefab and place the HUD inside a Canvas.", this);
                enabled = false;
                return;
            }
            _canvas = _canvas.rootCanvas;
            for (var i = 0; i < Mathf.Clamp(prewarmCount, 0, Mathf.Max(1, maximumVisible)); i++)
                _pool.Push(CreatePopup());
        }

        private void OnEnable() => EventBus.Game.OnCarbonPointsAwarded += Show;

        private void OnDisable()
        {
            EventBus.Game.OnCarbonPointsAwarded -= Show;
            while (_active.Count > 0) Release(_active.Count - 1);
        }

        private Popup CreatePopup()
        {
            var label = Instantiate(textPrefab, transform, false);
            label.raycastTarget = false;
            // ScreenPointToLocalPoint returns coordinates relative to this root's pivot.
            label.rectTransform.anchorMin = _root.pivot;
            label.rectTransform.anchorMax = _root.pivot;
            label.gameObject.SetActive(false);
            return new Popup { Label = label };
        }

        private void Show(int amount, Vector3 worldPosition)
        {
            if (amount <= 0) return;
            if (_active.Count >= Mathf.Max(1, maximumVisible)) Release(0);
            var popup = _pool.Count > 0 ? _pool.Pop() : CreatePopup();
            popup.Age = 0f;
            // Store a position rather than parenting to the task's object, which may be disabled.
            popup.WorldPosition = worldPosition + Vector3.up * worldHeightOffset;
            popup.Label.text = $"+{amount:N0} CP";
            popup.Label.alpha = 1f;
            popup.Label.rectTransform.localScale = Vector3.one;
            _active.Add(popup);
            PositionPopup(popup, 0f);
            popup.Label.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!worldCamera) worldCamera = Camera.main;
            var gameplay = GameManager.IsGameplay;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var popup = _active[i];
                if (!gameplay)
                {
                    popup.Label.enabled = false;
                    continue;
                }
                popup.Age += Time.deltaTime;
                var progress = popup.Age / Mathf.Max(0.1f, lifetime);
                if (progress >= 1f) { Release(i); continue; }
                PositionPopup(popup, progress);
            }
        }

        private void PositionPopup(Popup popup, float progress)
        {
            if (!worldCamera || !GameManager.IsGameplay)
            {
                popup.Label.enabled = false;
                return;
            }
            var viewport = worldCamera.WorldToViewportPoint(popup.WorldPosition);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
            {
                popup.Label.enabled = false;
                return;
            }
            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,
                    worldCamera.WorldToScreenPoint(popup.WorldPosition), uiCamera, out var point))
            {
                popup.Label.enabled = false;
                return;
            }
            popup.Label.enabled = true;
            var rise = 1f - (1f - progress) * (1f - progress);
            popup.Label.rectTransform.anchoredPosition = point + Vector2.up * (riseDistance * rise);
            popup.Label.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, progress));
        }

        private void Release(int index)
        {
            var popup = _active[index];
            popup.Label.gameObject.SetActive(false);
            _active.RemoveAt(index);
            _pool.Push(popup);
        }
    }
}
