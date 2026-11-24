// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    /// <summary>
    /// Die erfundenen Beispieldateien in <c>docs/kartei-beispiel/</c> entsprechen genau dem, was die Mod schreibt.
    /// Neu erzeugen: <c>KARTEI_BEISPIEL_SCHREIBEN=1 dotnet test</c>.
    /// </summary>
    public class KarteiBeispielTests
    {
        private static string Root
        {
            get
            {
                string dir = AppContext.BaseDirectory;
                while (dir != null && !File.Exists(Path.Combine(dir, "CHANGELOG.md"))) dir = Path.GetDirectoryName(dir);
                return Path.Combine(dir ?? throw new InvalidOperationException("Repository nicht gefunden"), "docs", "kartei-beispiel");
            }
        }

        private static string Pretty(string json)
        {
            using var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                doc.WriteTo(w);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray()).Replace("\r\n", "\n") + "\n";
        }

        [Fact]
        public void BeispieldateienSindAktuell()
        {
            KarteiSnapshot s = KarteiBeispiel.Snapshot();
            var files = new (string path, string json)[]
            {
                ("rimworld/pawns.json", KarteiJson.Pawns(s, forServer: false)),
                ("rimworld/colony.json", KarteiJson.Colony(s)),
                ("an-den-server.json", KarteiJson.ServerBody(s)),
            };
            bool write = Environment.GetEnvironmentVariable("KARTEI_BEISPIEL_SCHREIBEN") == "1";
            foreach (var (path, json) in files)
            {
                string full = Path.Combine(Root, path);
                string expected = Pretty(json);
                if (write)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllText(full, expected);
                }
                Assert.True(File.Exists(full), "fehlt: " + path);
                Assert.Equal(expected, File.ReadAllText(full).Replace("\r\n", "\n"));
            }
            Assert.DoesNotContain("linkedUserId", File.ReadAllText(Path.Combine(Root, "rimworld/pawns.json")));
            Assert.Contains("linkedUserId", File.ReadAllText(Path.Combine(Root, "an-den-server.json")));
        }
    }
}
