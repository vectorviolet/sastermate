using System;
using System.Collections.Generic;

namespace FactorySabotage.Gameplay.Tasks
{
    /// <summary>Session balance with one reward per completed task instance.</summary>
    public sealed class CarbonPointLedger
    {
        private readonly HashSet<Guid> _rewardedTasks = new();

        public int Balance { get; private set; }

        public CarbonPointLedger(int startingPoints = 0)
        {
            if (startingPoints < 0) throw new ArgumentOutOfRangeException(nameof(startingPoints));
            Balance = startingPoints;
        }

        public bool TryAward(Guid taskInstanceId, int points)
        {
            if (taskInstanceId == Guid.Empty || points < 0 || points > int.MaxValue - Balance ||
                !_rewardedTasks.Add(taskInstanceId)) return false;
            Balance += points;
            return true;
        }

        public bool TrySpend(int points)
        {
            if (points < 0 || points > Balance) return false;
            Balance -= points;
            return true;
        }
    }
}
