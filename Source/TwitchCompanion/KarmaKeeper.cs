// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Karma der Zuschauer (RICS) und der Kolonie, ausgleichende Gerechtigkeit (Feature-Issue #24).

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
    internal static class KarmaKeeper
    {
        private const string LogPrefix = "[Twitch Companion] ";
        private static readonly System.Random Rng = new System.Random();

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;
        private static TimeSpan HalfLife => TimeSpan.FromHours(Math.Max(0.1f, Settings.colonyKarmaHalfLifeHours));

        /// <summary>Karma der Kolonie im geladenen Spielstand; <c>null</c> ohne Spielstand.</summary>
        public static ColonyKarma Colony => Current.Game?.GetComponent<CompanionGameComponent>()?.ColonyKarma;

        /// <summary>Aktueller Wert (mit Abklingen bis jetzt), 0 ohne Spielstand.</summary>
        public static float ColonyValue
        {
            get
            {
                ColonyKarma c = Colony;
                if (c == null) return 0f;
                c.Decay(DateTime.UtcNow, HalfLife);
                return c.Value;
            }
        }

        public static string ColonyText => (ColonyValue >= 0 ? "+" : string.Empty) + ColonyValue.ToString("0") + " (" + ColonyKarma.Word(ColonyValue) + ")";

        /// <summary>Verbucht eine Aktion: Karma des Zuschauers (wenn angegeben) und der Kolonie.</summary>
        public static void Record(KarmaAction action, ChatLine viewer = null, int count = 1)
        {
            if (!Settings.karmaEnabled) return;
            float v = KarmaRules.ViewerDelta(action);
            if (viewer != null && Math.Abs(v) > 0f) GiveViewerKarma(viewer, v);
            float c = KarmaRules.ColonyDelta(action, count);
            ColonyKarma colony = Colony;
            if (colony != null && Math.Abs(c) > 0f) colony.Add(c, DateTime.UtcNow, HalfLife);
        }

        private static void GiveViewerKarma(ChatLine line, float delta)
        {
            try
            {
                object viewer = RicsApi.GetOrCreateViewer(line.UserId, line.Login);
                if (viewer == null) return;
                AccessTools.Method(viewer.GetType(), delta > 0 ? "GiveKarma" : "TakeKarma", new[] { typeof(float) })
                    ?.Invoke(viewer, new object[] { Math.Abs(delta) });
                RicsEconomy.SaveViewers();
            }
            catch (Exception ex) { Log.Warning(LogPrefix + "Karma nicht verbucht: " + ex.Message); }
        }

        public static float? ViewerKarma(ChatLine line)
        {
            object viewer = RicsApi.GetOrCreateViewer(line.UserId, line.Login);
            object k = viewer == null ? null : AccessTools.Property(viewer.GetType(), "Karma")?.GetValue(viewer);
            return k is float f ? f : (float?)null;
        }

        // ── Neuer Kolonist nach Karma des Käufers ────────────────────────

        /// <summary>Prägt den gerade gekauften Kolonisten nach dem Karma des Käufers (Hauptthread).</summary>
        public static void ApplyPawnFate(ChatLine buyer)
        {
            if (!Settings.karmaEnabled || !Settings.pawnFates) return;
            float? karma = ViewerKarma(buyer);
            if (karma == null) return;
            PawnFate fate = PawnFates.For(karma.Value);
            if (fate == PawnFate.Normal) return;
            Pawn pawn = AssignedPawn(buyer.NameForRics);
            if (pawn?.story?.traits == null) return;
            var effects = new List<string>();
            try
            {
                if (fate == PawnFate.Blessed) AddTrait(pawn, PawnFates.GoodTraits, effects);
                else AddTrait(pawn, PawnFates.BadTraits, effects);
                if (fate == PawnFate.Cursed) AddDisease(pawn, effects);
            }
            catch (Exception ex) { Log.Warning(LogPrefix + "Karma-Folgen nicht gesetzt: " + ex.Message); }
            if (effects.Count == 0) return;
            string text = fate == PawnFate.Blessed
                ? buyer.NameForRics + " hat strahlendes Karma: " + pawn.Name.ToStringShort + " ist " + string.Join(", ", effects) + "."
                : buyer.NameForRics + " hat " + (fate == PawnFate.Cursed ? "finsteres" : "schlechtes") + " Karma: " + pawn.Name.ToStringShort + " ist " + string.Join(", ", effects) + ".";
            Find.LetterStack.ReceiveLetter("Karma", text, fate == PawnFate.Blessed ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, pawn);
            Log.Message(LogPrefix + "Karma-Folgen für einen neuen Kolonisten: " + fate + " (" + effects.Count + ").");
        }

        private static void AddTrait(Pawn pawn, IReadOnlyList<KeyValuePair<string, int>> pool, List<string> effects)
        {
            foreach (KeyValuePair<string, int> t in pool.InRandomOrder())
            {
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(t.Key);
                if (def == null || PawnFates.IsForbidden(t.Key, t.Value) || pawn.story.traits.HasTrait(def) || pawn.story.traits.allTraits.Any(x => x.def.ConflictsWith(def) || def.ConflictsWith(x))) continue;
                var trait = new Trait(def, t.Value, forced: true);
                pawn.story.traits.GainTrait(trait);
                effects.Add(trait.LabelCap);
                return;
            }
        }

        private static void AddDisease(Pawn pawn, List<string> effects)
        {
            foreach (string name in PawnFates.Diseases.InRandomOrder())
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                Hediff h = HediffMaker.MakeHediff(def, pawn);
                h.Severity = Math.Max(h.Severity, 0.35f);
                pawn.health.AddHediff(h);
                effects.Add("schwer krank (" + def.LabelCap + ")");
                return;
            }
        }

        private static Pawn AssignedPawn(string username)
        {
            Type manager = AccessTools.TypeByName("CAP_ChatInteractive.GameComponent_PawnAssignmentManager");
            object comp = manager == null ? null : AccessTools.Method(typeof(Game), "GetComponent", Type.EmptyTypes)?.MakeGenericMethod(manager).Invoke(Current.Game, null);
            return comp == null ? null : AccessTools.Method(manager, "GetAssignedPawn", new[] { typeof(string) })?.Invoke(comp, new object[] { username }) as Pawn;
        }

        // ── Ausgleichende Gerechtigkeit ───────────────────────────────────

        /// <summary>Würfelt, ob in dieser Abstimmung ein besonderes Ereignis zur Wahl steht, und liefert es; sonst <c>null</c>.</summary>
        public static EventCandidate JusticeCandidate(Map map)
        {
            if (!Settings.karmaEnabled || map == null) return null;
            JusticeKind kind = Justice.Roll(ColonyValue, Settings.justiceThreshold, Settings.justiceMaxChance, Rng);
            if (kind == JusticeKind.None) return null;
            IEnumerable<CatalogEntry> pool = kind == JusticeKind.Doom ? EventCatalog.Doom : EventCatalog.Blessing;
            foreach (CatalogEntry e in pool.InRandomOrder())
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(e.DefName);
                if (def?.Worker == null) continue;
                try
                {
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    parms.forced = true;
                    if (!def.Worker.CanFireNow(parms)) continue;
                }
                catch { continue; }
                Log.Message(LogPrefix + "Ausgleichende Gerechtigkeit (Kolonie " + ColonyText + "): " + (kind == JusticeKind.Doom ? "verheerende" : "segensreiche") + " Option.");
                return new EventCandidate { DefName = def.defName, Label = def.LabelCap.ToString(), Karma = e.Karma, Weight = 1f };
            }
            return null;
        }
    }
}
