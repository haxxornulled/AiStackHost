#!/usr/bin/env bash
set -euo pipefail
PIDFILE=".dev_api.pid"

if [ -f "$PIDFILE" ]; then
  pid=$(cat "$PIDFILE")
  if kill -0 "$pid" 2>/dev/null; then
    echo "Killing PID $pid"
    kill "$pid"
    rm -f "$PIDFILE"
  else
    echo "PID $pid not running. Removing pidfile."
    rm -f "$PIDFILE"
  fi
fi

# Fallback: kill any process matching the API project
pids=$(pgrep -f "AiStackManager.Api.csproj" || true)
if [ -n "$pids" ]; then
  echo "Killing matching processes: $pids"
  echo "$pids" | xargs -r kill
fi

# Fallback: kill any process listening on port 5126 or 5000
for port in 5126 5000; do
  sockpid=$(lsof -ti tcp:$port || true)
  if [ -n "$sockpid" ]; then
    echo "Killing process listening on port $port: $sockpid"
    echo "$sockpid" | xargs -r kill
  fi
done

echo "Stop complete."
