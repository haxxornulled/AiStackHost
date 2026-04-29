#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR/.."

./scripts/dev-api-stop.sh || true
./scripts/dev-start-real.sh "$@"
