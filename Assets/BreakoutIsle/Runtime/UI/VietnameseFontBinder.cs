using UnityEngine;
using UnityEngine.UI;

namespace BreakoutIsle.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class VietnameseFontBinder : MonoBehaviour
    {
        private void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI" }, 16);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            foreach (var label in GetComponentsInChildren<Text>(true))
                if (label != null) label.font = font;
        }
    }
}
