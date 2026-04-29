#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

URL="http://127.0.0.1:5126"
LOGFILE=".dev_api.log"
PIDFILE=".dev_api.pid"

./scripts/dev-api-stop.sh || true

echo "Starting API ($URL)"
rm -f "$LOGFILE"
dotnet run --project src/AiStackManager.Api/AiStackManager.Api.csproj -- --urls "$URL" >"$LOGFILE" 2>&1 &
echo $! > "$PIDFILE"
echo "API process started with PID $(cat "$PIDFILE")"

for _ in {1..80}; do
  if curl -fsS "$URL/health" >/dev/null 2>&1; then
    echo "API is ready at $URL"
    exit 0
  fi

  pid=$(cat "$PIDFILE")
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "API process exited before it became ready. Log tail:" >&2
    tail -80 "$LOGFILE" >&2 || true
    exit 1
  fi

  sleep 0.25
done

echo "API did not become ready at $URL. Log tail:" >&2
tail -80 "$LOGFILE" >&2 || true
exit 1
