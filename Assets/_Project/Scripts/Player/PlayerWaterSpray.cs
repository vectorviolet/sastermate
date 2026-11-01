using System;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Input;
using UnityEngine;

namespace Sastermate.Player
{
    /// <summary>A mouse-aimed hose with ballistic water, swept collision, and impact splashes.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public class PlayerWaterSpray : GameStateListener
    {
        [Header("Mouse Aim")]
        [Tooltip("The rendered game camera. Uses Camera.main when left empty.")]
        [SerializeField] private Camera aimCamera;
        [Tooltip("Surfaces under the pointer. The player's colliders are always ignored.")]
        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;

        [Header("Water Effect")]
        [SerializeField] private GameObject waterPrefab;
        [Tooltip("Orientation relative to the nozzle. The pressure stream and droplets face local +Z.")]
        [SerializeField] private Vector3 effectRotation = Vector3.zero;
        [SerializeField, Min(0.01f)] private float effectScale = 0.2f;
        [SerializeField] private Color waterTint = new(0.7f, 0.9f, 1f, 1f);
        [Tooltip("Soft-particle intersection fade in world units. Keep this small for the miniature city.")]
        [SerializeField, Min(0.001f)] private float depthFade = 0.025f;

        [Header("Hose Physics")]
        [SerializeField, Min(0.1f)] private float range = 3f;
        [Tooltip("Launch speed in world units per second; higher pressure gives a flatter arc.")]
        [SerializeField, Min(0.1f)] private float jetSpeed = 8f;
        [SerializeField, Min(0f)] private float gravityScale = 1f;
        [SerializeField, Range(0.003f, 0.05f)] private float jetRadius = 0.015f;
        [SerializeField, Range(20, 100)] private int waterParcelsPerSecond = 50;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("The player's root, whose colliders the water ray ignores. Resolved from the parent Rigidbody.")]
        [SerializeField] private Transform ownerRoot;
        [SerializeField, Min(0f)] private float waterPerSecond = 1f;

        private static readonly int ColorId = Shader.PropertyToID("_Color01");
        private static readonly int DepthFadeId = Shader.PropertyToID("_DepthFade");
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
        private GameObject _waterInstance;
        private ParticleSystem[] _particles;
        private readonly WaterJetSimulation _water = new();
        private readonly Vector3[] _jetPoints = new Vector3[WaterJetSimulation.Capacity + 2];
        private WaterJetMesh _jetMesh;
        private ParticleSystem _impactSplash;
        private ParticleSystem _impactDrops;
        private Action<RaycastHit, Vector3, float> _impactHandler;
        private Vector3 _launchVelocity;
        private float _emissionTime;
        private bool _attackHeld;
        private bool _hasAimPosition;
        private Vector2 _aimScreenPosition;

