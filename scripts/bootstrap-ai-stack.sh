#!/usr/bin/env bash
set -Eeuo pipefail

# Bootstrap only. The .NET host is the controller.
export AI_MODEL="${AI_MODEL:-qwen25-coder-14b-64k}"
export OLLAMA_CONTEXT_LENGTH="${OLLAMA_CONTEXT_LENGTH:-65536}"

echo "Bootstrap sanity: $AI_MODEL / context $OLLAMA_CONTEXT_LENGTH"

command -v ollama >/dev/null || { echo "ollama missing" >&2; exit 1; }
command -v hermes >/dev/null || { echo "hermes missing" >&2; exit 1; }
command -v openclaw >/dev/null || { echo "openclaw missing" >&2; exit 1; }

if ! ollama list >/tmp/aistack-ollama-list.$$ 2>/tmp/aistack-ollama-error.$$; then
  if [ "${AISTACK_RUN_REAL_COMMANDS:-false}" = "true" ]; then
    echo "Ollama is not reachable; attempting to start user service."
    systemctl --user start ollama || true
    sleep 1
    ollama list >/tmp/aistack-ollama-list.$$ 2>/tmp/aistack-ollama-error.$$ || true
  fi
fi

if [ -s /tmp/aistack-ollama-list.$$ ]; then
  grep -F "$AI_MODEL" /tmp/aistack-ollama-list.$$ || echo "Model not found; create/pull it before starting the .NET host."
else
  cat /tmp/aistack-ollama-error.$$ >&2 || true
  echo "Ollama is not reachable yet; the .NET host will attempt to start it."
fi

rm -f /tmp/aistack-ollama-list.$$ /tmp/aistack-ollama-error.$$
