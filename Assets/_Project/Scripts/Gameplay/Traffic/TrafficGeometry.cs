using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
{
    internal readonly struct TrafficPose
    {
        public readonly Vector3 Position;
        public readonly float Yaw;
        public Quaternion Rotation
        {
            get
            {
                var halfAngle = Yaw * Mathf.Deg2Rad * 0.5f;
                return new Quaternion(0f, (float)System.Math.Sin(halfAngle), 0f,
                    (float)System.Math.Cos(halfAngle));
            }
        }

        public TrafficPose(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>Oriented boxes, including a conservative box covering translation and yaw rotation.</summary>
    internal readonly struct TrafficFootprint
    {
        public readonly Vector3 Center;
        public readonly Vector3 HalfSize;
        public readonly float Yaw;
        private readonly Vector2 _right;
        private readonly Vector2 _forward;

        public Quaternion Rotation => new TrafficPose(Vector3.zero, Yaw).Rotation;

        public TrafficFootprint(Vector3 center, Vector3 halfSize, float yaw)
        {
            Center = center;
            HalfSize = halfSize;
            Yaw = yaw;
            var radians = yaw * Mathf.Deg2Rad;
            var sine = (float)System.Math.Sin(radians);
            var cosine = (float)System.Math.Cos(radians);
            _right = new Vector2(cosine, -sine);
            _forward = new Vector2(sine, cosine);
        }

        public bool Overlaps(TrafficFootprint other)
        {
            if (Mathf.Abs(Center.y - other.Center.y) > HalfSize.y + other.HalfSize.y) return false;
            var delta = new Vector2(other.Center.x - Center.x, other.Center.z - Center.z);
            return AxisOverlaps(other, delta, _right) && AxisOverlaps(other, delta, _forward) &&
                   AxisOverlaps(other, delta, other._right) && AxisOverlaps(other, delta, other._forward);
        }

        private bool AxisOverlaps(TrafficFootprint other, Vector2 delta, Vector2 axis)
        {
            var first = Mathf.Abs(Vector2.Dot(_right, axis)) * HalfSize.x +
                        Mathf.Abs(Vector2.Dot(_forward, axis)) * HalfSize.z;
            var second = Mathf.Abs(Vector2.Dot(other._right, axis)) * other.HalfSize.x +
                         Mathf.Abs(Vector2.Dot(other._forward, axis)) * other.HalfSize.z;
            return Mathf.Abs(Vector2.Dot(delta, axis)) <= first + second + 0.00001f;
        }

        public static TrafficFootprint At(TrafficPose pose, Vector3 center, Vector3 halfSize, float gap)
        {
            halfSize.x += gap * 0.5f;
            halfSize.z += gap * 0.5f;
            return new TrafficFootprint(pose.Position + RotateYaw(center, pose.Yaw), halfSize, pose.Yaw);
        }

        public static TrafficFootprint Swept(TrafficPose from, TrafficPose to,
            Vector3 localCenter, Vector3 halfSize, float gap)
        {
            var deltaYaw = Mathf.DeltaAngle(from.Yaw, to.Yaw);
            var middle = new TrafficPose((from.Position + to.Position) * 0.5f, from.Yaw + deltaYaw * 0.5f);
            var translation = RotateYaw(to.Position - from.Position, -middle.Yaw) * 0.5f;
            var radius = new Vector2(localCenter.x, localCenter.z).magnitude +
                         new Vector2(halfSize.x, halfSize.z).magnitude;
            // Arc length bounds each corner's deviation from the middle orientation.
            var turnPadding = radius * Mathf.Abs(deltaYaw) * Mathf.Deg2Rad * 0.5f;
            halfSize += new Vector3(Mathf.Abs(translation.x) + turnPadding,
                Mathf.Abs(translation.y), Mathf.Abs(translation.z) + turnPadding);
            return At(middle, localCenter, halfSize, gap);
        }

        private static Vector3 RotateYaw(Vector3 value, float yaw)
        {
            var sine = (float)System.Math.Sin(yaw * Mathf.Deg2Rad);
            var cosine = (float)System.Math.Cos(yaw * Mathf.Deg2Rad);
            return new Vector3(cosine * value.x + sine * value.z, value.y,
                -sine * value.x + cosine * value.z);
        }
    }
}
