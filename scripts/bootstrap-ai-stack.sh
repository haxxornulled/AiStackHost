#!/usr/bin/env bash
set -Eeuo pipefail

# Bootstrap only. The .NET host is the controller.
export AI_MODEL="${AI_MODEL:-qwen25-coder-14b-64k}"
export OLLAMA_CONTEXT_LENGTH="${OLLAMA_CONTEXT_LENGTH:-65536}"

echo "Bootstrap sanity: $AI_MODEL / context $OLLAMA_CONTEXT_LENGTH"

command -v ollama >/dev/null || { echo "ollama missing" >&2; exit 1; }
command -v hermes >/dev/null || { echo "hermes missing" >&2; exit 1; }
command -v openclaw >/dev/null || { echo "openclaw missing" >&2; exit 1; }

ollama_list=$(mktemp)
ollama_error=$(mktemp)
trap 'rm -f "$ollama_list" "$ollama_error"' EXIT

if ollama list >"$ollama_list" 2>"$ollama_error"; then
  grep -F "$AI_MODEL" "$ollama_list" || echo "Model not found; the .NET host will attempt to pull it during startup."
else
  cat "$ollama_error" >&2 || true
  echo "Ollama is not reachable yet; the .NET host owns runtime startup."
fi
