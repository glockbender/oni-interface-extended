using System;

namespace CenteredHud
{
    public sealed class Options
    {
        public const int MaximumWidth = 32768;
        public bool Enabled = true;
        public int WidthPixels = 2560;
        public bool ShowGuides = false;
        public bool EnableShortcuts = false;
        public string CycleKey = "F8";
        public string SettingsKey = "F9";

        public void Normalize()
        {
            WidthPixels = Math.Max(0, Math.Min(MaximumWidth, WidthPixels));
            if (string.IsNullOrWhiteSpace(CycleKey)) CycleKey = "F8";
            if (string.IsNullOrWhiteSpace(SettingsKey)) SettingsKey = "F9";
        }
    }
}
