namespace Armagetron.Game.UI
{
    /// <summary>
    /// A tappable button: pure state (bounds, label, enabled/pressed) plus hit-testing. The
    /// view builder draws it via <see cref="SceneBuf.DrawButton"/>; the input router calls
    /// <see cref="HitTest"/>. No GPU or platform dependency.
    /// </summary>
    public sealed class UiButton
    {
        public string Id { get; }
        public UiRect Bounds { get; set; }
        public string Label { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Pressed { get; set; }

        public UiButton(string id, UiRect bounds, string label)
        {
            Id = id; Bounds = bounds; Label = label;
        }

        /// <summary>True when a tap at (x, y) should activate this button (ignored if disabled).</summary>
        public bool HitTest(int x, int y) => Enabled && Bounds.Contains(x, y);
    }

    /// <summary>
    /// A single-line text-entry field: pure edit state plus the editing rules (numeric-only
    /// filtering, max length, backspace). Platform text entry (desktop keyboard, Android soft
    /// keyboard) feeds characters in via <see cref="Append"/>/<see cref="Backspace"/>; the
    /// rules here are unit-tested, the platform plumbing is the thin I/O edge.
    /// </summary>
    public sealed class UiTextField
    {
        public string Id { get; }
        public UiRect Bounds { get; set; }
        public string Label { get; set; }
        public string Value { get; set; } = "";
        public bool Focused { get; set; }

        /// <summary>When true, only digits 0–9 are accepted (e.g. the port field).</summary>
        public bool Numeric { get; set; }
        /// <summary>Maximum accepted length (0 = unlimited).</summary>
        public int MaxLength { get; set; }

        public UiTextField(string id, UiRect bounds, string label)
        {
            Id = id; Bounds = bounds; Label = label;
        }

        public bool HitTest(int x, int y) => Bounds.Contains(x, y);

        /// <summary>Append a character if it passes the field's rules; otherwise ignore it.</summary>
        public void Append(char c)
        {
            if (Numeric && (c < '0' || c > '9')) return;
            if (c < ' ' || c > '~') return;                 // printable ASCII only
            if (MaxLength > 0 && Value.Length >= MaxLength) return;
            Value += c;
        }

        /// <summary>Remove the last character (no-op when empty).</summary>
        public void Backspace()
        {
            if (Value.Length > 0) Value = Value.Substring(0, Value.Length - 1);
        }
    }
}
