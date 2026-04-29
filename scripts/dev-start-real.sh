#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

usage() {
  cat <<EOF
Usage: $0 [--token <management-token>]

Enables real command execution, runs bootstrap checks, starts the API, and then
posts to the management start endpoint. If a token is provided, it will be
sent in the `X-AiStack-Token` header.
EOF
}

TOKEN=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --token)
      if [[ $# -lt 2 ]]; then
        echo "--token requires a value" >&2
        usage
        exit 1
      fi
      TOKEN="$2"; shift 2;;
    -h|--help)
      usage; exit 0;;
    *) echo "Unknown arg: $1"; usage; exit 1;;
  esac
done

export AISTACK_RUN_REAL_COMMANDS=true
echo "AISTACK_RUN_REAL_COMMANDS=true"

echo "Running bootstrap script (real command checks)"
bash scripts/bootstrap-ai-stack.sh

echo "Ensuring API is running on 127.0.0.1:5126..."
./scripts/dev-api-run.sh

echo "Calling management start endpoint"
if [ -n "$TOKEN" ]; then
  curl --fail-with-body -v -X POST http://127.0.0.1:5126/api/stack/start -H "X-AiStack-Token: $TOKEN" -w "\nHTTPSTATUS:%{http_code}\n"
elif [ -n "${AISTACK_MANAGEMENT_TOKEN:-}" ]; then
  curl --fail-with-body -v -X POST http://127.0.0.1:5126/api/stack/start -H "X-AiStack-Token: ${AISTACK_MANAGEMENT_TOKEN}" -w "\nHTTPSTATUS:%{http_code}\n"
else
  curl --fail-with-body -v -X POST http://127.0.0.1:5126/api/stack/start -w "\nHTTPSTATUS:%{http_code}\n"
fi
