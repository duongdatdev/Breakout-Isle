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

        public float Current { get; private set; }

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
                _timeSinceSprint = 0f;
                if (Current <= 0f)
                    return false;

                Current = Mathf.Max(0f, Current - _drainPerSecond * deltaTime);
                return true;
            }

            _timeSinceSprint += deltaTime;
            if (_timeSinceSprint > _recoveryDelay)
                Current = Mathf.Min(_maximum, Current + _recoveryPerSecond * deltaTime);

            return false;
        }
    }
}
