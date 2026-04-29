#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

./scripts/dev-api-stop.sh || true

echo "Starting API (http://127.0.0.1:5126)"
dotnet run --project src/AiStackManager.Api/AiStackManager.Api.csproj --urls http://127.0.0.1:5126 &
echo $! > .dev_api.pid
echo "API started with PID $(cat .dev_api.pid)"
