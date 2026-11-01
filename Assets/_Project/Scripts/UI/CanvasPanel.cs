using UnityEngine;
using VectorViolet.ProPanelManager;

namespace Sastermate.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/UI/Canvas Panel")]
    public sealed class CanvasPanel : PanelBase
    {
        private bool _initialized;

        protected override void Awake() => EnsureInitialized();

        public override void Show()
        {
            EnsureInitialized();
            base.Show();
        }

        public override void Hide()
        {
            EnsureInitialized();
            base.Hide();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            // A state presenter can address an active panel before its own Awake runs.
            base.Awake();
        }
    }
}
