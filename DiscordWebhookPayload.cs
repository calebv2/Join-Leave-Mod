using System;
using System.Text;

namespace JoinLeaveAlerts
{
    public static class DiscordWebhookPayload
    {
        private const int MaximumContentLength = 2000;

        public static string Create(string content)
        {
            if (content == null) content = String.Empty;
            if (content.Length > MaximumContentLength) content = content.Substring(0, MaximumContentLength);

            return "{\"content\":\"" + Escape(content) + "\"}";
        }

        public static string Escape(string content)
        {
            if (content == null) content = String.Empty;
            StringBuilder escaped = new StringBuilder(content.Length + 16);
            foreach (char character in content)
            {
                switch (character)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '\"': escaped.Append("\\\""); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < ' ') escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        else escaped.Append(character);
                        break;
                }
            }
            return escaped.ToString();
        }
    }
}
