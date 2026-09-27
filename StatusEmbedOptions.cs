using System;

namespace JoinLeaveAlerts
{
    public sealed class StatusEmbedOptions
    {
        public bool ShowTitle { get; set; } = true;
        public bool ShowServerStatus { get; set; } = true;
        public bool ShowActivityAge { get; set; } = true;
        public bool ShowActivity { get; set; } = true;
        public bool ShowOnlinePlayers { get; set; } = true;
        public bool ShowOfflinePlayers { get; set; } = true;
        public int OfflinePlayersLimit { get; set; } = 5;
        public bool ShowUptime { get; set; } = true;
        public bool ShowTimestamp { get; set; } = true;
        public bool ShowFooter { get; set; } = true;
        public bool ShowDescription { get; set; } = true;
        public bool ShowLinks { get; set; } = true;
        public bool ShowImage { get; set; } = true;
        public bool ShowThumbnail { get; set; } = true;
        public bool ShowColor { get; set; } = true;
        public string Title { get; set; } = "ATT Player Activity";
        public string Description { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";
        public string InviteUrl { get; set; } = "";
        public string FooterText { get; set; } = "Last updated";
        public string OnlineColor { get; set; } = "#57F287";
        public string OfflineColor { get; set; } = "#ED4245";
    }
}
