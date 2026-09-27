#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_path="${1:-/home/ATT/a-township-container/game-source}"
output_dir="$project_dir/bin/Release/net472"
mkdir -p "$output_dir"

# Bind framework APIs to the game runtime, not the build host's newer Mono.
mcs -noconfig -nostdlib -target:library -langversion:7 -out:"$output_dir/JoinLeaveAlerts.dll" \
  -r:"$game_path/A Township Tale_Data/Managed/mscorlib.dll" \
  -r:"$game_path/A Township Tale_Data/Managed/System.dll" \
  -r:"$game_path/A Township Tale_Data/Managed/System.Core.dll" \
  -r:"$game_path/MelonLoader/net35/MelonLoader.dll" \
  -r:"$game_path/MelonLoader/net35/0Harmony.dll" \
  -r:"$game_path/A Township Tale_Data/Managed/Root.Township.dll" \
  -r:"$game_path/A Township Tale_Data/Managed/UnityEngine.CoreModule.dll" \
  "$project_dir/Properties/AssemblyInfo.cs" \
  "$project_dir/AnnouncementFormatter.cs" \
  "$project_dir/PlayerNameResolver.cs" \
  "$project_dir/PlayerStatusRoster.cs" \
  "$project_dir/DiscordWebhookPayload.cs" \
  "$project_dir/DiscordDeliveryMode.cs" \
  "$project_dir/DiscordEmbedPayload.cs" \
  "$project_dir/StatusEmbedOptions.cs" \
  "$project_dir/ServerStatusState.cs" \
  "$project_dir/DiscordWebhookResponse.cs" \
  "$project_dir/OptionalEventSubscription.cs" \
  "$project_dir/DiscordWebhookClient.cs" \
  "$project_dir/DiscordStatusEmbedClient.cs" \
  "$project_dir/WebhookAnnouncementFormatter.cs" \
  "$project_dir/ModPackRejectionFormatter.cs" \
  "$project_dir/Core.cs"

echo "Built $output_dir/JoinLeaveAlerts.dll"
