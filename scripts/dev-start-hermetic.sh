#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

echo "Hermetic startup: no system changes."
export AISTACK_RUN_REAL_COMMANDS=false

echo "Running bootstrap (hermetic checks)"
bash scripts/bootstrap-ai-stack.sh

echo "Ensuring API is running on 127.0.0.1:5126..."
./scripts/dev-api-run.sh

echo "Posting start request to management endpoint"
curl --fail-with-body -v -X POST http://127.0.0.1:5126/api/stack/start -w "\nHTTPSTATUS:%{http_code}\n"
