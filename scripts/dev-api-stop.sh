#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

PIDFILE=".dev_api.pid"

stop_pid() {
  local pid="$1"
  if kill -0 "$pid" 2>/dev/null; then
    echo "Killing PID $pid"
    kill "$pid" 2>/dev/null || true
  fi
}

wait_for_exit() {
  local pid="$1"
  for _ in {1..20}; do
    if ! kill -0 "$pid" 2>/dev/null; then
      return 0
    fi
    sleep 0.1
  done

  if kill -0 "$pid" 2>/dev/null; then
    echo "PID $pid did not stop; forcing termination."
    kill -9 "$pid" 2>/dev/null || true
  fi
}

if [ -f "$PIDFILE" ]; then
  pid=$(cat "$PIDFILE")
  if kill -0 "$pid" 2>/dev/null; then
    stop_pid "$pid"
    wait_for_exit "$pid"
    rm -f "$PIDFILE"
  else
    echo "PID $pid not running. Removing pidfile."
    rm -f "$PIDFILE"
  fi
fi

# Fallback: kill any dotnet run parent or compiled API child.
pids=$(pgrep -f "([d]otnet run --project src/AiStackManager.Api/AiStackManager.Api.csproj|[A]iStackManager.Api.*127.0.0.1:5126)" || true)
if [ -n "$pids" ]; then
  echo "Killing matching processes: $pids"
  echo "$pids" | xargs -r kill || true
  sleep 0.5
fi

# Fallback: kill any process listening on port 5126 or 5000
for port in 5126 5000; do
  sockpid=$(lsof -ti tcp:$port || true)
  if [ -n "$sockpid" ]; then
    echo "Killing process listening on port $port: $sockpid"
    echo "$sockpid" | xargs -r kill || true
    sleep 0.5
    sockpid=$(lsof -ti tcp:$port || true)
    if [ -n "$sockpid" ]; then
      echo "Process on port $port did not stop; forcing termination: $sockpid"
      echo "$sockpid" | xargs -r kill -9 || true
    fi
  fi
done

echo "Stop complete."
