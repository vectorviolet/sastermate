using System;
using Sastermate.Core.App;
using UnityEngine;
using VectorViolet.ProPanelManager;

namespace Sastermate.UI
{
    /// <summary>Maps game states to visible Canvas panels and exposes panel actions for UI buttons.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/UI/Game Panel Presenter")]
    public sealed class GamePanelPresenter : GameStateListener
    {
        [Serializable]
        public sealed class PanelConfig
        {
            public CanvasPanel panel;
            public GameState[] visibleInStates = Array.Empty<GameState>();

            public bool Matches(GameState state) => visibleInStates != null &&
                Array.IndexOf(visibleInStates, state) >= 0;
        }

        [Tooltip("Keep these panel GameObjects active. Visibility uses their CanvasGroup, including input blocking.")]
        [SerializeField] private PanelConfig[] panels = Array.Empty<PanelConfig>();

        private IPanelService _service;

        protected override void OnEnable()
        {
            RegisterPanels();
            // Binding immediately reconciles the manager's current state.
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_service == null) return;
            foreach (var config in panels)
                if (config?.panel) _service.Unregister(config.panel.PanelId, config.panel);
            _service = null;
        }

        protected override void OnGameStateChanged(GameState state)
        {
            RegisterPanels();
            // Close outgoing surfaces before opening incoming ones.
            foreach (var config in panels)
                if (config?.panel && !config.Matches(state)) SetVisible(config.panel, false);
            foreach (var config in panels)
                if (config?.panel && config.Matches(state)) SetVisible(config.panel, true);
        }

        private void RegisterPanels()
        {
            _service ??= PanelService.Current;
            if (_service == null) return;
            foreach (var config in panels)
                if (config?.panel) _service.Register(config.panel.PanelId, config.panel);
        }

        private void SetVisible(CanvasPanel panel, bool visible)
        {
            if (panel.IsVisible == visible)
            {
                // Synchronize alpha authored in the scene before the panel's first Awake.
                if (visible) panel.Show();
                else panel.Hide();
                return;
            }
            if (_service != null)
            {
                if (visible) _service.Show(panel.PanelId);
                else _service.Hide(panel.PanelId);
            }
            else if (visible) panel.Show();
            else panel.Hide();
        }

        // String arguments can be assigned directly in Button.onClick.
        public void ShowPanel(string panelId)
        {
            RegisterPanels();
            _service?.Show(panelId);
        }

        public void HidePanel(string panelId)
        {
            RegisterPanels();
            _service?.Hide(panelId);
            if (panelId == "UpgradePanel" && GameManager.Instance) GameManager.Instance.ResumeGame();
        }

        public void ShowExclusivePanel(string panelId)
        {
            RegisterPanels();
            _service?.ShowExclusive(panelId);
        }

        public void TogglePanel(string panelId)
        {
            RegisterPanels();
            if (_service == null) return;
            if (_service.IsVisible(panelId)) HidePanel(panelId);
            else ShowPanel(panelId);
        }
    }
}
