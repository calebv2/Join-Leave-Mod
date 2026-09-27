# Join/Leave Alerts

Server-only MelonLoader mod for A Township Tale. It announces player joins and leaves through the game's native player lifecycle and chat broadcast APIs. Discord output can post individual messages or keep one customizable status embed.

## Build and install

```bash
./build.sh /home/ATT/a-township-container/game-source
./tests/run-tests.sh
```

The build explicitly references the game's shipped `mscorlib.dll`, `System.dll` and `System.Core.dll` so newer host-Mono APIs cannot leak into the release DLL. The test suite also resolves every emitted framework method against those game assemblies using Mono.Cecil. If installed elsewhere, set `MONO_CECIL_PATH` to its DLL.

The release DLL is `bin/Release/net472/JoinLeaveAlerts.dll`. Copy only that DLL to the dedicated server's `Mods` directory, then restart the server. Do not install it on clients. No server restart or live Discord deployment is performed by the build.

## Existing announcements and Discord delivery

The first server start saves settings to `UserData/MelonPreferences.cfg`, under `[JoinLeaveAlerts]`.

- `JoinMessage` and `LeaveMessage` accept `{player}`. An empty message disables that in-game announcement and its individual Discord message. StatusEmbed mode still tracks the player's roster change.
- `DisplayDuration` is clamped to 0.1–30 seconds.
- `DiscordWebhookUrl` enables Discord delivery; blank disables it. Keep this URL private. Status updates never log it, and Discord requests run in the background.
- `DiscordDeliveryMode = "Messages"` (default) posts each enabled join, leave and optional required-mod-pack rejection individually.
- `DiscordDeliveryMode = "StatusEmbed"` maintains one message, editing it on player events, server state changes and periodic refreshes.
- `StatusEmbedMessageId` is managed automatically and reused after a reboot. If the message is deleted, one replacement is created. Emptying this setting before restarting intentionally starts a new status message.
- `StatusEmbedOnlinePlayers` and `StatusEmbedOfflinePlayers` are managed roster settings. Known names survive restarts; previously connected players are initially marked offline. These are known players, not a complete server membership directory.

## Optional embed sections

Every section can be enabled or disabled independently. All `StatusEmbedShow...` settings default to `true`. Turning a setting off removes that section on the next event or refresh, even if its text or URL is configured. Blank optional text and URLs also omit their sections.

| Setting | Displays |
| --- | --- |
| `StatusEmbedShowTitle` | Embed heading |
| `StatusEmbedShowServerStatus` | Online/offline indicator, independent of player count |
| `StatusEmbedShowActivity` | Most recent join, leave, rejection or server lifecycle event |
| `StatusEmbedShowActivityAge` | Relative time beside the latest activity, for example `VMan has left • 2 hours ago` |
| `StatusEmbedShowOnlinePlayers` | Online player names and count |
| `StatusEmbedShowOfflinePlayers` | Known offline player names and count |
| `StatusEmbedShowUptime` | Time since the server became ready; freezes when stopped |
| `StatusEmbedShowTimestamp` | Discord timestamp for the last refresh |
| `StatusEmbedShowFooter` | Custom footer text |
| `StatusEmbedShowDescription` | Custom server description |
| `StatusEmbedShowLinks` | Website and Discord invite links |
| `StatusEmbedShowImage` | Large banner image |
| `StatusEmbedShowThumbnail` | Small logo thumbnail |
| `StatusEmbedShowColor` | Online/offline accent color |

