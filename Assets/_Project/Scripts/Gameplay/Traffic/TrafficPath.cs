using System;
using UnityEngine;

namespace Sastermate.Gameplay.Traffic
{
    /// <summary>Distance-based traversal of a directed lane polyline.</summary>
    internal sealed class TrafficPath
    {
        private readonly Vector3[] _points;
        private readonly float[] _ends;
        private readonly bool _loop;

        public float Length { get; }
        public int PointCount => _points.Length;
        public int SegmentCount => _ends.Length;
        public bool IsValid => _ends.Length > 0 && Length > 0.001f;

        public Vector3 SegmentStart(int index) => _points[index];
        public Vector3 SegmentEnd(int index) => _points[(index + 1) % _points.Length];
        public Vector3 PointPosition(int index) => _points[index];

        public TrafficPath(Vector3[] points, bool loop, float rightOffset = 0f)
        {
            _points = OffsetLane(points ?? Array.Empty<Vector3>(), loop, rightOffset);
            _loop = loop;
            _ends = new float[_points.Length < 2 ? 0 : _points.Length - (loop ? 0 : 1)];
            var length = 0f;
            for (var i = 0; i < _ends.Length; i++)
            {
                length += Vector3.Distance(_points[i], _points[(i + 1) % _points.Length]);
                _ends[i] = length;
            }
            Length = length;
        }

        private static Vector3[] OffsetLane(Vector3[] points, bool loop, float offset)
        {
            if (points.Length < 2 || offset <= 0f) return points;
            var shifted = new Vector3[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var incoming = i > 0 || loop
                    ? points[i] - points[(i + points.Length - 1) % points.Length] : Vector3.zero;
                var outgoing = i < points.Length - 1 || loop
                    ? points[(i + 1) % points.Length] - points[i] : Vector3.zero;
                incoming.y = outgoing.y = 0f;
                incoming = incoming.normalized;
                outgoing = outgoing.normalized;
                if (incoming.sqrMagnitude < 0.0001f) incoming = outgoing;
                if (outgoing.sqrMagnitude < 0.0001f) outgoing = incoming;
                var rightIn = new Vector3(incoming.z, 0f, -incoming.x);
                var rightOut = new Vector3(outgoing.z, 0f, -outgoing.x);
                var miter = (rightIn + rightOut).normalized;
                var alignment = Vector3.Dot(miter, rightOut);
                // Cap acute corners; a reversing/degenerate waypoint must never create a spike or NaN.
                var displacement = alignment > 0.001f
                    ? miter * (offset / Mathf.Max(alignment, 0.5f)) : rightOut * offset;
                shifted[i] = points[i] + displacement;
            }
            return shifted;
        }

        public float NormalizeDistance(float distance) => !IsValid ? 0f :
            _loop ? Mathf.Repeat(distance, Length) : Mathf.Clamp(distance, 0f, Length);

        public Vector3 Position(float distance)
        {
            if (!IsValid) return _points.Length > 0 ? _points[0] : Vector3.zero;
            var d = NormalizeDistance(distance);
            var segment = FindSegment(d);
            var start = segment == 0 ? 0f : _ends[segment - 1];
            var length = _ends[segment] - start;
            return Vector3.Lerp(_points[segment], _points[(segment + 1) % _points.Length],
                length > 0.0001f ? (d - start) / length : 1f);
        }

        public float DistanceToCorner(float distance)
        {
            if (!IsValid) return 0f;
            var d = NormalizeDistance(distance);
            return Mathf.Max(0f, _ends[FindSegment(d)] - d);
        }

        public Vector3 Direction(float distance)
        {
            if (!IsValid) return Vector3.forward;
            var segment = FindSegment(NormalizeDistance(distance));
            return (_points[(segment + 1) % _points.Length] - _points[segment]).normalized;
        }

        private int FindSegment(float distance)
        {
            for (var i = 0; i < _ends.Length - 1; i++)
                if (_ends[i] > distance + 0.00001f) return i;
            return _ends.Length - 1;
        }
    }
}
