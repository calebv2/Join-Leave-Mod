using System;
using System.Net;
using System.Threading;

namespace JoinLeaveAlerts
{
    internal static class DiscordStatusEmbedClient
    {
        private sealed class Request
        {
            public Uri Endpoint;
            public string Payload;
            public Action<string> PersistMessageId;
            public Action<string> ReportFailure;
        }

        private static readonly object Sync = new object();
        private static Request pending;
        private static string activeWebhookUrl;
        private static string messageId;
        private static bool workerRunning;

        public static bool WaitForIdle(int milliseconds)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            lock (Sync)
            {
                while (workerRunning)
                {
                    long remaining = milliseconds - timer.ElapsedMilliseconds;
                    if (remaining <= 0) return false;
                    Monitor.Wait(Sync, (int)remaining);
                }
                return true;
            }
        }

        public static void QueueAsync(string webhookUrl, string storedMessageId, string payload, Action<string> persistMessageId, Action<string> reportFailure)
        {
            Uri endpoint;
            if (!TryCreateEndpoint(webhookUrl, out endpoint)) return;

            lock (Sync)
            {
                if (!String.Equals(activeWebhookUrl, endpoint.AbsoluteUri, StringComparison.Ordinal))
                {
                    activeWebhookUrl = endpoint.AbsoluteUri;
                    messageId = storedMessageId;
                }

                if (payload == "{\"embeds\":[]}" && String.IsNullOrWhiteSpace(messageId) && !workerRunning) return;
                pending = new Request
                {
                    Endpoint = endpoint,
                    Payload = payload,
                    PersistMessageId = persistMessageId,
                    ReportFailure = reportFailure
                };

                if (workerRunning) return;
                workerRunning = true;
                ThreadPool.QueueUserWorkItem(delegate { ProcessQueue(); });
            }
        }

        private static void ProcessQueue()
        {
            while (true)
            {
                Request request;
                string currentMessageId;
                lock (Sync)
                {
                    request = pending;
                    pending = null;
                    if (request == null)
                    {
                        workerRunning = false;
                        Monitor.PulseAll(Sync);
                        return;
                    }
                    currentMessageId = messageId;
                }

                try
                {
                    if (String.IsNullOrWhiteSpace(currentMessageId))
                    {
                        if (request.Payload == "{\"embeds\":[]}") continue;
                        string response = Send(request.Endpoint, "POST", request.Payload, true);
                        string newMessageId = DiscordWebhookResponse.TryGetMessageId(response);
                        if (String.IsNullOrEmpty(newMessageId))
                        {
                            request.ReportFailure("Discord status embed was created, but Discord did not return its message ID.");
                            continue;
                        }

                        lock (Sync) messageId = newMessageId;
                        request.PersistMessageId(newMessageId);
                    }
                    else
                    {
                        try
                        {
                            Send(BuildMessageEndpoint(request.Endpoint, currentMessageId), "PATCH", request.Payload, false);
                        }
                        catch (WebException exception)
                        {
                            if (!IsNotFound(exception)) throw;

                            lock (Sync) messageId = null;
                            request.PersistMessageId(String.Empty);
                            if (request.Payload == "{\"embeds\":[]}") continue;
                            string response = Send(request.Endpoint, "POST", request.Payload, true);
                            string newMessageId = DiscordWebhookResponse.TryGetMessageId(response);
                            if (String.IsNullOrEmpty(newMessageId)) throw new InvalidOperationException("Discord did not return a replacement status embed message ID.");
                            lock (Sync) messageId = newMessageId;
                            request.PersistMessageId(newMessageId);
                        }
                    }
                }
                catch (Exception exception)
                {
                    // Never include request URLs or webhook tokens in failure logs.
                    try { request.ReportFailure("Discord status embed update failed (" + exception.GetType().Name + ")."); }
                    catch (Exception) { /* A logging failure must not strand the queue. */ }
                }
            }
        }

        private sealed class BoundedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                request.Timeout = 3000;
                HttpWebRequest http = request as HttpWebRequest;
                if (http != null) http.ReadWriteTimeout = 3000;
                return request;
            }
        }

        private static string Send(Uri endpoint, string method, string payload, bool waitForResponse)
        {
            var builder = new UriBuilder(endpoint);
            if (waitForResponse)
            {
                string query = builder.Query.TrimStart(new[] { '?' });
                var parameters = new System.Collections.Generic.List<string>();
                foreach (string parameter in query.Split(new[] { '&' }))
                    if (!String.IsNullOrEmpty(parameter) && !parameter.StartsWith("wait=", StringComparison.OrdinalIgnoreCase)) parameters.Add(parameter);
                parameters.Add("wait=true");
                builder.Query = String.Join("&", parameters.ToArray());
            }
            Uri requestUri = builder.Uri;
            using (WebClient client = new BoundedWebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                return client.UploadString(requestUri, method, payload);
            }
        }

        private static Uri BuildMessageEndpoint(Uri endpoint, string currentMessageId)
        {
            var builder = new UriBuilder(endpoint);
            builder.Path = builder.Path.TrimEnd(new[] { '/' }) + "/messages/" + Uri.EscapeDataString(currentMessageId);
            return builder.Uri;
        }

        private static bool TryCreateEndpoint(string webhookUrl, out Uri endpoint)
        {
            endpoint = null;
            return !String.IsNullOrWhiteSpace(webhookUrl) &&
                   Uri.TryCreate(webhookUrl.Trim(), UriKind.Absolute, out endpoint) &&
                   (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.Scheme == Uri.UriSchemeHttp);
        }

        private static bool IsNotFound(WebException exception)
        {
            HttpWebResponse response = exception.Response as HttpWebResponse;
            return response != null && response.StatusCode == HttpStatusCode.NotFound;
        }
    }
}
