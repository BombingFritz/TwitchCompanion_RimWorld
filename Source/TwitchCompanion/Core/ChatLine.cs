// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – eine Chatnachricht, wie sie die Mod an RICS weitergibt.

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Eingehende Chatnachricht. Felder entsprechen dem, was die Mod vom Server braucht
    /// (Server-Issue #74, Ereignis <c>chat</c>) oder aus dem Testmodus.
    /// </summary>
    public sealed class ChatLine
    {
        public string MessageId;
        public string UserId;
        public string Login;
        public string DisplayName;
        public string Text;
        /// <summary>Empfang beim Server (UTC), falls gemeldet.</summary>
        public System.DateTime? ReceivedAt;
        public bool IsBroadcaster;
        public bool IsModerator;
        public bool IsVip;
        public bool IsSubscriber;
        /// <summary>Zuschauer hat <c>!privat</c> gewählt (Server-ADR 0022); nimmt nicht teil.</summary>
        public bool IsPrivate;

        /// <summary>Name für RICS: Anzeigename, wenn er sich nur in der Schreibweise vom Login unterscheidet, sonst der Login.</summary>
        public string NameForRics
        {
            get
            {
                if (!string.IsNullOrEmpty(DisplayName) && !string.IsNullOrEmpty(Login)
                    && string.Equals(DisplayName, Login, System.StringComparison.OrdinalIgnoreCase))
                    return DisplayName;
                return string.IsNullOrEmpty(Login) ? DisplayName : Login;
            }
        }

        /// <summary>Mindestangaben vorhanden, damit RICS den Zuschauer sicher zuordnen kann.</summary>
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(UserId) && !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(Text);
    }
}
