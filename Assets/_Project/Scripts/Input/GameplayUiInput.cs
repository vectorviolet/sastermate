using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VectorViolet.ProPanelManager;

namespace Sastermate.Input
{
    public static class GameplayUiInput
    {
        private static readonly List<RaycastResult> Hits = new();
        private static EventSystem _eventSystem;
        private static PointerEventData _pointerData;

        public static bool IsUpgradePanelOpen => PanelService.Current?.IsVisible("UpgradePanel") == true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Hits.Clear();
            _eventSystem = null;
            _pointerData = null;
        }

        // Called from Update, after input callbacks. IsPointerOverGameObject inside an
        // InputAction callback would use stale UI state and produce an Input System warning.
        public static bool IsAttackBlocked()
        {
            if (IsUpgradePanelOpen) return true;
            var system = EventSystem.current;
            if (!system || Pointer.current == null) return false;
            if (_eventSystem != system)
            {
                _eventSystem = system;
                _pointerData = new PointerEventData(system);
            }
            _pointerData.position = Pointer.current.position.ReadValue();
            Hits.Clear();
            system.RaycastAll(_pointerData, Hits);
            foreach (var hit in Hits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }
    }
}
