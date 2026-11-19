// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – liest Server-Sent Events zeilenweise.

using System.Text;

namespace TwitchCompanion.RimWorld.Core
{
    public sealed class SseEvent
    {
        public string Name;
        public string Data;
    }

    /// <summary>
    /// Zerlegt einen SSE-Strom nach dem HTML-Standard: <c>event:</c>, <c>data:</c> (mehrzeilig), Leerzeile beendet
    /// ein Ereignis, Zeilen mit <c>:</c> sind Kommentare (Lebenszeichen). <c>id:</c> und <c>retry:</c> werden
    /// ignoriert; die Wartezeit vor dem Neuverbinden ist fest (Spec: 5 s).
    /// </summary>
    public sealed class SseParser
    {
        private string _name;
        private readonly StringBuilder _data = new StringBuilder();
        private bool _hasData;

        /// <summary>Eine Zeile ohne Zeilenende. Liefert ein fertiges Ereignis oder <c>null</c>.</summary>
        public SseEvent Feed(string line)
        {
            if (line == null) return null;
            if (line.Length == 0)
            {
                if (!_hasData) { _name = null; return null; }
                var ev = new SseEvent { Name = string.IsNullOrEmpty(_name) ? "message" : _name, Data = _data.ToString() };
                _name = null;
                _data.Clear();
                _hasData = false;
                return ev;
            }
            if (line[0] == ':') return null;

            int colon = line.IndexOf(':');
            string field = colon < 0 ? line : line.Substring(0, colon);
            string value = colon < 0 ? string.Empty : line.Substring(colon + 1);
            if (value.Length > 0 && value[0] == ' ') value = value.Substring(1);

            if (field == "event") _name = value;
            else if (field == "data")
            {
                if (_hasData) _data.Append('\n');
                _data.Append(value);
                _hasData = true;
            }
            return null;
        }

        public void Reset()
        {
            _name = null;
            _data.Clear();
            _hasData = false;
        }
    }
}
