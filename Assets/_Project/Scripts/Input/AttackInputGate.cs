namespace Sastermate.Input
{
    public enum AttackInputChange { None, Pressed, Released }

    /// <summary>Consumes UI presses and requires a fresh press after an active attack is blocked.</summary>
    public sealed class AttackInputGate
    {
        private bool _queued;
        private bool _held;
        public bool HasInput => _queued || _held;

        public void QueuePress() => _queued = true;
        public void Release() { _queued = false; _held = false; }

        public AttackInputChange Update(bool blocked)
        {
            if (blocked)
            {
                var wasHeld = _held;
                Release();
                return wasHeld ? AttackInputChange.Released : AttackInputChange.None;
            }
            if (!_queued) return AttackInputChange.None;
            _queued = false;
            if (_held) return AttackInputChange.None;
            _held = true;
            return AttackInputChange.Pressed;
        }
    }
}
