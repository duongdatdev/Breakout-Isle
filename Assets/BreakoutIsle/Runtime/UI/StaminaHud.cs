using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.Gameplay
{
    public sealed class StaminaHud : MonoBehaviour
    {
        [SerializeField] private Image meter;
        private PlayerMotor _motor;
        private void Awake() => _motor = GetComponent<PlayerMotor>();
        public void Bind(Image target, PlayerMotor motor) { meter = target; _motor = motor; }
        private void Update()
        {
            if (meter == null || _motor == null) return;
            var scale = meter.rectTransform.localScale;
            scale.x = Mathf.Clamp01(_motor.Stamina / 100f);
            meter.rectTransform.localScale = scale;
        }
    }
}
