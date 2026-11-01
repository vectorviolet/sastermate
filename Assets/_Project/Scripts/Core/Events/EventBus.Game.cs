using System;
using Sastermate.Gameplay.Fire;
using Sastermate.Gameplay.Tasks;
using UnityEngine;

namespace Sastermate.Core.Events
{
    public static partial class EventBus
    {
        public static class Game
        {
            public static event Action<BurnableObject> OnFireIgnited;
            public static event Action<BurnableObject> OnFireExtinguished;

            public static void RaiseFireIgnited(BurnableObject target) => OnFireIgnited?.Invoke(target);
            public static void RaiseFireExtinguished(BurnableObject target) => OnFireExtinguished?.Invoke(target);

            /// <summary>Water completed a fire; cleanup and disabling objects do not raise this.</summary>
            public static event Action<Vector3> OnFireSuccessfullyExtinguished;

            public static void RaiseFireSuccessfullyExtinguished(Vector3 worldPosition) =>
                OnFireSuccessfullyExtinguished?.Invoke(worldPosition);

            /// <summary>Successful completion of one task instance; reuse its ID for any repeated notifications.</summary>
            public static event Action<TaskDefinition, Guid, Vector3?> OnTaskCompleted;

            public static void RaiseTaskCompleted(TaskDefinition task, Guid taskInstanceId,
                Vector3? worldPosition = null) =>
                OnTaskCompleted?.Invoke(task, taskInstanceId, worldPosition);

            /// <summary>Published only after a positioned task's CP reward is credited.</summary>
            public static event Action<int, Vector3> OnCarbonPointsAwarded;

            public static void RaiseCarbonPointsAwarded(int amount, Vector3 worldPosition) =>
                OnCarbonPointsAwarded?.Invoke(amount, worldPosition);

            /// <summary>Raised for the nearest water-ray hit with the amount of water applied this frame.</summary>
            public static event Action<RaycastHit, float> OnWaterHit;

            public static void RaiseWaterHit(RaycastHit hit, float waterAmount) =>
                OnWaterHit?.Invoke(hit, waterAmount);

            /// <summary>
            ///     Raised once after a save has been restored (souls, upgrade build, etc.). UI that
            ///     cached values at bind time (before the restore ran) should re-read on this.
            /// </summary>
            public static event Action OnSaveLoaded;

            public static void TriggerSaveLoaded() => OnSaveLoaded?.Invoke();
        }
    }
}
