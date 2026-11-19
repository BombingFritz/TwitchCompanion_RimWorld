// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – kleiner JSON-Leser und -Schreiber ohne Fremdbibliothek.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Liest JSON in <see cref="Dictionary{TKey,TValue}"/> (Objekt), <see cref="List{T}"/> (Feld), <c>string</c>,
    /// <c>double</c>, <c>bool</c> und <c>null</c>. Reicht für die kleinen Antworten des Servers; RimWorld bringt
    /// keinen JSON-Leser mit, den die Mod sicher nutzen könnte.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON fehlt");
            int pos = 0;
            object value = ReadValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length) throw new FormatException("Zeichen nach dem JSON-Wert an Stelle " + pos);
            return value;
        }

        /// <summary>Wie <see cref="Parse"/>, liefert bei Fehlern <c>null</c> statt einer Ausnahme.</summary>
        public static Dictionary<string, object> TryParseObject(string text)
        {
            try { return Parse(text) as Dictionary<string, object>; }
            catch (FormatException) { return null; }
        }

        public static string Quote(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── Zugriff mit Typprüfung ──────────────────────────────────────

        public static string GetString(Dictionary<string, object> obj, string key)
            => obj != null && obj.TryGetValue(key, out object v) ? v as string : null;

        public static bool GetBool(Dictionary<string, object> obj, string key, bool fallback = false)
            => obj != null && obj.TryGetValue(key, out object v) && v is bool b ? b : fallback;

        public static bool? GetNullableBool(Dictionary<string, object> obj, string key)
            => obj != null && obj.TryGetValue(key, out object v) && v is bool b ? b : (bool?)null;

        public static double? GetNumber(Dictionary<string, object> obj, string key)
            => obj != null && obj.TryGetValue(key, out object v) && v is double d ? d : (double?)null;

        public static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key)
            => obj != null && obj.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;

        public static List<object> GetArray(Dictionary<string, object> obj, string key)
            => obj != null && obj.TryGetValue(key, out object v) ? v as List<object> : null;

        /// <summary>ISO-Zeitpunkt als UTC; <c>null</c>, wenn er fehlt oder unlesbar ist.</summary>
        public static DateTime? GetTime(Dictionary<string, object> obj, string key)
        {
            string s = GetString(obj, key);
            if (string.IsNullOrEmpty(s)) return null;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t))
                return DateTime.SpecifyKind(t, DateTimeKind.Utc);
            return null;
        }

        // ── Leser ───────────────────────────────────────────────────────

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\n' || s[pos] == '\r')) pos++;
        }

        private static object ReadValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) throw new FormatException("JSON endet unerwartet");
            char c = s[pos];
            switch (c)
            {
                case '{': return ReadObject(s, ref pos);
                case '[': return ReadArray(s, ref pos);
                case '"': return ReadString(s, ref pos);
                case 't': Expect(s, ref pos, "true"); return true;
                case 'f': Expect(s, ref pos, "false"); return false;
                case 'n': Expect(s, ref pos, "null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(s, ref pos);
                    throw new FormatException("Unerwartetes Zeichen '" + c + "' an Stelle " + pos);
            }
        }

        private static void Expect(string s, ref int pos, string word)
        {
            if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0) throw new FormatException("'" + word + "' erwartet an Stelle " + pos);
            pos += word.Length;
        }

        private static Dictionary<string, object> ReadObject(string s, ref int pos)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            pos++; // {
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != '"') throw new FormatException("Schlüssel erwartet an Stelle " + pos);
                string key = ReadString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw new FormatException("':' erwartet an Stelle " + pos);
                pos++;
                result[key] = ReadValue(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("JSON-Objekt endet unerwartet");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return result; }
                throw new FormatException("',' oder '}' erwartet an Stelle " + pos);
            }
        }

        private static List<object> ReadArray(string s, ref int pos)
        {
            var result = new List<object>();
            pos++; // [
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return result; }
            while (true)
            {
                result.Add(ReadValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length) throw new FormatException("JSON-Feld endet unerwartet");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return result; }
                throw new FormatException("',' oder ']' erwartet an Stelle " + pos);
            }
        }

        private static string ReadString(string s, ref int pos)
        {
            var sb = new StringBuilder();
            pos++; // "
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (pos >= s.Length) break;
                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > s.Length) throw new FormatException("Unvollständiges \\u");
                        sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        pos += 4;
                        break;
                    default: throw new FormatException("Unbekannte Escape-Folge \\" + e);
                }
            }
            throw new FormatException("Zeichenkette endet unerwartet");
        }

        private static double ReadNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length && "+-0123456789.eE".IndexOf(s[pos]) >= 0) pos++;
            if (!double.TryParse(s.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw new FormatException("Ungültige Zahl an Stelle " + start);
            return d;
        }
    }
}
