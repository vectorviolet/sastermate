using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
{
    /// <summary>Reserves a crossing or tight bend until the car's whole footprint leaves the area.</summary>
    [DisallowMultipleComponent]
    public sealed class TrafficJunction : MonoBehaviour
    {
        [Tooltip("Cover the full crossing or sharp bend, including the space a turning car's rear needs.")]
        [SerializeField] private Vector3 size = new(1f, 2f, 1f);
        [SerializeField] private Vector3 center;

        internal TrafficVehicle Owner { get; private set; }
        private readonly TrafficGate _gate = new();
        internal TrafficFootprint Area => new(
            transform.TransformPoint(center), Vector3.Scale(size * 0.5f, Abs(transform.lossyScale)),
            transform.eulerAngles.y);

        internal bool Allows(TrafficVehicle vehicle) => _gate.Allows(vehicle.GetInstanceID());
        internal void Reserve(TrafficVehicle vehicle)
        {
            if (_gate.TryReserve(vehicle.GetInstanceID())) Owner = vehicle;
        }
        internal void Release(TrafficVehicle vehicle)
        {
            if (Owner != vehicle) return;
            _gate.Clear();
            Owner = null;
        }

        internal void ReleaseIfClear(float gap)
        {
            if (!Owner || !Owner.gameObject.activeInHierarchy || !Area.Overlaps(Owner.Footprint(gap)))
            {
                _gate.Clear();
                Owner = null;
            }
        }

        private void OnDisable() { _gate.Clear(); Owner = null; }
        private void OnValidate() => size = new Vector3(Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));

        private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private void OnDrawGizmos()
        {
            var old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.15f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = old;
        }
    }
}
