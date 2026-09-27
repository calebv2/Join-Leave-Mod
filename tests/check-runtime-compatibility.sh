#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_path="${1:-/home/ATT/a-township-container/game-source}"
cecil_path="${MONO_CECIL_PATH:-/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll}"
if [ ! -f "$cecil_path" ]; then
  echo 'Mono.Cecil is required for the runtime compatibility check; set MONO_CECIL_PATH to its DLL.' >&2
  exit 1
fi
mcs -target:exe -r:"$cecil_path" -out:"$project_dir/tests/RuntimeCompatibility.exe" "$project_dir/tests/RuntimeCompatibility.cs"
mono "$project_dir/tests/RuntimeCompatibility.exe" "$project_dir/bin/Release/net472/JoinLeaveAlerts.dll" "$game_path/A Township Tale_Data/Managed"
