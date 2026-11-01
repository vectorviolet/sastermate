using System;
using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
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

        public TrafficPath(Vector3[] points, bool loop)
        {
            _points = points ?? Array.Empty<Vector3>();
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