        public bool IsSpraying => _attackHeld && isActiveAndEnabled && _jetMesh != null && GameManager.IsGameplay
            && !GameplayUiInput.IsUpgradePanelOpen;
        public bool HasHit { get; private set; }
        public RaycastHit CurrentHit { get; private set; }
        public Vector3 RayEndPoint { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector3 LaunchVelocity => _launchVelocity;
        public int ActiveWaterParcels => _water.Count;

        private void Awake()
        {
            if (!ownerRoot)
            {
                var body = GetComponentInParent<Rigidbody>();
                ownerRoot = body ? body.transform : transform;
            }

            if (!waterPrefab)
            {
                Debug.LogWarning($"[PlayerWaterSpray] Assign a water prefab on '{name}'.", this);
                enabled = false;
                return;
            }

            // Instantiate once, then reuse it for each press instead of allocating effects per frame.
            _waterInstance = Instantiate(waterPrefab, transform, false);
            _waterInstance.name = "Water Effect";
            _waterInstance.transform.localPosition = Vector3.zero;
            _waterInstance.transform.localRotation = Quaternion.Euler(effectRotation);
            _waterInstance.transform.localScale *= effectScale;
            _particles = _waterInstance.GetComponentsInChildren<ParticleSystem>(true);
            Material jetMaterial = null;

            foreach (var particle in _particles)
            {
                var main = particle.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.startColor = Color.white;
                main.gravityModifier = gravityScale;
                var emission = particle.emission;
                emission.enabled = false;
                var subEmitters = particle.subEmitters;
                subEmitters.enabled = false;
                var collision = particle.collision;
                collision.enabled = false;
                // Reuse the package's water materials/splashes. The straight authored stream
                // is replaced by a tube built from the actual moving water positions.
                if (particle.name == "Stream")
                {
                    var renderer = particle.GetComponent<Renderer>();
                    jetMaterial = renderer.sharedMaterial;
                    renderer.enabled = false;
                }
                if (particle.name == "Splash" || particle.name == "Pee Droplet")
                {
                    main.loop = true;
                    if (particle.name == "Splash") _impactSplash = particle;
                    else _impactDrops = particle;
                }
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var properties = new MaterialPropertyBlock();
            foreach (var renderer in _waterInstance.GetComponentsInChildren<Renderer>(true))
            {
                properties.Clear();
                renderer.GetPropertyBlock(properties);
                properties.SetColor(ColorId, waterTint);
                properties.SetFloat(DepthFadeId, depthFade);
                renderer.SetPropertyBlock(properties);
            }

            if (_particles.Length == 0 || !jetMaterial)
            {
                Debug.LogWarning($"[PlayerWaterSpray] '{waterPrefab.name}' needs a Stream water material.", this);
                enabled = false;
                return;
            }
            _jetMesh = new WaterJetMesh(transform, jetMaterial, waterTint, depthFade);
            _impactHandler = HandleWaterImpact;
            _launchVelocity = transform.forward * jetSpeed;
        }

        protected override void OnEnable()
        {
            EventBus.Input.OnAimPositionChanged += HandleAimPositionChanged;
            EventBus.Input.OnAttackPressed += HandleAttackPressed;
            EventBus.Input.OnAttackReleased += StopSpraying;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            EventBus.Input.OnAimPositionChanged -= HandleAimPositionChanged;
            _hasAimPosition = false;
            EventBus.Input.OnAttackPressed -= HandleAttackPressed;
            EventBus.Input.OnAttackReleased -= StopSpraying;
            StopSpraying();
            ClearWater();
            base.OnDisable();
        }

        protected override void OnGameStateChanged(GameState newState)
        {
            if (newState != GameState.Gameplay) StopSpraying();
            if (_particles == null) return;
            foreach (var particle in _particles)
            {
                if (newState != GameState.Gameplay && particle.isPlaying) particle.Pause(false);
                else if (newState == GameState.Gameplay && particle.isPaused) particle.Play(false);
            }
        }

        private void HandleAttackPressed()
        {
            if (GameManager.IsGameplay) _attackHeld = true;
        }

        private void HandleAimPositionChanged(Vector2 screenPosition)
        {
            _aimScreenPosition = screenPosition;
            _hasAimPosition = true;
        }

        private void UpdateMouseAim()
        {
            AimPoint = transform.position + transform.forward * range;
            _launchVelocity = transform.forward * jetSpeed;
            if (!_hasAimPosition) return;
            if (!aimCamera) aimCamera = Camera.main;
            if (!aimCamera) return;

            var pointerRay = aimCamera.ScreenPointToRay(_aimScreenPosition);
            Vector3 target;
            if (TryFindHit(pointerRay, aimCamera.farClipPlane, aimMask, out var surface))
                target = surface.point;
            else
            {
                var plane = new Plane(Vector3.up, transform.position);
                if (!plane.Raycast(pointerRay, out var enter)) return;
                target = pointerRay.GetPoint(enter);
            }

            AimPoint = target;
            var direction = target - transform.position;
            if (direction.magnitude > range) target = transform.position + direction.normalized * range;
            _launchVelocity = WaterJetSimulation.LaunchVelocity(transform.position, target,
                jetSpeed, Physics.gravity * gravityScale);
            // Leave the character/physics body alone; only this nozzle turns toward the pointer.
            if (_launchVelocity.sqrMagnitude > 0.000001f)
            {
                var up = Mathf.Abs(Vector3.Dot(_launchVelocity.normalized, Vector3.up)) > 0.999f
                    ? Vector3.forward : Vector3.up;
                transform.rotation = Quaternion.LookRotation(_launchVelocity, up);
            }
        }

        private void FixedUpdate()
        {
            if (!GameManager.IsGameplay || _jetMesh == null) return;
            HasHit = false;
            CurrentHit = default;
            var dt = Time.fixedDeltaTime;
            if (_attackHeld && !GameplayUiInput.IsUpgradePanelOpen)
            {
                _emissionTime += dt;
                var interval = 1f / Mathf.Max(1, waterParcelsPerSecond);
                while (_emissionTime >= interval)
                {
                    if (_launchVelocity.sqrMagnitude > 0.000001f)
                        _water.Emit(transform.position, _launchVelocity, waterPerSecond * interval,
                            range, jetRadius);
                    _emissionTime -= interval;
                }
            }
            _water.Step(dt, Physics.gravity * gravityScale, hitMask, ownerRoot, _impactHandler);
        }

        private void LateUpdate()
        {
            if (!GameManager.IsGameplay || _jetMesh == null) return;
            // Update aim after camera/model movement. Already-emitted water keeps its velocity.
            UpdateMouseAim();
            var count = 0;
            if (_attackHeld && !GameplayUiInput.IsUpgradePanelOpen) _jetPoints[count++] = transform.position;
            count = _water.CopyPositions(_jetPoints, count);
            var joinDistance = Mathf.Max(0.15f, jetSpeed / Mathf.Max(1, waterParcelsPerSecond) * 3f);
            if (HasHit && count > 0 && Vector3.Distance(_jetPoints[count - 1], CurrentHit.point) < joinDistance)
                _jetPoints[count++] = CurrentHit.point;
            RayEndPoint = count > 0 ? _jetPoints[count - 1] : transform.position;
            _jetMesh.Draw(_jetPoints, count, jetRadius, joinDistance);
            for (var i = 1; i < count; i++) Debug.DrawLine(_jetPoints[i - 1], _jetPoints[i], Color.cyan);
        }

        private void HandleWaterImpact(RaycastHit hit, Vector3 incomingVelocity, float amount)
        {
            HasHit = true;
            CurrentHit = hit;
            // The water parcel has physically arrived: only now apply its amount to a fire.
            if (amount > 0f) EventBus.Game.RaiseWaterHit(hit, amount);
            EmitImpact(_impactSplash, hit, incomingVelocity, 2, 0.035f, 0.18f);
            EmitImpact(_impactDrops, hit, incomingVelocity, 3, 0.012f, 0.3f);
        }

        private void EmitImpact(ParticleSystem particles, RaycastHit hit, Vector3 incoming,
            int count, float size, float lifetime)
        {
            if (!particles) return;
            if (!particles.isPlaying) particles.Play(false);
            var reflected = Vector3.Reflect(incoming.normalized, hit.normal);
            for (var i = 0; i < count; i++)
            {
                var velocity = hit.normal * 0.4f + reflected * 0.4f + UnityEngine.Random.insideUnitSphere * 0.5f;
                if (Vector3.Dot(velocity, hit.normal) < 0f) velocity = Vector3.Reflect(velocity, hit.normal);
                var emit = new ParticleSystem.EmitParams
                {
                    position = hit.point + hit.normal * 0.01f,
                    velocity = velocity,
                    startSize = size * UnityEngine.Random.Range(0.7f, 1.3f),
                    startLifetime = lifetime * UnityEngine.Random.Range(0.7f, 1.2f),
                    startColor = waterTint,
                    applyShapeToPosition = false
                };
                particles.Emit(emit, 1);
            }
        }

        private bool TryFindHit(Ray ray, float maxDistance, LayerMask mask, out RaycastHit closest)
        {
            var hits = _hitBuffer;
            var count = Physics.RaycastNonAlloc(ray, hits, maxDistance, mask, QueryTriggerInteraction.Ignore);
            // A full NonAlloc buffer may omit the nearest collider. Fall back rather than allow
            // dense geometry to make water hit a target behind a wall.
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            closest = default;
            var distance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || (ownerRoot && hit.collider.transform.IsChildOf(ownerRoot))) continue;
                if (hit.distance >= distance) continue;
                distance = hit.distance;
                closest = hit;
            }
            return !float.IsPositiveInfinity(distance);
        }

        private void StopSpraying()
        {
            _attackHeld = false;
            _emissionTime = 0f;
            // Parcels already in flight keep moving and can still splash/extinguish after release.
        }

        private void ClearWater()
        {
            _water.Clear();
            HasHit = false;
            CurrentHit = default;
            if (_particles != null)
                foreach (var particle in _particles)
                    if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _jetMesh?.Hide();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) StopSpraying();
        }

        private void OnDestroy()
        {
            _jetMesh?.Dispose();
            if (_waterInstance) Destroy(_waterInstance);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (Application.isPlaying && (IsSpraying || _water.Count > 0))
                Gizmos.DrawLine(transform.position, RayEndPoint);
            else Gizmos.DrawRay(transform.position, transform.forward * range);
            if (HasHit) Gizmos.DrawWireSphere(CurrentHit.point, jetRadius * 2f);
        }
    }
}
