using System;

namespace JoinLeaveAlerts
{
    public enum DiscordDeliveryMode
    {
        Messages,
        StatusEmbed
    }

    public static class DiscordDeliveryModeParser
    {
        public static DiscordDeliveryMode Parse(string value)
        {
            return String.Equals(value, "StatusEmbed", StringComparison.OrdinalIgnoreCase)
                ? DiscordDeliveryMode.StatusEmbed
                : DiscordDeliveryMode.Messages;
        }
    }
}
