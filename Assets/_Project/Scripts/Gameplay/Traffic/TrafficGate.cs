namespace FactorySabotage.Gameplay.Traffic
{
    /// <summary>Exclusive access to a crossing or a tight turn.</summary>
    internal sealed class TrafficGate
    {
        public int Owner { get; private set; }
        public bool Allows(int vehicleId) => Owner == 0 || Owner == vehicleId;

        public bool TryReserve(int vehicleId)
        {
            if (vehicleId == 0 || !Allows(vehicleId)) return false;
            Owner = vehicleId;
            return true;
        }

        public void Release(int vehicleId) { if (Owner == vehicleId) Owner = 0; }
        public void Clear() => Owner = 0;
    }
}
