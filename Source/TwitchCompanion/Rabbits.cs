// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Killer-Kaninchen (Nutzer, 22.11.2026): kaufbar, mit Beitritt per !joinraid wie beim Raid.

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TwitchCompanion.RimWorld.Core;
using TwitchCompanion.RimWorld.Rics;
using Verse;

namespace TwitchCompanion.RimWorld
{
    internal static class Rabbits
    {
        private const string LogPrefix = "[Twitch Companion] ";
        private static readonly System.Random Rng = new System.Random();

        public static RabbitAttack Current { get; private set; }
        public static string LastAttack { get; private set; }

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        public static int? Price
        {
            get
            {
                int? pawn = RicsEconomy.PawnPrice();
                return pawn.HasValue ? KillerRabbits.Price(pawn.Value, Settings.rabbitPriceShare) : (int?)null;
            }
        }

        /// <summary>Kauf über <c>$Killer-Kaninchen</c> (Hauptthread). Liefert die Antwort für die Einblendung.</summary>
        public static string Buy(ChatLine line)
        {
            DateTime now = DateTime.UtcNow;
            if (!Settings.rabbitsEnabled) return "Killer-Kaninchen gibt es gerade nicht.";
            if (Current != null) return "Es läuft schon ein Kaninchen-Angriff: mit !joinraid mitmachen.";
            if (RaidWindowActive) return "Gerade läuft ein Twitch-Raid. Kaninchen bitte später.";
            if (HomeMap == null) return "Gerade keine Kolonie geladen.";
            TimeSpan wait = KillerRabbits.Cooldown(Settings.LastRabbitUtc, now, TimeSpan.FromMinutes(Math.Max(0, Settings.rabbitCooldownMinutes)));
            if (wait > TimeSpan.Zero) return "Das nächste Killer-Kaninchen ist in " + Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes)) + " min möglich.";
            int? price = Price;
            if (price == null) return "Preis unbekannt (RICS-Rasseneinstellungen fehlen).";
            if (!RicsEconomy.TakeCoins(line, price.Value)) return "Zu wenig Pfennig: Ein Killer-Kaninchen kostet " + price.Value + ".";

            Current = new RabbitAttack(line.UserId, line.NameForRics, now, TimeSpan.FromSeconds(Math.Max(15, Settings.rabbitWindowSeconds)));
            Settings.MarkRabbit(now);
            KarmaKeeper.Record(KarmaAction.RabbitPurchase, line, 0); // Zuschauer −20; die Kolonie zählt beim Angriff je Kaninchen
            string text = "🐇 " + line.NameForRics + " hetzt ein Killer-Kaninchen auf die Kolonie! Wer mit !joinraid in den nächsten "
                + Math.Max(15, Settings.rabbitWindowSeconds) + " s mitmacht, greift als Kaninchen mit an.";
            Messages.Message(text, MessageTypeDefOf.ThreatSmall, false);
            Log.Message(LogPrefix + "Killer-Kaninchen gekauft (" + price.Value + " Pfennig), Beitrittsfenster offen.");
            return "Killer-Kaninchen gekauft (" + price.Value + " Pfennig).";
        }

        /// <summary><c>!joinraid</c> während eines Kaninchen-Angriffs.</summary>
        public static string Join(ChatLine line)
        {
            RabbitAttack attack = Current;
            if (attack == null) return null;
            if (!attack.Join(line.UserId, line.NameForRics, DateTime.UtcNow)) return "Du bist schon dabei.";
            KarmaKeeper.Record(KarmaAction.RabbitJoin, line);
            return "🐇 Dabei! (" + attack.Count + " Kaninchen gemeldet)";
        }

        public static bool WindowOpen => Current != null && Current.IsOpen(DateTime.UtcNow);

        /// <summary>Im Hauptthread, aus <see cref="Bridge.MainThreadTick"/>.</summary>
        public static void Tick()
        {
            RabbitAttack attack = Current;
            if (attack == null || attack.IsOpen(DateTime.UtcNow)) return;
            Current = null;
            Spawn(attack);
        }

        private static void Spawn(RabbitAttack attack)
        {
            Map map = HomeMap;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rabbit") ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Hare");
            if (map == null || kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Animal))
            {
                LastAttack = DateTime.Now.ToString("HH:mm") + ": Kaninchen konnten nicht losgelassen werden (Karte, Tierart oder Rand fehlt).";
                Log.Warning(LogPrefix + LastAttack);
                return;
            }
            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            int max = KillerRabbits.MaxRabbits(points, Settings.rabbitStrengthFactor, kind.combatPower, attack.Count);
            List<string> names = attack.Attackers(max, Rng);
            var pawns = new List<Pawn>();
            EventVotes.NoteExternalEvent(); // Abstimmungen halten danach die Ruhezeit ein
            foreach (string name in names)
            {
                try
                {
                    Pawn rabbit = PawnGenerator.GeneratePawn(kind, null);
                    rabbit.Name = new NameSingle(name);
                    GenSpawn.Spawn(rabbit, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
                    rabbit.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, forced: true);
                    pawns.Add(rabbit);
                }
                catch (Exception ex) { Log.Warning(LogPrefix + "Kaninchen nicht erzeugt: " + ex.Message); }
            }
            if (pawns.Count == 0) return;
            KarmaKeeper.Record(KarmaAction.RabbitPurchase, null, pawns.Count);
            string list = string.Join(", ", names);
            Find.LetterStack.ReceiveLetter("Killer-Kaninchen!",
                pawns.Count + " Killer-Kaninchen greifen die Kolonie an: " + list + "."
                + (attack.Count > pawns.Count ? " (" + (attack.Count - pawns.Count) + " weitere hatten keinen Platz.)" : string.Empty),
                LetterDefOf.ThreatSmall, new LookTargets(pawns));
            LastAttack = DateTime.Now.ToString("HH:mm") + ": " + pawns.Count + " von " + attack.Count + " Kaninchen (Bedrohung " + (int)points + ").";
            Log.Message(LogPrefix + "Killer-Kaninchen: " + LastAttack);
        }

        private static Map HomeMap => Find.Maps?.Where(m => m.IsPlayerHome).InRandomOrder().FirstOrDefault();

        /// <summary>Läuft gerade das Beitrittsfenster eines RICS-Raids? Dann gehört !joinraid dorthin.</summary>
        public static bool RaidWindowActive
        {
            get
            {
                Type mod = AccessTools.TypeByName("CAP_ChatInteractive.CAPChatInteractiveMod");
                object instance = mod == null ? null : AccessTools.Property(mod, "Instance")?.GetValue(null);
                object service = instance == null ? null : AccessTools.Property(mod, "TwitchService")?.GetValue(instance);
                return service != null && AccessTools.Property(service.GetType(), "IsRaidJoinWindowActive")?.GetValue(service) is bool b && b;
            }
        }
    }
}
