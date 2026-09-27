using System;
using System.Text.RegularExpressions;

namespace JoinLeaveAlerts
{
    public static class DiscordWebhookResponse
    {
        private static readonly Regex MessageIdPattern = new Regex("\\\"id\\\"\\s*:\\s*\\\"([0-9]+)\\\"", RegexOptions.Compiled);

        public static string TryGetMessageId(string response)
        {
            if (String.IsNullOrEmpty(response)) return null;
            Match match = MessageIdPattern.Match(response);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
