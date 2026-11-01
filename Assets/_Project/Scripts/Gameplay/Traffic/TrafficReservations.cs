using System.Collections.Generic;

namespace Sastermate.Gameplay.Traffic
{
    /// <summary>Current footprints become whole-movement reservations as cars advance this tick.</summary>
    internal sealed class TrafficReservations
    {
        private readonly Dictionary<int, TrafficFootprint> _occupied = new();

        public void BeginStep() => _occupied.Clear();
        public void Occupy(int vehicleId, TrafficFootprint area) => _occupied[vehicleId] = area;
        public void Release(int vehicleId) => _occupied.Remove(vehicleId);

        public bool IsClear(int vehicleId, TrafficFootprint candidate)
        {
            foreach (var entry in _occupied)
                if (entry.Key != vehicleId && candidate.Overlaps(entry.Value)) return false;
            return true;
        }
    }
}
