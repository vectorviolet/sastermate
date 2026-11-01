using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
{
    [DisallowMultipleComponent]
    public sealed class TrafficRoute : MonoBehaviour
    {
        [Tooltip("In driving order. Leave empty to use this object's direct children in Hierarchy order.")]
        [SerializeField] private Transform[] waypoints;
        [Tooltip("Connect the last waypoint back to the first only when a complete road loop exists.")]
        [SerializeField] private bool loop;

#if UNITY_EDITOR
        [Header("Scene Gizmos")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showWaypointLabels = true;
        [SerializeField] private bool drawOnTop = true;
        [SerializeField] private Color routeColor = new(0.1f, 0.85f, 1f, 1f);
        [Tooltip("Screen-relative waypoint size in the Scene view.")]
        [SerializeField, Range(0.01f, 0.15f)] private float gizmoSize = 0.045f;
        [SerializeField, Min(0f)] private float gizmoHeightOffset = 0.025f;
        private GUIStyle _labelStyle;
#endif

        private TrafficPath _path;
        internal TrafficPath Path => _path ??= BuildPath();
        public bool IsLoop => loop;
        public float Length => Path.Length;
        public bool IsValid => Path.IsValid;

        private void Awake() => _path = BuildPath();
        private void OnValidate() => _path = null;

        private TrafficPath BuildPath()
        {
            var explicitPoints = waypoints != null && waypoints.Length > 0;
            var count = explicitPoints ? waypoints.Length : transform.childCount;
            var points = new System.Collections.Generic.List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                var point = explicitPoints ? waypoints[i] : transform.GetChild(i);
                if (point && (points.Count == 0 || Vector3.Distance(points[points.Count - 1], point.position) > 0.0001f))
                    points.Add(point.position);
            }
            return new TrafficPath(points.ToArray(), loop);
        }

#if UNITY_EDITOR
        // DrawGizmo also works when the route component is collapsed in the Inspector.
        [UnityEditor.DrawGizmo(UnityEditor.GizmoType.Selected | UnityEditor.GizmoType.NonSelected)]
        private static void DrawRouteGizmos(TrafficRoute route, UnityEditor.GizmoType gizmoType)
        {
            if (!route || !route.showGizmos) return;
            if (Camera.current && Camera.current.cameraType != UnityEngine.CameraType.SceneView) return;
            route.DrawSceneGizmos((gizmoType & UnityEditor.GizmoType.Selected) != 0);
        }

        private void DrawSceneGizmos(bool selected)
        {
            var path = Application.isPlaying ? Path : BuildPath();
            var oldColor = UnityEditor.Handles.color;
            var oldMatrix = UnityEditor.Handles.matrix;
            var oldDepth = UnityEditor.Handles.zTest;
            try
            {
                UnityEditor.Handles.matrix = Matrix4x4.identity;
                UnityEditor.Handles.zTest = drawOnTop
                    ? UnityEngine.Rendering.CompareFunction.Always
                    : UnityEngine.Rendering.CompareFunction.LessEqual;
                var offset = Vector3.up * gizmoHeightOffset;
                var width = selected ? 4f : 2.5f;
                // Use the actual lane segments, including the closing segment of a loop.
                UnityEditor.Handles.color = routeColor;
                for (var i = 0; i < path.SegmentCount; i++)
                {
                    var from = path.SegmentStart(i) + offset;
                    var to = path.SegmentEnd(i) + offset;
                    var length = Vector3.Distance(from, to);
                    UnityEditor.Handles.DrawAAPolyLine(width, from, to);
                    if (length < 0.0001f) continue;
                    var direction = (to - from).normalized;
                    var side = Vector3.Cross(Vector3.up, direction).normalized;
                    var middle = (from + to) * 0.5f;
                    var arrowSize = Mathf.Min(UnityEditor.HandleUtility.GetHandleSize(middle) * gizmoSize * 3f,
                        length * 0.25f);
                    UnityEditor.Handles.DrawAAPolyLine(width,
                        middle - direction * arrowSize + side * arrowSize * 0.5f, middle,
                        middle - direction * arrowSize - side * arrowSize * 0.5f);
                }
                if (path.PointCount == 0)
                {
                    DrawWaypoint(transform.position + offset, Color.yellow, name + ": add waypoint children");
                    return;
                }
                for (var i = 0; i < path.PointCount; i++)
                {
                    var start = i == 0;
                    var finish = !loop && i == path.PointCount - 1 && !start;
                    var color = start ? Color.green : finish ? new Color(1f, 0.25f, 0.2f) : routeColor;
                    var label = i.ToString("00");
                    if (start) label = name + " - " + label + (loop ? " START / LOOP" : " START");
                    else if (finish) label += " END";
                    if (!path.IsValid) label += " (needs 2 distinct points)";
                    DrawWaypoint(path.PointPosition(i) + offset, color, label);
                }
            }
            finally
            {
                UnityEditor.Handles.color = oldColor;
                UnityEditor.Handles.matrix = oldMatrix;
                UnityEditor.Handles.zTest = oldDepth;
            }
        }

        private void DrawWaypoint(Vector3 position, Color color, string label)
        {
            var size = UnityEditor.HandleUtility.GetHandleSize(position) * gizmoSize;
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.SphereHandleCap(0, position, Quaternion.identity, size * 2f, EventType.Repaint);
            if (!showWaypointLabels) return;
            if (_labelStyle == null)
                _labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel) { fontSize = 11 };
            _labelStyle.normal.textColor = color;
            UnityEditor.Handles.Label(position + Vector3.up * size * 1.8f, label, _labelStyle);
        }
#endif
    }
}
