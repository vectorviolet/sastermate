using System.Collections.Generic;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using UnityEngine;
using UnityEngine.Audio;
using VectorViolet.ProSoundManager;

namespace Sastermate.Gameplay.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireAmbienceController : GameStateListener
    {
        public enum FireLevel { None, Small, Medium, Large }

        [Header("Fire SoundData")]
        [SerializeField] private SoundData smallFire;
        [SerializeField] private SoundData mediumFire;
        [SerializeField] private SoundData largeFire;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Player Proximity")]
        [Tooltip("Usually the player root. Distance is measured in XZ, independently of the elevated camera's AudioListener.")]
        [SerializeField] private Transform proximityTarget;
        [SerializeField, Min(0.1f)] private float hearingDistance = 6f;
        [SerializeField, Min(0f)] private float fullVolumeDistance = 1.5f;
        [Tooltip("A counted fire must move this much beyond Hearing Distance before leaving the group, preventing rapid tier changes at the boundary.")]
        [SerializeField, Min(0f)] private float distanceHysteresis = 0.35f;

        [Header("Density and Mixing")]
        [SerializeField, Min(2)] private int mediumFireCount = 2;
        [SerializeField, Min(3)] private int largeFireCount = 3;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.65f;
        [SerializeField, Min(0.05f)] private float crossfadeDuration = 0.5f;

        private readonly HashSet<BurnableObject> _burning = new();
        private readonly HashSet<BurnableObject> _nearby = new();
        private readonly List<BurnableObject> _stale = new();
        private readonly AudioSource[] _sources = new AudioSource[3];
        private readonly float[] _gains = new float[3];
        private SoundData[] _sounds;
        private bool _paused;

        public int NearbyFireCount { get; private set; }
        public FireLevel CurrentLevel { get; private set; }
        public float ProximityGain { get; private set; }

        private void Awake()
        {
            if (!proximityTarget)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player) proximityTarget = player.transform;
            }
            _sounds = new[] { smallFire, mediumFire, largeFire };
            if (!proximityTarget || !smallFire || !smallFire.clip || !mediumFire ||
                !mediumFire.clip || !largeFire || !largeFire.clip)
            {
                Debug.LogWarning("[FireAmbienceController] Assign the player and all three fire SoundData assets.", this);
                enabled = false;
                return;
            }
            for (var i = 0; i < _sounds.Length; i++)
            {
                var source = new GameObject("Fire Ambience - " + _sounds[i].name).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.outputAudioMixerGroup = sfxGroup;
                source.clip = _sounds[i].clip;
                source.loop = true;
                // This is a player-relative ambience mix; XZ attenuation is applied below.
                source.spatialBlend = 0f;
                source.volume = 0f;
                source.pitch = Mathf.Clamp(_sounds[i].pitch + (_sounds[i].useRandomPitch
                    ? Random.Range(-_sounds[i].randomPitchRange, _sounds[i].randomPitchRange) : 0f), 0.1f, 3f);
                _sources[i] = source;
            }
        }

        protected override void OnEnable()
        {
            EventBus.Game.OnFireIgnited += HandleIgnited;
            EventBus.Game.OnFireExtinguished += HandleExtinguished;
            foreach (var target in FindObjectsByType<BurnableObject>(FindObjectsSortMode.None))
                if (target.IsBurning) _burning.Add(target);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            EventBus.Game.OnFireIgnited -= HandleIgnited;
            EventBus.Game.OnFireExtinguished -= HandleExtinguished;
            _burning.Clear();
            _nearby.Clear();
            NearbyFireCount = 0;
            CurrentLevel = FireLevel.None;
            ProximityGain = 0f;
            _paused = false;
            for (var i = 0; i < _sources.Length; i++)
            {
                _gains[i] = 0f;
                if (!_sources[i]) continue;
                _sources[i].Stop();
                _sources[i].volume = 0f;
            }
            base.OnDisable();
        }

        protected override void OnGameStateChanged(GameState state) =>
            SetPaused(state != GameState.Gameplay || Time.timeScale <= 0f);

        private void HandleIgnited(BurnableObject target)
        {
            if (target && target.IsBurning) _burning.Add(target);
        }

        private void HandleExtinguished(BurnableObject target)
        {
            _burning.Remove(target);
            _nearby.Remove(target);
        }

        private void Update()
        {
            SetPaused(!GameManager.IsGameplay || Time.timeScale <= 0f);
            if (_paused || !proximityTarget) return;

            var range = Mathf.Max(0.1f, hearingDistance);
            var fullRange = Mathf.Clamp(fullVolumeDistance, 0f, range - 0.01f);
            var nearest = float.PositiveInfinity;
            NearbyFireCount = 0;
            _stale.Clear();
            foreach (var fire in _burning)
            {
                if (!fire || !fire.IsBurning) { _stale.Add(fire); continue; }
                var delta = fire.FirePosition - proximityTarget.position;
                var distance = new Vector2(delta.x, delta.z).magnitude;
                var exitRange = range + (_nearby.Contains(fire) ? Mathf.Max(0f, distanceHysteresis) : 0f);
                if (distance > exitRange) { _nearby.Remove(fire); continue; }
                _nearby.Add(fire);
                NearbyFireCount++;
                nearest = Mathf.Min(nearest, distance);
            }
            foreach (var fire in _stale) { _burning.Remove(fire); _nearby.Remove(fire); }

            var medium = Mathf.Max(2, mediumFireCount);
            var large = Mathf.Max(medium + 1, largeFireCount);
            CurrentLevel = NearbyFireCount == 0 ? FireLevel.None : NearbyFireCount >= large
                ? FireLevel.Large : NearbyFireCount >= medium ? FireLevel.Medium : FireLevel.Small;
            ProximityGain = NearbyFireCount == 0 ? 0f :
                1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fullRange, range, nearest));

            for (var i = 0; i < _sources.Length; i++)
            {
                var desired = (int)CurrentLevel == i + 1 ? ProximityGain : 0f;
                _gains[i] = Mathf.MoveTowards(_gains[i], desired,
                    Time.deltaTime / Mathf.Max(0.05f, crossfadeDuration));
                var source = _sources[i];
                source.volume = Mathf.Clamp01(masterVolume) * Mathf.Clamp01(_sounds[i].volume) * _gains[i];
                if (source.volume > 0.0001f)
                {
                    if (!source.isPlaying) source.Play();
                }
                else if (source.isPlaying) source.Stop();
            }
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            foreach (var source in _sources)
            {
                if (!source) continue;
                if (paused) source.Pause();
                else source.UnPause();
            }
        }
    }
}
