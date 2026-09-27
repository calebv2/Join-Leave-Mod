using System;

namespace JoinLeaveAlerts
{
    public static class WebhookAnnouncementFormatter
    {
        public static string WithPlayerCount(string announcement, int playerCount)
        {
            if (String.IsNullOrWhiteSpace(announcement)) return announcement;
            return announcement + "\nPlayers online: " + Math.Max(0, playerCount);
        }
    }
}
