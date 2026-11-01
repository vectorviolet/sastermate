using Sastermate.Core.Events;
using UnityEngine;
using VectorViolet.ProSoundManager;

namespace Sastermate.Gameplay.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguishAudio : MonoBehaviour
    {
        [Tooltip("Non-looping SoundData played once when water completely extinguishes a fire.")]
        [SerializeField] private SoundData extinguishSound;

        private void Awake()
        {
            if (extinguishSound && extinguishSound.clip) extinguishSound.clip.LoadAudioData();
        }

        private void OnEnable() => EventBus.Game.OnFireSuccessfullyExtinguished += Play;

        private void OnDisable() => EventBus.Game.OnFireSuccessfullyExtinguished -= Play;

        private void Play(Vector3 worldPosition)
        {
            if (!extinguishSound || !extinguishSound.clip) return;
            SoundService.Current?.PlaySfx(extinguishSound, worldPosition);
        }
    }
}
