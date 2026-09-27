using System;

namespace JoinLeaveAlerts
{
    public sealed class ServerStatusState
    {
        public DateTime LastActivityAt { get; private set; } = DateTime.UtcNow;
        private DateTime startedAt;
        private DateTime stoppedAt;
        public bool IsOnline { get; private set; }
        public string LastActivity { get; private set; } = "Server is starting";

        public bool SetOnline(bool online, DateTime now)
        {
            if (IsOnline == online) return false;
            IsOnline = online;
            if (online) startedAt = now;
            else stoppedAt = now;
            RecordActivity(online ? "Server is online" : "Server has stopped", now);
            return true;
        }

        public void RecordActivity(string activity)
        {
            RecordActivity(activity, DateTime.UtcNow);
        }

        public void RecordActivity(string activity, DateTime now)
        {
            if (String.IsNullOrWhiteSpace(activity)) return;
            LastActivity = activity;
            LastActivityAt = now.ToUniversalTime();
        }

        public TimeSpan GetUptime(DateTime now)
        {
            if (startedAt == default(DateTime)) return TimeSpan.Zero;
            TimeSpan elapsed = (IsOnline ? now : stoppedAt) - startedAt;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
    }
}