Activity age uses [Discord's relative timestamp format](https://docs.discord.com/developers/reference#message-formatting), so Discord displays how long ago the event occurred. It records the event time separately from embed refreshes. Set `StatusEmbedShowActivityAge = false` to hide just the age; `StatusEmbedShowActivity = false` hides the activity and its age together. Server readiness/shutdown events also record their own time, and crash-monitor events start at detection. Exact wording follows the viewer's Discord language settings. This setting defaults to `true`.

If every visible section is disabled or empty, the existing embed is cleared. No new empty message is posted. A color or timestamp alone does not create a card.

| Setting | Default / usage |
| --- | --- |
| `StatusEmbedTitle` | `ATT Player Activity` |
| `StatusEmbedDescription` | Empty; optional server description |
| `StatusEmbedImageUrl` | Empty; public HTTP(S) URL for the large banner |
| `StatusEmbedThumbnailUrl` | Empty; public HTTP(S) URL for the small logo |
| `StatusEmbedWebsiteUrl` | Empty; optional website link |
| `StatusEmbedInviteUrl` | Empty; optional Discord invite link |
| `StatusEmbedFooterText` | `Last updated`; customizable or blank |
| `StatusEmbedOnlineColor` | `#57F287`; six-digit hex, with optional `#` |
| `StatusEmbedOfflineColor` | `#ED4245`; six-digit hex, with optional `#` |
| `StatusEmbedRefreshSeconds` | `60`; clamped to 15–3600 seconds |
| `StatusEmbedHeartbeatPath` | Empty; optional absolute path for the external monitor's heartbeat JSON |

Image URLs must be publicly accessible so Discord can load them. Invalid URLs are omitted; invalid colors fall back to the defaults. Image URLs support up to 2048 characters, website/invite URLs up to 400 characters. Long descriptions and player lists are truncated to stay within [Discord's embed limits](https://docs.discord.com/developers/resources/message#embed-object).

Server status uses the actual game server's running/boot state. A running server with zero players remains **🟢 Server Online** and uses the online accent. When the server stops, players are moved offline, uptime freezes, and the embed becomes **🔴 Server Offline**. Uptime begins when the server is ready, not when the mod loads. Normal application shutdown waits up to five seconds for the queued Discord edit; network failures can prevent delivery.

Start with [MelonPreferences.example.cfg](MelonPreferences.example.cfg). Merge the example keys into your existing `[JoinLeaveAlerts]` section; preserve your real webhook URL, message ID and roster settings. Change any `StatusEmbedShow...` value to `false` to hide that item. Restart after editing preferences to ensure they load.

For example, add your banner and logo while hiding the offline list:

```toml
StatusEmbedImageUrl = "https://your-site.example/banner.png"
StatusEmbedThumbnailUrl = "https://your-site.example/logo.png"
StatusEmbedShowOfflinePlayers = false
```

## Optional crash/offline monitor

A crashed or force-killed game cannot send its own offline update. `status_monitor.py` is an optional companion process using only Python's standard library (Python 3.11+). It edits the existing message after a heartbeat becomes stale and preserves all visibility settings, images and colors. It never creates a second message. Uptime in the offline card is the last known uptime; the optional timestamp reflects when the monitor detected heartbeat loss.

1. Enable `StatusEmbed` delivery and set `StatusEmbedHeartbeatPath` to a writable absolute path, for example `/srv/att/UserData/JoinLeaveStatus.json`. The mod writes it atomically every 30 seconds, independently of embed refresh. The file contains the message ID and offline payload, but no webhook URL.
2. Put the same Discord webhook URL into a private file accessible to the monitor, for example `/srv/att/status-webhook.txt`, and restrict its permissions (`chmod 600`). Alternatively, supply it through `ATT_STATUS_WEBHOOK_URL`.
3. Run the monitor outside the game process, with access to the heartbeat file:

```bash
python3 status_monitor.py \
  --heartbeat /srv/att/UserData/JoinLeaveStatus.json \
  --webhook-file /srv/att/status-webhook.txt \
  --timeout 180
```

The default timeout is three minutes; polling adds up to ten seconds. Use at least 60 seconds. Failed edits are retried; successful edits are not repeated for the same snapshot. Missing/incomplete files and missing message IDs are ignored. If normal shutdown's edit failed, the stale heartbeat allows the monitor to retry the offline state. Switching away from StatusEmbed or clearing the webhook disables monitoring in the next heartbeat.

Run the monitor as a separate service if you need it to survive game restarts. In a container setup, keep it outside the game container and share the heartbeat directory. A monitor on the same host cannot update Discord if that entire host loses power or network connectivity. No monitor is installed or started automatically. Leave `StatusEmbedHeartbeatPath` blank and stop the companion process to disable crash monitoring.
