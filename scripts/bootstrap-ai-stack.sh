#!/usr/bin/env bash
set -Eeuo pipefail

# Bootstrap only. The .NET host is the controller.
export AI_MODEL="${AI_MODEL:-qwen25-coder-14b-64k}"
export OLLAMA_CONTEXT_LENGTH="${OLLAMA_CONTEXT_LENGTH:-65536}"

echo "Bootstrap sanity: $AI_MODEL / context $OLLAMA_CONTEXT_LENGTH"

command -v ollama >/dev/null || { echo "ollama missing" >&2; exit 1; }
command -v hermes >/dev/null || { echo "hermes missing" >&2; exit 1; }
command -v openclaw >/dev/null || { echo "openclaw missing" >&2; exit 1; }

ollama list | grep -F "$AI_MODEL" || echo "Model not found; create/pull it before starting the .NET host."
