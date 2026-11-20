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
            return BalanceOf(getViewer?.Invoke(null, new[] { messageWrapper }), withLootboxes: true);
        }

        /// <summary>Kurzes Guthaben eines Zuschauers („Guthaben: 175 Pfennig · Karma 🙂“), ohne Lootbox-Zahl.</summary>
        public static string BalanceFor(ChatLine line) => BalanceOf(RicsApi.GetOrCreateViewer(line.UserId, line.Login), withLootboxes: false);

        public static int? CoinsOf(ChatLine line)
        {
            object viewer = RicsApi.GetOrCreateViewer(line.UserId, line.Login);
            return viewer == null ? (int?)null : Get(viewer, "Coins", 0);
        }

        private static string BalanceOf(object viewer, bool withLootboxes)
        {
            if (viewer == null) return null;
            int coins = Get(viewer, "Coins", 0);
            float karma = Get(viewer, "Karma", 100f);
            string username = Get<string>(viewer, "Username", null);
            MethodInfo emoji = AccessTools.Method(AccessTools.TypeByName(Ns + "Commands.ViewerCommands.Bal"), "GetKarmaEmoji");
            string karmaEmoji = emoji?.Invoke(null, new object[] { karma }) as string;
            int lootboxes = withLootboxes && username != null ? LootboxCount(username) : 0;
            return ChatTranslator.Balance(coins, CurrencyName, karmaEmoji, lootboxes);
        }

        private static GameComponent LootboxComponent =>
            Current.Game?.components?.FirstOrDefault(c => c.GetType().FullName == Ns + "LootBoxComponent");

        private static int LootboxCount(string username)
        {
            GameComponent box = LootboxComponent;
            MethodInfo count = box == null ? null : AccessTools.Method(box.GetType(), "HowManyLootboxesDoesViewerHave", new[] { typeof(string) });
            return count == null ? 0 : (int)count.Invoke(box, new object[] { username.ToLowerInvariant() });
        }

        /// <summary>
        /// Vergibt die tägliche Lootbox (wie RICS bei der ersten Nachricht des Tages) und öffnet alle offenen sofort
        /// (Nutzer, 20.11.2026: kein eigener Lootbox-Befehl). Gibt die Antwort von RICS zurück oder <c>null</c>.
        /// </summary>
        public static string AwardAndOpenLootboxes(ChatLine line)
        {
            GameComponent box = LootboxComponent;
            if (box == null) return null;
            object wrapper = RicsApi.NewWrapperFor(line);
            AccessTools.Method(box.GetType(), "ProcessViewerMessage", new[] { wrapper.GetType() })?.Invoke(box, new[] { wrapper });
            object viewer = RicsApi.GetOrCreateViewer(line.UserId, line.Login);
            string username = Get<string>(viewer, "Username", null);
            if (username == null || LootboxCount(username) <= 0) return null;
            MethodInfo openAll = AccessTools.Method(AccessTools.TypeByName(Ns + "Commands.ViewerCommands.LootBoxCommandHandler"), "OpenAllLootboxes");
            return openAll?.Invoke(null, new[] { wrapper, box, GlobalSettings }) as string;
        }

        /// <summary>Laden auf die Auswahl beschränken: nur diese Items an, mit eigenem Namen und Grenze je Kauf; RICS speichert.</summary>
        public static string ApplyShopSelection(IEnumerable<ShopEntry> entries)
        {
            Type inventory = AccessTools.TypeByName(Ns + "Store.StoreInventory");
            IDictionary items = AccessTools.Property(inventory, "AllStoreItems")?.GetValue(null) as IDictionary;
            if (items == null) return "RICS-Laden nicht gefunden";
            Dictionary<string, ShopEntry> wanted = entries.ToDictionary(e => e.DefName, StringComparer.OrdinalIgnoreCase);
            int on = 0, off = 0;
            var missing = new HashSet<string>(wanted.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (object item in items.Values)
            {
                string defName = Get<string>(item, "DefName", null);
                if (defName != null && wanted.TryGetValue(defName, out ShopEntry e))
                {
                    Set(item, "Enabled", true);
                    Set(item, "CustomName", e.CustomName);
                    Set(item, "HasQuantityLimit", true);
                    Set(item, "QuantityLimit", e.MaxPerPurchase);
                    PropertyInfo mode = AccessTools.Property(item.GetType(), "LimitMode");
                    if (mode != null && mode.PropertyType.IsEnum) mode.SetValue(item, Enum.Parse(mode.PropertyType, "Each"));
                    missing.Remove(defName);
                    on++;
                }
                else if (Get(item, "Enabled", false))
                {
                    Set(item, "Enabled", false);
                    off++;
                }
            }
            AccessTools.Method(inventory, "SaveStoreToJsonImmediate")?.Invoke(null, null);
            return on + " Items im Laden, " + off + " abgeschaltet" + (missing.Count > 0 ? "; nicht gefunden: " + string.Join(", ", missing) : string.Empty);
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
