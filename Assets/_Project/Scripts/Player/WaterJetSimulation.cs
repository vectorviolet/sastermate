using System;
using UnityEngine;

namespace Sastermate.Player
{
    /// <summary>Fixed-step water parcels: gravity, finite travel, and swept collision.</summary>
    internal sealed class WaterJetSimulation
    {
        internal const int Capacity = 128;

        private struct Parcel
        {
            public Vector3 origin, position, velocity;
            public float water, range, radius, age, lifetime;
        }

        private readonly Parcel[] _parcels = new Parcel[Capacity];
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        public int Count { get; private set; }

        public void Clear() => Count = 0;

        public void Emit(Vector3 origin, Vector3 velocity, float water, float range, float radius)
        {
            if (Count == Capacity) return;
            _parcels[Count++] = new Parcel
            {
                origin = origin, position = origin, velocity = velocity,
                water = water, range = range, radius = radius,
                lifetime = 2f * range / Mathf.Max(0.1f, velocity.magnitude) + 0.5f
            };
        }

        public void Step(float dt, Vector3 gravity, LayerMask mask, Transform owner,
            Action<RaycastHit, Vector3, float> impact)
        {
            for (var i = 0; i < Count;)
            {
                var parcel = _parcels[i];
                var displacement = parcel.velocity * dt + gravity * (0.5f * dt * dt);
                var next = parcel.position + displacement;
                var exhausted = (next - parcel.origin).sqrMagnitude > parcel.range * parcel.range;
                if (exhausted)
                {
                    // Clip the sweep at the range boundary, before checking collisions.
                    var offset = parcel.position - parcel.origin;
                    var a = displacement.sqrMagnitude;
                    var b = 2f * Vector3.Dot(offset, displacement);
                    var c = offset.sqrMagnitude - parcel.range * parcel.range;
                    var fraction = a > 0.000001f
                        ? Mathf.Clamp01((-b + Mathf.Sqrt(Mathf.Max(0f, b * b - 4f * a * c))) / (2f * a))
                        : 0f;
                    displacement *= fraction;
                }

                var distance = displacement.magnitude;
                if (distance > 0.000001f && FindHit(new Ray(parcel.position, displacement / distance),
                        parcel.radius, distance, mask, owner, out var hit))
                {
                    impact(hit, parcel.velocity + gravity * dt, parcel.water);
                    RemoveAt(i);
                    continue;
                }

                parcel.age += dt;
                if (exhausted || parcel.age >= parcel.lifetime)
                {
                    RemoveAt(i);
                    continue;
                }
                parcel.position += displacement;
                parcel.velocity += gravity * dt;
                _parcels[i++] = parcel;
            }
        }

        public int CopyPositions(Vector3[] destination, int offset)
        {
            // Newest water is closest to the nozzle; the oldest forms the moving tip.
            for (var i = Count - 1; i >= 0; i--) destination[offset++] = _parcels[i].position;
            return offset;
        }

        private void RemoveAt(int index)
        {
            Count--;
            if (index < Count) Array.Copy(_parcels, index + 1, _parcels, index, Count - index);
        }

        private bool FindHit(Ray ray, float radius, float distance, LayerMask mask,
            Transform owner, out RaycastHit closest)
        {
            var hits = _hits;
            var count = Physics.SphereCastNonAlloc(ray, radius, hits, distance, mask,
                QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(ray, radius, distance, mask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            closest = default;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || (owner && hit.collider.transform.IsChildOf(owner))) continue;
                if (hit.distance >= nearest) continue;
                closest = hit;
                nearest = hit.distance;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        internal static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, float speed, Vector3 gravity)
        {
            var delta = target - origin;
            if (delta.sqrMagnitude < 0.000001f) return Vector3.zero;
            var g = gravity.magnitude;
            if (g < 0.0001f) return delta.normalized * speed;
            var up = -gravity / g;
            var height = Vector3.Dot(delta, up);
            var horizontal = delta - up * height;
            var distance = horizontal.magnitude;
            if (distance < 0.0001f) return delta.normalized * speed;
            var speedSquared = speed * speed;
            var discriminant = speedSquared * speedSquared - g * (g * distance * distance + 2f * height * speedSquared);
            if (discriminant < 0f) return delta.normalized * speed;
            // Low-angle ballistic solution: pressure lifts the nozzle enough to counter gravity.
            var tangent = (speedSquared - Mathf.Sqrt(discriminant)) / (g * distance);
            var horizontalSpeed = speed / Mathf.Sqrt(1f + tangent * tangent);
            return horizontal / distance * horizontalSpeed + up * (horizontalSpeed * tangent);
        }
    }
}
