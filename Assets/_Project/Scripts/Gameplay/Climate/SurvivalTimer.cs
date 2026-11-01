using UnityEngine;
using VectorViolet.Utilities;

namespace Sastermate.Gameplay.Climate
{
    /// <summary>VectorViolet countdown advanced by ClimateManager only during active gameplay.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/Climate/Survival Timer")]
    public sealed class SurvivalTimer : Timer
    {
        public float Duration => _duration;
        public float Elapsed => Mathf.Max(0f, _duration - CurrentTime);
        public bool IsConfigured { get; private set; }

        public void Configure(float duration)
        {
            _timerType = TimerType.Countdown;
            _duration = Mathf.Max(1f, duration);
            _circulate = false;
            CurrentTime = _duration;
            IsRunning = false;
            IsConfigured = true;
        }

        public void AdvanceGameplay()
        {
            IsRunning = true;
            base.Update();
            IsRunning = false;
        }

        // The owning climate manager initializes and advances the shared countdown. Running
        // the normal MonoBehaviour callbacks as well would reset or double-tick the clock.
        protected override void Start() { }
        protected override void Update() { }
    }
}
