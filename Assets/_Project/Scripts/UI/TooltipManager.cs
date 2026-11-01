using Sastermate.Core.App;
using Sastermate.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sastermate.UI
{
    /// <summary>Scene-owned, non-interactive tooltip adapted from Soulbonk's shared hover popup.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("Sastermate/UI/Tooltip Manager")]
    public sealed class TooltipManager : GameStateListener
    {
        [SerializeField] private CanvasGroup tooltipGroup;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private Vector2 tooltipOffset = new(15f, -15f);
        [SerializeField, Min(0f)] private float hoverDelay = 0.35f;
        [SerializeField, Min(100f)] private float maximumWidth = 380f;
        [SerializeField, Min(0f)] private float screenPadding = 8f;

        private MonoBehaviour _owner;
        private CanvasGroup[] _ownerGroups;
        private RectTransform _tooltipRect;
        private RectTransform _canvasRect;
        private Canvas _canvas;
        private float _showAt;
        private bool _visible;
        private bool _layoutDirty;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (!tooltipGroup || !tooltipText || !_canvas)
            {
                Debug.LogWarning("[TooltipManager] Use the prewired CanvasTooltips prefab or assign its group and text.", this);
                enabled = false;
                return;
            }
            _canvasRect = (RectTransform)transform;
            _tooltipRect = (RectTransform)tooltipGroup.transform;
            tooltipGroup.interactable = false;
            tooltipGroup.blocksRaycasts = false;
            foreach (var graphic in tooltipGroup.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            HideCurrent();
        }

        protected override void OnEnable()
        {
            EventBus.Tooltip.OnShowRequested += RequestShow;
            EventBus.Tooltip.OnUpdateRequested += UpdateText;
            EventBus.Tooltip.OnHideRequested += RequestHide;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            EventBus.Tooltip.OnShowRequested -= RequestShow;
            EventBus.Tooltip.OnUpdateRequested -= UpdateText;
            EventBus.Tooltip.OnHideRequested -= RequestHide;
            HideCurrent();
            base.OnDisable();
        }

        protected override void OnGameStateChanged(GameState state) => HideCurrent();

        private void RequestShow(MonoBehaviour owner, string text, bool immediate)
        {
            if (!tooltipGroup || !tooltipText || !owner || string.IsNullOrWhiteSpace(text)) return;
            if (_owner != owner)
            {
                HideCurrent();
                _owner = owner;
                _ownerGroups = owner.GetComponentsInParent<CanvasGroup>(true);
                _showAt = Time.unscaledTime + Mathf.Max(0f, hoverDelay);
            }
            UpdateText(owner, text);
            if (immediate) _showAt = Time.unscaledTime;
        }

        private void UpdateText(MonoBehaviour owner, string text)
        {
            if (_owner != owner || !tooltipText || tooltipText.text == text) return;
            tooltipText.text = text;
            _layoutDirty = true;
        }

        private void RequestHide(MonoBehaviour owner)
        {
            if (_owner == owner) HideCurrent();
        }

        private void LateUpdate()
        {
            if (!_owner || !OwnerIsVisible() || Pointer.current == null)
            {
                HideCurrent();
                return;
            }
            if (Time.unscaledTime < _showAt) return;
            if (!_visible)
            {
                tooltipGroup.alpha = 1f;
                _visible = true;
                _layoutDirty = true;
            }

            var width = Mathf.Max(1f, Mathf.Min(maximumWidth, _canvasRect.rect.width - screenPadding * 2f));
            if (!Mathf.Approximately(_tooltipRect.rect.width, width))
            {
                _tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                _layoutDirty = true;
            }
            if (_layoutDirty)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);
                _layoutDirty = false;
            }

            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect,
                    Pointer.current.position.ReadValue(), uiCamera, out var point)) return;
            // Prefab anchors are centered and its tooltip pivot is top-left.
            _tooltipRect.anchoredPosition = ConstrainPosition(point, tooltipOffset, _tooltipRect.rect.size,
                _canvasRect.rect, screenPadding);
        }

        private bool OwnerIsVisible()
        {
            if (!_owner.isActiveAndEnabled) return false;
            // CanvasPanel hides by alpha without disabling its buttons.
            foreach (var group in _ownerGroups)
                if (group && group.isActiveAndEnabled && group.alpha <= 0.001f) return false;
            return true;
        }

        private void HideCurrent()
        {
            if (tooltipGroup) tooltipGroup.alpha = 0f;
            _owner = null;
            _ownerGroups = null;
            _visible = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) HideCurrent();
        }

        internal static Vector2 ConstrainPosition(Vector2 pointer, Vector2 offset, Vector2 size, Rect bounds, float padding)
        {
            var position = pointer + offset;
            if (position.x + size.x > bounds.xMax - padding) position.x = pointer.x - Mathf.Abs(offset.x) - size.x;
            if (position.y - size.y < bounds.yMin + padding) position.y = pointer.y + Mathf.Abs(offset.y) + size.y;
            position.x = Mathf.Clamp(position.x, bounds.xMin + padding, Mathf.Max(bounds.xMin + padding, bounds.xMax - padding - size.x));
            position.y = Mathf.Clamp(position.y, Mathf.Min(bounds.yMax - padding, bounds.yMin + padding + size.y), bounds.yMax - padding);
            return position;
        }
    }
}
