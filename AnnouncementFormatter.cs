using System;

namespace JoinLeaveAlerts
{
    public static class AnnouncementFormatter
    {
        public static string Format(string template, string playerName)
        {
            if (String.IsNullOrWhiteSpace(template) || String.IsNullOrWhiteSpace(playerName)) return null;

            string normalizedName = playerName.Trim().Replace("\r", String.Empty).Replace("\n", String.Empty);
            return normalizedName.Length == 0 ? null : template.Replace("{player}", normalizedName);
        }
    }
}
