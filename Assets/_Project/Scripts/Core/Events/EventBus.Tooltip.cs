using System;
using UnityEngine;

namespace Sastermate.Core.Events
{
    public static partial class EventBus
    {
        // Soulbonk's tooltip request pattern, with an owner so a late pointer exit cannot hide another button.
        public static class Tooltip
        {
            public static event Action<MonoBehaviour, string, bool> OnShowRequested;
            public static event Action<MonoBehaviour, string> OnUpdateRequested;
            public static event Action<MonoBehaviour> OnHideRequested;

            public static void RequestShow(MonoBehaviour owner, string text, bool immediate = false) =>
                OnShowRequested?.Invoke(owner, text, immediate);
            public static void RequestUpdate(MonoBehaviour owner, string text) => OnUpdateRequested?.Invoke(owner, text);
            public static void RequestHide(MonoBehaviour owner) => OnHideRequested?.Invoke(owner);
        }
    }
}
