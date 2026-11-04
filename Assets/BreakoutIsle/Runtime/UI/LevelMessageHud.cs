using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.Gameplay
{
    public sealed class LevelMessageHud : MonoBehaviour
    {
        [SerializeField] private LevelSession session;
        [SerializeField] private Text messageText;
        [SerializeField, Min(0.1f)] private float visibleSeconds = 2.5f;
        private float _remaining;
        private bool _subscribed;
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Bind(LevelSession levelSession, Text label)
        {
            Unsubscribe();
            session = levelSession;
            messageText = label;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || session == null) return;
            session.MessageShown += Show;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || session == null) return;
            session.MessageShown -= Show;
            _subscribed = false;
        }
        private void Update()
        {
            if (_remaining <= 0f || messageText == null) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) messageText.text = string.Empty;
        }
        private void Show(string message)
        {
            if (messageText == null) return;
            messageText.text = message;
            _remaining = visibleSeconds;
        }
    }
}
