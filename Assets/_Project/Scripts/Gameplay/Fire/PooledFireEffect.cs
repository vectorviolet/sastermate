using UnityEngine;
using VectorViolet.Utilities;

namespace Sastermate.Gameplay.Fire
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
    public sealed class PooledFireEffect : PoolableBase<PooledFireEffect>
    {
        [SerializeField, Min(0.001f)] private float depthFade = 0.025f;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.25f;
        [SerializeField, Min(0f)] private float flameEmission = 4f;
        [SerializeField, Min(0f)] private float flameOpacity = 2f;

        private ParticleSystem[] _particles;
        private AudioSource[] _audio;
        private Transform _poolRoot;
        private Vector3 _authoredScale;
        private BurnableObject _target;
        private bool _paused;
        private Camera _worldCamera;

        protected override void OnCreated()
        {
            _poolRoot = transform.parent;
            _authoredScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _audio = GetComponentsInChildren<AudioSource>(true);
            foreach (var particle in _particles)
            {
                var main = particle.main;
                main.playOnAwake = false;
                main.useUnscaledTime = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            var properties = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                properties.Clear();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_DepthFade", depthFade);
                if (renderer.name.StartsWith("Flames"))
                {
                    // Keep the flame color readable instead of clipping the authored HDR value to yellow.
                    properties.SetFloat("_EmissionIntensity", flameEmission);
                    properties.SetFloat("_OpacityBoost", flameOpacity);
                }
                renderer.SetPropertyBlock(properties);
            }
            foreach (var source in _audio)
            {
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.minDistance = 0.3f;
                source.maxDistance = 5f;
                source.volume *= soundVolume;
            }
            OnRelease();
        }

        public void Show(BurnableObject target)
        {
            _target = target;
            UpdateIntensity();
            foreach (var particle in _particles)
                if (particle.gameObject.activeInHierarchy) particle.Play(false);
            foreach (var source in _audio)
                if (source.isActiveAndEnabled && source.clip) source.Play();
        }

        public void UpdateIntensity()
        {
            if (!_target) return;
            // Keep the effect under its pool container and follow the target in world space.
            // Returning a child from BurnableObject.OnDisable would otherwise reparent it
            // during Unity's hierarchy activation/deactivation traversal, which is forbidden.
            UpdatePosition();
            var size = _target.FireScale * Mathf.Lerp(0.35f, 1f, _target.FireIntensity);
            var parentScale = _poolRoot ? _poolRoot.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                _authoredScale.x * size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                _authoredScale.y * size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                _authoredScale.z * size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        // Cinemachine settles the camera in LateUpdate; keep the surface offset aligned
        // with that final view instead of using the previous frame's camera direction.
        private void LateUpdate()
        {
            if (_target) UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (!_worldCamera) _worldCamera = Camera.main;
            transform.SetPositionAndRotation(_target.GetFireEffectPosition(_worldCamera), Quaternion.identity);
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            foreach (var particle in _particles)
            {
                if (paused && particle.isPlaying) particle.Pause(false);
                else if (!paused && particle.isPaused) particle.Play(false);
            }
            foreach (var source in _audio)
            {
                if (!source || !source.isActiveAndEnabled) continue;
                if (paused) source.Pause();
                else source.UnPause();
            }
        }

        protected override void OnRelease()
        {
            foreach (var particle in _particles)
                if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var source in _audio)
                if (source) source.Stop();
            _target = null;
            _paused = false;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = _authoredScale;
        }
    }
}
