using System;

namespace JoinLeaveAlerts
{
    public static class DiscordEmbedPayload
    {
        private const int OnlineColor = 5763719;
        private const int EmptyColor = 15548997;

        public static string CreateStatus(StatusEmbedOptions options, string activity, PlayerStatusRoster roster, bool online, TimeSpan uptime, DateTime updatedAt)
        {
            return CreateStatus(options, activity, roster, online, uptime, updatedAt, updatedAt);
        }

        public static string CreateStatus(StatusEmbedOptions options, string activity, PlayerStatusRoster roster, bool online, TimeSpan uptime, DateTime updatedAt, DateTime activityAt)
        {
            options = options ?? new StatusEmbedOptions();
            roster = roster ?? new PlayerStatusRoster();
            var properties = new System.Collections.Generic.List<string>();
            var fields = new System.Collections.Generic.List<string>();
            bool hasContent = false;
            if (options.ShowTitle && !String.IsNullOrWhiteSpace(options.Title))
            {
                properties.Add(Property("title", Limit(options.Title, 256)));
                hasContent = true;
            }
            var descriptions = new System.Collections.Generic.List<string>();
            if (options.ShowDescription && !String.IsNullOrWhiteSpace(options.Description)) descriptions.Add(Limit(options.Description, 1000));
            if (options.ShowActivity && !String.IsNullOrWhiteSpace(activity))
            {
                string age = options.ShowActivityAge && activityAt != default(DateTime) ? " • " + RelativeTimestamp(activityAt) : String.Empty;
                descriptions.Add("**Latest activity**\n" + Limit(activity, 1000) + age);
            }
            if (descriptions.Count > 0)
            {
                properties.Add(Property("description", String.Join("\n\n", descriptions.ToArray())));
                hasContent = true;
            }
            if (options.ShowServerStatus) fields.Add(Field("Server status", online ? "🟢 Server Online" : "🔴 Server Offline", false));
            if (options.ShowOnlinePlayers) fields.Add(Field("👥 Online — " + roster.OnlineCount, roster.OnlineCount == 0 ? "Nobody online" : roster.OnlineNames, true));
            if (options.ShowOfflinePlayers) fields.Add(Field("💤 Offline — " + roster.OfflineCount, roster.OfflineCount == 0 ? "No known offline players" : roster.OfflineNames, true));
            if (options.ShowUptime)
            {
                long minutes = Math.Max(0, (long)uptime.TotalMinutes);
                fields.Add(Field("⏱ Uptime", (minutes / 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + "h " + (minutes % 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m", true));
            }
            if (options.ShowLinks)
            {
                var links = new System.Collections.Generic.List<string>();
                string website = WebUrl(options.WebsiteUrl, 400);
                string invite = WebUrl(options.InviteUrl, 400);
                if (website != null) links.Add("[Website](<" + website + ">)");
                if (invite != null) links.Add("[Join our Discord](<" + invite + ">)");
                if (links.Count > 0) fields.Add(Field("🔗 Links", String.Join(" • ", links.ToArray()), false));
            }
            if (fields.Count > 0)
            {
                properties.Add("\"fields\":[" + String.Join(",", fields.ToArray()) + "]");
                hasContent = true;
            }
            string image = options.ShowImage ? WebUrl(options.ImageUrl, 2048) : null;
            string thumbnail = options.ShowThumbnail ? WebUrl(options.ThumbnailUrl, 2048) : null;
            if (image != null) { properties.Add("\"image\":{" + Property("url", image) + "}"); hasContent = true; }
            if (thumbnail != null) { properties.Add("\"thumbnail\":{" + Property("url", thumbnail) + "}"); hasContent = true; }
            if (options.ShowFooter && !String.IsNullOrWhiteSpace(options.FooterText))
            {
                properties.Add("\"footer\":{" + Property("text", Limit(options.FooterText, 500)) + "}");
                hasContent = true;
            }
            if (!hasContent) return "{\"embeds\":[]}";
            if (options.ShowTimestamp) properties.Add(Property("timestamp", updatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)));
            if (options.ShowColor) properties.Add("\"color\":" + ParseColor(online ? options.OnlineColor : options.OfflineColor, online ? OnlineColor : EmptyColor));
            return "{\"allowed_mentions\":{\"parse\":[]},\"embeds\":[{" + String.Join(",", properties.ToArray()) + "}]}";
        }

        public static string RelativeTimestamp(DateTime time)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long seconds = (long)(time.ToUniversalTime() - epoch).TotalSeconds;
            return "<t:" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":R>";
        }

        private static string Property(string name, string value)
        {
            return "\"" + name + "\":\"" + DiscordWebhookPayload.Escape(value) + "\"";
        }

        private static string Field(string name, string value, bool inline)
        {
            return "{" + Property("name", name) + "," + Property("value", Limit(value, 900)) + ",\"inline\":" + (inline ? "true" : "false") + "}";
        }

        private static string Limit(string value, int length)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            if (value.Length <= length) return value;
            int end = length - 1;
            if (Char.IsHighSurrogate(value[end - 1])) end--;
            return value.Substring(0, end) + "…";
        }

        private static string WebUrl(string value, int maxLength)
        {
            Uri uri;
            if (String.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !String.IsNullOrEmpty(uri.UserInfo) || uri.AbsoluteUri.Length > maxLength) return null;
            return uri.AbsoluteUri.Replace("<", "%3C").Replace(">", "%3E");
        }

        private static int ParseColor(string value, int fallback)
        {
            int color;
            string hex = (value ?? "").Trim().TrimStart(new[] { '#' });
            return hex.Length == 6 && Int32.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out color) ? color : fallback;
        }

        public static string Create(string title, string description, int playerCount)
        {
            string safeTitle = DiscordWebhookPayload.Escape(title);
            string safeDescription = DiscordWebhookPayload.Escape(description);
            int safeCount = playerCount < 0 ? 0 : playerCount;
            int color = safeCount > 0 ? OnlineColor : EmptyColor;
            return "{\"embeds\":[{\"title\":\"" + safeTitle + "\",\"description\":\"" + safeDescription + "\",\"color\":" + color + ",\"footer\":{\"text\":\"Players online: " + safeCount + "\"}}]}";
        }

        public static string CreateStatus(string title, string description, PlayerStatusRoster roster)
        {
            if (roster == null) roster = new PlayerStatusRoster();
            string safeTitle = DiscordWebhookPayload.Escape(title);
            string safeDescription = DiscordWebhookPayload.Escape(description);
            int color = roster.OnlineCount > 0 ? OnlineColor : EmptyColor;
            return "{\"embeds\":[{\"title\":\"" + safeTitle + "\",\"description\":\"" + safeDescription + "\",\"color\":" + color + ",\"fields\":[{\"name\":\"Online (" + roster.OnlineCount + ")\",\"value\":\"" + DiscordWebhookPayload.Escape(roster.OnlineNames) + "\",\"inline\":true},{\"name\":\"Offline (" + roster.OfflineCount + ")\",\"value\":\"" + DiscordWebhookPayload.Escape(roster.OfflineNames) + "\",\"inline\":true}]}]}";
        }
    }
}
