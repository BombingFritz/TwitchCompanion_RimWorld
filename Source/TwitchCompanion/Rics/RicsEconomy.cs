// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Preise, Währung und Pfennig in RICS (per Reflexion, geprüft an RICS 1.56).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TwitchCompanion.RimWorld.Core;
using Verse;

namespace TwitchCompanion.RimWorld.Rics
{
    internal sealed class PricePlanResult
    {
        public int Items;
        public int Animals;
        public int AnimalsRaised;
        public int Asparagus;
        public int Races;
        public int Xenotypes;
        public int CoinsPerHour;
        public string Problem;

        public override string ToString() => Problem ?? $"{Items} Items (davon {Animals} Tiere, {AnimalsRaised} auf mindestens 1 h angehoben, {Asparagus} Spargel), {Races} Rassen, {Xenotypes} Xenotypen; 1 Zuschaustunde = {CoinsPerHour} Pfennig";
    }

    /// <summary>
    /// Greift auf die Wirtschaft von RICS zu: Grundeinstellungen, Shop, Rassen, Zuschauer. Preise werden über RICS
    /// selbst gespeichert, damit RICS-Editor und Preisliste die echten Werte zeigen (Feature-Issue #10).
    /// </summary>
    internal static class RicsEconomy
    {
        private const string Ns = "CAP_ChatInteractive.";
        public const string Currency = "Pfennig";

        private static object Mod => AccessTools.TypeByName(Ns + "CAPChatInteractiveMod") is Type t
            ? AccessTools.Property(t, "Instance")?.GetValue(null) : null;

        public static object GlobalSettings => RicsApi.GetGlobalSettings();

        public static int BaseCoinReward => Get<int>(GlobalSettings, "BaseCoinReward", 10);
        public static string CurrencyName => Get<string>(GlobalSettings, "CurrencyName", null);
        public static int CoinsPerHour => Economy.CoinsPerHour(BaseCoinReward);

        /// <summary>Höchstwert der Lootbox-Spanne aus RICS (Vorgabe 750).</summary>
        public static int LootboxMax
        {
            get
            {
                object range = Get<object>(GlobalSettings, "LootBoxRandomCoinRange", null);
                return range is IntRange r ? r.max : 750;
            }
        }

        public static bool CurrencyIsPfennig => string.Equals(CurrencyName?.Trim(), Currency, StringComparison.OrdinalIgnoreCase);

        /// <summary>Setzt die Währung in RICS auf „Pfennig“ und speichert die RICS-Einstellungen.</summary>
        public static void SetCurrencyPfennig()
        {
            object gs = GlobalSettings;
            FieldInfo f = gs == null ? null : AccessTools.Field(gs.GetType(), "CurrencyName");
            if (f == null) throw new InvalidOperationException("RICS-Einstellung CurrencyName nicht gefunden");
            f.SetValue(gs, " " + Currency + " ");
            WriteRicsSettings();
        }

        private static void WriteRicsSettings()
        {
            object mod = Mod;
            if (mod != null) AccessTools.Method(mod.GetType(), "WriteSettings")?.Invoke(mod, null);
        }

        /// <summary>
        /// Setzt alle Grundpreise nach dem Plan: Items = RICS-Standardpreis × Faktor (immer vom Standard aus, also
        /// beliebig oft wiederholbar), Tiere mindestens <see cref="PricePlan.AnimalMinHours"/>, Spargel fest,
        /// neue Kolonisten <see cref="PricePlan.NewPawnHours"/> (Xenotypen im alten Verhältnis zur Rasse).
        /// </summary>
        public static PricePlanResult ApplyPricePlan(PricePlan plan)
        {
            var result = new PricePlanResult { CoinsPerHour = CoinsPerHour };
            Type inventory = AccessTools.TypeByName(Ns + "Store.StoreInventory");
            Type storeItem = AccessTools.TypeByName(Ns + "Store.StoreItem");
            Type raceManager = AccessTools.TypeByName("_CAP__Chat_Interactive.Utilities.RaceSettingsManager");
            MethodInfo calc = AccessTools.Method(storeItem, "CalculateBasePrice", new[] { typeof(ThingDef) });
            IDictionary items = AccessTools.Property(inventory, "AllStoreItems")?.GetValue(null) as IDictionary;
            if (items == null || calc == null) { result.Problem = "RICS-Shop nicht gefunden (StoreInventory/StoreItem)"; return result; }

            int animalMin = Economy.HoursToCoins(plan.AnimalMinHours, result.CoinsPerHour);
            int asparagus = Economy.HoursToCoins(plan.AsparagusMinutes / 60f, result.CoinsPerHour);
            foreach (object item in items.Values)
            {
                string defName = Get<string>(item, "DefName", null);
                ThingDef def = defName == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null) continue;
                int price = Economy.ScaledItemPrice((int)calc.Invoke(null, new object[] { def }), plan.ItemFactor);
                string category = Get<string>(item, "Category", null);
                if (def.race != null || string.Equals(category, "Animals", StringComparison.OrdinalIgnoreCase) || string.Equals(category, "Animal", StringComparison.OrdinalIgnoreCase))
                {
                    result.Animals++;
                    if (price < animalMin) { price = animalMin; result.AnimalsRaised++; }
                }
                if (Economy.IsAsparagus(def.defName, def.label)) { price = asparagus; result.Asparagus++; }
                Set(item, "BasePrice", price);
                result.Items++;
            }
            AccessTools.Method(inventory, "SaveStoreToJsonImmediate")?.Invoke(null, null);

