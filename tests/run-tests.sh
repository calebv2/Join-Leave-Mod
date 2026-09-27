#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_dir="$project_dir/tests"
mcs -target:exe -out:"$test_dir/JoinLeaveAlerts.Tests.exe" "$test_dir/Program.cs" "$project_dir/AnnouncementFormatter.cs" "$project_dir/DiscordWebhookPayload.cs" "$project_dir/PlayerNameResolver.cs" "$project_dir/WebhookAnnouncementFormatter.cs" "$project_dir/ModPackRejectionFormatter.cs" "$project_dir/DiscordDeliveryMode.cs" "$project_dir/DiscordEmbedPayload.cs" "$project_dir/StatusEmbedOptions.cs" "$project_dir/ServerStatusState.cs" "$project_dir/DiscordWebhookResponse.cs" "$project_dir/PlayerStatusRoster.cs" "$project_dir/OptionalEventSubscription.cs" "$project_dir/DiscordStatusEmbedClient.cs"
mono "$test_dir/JoinLeaveAlerts.Tests.exe"
python3 -m unittest discover -s "$test_dir" -p 'test_status_monitor.py'
bash "$project_dir/build.sh" "${1:-/home/ATT/a-township-container/game-source}"
bash "$test_dir/check-runtime-compatibility.sh" "${1:-/home/ATT/a-township-container/game-source}"
