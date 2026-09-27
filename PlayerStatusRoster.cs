using System;
using System.Collections.Generic;
using System.Text;

namespace JoinLeaveAlerts
{
    public sealed class PlayerStatusRoster
    {
        private readonly List<string> online = new List<string>();
        private readonly List<string> offline = new List<string>();

        public int OnlineCount { get { return online.Count; } }
        public int OfflineCount { get { return offline.Count; } }
        public string OnlineNames { get { return Display(online); } }
        public string OfflineNames { get { return Display(offline); } }

        public void MarkOnline(string playerName)
        {
            Move(playerName, online, offline);
        }

        public void MarkOffline(string playerName)
        {
            Move(playerName, offline, online);
        }

        public void MarkAllOffline()
        {
            string[] names = online.ToArray();
            for (int index = 0; index < names.Length; index++) MarkOffline(names[index]);
        }

        public string SerializeOnline()
        {
            return Serialize(online);
        }

        public string SerializeOffline()
        {
            return Serialize(offline);
        }

        public static PlayerStatusRoster FromPersisted(string onlineValue, string offlineValue)
        {
            PlayerStatusRoster roster = new PlayerStatusRoster();
            AddPersisted(roster.online, onlineValue);
            AddPersisted(roster.offline, offlineValue);
            return roster;
        }

        private static void AddPersisted(List<string> destination, string value)
        {
            if (String.IsNullOrEmpty(value)) return;
            string[] parts = SplitPersisted(value);
            for (int index = 0; index < parts.Length; index++)
            {
                try
                {
                    string name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[index]));
                    if (!String.IsNullOrWhiteSpace(name) && !Contains(destination, name)) destination.Add(name);
                }
                catch (FormatException)
                {
                    // Ignore malformed persisted data rather than disabling player alerts.
                }
            }
        }

        private static string Serialize(List<string> source)
        {
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < source.Count; index++)
            {
                if (index > 0) result.Append(',');
                result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(source[index])));
            }
            return result.ToString();
        }

        private static string[] SplitPersisted(string value)
        {
            List<string> parts = new List<string>();
            int start = 0;
            while (start <= value.Length)
            {
                int separator = value.IndexOf(',', start);
                if (separator < 0)
                {
                    parts.Add(value.Substring(start));
                    break;
                }

                parts.Add(value.Substring(start, separator - start));
                start = separator + 1;
            }
            return parts.ToArray();
        }

        private static void Move(string playerName, List<string> destination, List<string> source)
        {
            if (String.IsNullOrWhiteSpace(playerName)) return;
            string name = playerName.Trim();
            Remove(source, name);
            if (!Contains(destination, name)) destination.Add(name);
        }

        private static void Remove(List<string> values, string name)
        {
            for (int index = values.Count - 1; index >= 0; index--)
                if (String.Equals(values[index], name, StringComparison.OrdinalIgnoreCase)) values.RemoveAt(index);
        }

        private static bool Contains(List<string> values, string name)
        {
            for (int index = 0; index < values.Count; index++)
                if (String.Equals(values[index], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Display(List<string> values)
        {
            return values.Count == 0 ? "—" : String.Join("\n", values.ToArray());
        }
    }
}