            if (raceManager != null && AccessTools.Property(raceManager, "RaceSettings")?.GetValue(null) is IDictionary races)
            {
                int pawnPrice = Economy.HoursToCoins(plan.NewPawnHours, result.CoinsPerHour);
                foreach (object race in races.Values)
                {
                    int oldBase = Get(race, "BasePrice", 0);
                    if (Get<object>(race, "XenotypePrices", null) is IDictionary xeno)
                    {
                        foreach (object key in xeno.Keys.Cast<object>().ToList())
                        {
                            xeno[key] = (float)Economy.ScaleRelative(Convert.ToSingle(xeno[key]), oldBase, pawnPrice);
                            result.Xenotypes++;
                        }
                        if (xeno.Contains("Baseliner")) xeno["Baseliner"] = (float)pawnPrice;
                    }
                    Set(race, "BasePrice", pawnPrice);
                    result.Races++;
                }
                AccessTools.Method(raceManager, "SaveSettings")?.Invoke(null, null);
            }
            return result;
        }

        /// <summary>Schreibt einem Zuschauer Pfennig gut; legt ihn in RICS an, falls nötig (wie beim ersten Chat).</summary>
        public static bool GiveCoins(string userId, string login, int amount)
        {
            object viewer = RicsApi.GetOrCreateViewer(userId, login);
            if (viewer == null) return false;
            AccessTools.Method(viewer.GetType(), "GiveCoins", new[] { typeof(int) })?.Invoke(viewer, new object[] { amount });
            return true;
        }

        /// <summary>Kurze !bal-Antwort für den Zuschauer der Nachricht; <c>null</c>, wenn nicht ermittelbar.</summary>
        public static string ShortBalance(object messageWrapper)
        {
            Type viewers = AccessTools.TypeByName(Ns + "Viewers");
            MethodInfo getViewer = AccessTools.Method(viewers, "GetViewer", new[] { messageWrapper.GetType() });
            object viewer = getViewer?.Invoke(null, new[] { messageWrapper });
            if (viewer == null) return null;
            int coins = Get(viewer, "Coins", 0);
            float karma = Get(viewer, "Karma", 100f);
            string username = Get<string>(viewer, "Username", null);
            MethodInfo emoji = AccessTools.Method(AccessTools.TypeByName(Ns + "Commands.ViewerCommands.Bal"), "GetKarmaEmoji");
            string karmaEmoji = emoji?.Invoke(null, new object[] { karma }) as string;

            int lootboxes = 0;
            GameComponent box = Current.Game?.components?.FirstOrDefault(c => c.GetType().FullName == Ns + "LootBoxComponent");
            MethodInfo count = box == null ? null : AccessTools.Method(box.GetType(), "HowManyLootboxesDoesViewerHave", new[] { typeof(string) });
            if (count != null && username != null) lootboxes = (int)count.Invoke(box, new object[] { username });

            return ChatTranslator.Balance(coins, CurrencyName, karmaEmoji, lootboxes);
        }

        public static void SaveViewers() => AccessTools.Method(AccessTools.TypeByName(Ns + "Viewers"), "SaveViewers")?.Invoke(null, null);

        private static T Get<T>(object target, string name, T fallback)
        {
            if (target == null) return fallback;
            Type t = target.GetType();
            object v = AccessTools.Property(t, name) is PropertyInfo p ? p.GetValue(target) : AccessTools.Field(t, name)?.GetValue(target);
            return v is T typed ? typed : fallback;
        }

        private static void Set(object target, string name, object value)
        {
            Type t = target.GetType();
            if (AccessTools.Property(t, name) is PropertyInfo p) p.SetValue(target, value);
            else AccessTools.Field(t, name)?.SetValue(target, value);
        }
    }
}
