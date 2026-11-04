using UnityEngine;

namespace BreakoutIsle.Gameplay
{
    public sealed class StaminaMeter
    {
        private readonly float _maximum;
        private readonly float _drainPerSecond;
        private readonly float _recoveryPerSecond;
        private readonly float _recoveryDelay;
        private float _timeSinceSprint;
        private bool _exhausted;

        public float Current { get; private set; }
        public bool CanSprint => !_exhausted && Current > 0f;

        public void RestoreFull()
        {
            Current = _maximum;
            _timeSinceSprint = _recoveryDelay;
            _exhausted = false;
        }

        public StaminaMeter(float maximum = 100f, float drainPerSecond = 20f, float recoveryPerSecond = 15f, float recoveryDelay = 1f)
        {
            _maximum = Mathf.Max(0f, maximum);
            _drainPerSecond = Mathf.Max(0f, drainPerSecond);
            _recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
            _recoveryDelay = Mathf.Max(0f, recoveryDelay);
            Current = _maximum;
            _timeSinceSprint = _recoveryDelay;
        }

        public bool Tick(bool sprintRequested, float deltaTime)
        {
            if (deltaTime <= 0f)
                return sprintRequested && Current > 0f;

            if (sprintRequested)
            {
                if (_exhausted || Current <= 0f)
                {
                    _exhausted = true;
                    Recover(deltaTime);
                    return false;
                }

                _timeSinceSprint = 0f;
                Current = Mathf.Max(0f, Current - _drainPerSecond * deltaTime);
                _exhausted = Current <= 0f;
                return true;
            }

            _exhausted = false;
            Recover(deltaTime);

            return false;
        }

        private void Recover(float deltaTime)
        {
            var timeBefore = _timeSinceSprint;
            _timeSinceSprint += deltaTime;
            var recoveryTime = Mathf.Clamp(_timeSinceSprint - _recoveryDelay, 0f, deltaTime);
            if (timeBefore >= _recoveryDelay)
                recoveryTime = deltaTime;

            Current = Mathf.Min(_maximum, Current + _recoveryPerSecond * recoveryTime);
        }
    }
}
