using System;
using System.Net;
using System.Threading;

namespace JoinLeaveAlerts
{
    internal static class DiscordWebhookClient
    {
        public static void PostAsync(string webhookUrl, string content, Action<string> reportFailure)
        {
            Uri endpoint;
            if (String.IsNullOrWhiteSpace(webhookUrl) ||
                !Uri.TryCreate(webhookUrl.Trim(), UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp)) return;

            string payload = DiscordWebhookPayload.Create(content);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.ContentType] = "application/json";
                        client.UploadString(endpoint, "POST", payload);
                    }
                }
                catch (Exception exception)
                {
                    reportFailure("Discord webhook post failed: " + exception.Message);
                }
            });
        }
    }
}
