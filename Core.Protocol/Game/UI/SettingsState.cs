namespace Armagetron.Game.UI
{
    /// <summary>
    /// Player-adjustable settings from the design's settings comp: four on/off toggles, two
    /// 0–1 sliders, and the signature-color choice. Pure state with clamped mutators; the
    /// SettingsView renders it and the shell routes taps to <see cref="Toggle"/>/<see cref="SetSlider"/>.
    /// Some values (turn-zone size, sensitivity, signature color) are captured but not yet wired
    /// into gameplay — placeholders until those systems exist; the HUD already honors the
    /// signature color and the touch overlay honors <see cref="Hints"/>.
    /// </summary>
    public sealed class SettingsState
    {
        public bool Sound { get; set; } = true;
        public bool Music { get; set; } = true;
        public bool Haptics { get; set; } = true;
        public bool Hints { get; set; } = true;

        public float TurnZone { get; private set; } = 0.5f;
        public float Sensitivity { get; private set; } = 0.7f;

        /// <summary>Index into <see cref="CyclePalette"/>'s signature options (0 = cyan).</summary>
        public int SignatureColor { get; set; }

        public void Toggle(string id)
        {
            switch (id)
            {
                case "sound":   Sound = !Sound; break;
                case "music":   Music = !Music; break;
                case "haptics": Haptics = !Haptics; break;
                case "hints":   Hints = !Hints; break;
            }
        }

        public void SetSlider(string id, float value01)
        {
            float v = value01 < 0f ? 0f : value01 > 1f ? 1f : value01;
            if (id == "turnzone") TurnZone = v;
            else if (id == "sensitivity") Sensitivity = v;
        }
    }
}
