using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using JoinLeaveAlerts;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static int Main()
    {
        Assert(AnnouncementFormatter.Format("Welcome, {player}!", "  Ava  ") == "Welcome, Ava!", "Names must be trimmed and inserted.");
        Assert(AnnouncementFormatter.Format("", "Ava") == null, "Blank templates disable announcements.");
        Assert(AnnouncementFormatter.Format("{player} joined", null) == null, "Missing names are ignored.");
        Assert(DiscordWebhookPayload.Create("hello \"Ava\"\nnext") == "{\"content\":\"hello \\\"Ava\\\"\\nnext\"}", "Webhook JSON must escape text.");
        Assert(PlayerNameResolver.Choose("Unknown Player", "VMan") == "VMan", "Temporary names must use authenticated names.");
        Assert(PlayerNameResolver.Choose("Ava", "Other") == "Ava", "Valid names must be retained.");
        Assert(PlayerNameResolver.Choose("Unknown Player", "") == null, "Unresolved names must be ignored.");
        Assert(WebhookAnnouncementFormatter.WithPlayerCount("Ava left", -1) == "Ava left\nPlayers online: 0", "Negative player counts must be clamped.");
        Assert(ModPackRejectionFormatter.Format("{player}: {mods}", "Ava", new[] { "Pet missing", "Housing missing" }) == "Ava: Pet missing; Housing missing", "Rejection placeholders must be expanded.");
        Assert(ModPackRejectionFormatter.Format("", "Ava", new[] { "Pet missing" }) == null, "Blank rejection messages must be disabled.");
        Assert(DiscordDeliveryModeParser.Parse("StatusEmbed") == DiscordDeliveryMode.StatusEmbed, "StatusEmbed must be selected.");
        Assert(DiscordDeliveryModeParser.Parse("other") == DiscordDeliveryMode.Messages, "Unknown modes must safely fall back.");
        Assert(DiscordWebhookResponse.TryGetMessageId("{\"id\":\"123\"}") == "123", "Message IDs must be persisted.");
        Assert(DiscordWebhookResponse.TryGetMessageId("{}") == null, "Missing message IDs must be ignored.");
        TestEmbeds();
        TestLifecycle();
        TestDelivery();
        Console.WriteLine("Join/Leave Alerts tests passed: " + checks + " assertions.");
        return 0;
    }

    private static void TestEmbeds()
    {
        var options = new StatusEmbedOptions();
        var roster = new PlayerStatusRoster();
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var eventAt = now.AddHours(-2);
        string payload = DiscordEmbedPayload.CreateStatus(options, "VMan has left", roster, true, TimeSpan.FromMinutes(65), now, eventAt);
        Assert(payload.Contains("Server Online") && payload.Contains("\"color\":5763719"), "An empty running server must remain online and green.");
        Assert(payload.Contains("VMan has left • <t:1790416800:R>"), "Activity age must use the event time rather than refresh time.");
        Assert(payload.Contains("1h 5m") && payload.Contains("2026-09-26T12:00:00Z"), "Uptime and refresh time must be rendered.");
        roster.MarkOnline("VMan"); roster.MarkOnline("Ava"); roster.MarkOffline("VMan");
        Assert(roster.OnlineNames == "Ava" && roster.OfflineNames == "VMan", "Leaving players must move between lists.");
        Assert(PlayerStatusRoster.FromPersisted(roster.SerializeOnline(), roster.SerializeOffline()).OfflineNames == "VMan", "Roster names must persist.");
        payload = DiscordEmbedPayload.CreateStatus(options, "Stopped", roster, false, TimeSpan.Zero, now, eventAt);
        Assert(payload.Contains("Server Offline") && payload.Contains("\"color\":15548997"), "Offline state must be independent of roster counts.");
        options.ImageUrl = "https://example.com/banner.png"; options.ThumbnailUrl = "https://example.com/logo.png";
        options.Description = "Township \"test\""; options.WebsiteUrl = "https://example.com"; options.InviteUrl = "https://discord.gg/example"; options.OnlineColor = "#123ABC";
        payload = DiscordEmbedPayload.CreateStatus(options, "Joined", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(payload.Contains("\"image\":") && payload.Contains("\"thumbnail\":"), "Both image URLs must render.");
        Assert(payload.Contains("Township \\\"test\\\"") && payload.Contains("https://discord.gg/example") && payload.Contains("\"color\":1194684"), "Descriptions, links and colors must render safely.");
        options.ImageUrl = "file:///secret"; options.ThumbnailUrl = "invalid"; options.WebsiteUrl = "javascript:alert(1)"; options.InviteUrl = ""; options.OnlineColor = "invalid";
        payload = DiscordEmbedPayload.CreateStatus(options, "Joined", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(!payload.Contains("\"image\":") && !payload.Contains("\"thumbnail\":") && !payload.Contains("javascript:"), "Invalid URLs must be omitted.");
        Assert(payload.Contains("\"color\":5763719"), "Invalid colors must safely fall back.");
        options.ShowActivityAge = false;
        payload = DiscordEmbedPayload.CreateStatus(options, "VMan has left", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(payload.Contains("VMan has left") && !payload.Contains("<t:"), "Activity age must be independently optional.");
        options.ShowActivityAge = true; options.ShowActivity = false;
        payload = DiscordEmbedPayload.CreateStatus(options, "VMan has left", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(!payload.Contains("VMan has left") && !payload.Contains("<t:"), "Hiding activity must hide its age.");
        options.ImageUrl = "https://example.com/banner.png"; options.ThumbnailUrl = "https://example.com/logo.png"; options.WebsiteUrl = "https://example.com";
        foreach (var property in typeof(StatusEmbedOptions).GetProperties()) if (property.PropertyType == typeof(bool)) property.SetValue(options, false, null);
        Assert(DiscordEmbedPayload.CreateStatus(options, "Hidden", roster, true, TimeSpan.Zero, now, eventAt) == "{\"embeds\":[]}", "All disabled sections must clear the embed.");
        options.ShowOnlinePlayers = true;
        payload = DiscordEmbedPayload.CreateStatus(options, "Hidden", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(payload.Contains("Ava") && !payload.Contains("Offline") && !payload.Contains("Hidden") && !payload.Contains("\"color\":") && !payload.Contains("timestamp"), "A list-only embed must not leak hidden sections.");
        for (int i = 0; i < 100; i++) roster.MarkOnline(new string('x', 60) + i);
        payload = DiscordEmbedPayload.CreateStatus(options, "", roster, true, TimeSpan.Zero, now, eventAt);
        Assert(payload.Length < 1500 && payload.Contains("…"), "Long rosters must fit Discord field limits.");
    }

    private static void TestLifecycle()
    {
        var state = new ServerStatusState();
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert(!state.IsOnline && state.SetOnline(true, now), "Readiness must transition the server online.");
        state.RecordActivity("Ava joined", now);
        state.RecordActivity("", now.AddMinutes(1));
        Assert(!state.SetOnline(true, now.AddMinutes(1)) && state.LastActivity == "Ava joined" && state.LastActivityAt == now, "Refreshes and blank events must retain the event time.");
        Assert(state.GetUptime(now.AddMinutes(2)) == TimeSpan.FromMinutes(2), "Uptime must start at readiness.");
        Assert(state.SetOnline(false, now.AddMinutes(3)) && state.GetUptime(now.AddMinutes(4)) == TimeSpan.FromMinutes(3), "Shutdown must freeze uptime.");
        Assert(state.LastActivityAt == now.AddMinutes(3), "Shutdown must have its own event time.");
        Assert(state.SetOnline(true, now.AddHours(1)) && state.GetUptime(now.AddHours(1)) == TimeSpan.Zero, "Restart must reset uptime.");
    }

    private static HttpListenerContext AwaitRequest(HttpListener listener)
    {
        var task = listener.GetContextAsync();
        Assert(task.Wait(5000), "Expected a webhook request within five seconds.");
        return task.Result;
    }

    private static void TestDelivery()
    {
        var portFinder = new TcpListener(IPAddress.Loopback, 0); portFinder.Start();
        int port = ((IPEndPoint)portFinder.LocalEndpoint).Port; portFinder.Stop();
        string endpoint = "http://127.0.0.1:" + port + "/webhook?thread_id=789";
        using (var listener = new HttpListener())
        {
            listener.Prefixes.Add("http://127.0.0.1:" + port + "/"); listener.Start();
            string id = null; string failure = null;
            string payload = "{\"embeds\":[{\"title\":\"Test\"}]}";
            DiscordStatusEmbedClient.QueueAsync(endpoint, "", payload, value => id = value, error => failure = error);
            var request = AwaitRequest(listener);
            Assert(request.Request.HttpMethod == "POST" && request.Request.QueryString["wait"] == "true" && request.Request.QueryString["thread_id"] == "789", "Creation must request an ID and preserve the thread.");
            byte[] response = System.Text.Encoding.UTF8.GetBytes("{\"id\":\"123\"}"); request.Response.OutputStream.Write(response, 0, response.Length); request.Response.Close();
            Assert(DiscordStatusEmbedClient.WaitForIdle(3000) && id == "123" && failure == null, "Delivery must persist the ID and finish before shutdown.");
            DiscordStatusEmbedClient.QueueAsync(endpoint, id, "{\"embeds\":[]}", value => id = value, error => failure = error);
            request = AwaitRequest(listener);
            Assert(request.Request.HttpMethod == "PATCH" && request.Request.Url.AbsolutePath == "/webhook/messages/123" && request.Request.QueryString["thread_id"] == "789", "Updates must edit the same message and preserve the thread.");
            Assert(new StreamReader(request.Request.InputStream).ReadToEnd() == "{\"embeds\":[]}", "Disabling everything must clear existing embeds."); request.Response.Close();
            Assert(DiscordStatusEmbedClient.WaitForIdle(3000), "The clear edit must finish.");
            DiscordStatusEmbedClient.QueueAsync(endpoint, id, payload, value => id = value, error => failure = error);
            request = AwaitRequest(listener); request.Response.StatusCode = 404; request.Response.Close();
            request = AwaitRequest(listener); Assert(request.Request.HttpMethod == "POST", "Deleted status messages must be replaced.");
            response = System.Text.Encoding.UTF8.GetBytes("{\"id\":\"456\"}"); request.Response.OutputStream.Write(response, 0, response.Length); request.Response.Close();
            Assert(DiscordStatusEmbedClient.WaitForIdle(3000) && id == "456", "Replacement IDs must be saved.");
            DiscordStatusEmbedClient.QueueAsync("http://127.0.0.1:" + port + "/empty", "", "{\"embeds\":[]}", value => id = value, error => failure = error);
            Assert(DiscordStatusEmbedClient.WaitForIdle(3000) && !listener.GetContextAsync().Wait(100), "An empty new embed must not POST.");
        }
    }
}
