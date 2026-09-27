using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Globalization;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace JoinLeaveAlerts
{
    public sealed class Core : MelonMod
    {
        private static Core instance;
        private MelonPreferences_Entry<string> joinMessage;
        private MelonPreferences_Entry<string> leaveMessage;
        private MelonPreferences_Entry<string> discordWebhookUrl;
        private MelonPreferences_Entry<string> discordDeliveryMode;
        private readonly Dictionary<string, MelonPreferences_Entry<bool>> embedFlags = new Dictionary<string, MelonPreferences_Entry<bool>>();
        private readonly Dictionary<string, MelonPreferences_Entry<string>> embedText = new Dictionary<string, MelonPreferences_Entry<string>>();
        private MelonPreferences_Entry<float> statusEmbedRefreshSeconds;
        private MelonPreferences_Entry<string> statusEmbedHeartbeatPath;
        private readonly ServerStatusState serverStatus = new ServerStatusState();
        private DateTime nextServerCheck;
        private DateTime nextStatusRefresh;
        private DateTime nextHeartbeat;
        private bool shuttingDown;
        private bool heartbeatWarningLogged;
        private MelonPreferences_Entry<string> statusEmbedMessageId;
        private MelonPreferences_Entry<string> statusEmbedOnlinePlayers;
        private MelonPreferences_Entry<string> statusEmbedOfflinePlayers;
        private MelonPreferences_Entry<string> modPackRejectedMessage;
        private MelonPreferences_Entry<float> displayDuration;
        private readonly object preferenceSync = new object();
        private string pendingStatusEmbedMessageId;
        private PlayerStatusRoster statusRoster;
        private Delegate optionalModPackRejectedHandler;

        public override void OnInitializeMelon()
        {
            if (!Application.isBatchMode)
            {
                LoggerInstance.Warning("Join/Leave Alerts is server-only and will remain disabled on this client.");
                return;
            }

            instance = this;
            MelonPreferences_Category category = MelonPreferences.CreateCategory("JoinLeaveAlerts");
            joinMessage = category.CreateEntry("JoinMessage", "{player} has joined", "Join message ({player} is replaced by the player name)");
            leaveMessage = category.CreateEntry("LeaveMessage", "{player} has left", "Leave message ({player} is replaced by the player name)");
            discordWebhookUrl = category.CreateEntry("DiscordWebhookUrl", String.Empty, "Discord webhook URL; leave empty to disable webhook posts");
            discordDeliveryMode = category.CreateEntry("DiscordDeliveryMode", "Messages", "Messages posts every event; StatusEmbed edits one persistent Discord embed");
            var defaults = new StatusEmbedOptions();
            foreach (PropertyInfo property in typeof(StatusEmbedOptions).GetProperties())
            {
                string key = "StatusEmbed" + property.Name;
                if (property.PropertyType == typeof(bool))
                    embedFlags[property.Name] = category.CreateEntry(key, (bool)property.GetValue(defaults, null), "Enable " + property.Name.Substring(4) + " in the status embed");
                else
                    embedText[property.Name] = category.CreateEntry(key, (string)property.GetValue(defaults, null), "Optional status embed " + property.Name + "; blank omits text/URLs; colors accept #RRGGBB");
            }
            statusEmbedRefreshSeconds = category.CreateEntry("StatusEmbedRefreshSeconds", 60f, "Refresh interval in seconds (15-3600), including uptime and last updated");
            statusEmbedHeartbeatPath = category.CreateEntry("StatusEmbedHeartbeatPath", String.Empty, "Optional heartbeat JSON path for the external crash monitor; empty disables it");
            statusEmbedMessageId = category.CreateEntry("StatusEmbedMessageId", String.Empty, "Managed automatically; do not edit unless resetting the StatusEmbed");
            statusEmbedOnlinePlayers = category.CreateEntry("StatusEmbedOnlinePlayers", String.Empty, "Managed automatically; persisted online names for StatusEmbed");
            statusEmbedOfflinePlayers = category.CreateEntry("StatusEmbedOfflinePlayers", String.Empty, "Managed automatically; persisted offline names for StatusEmbed");
            modPackRejectedMessage = category.CreateEntry("ModPackRejectedMessage", "{player} could not join: missing or incompatible mod pack ({mods}).", "Discord-only message when optional RequiredModsGate rejects a client");
            displayDuration = category.CreateEntry("DisplayDuration", 2.5f, "Seconds each announcement remains visible");
            statusRoster = PlayerStatusRoster.FromPersisted(statusEmbedOnlinePlayers.Value, statusEmbedOfflinePlayers.Value);
            statusRoster.MarkAllOffline();
            PersistStatusRoster();
            MelonPreferences.Save();

            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("TheRavenSeb.JoinLeaveAlerts");
            harmony.Patch(AccessTools.Method(typeof(ServerHandler), "UserJoined"), postfix: new HarmonyLib.HarmonyMethod(typeof(Core), "OnUserJoined"));
            harmony.Patch(AccessTools.Method(typeof(ServerHandler), "UserLeft"), prefix: new HarmonyLib.HarmonyMethod(typeof(Core), "OnUserLeft"));
            SubscribeToOptionalRequiredModsGate();
            LoggerInstance.Msg("Join/Leave Alerts initialized. Server player lifecycle hooks are active.");
            PublishStatus(DateTime.UtcNow);
        }

        private static void OnUserJoined(Connection connection)
        {
            Announce(connection, instance == null ? null : instance.joinMessage.Value, "joined", false);
        }

        private static void OnUserLeft(Connection connection)
        {
            Announce(connection, instance == null ? null : instance.leaveMessage.Value, "left", true);
        }

        private void SubscribeToOptionalRequiredModsGate()
        {
            MethodInfo handler = typeof(Core).GetMethod("OnOptionalModPackRejected", BindingFlags.NonPublic | BindingFlags.Static);
            if (OptionalEventSubscription.TrySubscribe("RequiredModsGate", "RequiredModsGate.Core", "ModPackRejected", handler, out optionalModPackRejectedHandler))
                LoggerInstance.Msg("Optional RequiredModsGate integration enabled.");
        }

        private static void OnOptionalModPackRejected(object sender, object rejection)
        {
            if (instance == null || rejection == null) return;
            try
            {
                PropertyInfo playerNameProperty = rejection.GetType().GetProperty("PlayerName");
                PropertyInfo problemsProperty = rejection.GetType().GetProperty("Problems");
                string playerName = playerNameProperty == null ? null : playerNameProperty.GetValue(rejection, null) as string;
                IEnumerable rawProblems = problemsProperty == null ? null : problemsProperty.GetValue(rejection, null) as IEnumerable;
                List<string> problems = new List<string>();
                if (rawProblems != null)
                    foreach (object problem in rawProblems)
                        if (problem != null) problems.Add(problem.ToString());

                string message = ModPackRejectionFormatter.Format(instance.modPackRejectedMessage.Value, playerName, problems);
                if (message == null) return;
                DeliverDiscord(message, GetPlayerCount(false));
            }
            catch (Exception exception)
            {
                instance.LoggerInstance.Warning("Optional RequiredModsGate event could not be reported: " + exception.Message);
            }
        }

        private static void Announce(Connection connection, string template, string action, bool isLeaving)
        {
            if (instance == null || connection == null) return;

            try
            {
                string playerName = ResolvePlayerName(connection);
                if (String.IsNullOrWhiteSpace(playerName)) return;
                string message = AnnouncementFormatter.Format(template, playerName);
                instance.UpdateStatusRoster(playerName, !isLeaving);
                instance.serverStatus.SetOnline(IsServerReady(), DateTime.UtcNow);

                if (message != null)
                {
                    PlayerCommunicationManager manager = PlayerCommunicationManager.Instance;
                    if (manager == null)
                        instance.LoggerInstance.Warning("Could not announce player " + action + ": PlayerCommunicationManager is not ready.");
                    else
                    {
                        float duration = Mathf.Clamp(instance.displayDuration.Value, 0.1f, 30f);
                        manager.SendMessageToAllPlayers(message, duration);
                    }
                }
                if (DiscordDeliveryModeParser.Parse(instance.discordDeliveryMode.Value) == DiscordDeliveryMode.StatusEmbed)
                    DeliverDiscord(message ?? playerName + " has " + action, GetPlayerCount(isLeaving));
                else if (message != null)
                    DeliverDiscord(message, GetPlayerCount(isLeaving));
                instance.LoggerInstance.Msg("Player " + action + ": " + playerName);
            }
            catch (Exception exception)
            {
                instance.LoggerInstance.Error("Failed to announce player " + action + ": " + exception);
            }
        }

        private static string ResolvePlayerName(Connection connection)
        {
            string authenticatedUsername = null;
            try
            {
                authenticatedUsername = connection.UserInfo.Username;
            }
            catch (Exception)
            {
                // A disconnect can clear connection details while the leave hook is executing.
            }

            return PlayerNameResolver.Choose(connection.PlayerName, authenticatedUsername);
        }

        private static int GetPlayerCount(bool isLeaving)
        {
            try
            {
                ServerHandler server = ServerHandler.Current;
                int count = server == null ? 0 : server.Connections;
                return isLeaving ? Math.Max(0, count - 1) : count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public override void OnUpdate()
        {
            if (instance != this || shuttingDown) return;
            SavePendingMessageId();
            DateTime now = DateTime.UtcNow;
            if (now < nextServerCheck) return;
            nextServerCheck = now.AddSeconds(1);
            bool changed = serverStatus.SetOnline(IsServerReady(), now);
            if (changed && !serverStatus.IsOnline)
            {
                statusRoster.MarkAllOffline();
                PersistStatusRoster();
            }
            if (changed || now >= nextStatusRefresh) PublishStatus(now);
            if (now >= nextHeartbeat) WriteHeartbeat(now);
        }

        private void SavePendingMessageId()
        {
            string messageId;
            lock (preferenceSync)
            {
                messageId = pendingStatusEmbedMessageId;
                pendingStatusEmbedMessageId = null;
            }
            if (messageId == null || statusEmbedMessageId == null || statusEmbedMessageId.Value == messageId) return;
            statusEmbedMessageId.Value = messageId;
            MelonPreferences.Save();
            // Refresh the heartbeat immediately with the new message ID.
            WriteHeartbeat(DateTime.UtcNow);
            LoggerInstance.Msg("Discord status embed message ID saved for the next server restart: " + messageId);
        }

        private static bool IsServerReady()
        {
            ServerHandler server = ServerHandler.Current;
            return server != null && server.IsRunning && !server.IsBottingUp;
        }

        private StatusEmbedOptions ReadEmbedOptions()
        {
            var options = new StatusEmbedOptions();
            foreach (PropertyInfo property in typeof(StatusEmbedOptions).GetProperties())
            {
                if (property.PropertyType == typeof(bool)) property.SetValue(options, embedFlags[property.Name].Value, null);
                else property.SetValue(options, embedText[property.Name].Value, null);
            }
            return options;
        }

        private void PublishStatus(DateTime now)
        {
            float seconds = statusEmbedRefreshSeconds.Value;
            if (Single.IsNaN(seconds) || Single.IsInfinity(seconds)) seconds = 60f;
            nextStatusRefresh = now.AddSeconds(Mathf.Clamp(seconds, 15f, 3600f));
            if (DiscordDeliveryModeParser.Parse(discordDeliveryMode.Value) != DiscordDeliveryMode.StatusEmbed || String.IsNullOrWhiteSpace(discordWebhookUrl.Value)) return;
            string payload = DiscordEmbedPayload.CreateStatus(ReadEmbedOptions(), serverStatus.LastActivity, statusRoster, serverStatus.IsOnline, serverStatus.GetUptime(now), now, serverStatus.LastActivityAt);
            DiscordStatusEmbedClient.QueueAsync(discordWebhookUrl.Value, statusEmbedMessageId.Value, payload, QueueStatusEmbedMessageId, LoggerInstance.Warning);
            WriteHeartbeat(now);
        }

        private void WriteHeartbeat(DateTime now)
        {
            nextHeartbeat = now.AddSeconds(30);
            if (statusEmbedHeartbeatPath == null || String.IsNullOrWhiteSpace(statusEmbedHeartbeatPath.Value)) return;
            try
            {
                var offlineRoster = PlayerStatusRoster.FromPersisted(statusRoster.SerializeOnline(), statusRoster.SerializeOffline());
                offlineRoster.MarkAllOffline();
                string offlinePayload = DiscordEmbedPayload.CreateStatus(ReadEmbedOptions(), serverStatus.IsOnline ? "Server stopped responding" : serverStatus.LastActivity, offlineRoster, false, serverStatus.GetUptime(now), now, serverStatus.IsOnline ? now : serverStatus.LastActivityAt);
                bool enabled = DiscordDeliveryModeParser.Parse(discordDeliveryMode.Value) == DiscordDeliveryMode.StatusEmbed && !String.IsNullOrWhiteSpace(discordWebhookUrl.Value);
                string offlineActivityMarker = serverStatus.IsOnline ? DiscordEmbedPayload.RelativeTimestamp(now) : String.Empty;
                string document = "{\"enabled\":" + (enabled ? "true" : "false") + ",\"updated_at\":\"" + now.ToString("o", CultureInfo.InvariantCulture) + "\",\"online\":" + (serverStatus.IsOnline ? "true" : "false") + ",\"message_id\":\"" + DiscordWebhookPayload.Escape(statusEmbedMessageId.Value) + "\",\"offline_payload\":" + offlinePayload + ",\"offline_activity_marker\":\"" + DiscordWebhookPayload.Escape(offlineActivityMarker) + "\"}";
                string path = Path.GetFullPath(statusEmbedHeartbeatPath.Value);
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, document);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                heartbeatWarningLogged = false;
            }
            catch (Exception)
            {
                if (!heartbeatWarningLogged) LoggerInstance.Warning("Could not write the optional status heartbeat. Check StatusEmbedHeartbeatPath and directory permissions.");
                heartbeatWarningLogged = true;
            }
        }

        public override void OnApplicationQuit()
        {
            if (instance != this) return;
            shuttingDown = true;
            DateTime now = DateTime.UtcNow;
            serverStatus.SetOnline(false, now);
            statusRoster.MarkAllOffline();
            PersistStatusRoster();
            PublishStatus(now);
            // Allow the queued offline edit to complete before the process exits.
            if (!DiscordStatusEmbedClient.WaitForIdle(5000))
                LoggerInstance.Warning("Discord shutdown update did not finish before exit. The optional external monitor can detect the stale heartbeat.");
            SavePendingMessageId();
        }

        private static void DeliverDiscord(string message, int playerCount)
        {
            if (instance == null) return;

            if (DiscordDeliveryModeParser.Parse(instance.discordDeliveryMode.Value) == DiscordDeliveryMode.StatusEmbed)
            {
                DateTime now = DateTime.UtcNow;
                instance.serverStatus.RecordActivity(message, now);
                instance.PublishStatus(now);
                return;
            }

            DiscordWebhookClient.PostAsync(
                instance.discordWebhookUrl.Value,
                WebhookAnnouncementFormatter.WithPlayerCount(message, playerCount),
                instance.LoggerInstance.Warning);
        }

        private void QueueStatusEmbedMessageId(string messageId)
        {
            lock (preferenceSync) pendingStatusEmbedMessageId = messageId;
        }

        private void UpdateStatusRoster(string playerName, bool isOnline)
        {
            if (statusRoster == null) statusRoster = new PlayerStatusRoster();
            if (isOnline) statusRoster.MarkOnline(playerName);
            else statusRoster.MarkOffline(playerName);
            PersistStatusRoster();
        }

        private void PersistStatusRoster()
        {
            if (statusRoster == null || statusEmbedOnlinePlayers == null || statusEmbedOfflinePlayers == null) return;
            statusEmbedOnlinePlayers.Value = statusRoster.SerializeOnline();
            statusEmbedOfflinePlayers.Value = statusRoster.SerializeOffline();
            MelonPreferences.Save();
        }
    }
}
