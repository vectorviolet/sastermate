using Sastermate.Core.App;
using Unity.Cinemachine;
using UnityEngine;

namespace Sastermate.Cameras
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/Camera/Menu City Camera")]
    public sealed class MenuCityCamera : GameStateListener
    {
        [SerializeField] private CinemachineCamera menuCamera;
        [Tooltip("City geometry to frame. Include buildings, streets, and trees under this root.")]
        [SerializeField] private Transform cityRoot;
        [SerializeField] private bool frameOnMenuOpen = true;

        [Header("Camera Selection")]
        [SerializeField] private int menuPriority = 100;
        [SerializeField] private int inactivePriority = -100;

        [Header("City Framing")]
        [SerializeField, Range(1f, 90f)] private float pitch = 35f;
        [SerializeField] private float yaw = 45f;
        [SerializeField, Min(1f)] private float distance = 50f;
        [SerializeField, Min(0f)] private float padding = 2f;

        [Header("Menu Orbit")]
        [SerializeField] private bool orbitInMenu = true;
        [Tooltip("Degrees per second. Use a negative value to reverse direction, or zero to stop.")]
        [SerializeField] private float orbitSpeed = 5f;

        private PrioritySettings _originalPriority;
        private bool _priorityCaptured;
        private Vector3 _orbitCenter;
        private Vector3 _orbitStartOffset;
        private Quaternion _orbitStartRotation;
        private float _orbitAngle;
        private bool _orbitReady;

        protected override void OnGameStateChanged(GameState state)
        {
            if (!menuCamera) return;
            if (!_priorityCaptured)
            {
                _originalPriority = menuCamera.Priority;
                _priorityCaptured = true;
            }

            _orbitReady = false;
            if (state == GameState.MainMenu)
            {
                if (frameOnMenuOpen) FrameCity();
                else if (TryGetCityBounds(out var bounds)) CaptureOrbit(bounds.center);
            }
            menuCamera.Priority = state == GameState.MainMenu ? menuPriority : inactivePriority;
        }

        private void Update()
        {
            if (!orbitInMenu || !_orbitReady || !menuCamera || Manager == null
                || Manager.CurrentState != GameState.MainMenu) return;

            // Move before the Cinemachine Brain's LateUpdate; keep the outgoing pose still during the Play blend.
            _orbitAngle = Mathf.Repeat(_orbitAngle + orbitSpeed * Time.unscaledDeltaTime, 360f);
            var orbit = Quaternion.AngleAxis(_orbitAngle, Vector3.up);
            menuCamera.transform.SetPositionAndRotation(_orbitCenter + orbit * _orbitStartOffset,
                orbit * _orbitStartRotation);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_priorityCaptured && menuCamera) menuCamera.Priority = _originalPriority;
            _priorityCaptured = false;
            _orbitReady = false;
        }

        [ContextMenu("Frame City")]
        public void FrameCity()
        {
            _orbitReady = false;
            if (!menuCamera || !TryGetCityBounds(out var bounds)) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObjects(new Object[] { menuCamera, menuCamera.transform }, "Frame menu city camera");
#endif
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            var output = UnityEngine.Camera.main;
            var aspect = output ? output.aspect : 16f / 9f;
            var lens = menuCamera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.Dutch = 0f;
            lens.OrthographicSize = CalculateOrthographicSize(bounds.extents, right, up, aspect, padding);
            lens.NearClipPlane = Mathf.Max(0.01f, lens.NearClipPlane);
            var depth = ProjectedHalfExtent(bounds.extents, forward);
            if (orbitInMenu)
            {
                // Fit the entire turn once so the lens stays fixed and city corners do not rotate out of view.
                var orbitExtents = CalculateOrbitViewExtents(bounds.extents, pitch);
                lens.OrthographicSize = Mathf.Max(0.01f,
                    Mathf.Max(orbitExtents.y, orbitExtents.x / Mathf.Max(0.01f, aspect)) + padding);
                depth = orbitExtents.z;
            }
            var cameraDistance = Mathf.Max(distance, depth + lens.NearClipPlane + 1f);
            lens.FarClipPlane = Mathf.Max(lens.FarClipPlane, cameraDistance + depth + padding + 1f);
            menuCamera.transform.SetPositionAndRotation(bounds.center - forward * cameraDistance, rotation);
            menuCamera.Lens = lens;
            menuCamera.PreviousStateIsValid = false;
            CaptureOrbit(bounds.center);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(menuCamera);
                UnityEditor.EditorUtility.SetDirty(menuCamera.transform);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(menuCamera);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(menuCamera.transform);
                if (menuCamera.gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menuCamera.gameObject.scene);
            }
#endif
        }

        private bool TryGetCityBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            if (!cityRoot) return false;
            var found = false;
            foreach (var renderer in cityRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void CaptureOrbit(Vector3 center)
        {
            _orbitCenter = center;
            _orbitStartOffset = menuCamera.transform.position - center;
            _orbitStartRotation = menuCamera.transform.rotation;
            _orbitAngle = 0f;
            _orbitReady = true;
        }

        internal static Vector3 CalculateOrbitViewExtents(Vector3 extents, float pitch)
        {
            var radius = Mathf.Sqrt(extents.x * extents.x + extents.z * extents.z);
            var sin = Mathf.Abs(Mathf.Sin(pitch * Mathf.Deg2Rad));
            var cos = Mathf.Abs(Mathf.Cos(pitch * Mathf.Deg2Rad));
            return new Vector3(radius, radius * sin + extents.y * cos,
                radius * cos + extents.y * sin);
        }

        internal static float CalculateOrthographicSize(Vector3 extents, Vector3 right, Vector3 up,
            float aspect, float padding) => Mathf.Max(0.01f,
                Mathf.Max(ProjectedHalfExtent(extents, up),
                    ProjectedHalfExtent(extents, right) / Mathf.Max(0.01f, aspect)) + padding);

        private static float ProjectedHalfExtent(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
    }
}
