using UnityEngine;
using VectorViolet.Core.Events;

namespace FactorySabotage.Core.Events
{
    /// <summary>
    ///     Centralized event hub for decoupled communication between systems.
    ///     Uses static actions to prevent tight coupling between managers.
    /// </summary>
    public static partial class EventBus
    {
        /// <summary>
        ///     Clears every subscription on every nested event group.
        ///     Runs automatically before scene load on each play so that handlers
        ///     left over from a previous session (when "Enter Play Mode Options"
        ///     disables domain reload) cannot fire twice or leak. Also callable
        ///     manually, e.g. on a hard restart.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => StaticDelegateReset.ClearAll(typeof(EventBus));
    }
}