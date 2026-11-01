using System;
using Sastermate.Core.Events;
using UnityEngine;

namespace Sastermate.Gameplay.Tasks
{
    /// <summary>Credits carbon points for successful tasks of any type.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class TaskRewardManager : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingCarbonPoints;

        private CarbonPointLedger _ledger;

        public int CarbonPoints => _ledger?.Balance ?? Mathf.Max(0, startingCarbonPoints);
        public event Action<int> CarbonPointsChanged;

        private void Awake() => _ledger = new CarbonPointLedger(Mathf.Max(0, startingCarbonPoints));

        private void OnEnable() => EventBus.Game.OnTaskCompleted += HandleTaskCompleted;

        private void OnDisable() => EventBus.Game.OnTaskCompleted -= HandleTaskCompleted;

        private void Start() => CarbonPointsChanged?.Invoke(CarbonPoints);

        private void HandleTaskCompleted(TaskDefinition task, Guid taskInstanceId, Vector3? worldPosition)
        {
            if (!task || _ledger == null) return;
            var points = task.CarbonPoints;
            if (!_ledger.TryAward(taskInstanceId, points)) return;
            CarbonPointsChanged?.Invoke(CarbonPoints);
            if (points > 0 && worldPosition.HasValue)
                EventBus.Game.RaiseCarbonPointsAwarded(points, worldPosition.Value);
        }

        public bool TrySpendCarbonPoints(int amount)
        {
            if (_ledger == null || !_ledger.TrySpend(amount)) return false;
            CarbonPointsChanged?.Invoke(CarbonPoints);
            return true;
        }
    }
}
