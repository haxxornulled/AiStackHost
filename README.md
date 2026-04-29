# AI Stack Manager

Clean Architecture .NET host for managing the local AI coding stack on Windows 11 + Debian WSL2:

- Ollama model runtime
- Hermes Agent code-review workflow
- OpenClaw gateway/TUI/dashboard bridge
- Git/dotnet review commands
- Health/status/log APIs

The original `ai-stack` bash script proved the workflow, but it was turning into a god controller. This project moves orchestration into a typed .NET host with lifecycle services, background health monitoring, API surfaces, observability, and testable seams.

The host is a local management plane. Management API calls are accepted from loopback by default, or from any caller that presents `X-AiStack-Token` / `Authorization: Bearer <token>` when `AiStack:ManagementToken` is configured. Do not expose the API beyond loopback without a real token and network controls.

## Key defaults

```json
{
  "AiStack": {
    "Model": "qwen25-coder-14b-64k",
    "InferenceProvider": "ollama",
    "OllamaBaseUrl": "http://127.0.0.1:11434",
    "HermesBaseUrl": "http://127.0.0.1:11434",
    "HermesProvider": "ollama",
    "OpenClawBaseUrl": "http://127.0.0.1:11434",
    "OpenClawModelRef": "ollama/qwen25-coder-14b-64k",
    "ContextLength": 65536,
    "OpenClawPort": 18789,
    "AutoStartOnHostStartup": false
  }
}
```

## Build

```bash
dotnet restore ./AiStackManager.sln
dotnet build ./AiStackManager.sln -c Release --no-restore
dotnet test ./AiStackManager.sln -c Release --no-build
```

## Run

For local development the project provides simple scripts under `scripts/` and a dev launcher.

- Start the API on the locked dev port `127.0.0.1:5126`:

```bash
./scripts/dev-api-run.sh
```

- Hermetic startup (safe default) — runs bootstrap checks and calls the management start endpoint without changing system services:

```bash
./scripts/dev-start-hermetic.sh
```

- Real startup (opt-in) — enables real command execution and will configure/start local user services (`systemctl --user`) and call `ollama`, `hermes`, `openclaw`:

```bash
./scripts/dev-start-real.sh --token <management-token>
```

To require a token for management endpoints when running manually:

```bash
dotnet run --project src/AiStackManager.Api/AiStackManager.Api.csproj -- \
  --AiStack:ManagementToken "$AISTACK_MANAGEMENT_TOKEN" \
  --AiStack:AllowManagementWithoutTokenFromLoopback false
```

## Endpoints

Minimal API:

```http
GET  /health
GET  /api/stack/status
POST /api/stack/start
POST /api/stack/stop
POST /api/stack/restart
```

Controller APIs:

```http
POST /api/review/pre-push
POST /api/hermes/smoke-test
POST /api/commands/run-safe-status
GET  /api/models/providers
GET  /api/models
GET  /api/models/current
POST /api/models/select
POST /api/models/download
```

## Design rule

Bash is bootstrap glue. C# is the controller, bridge, orchestrator, workflow engine, and API host.

## External tool assumptions

- Model selection and downloads flow through provider-neutral `IInferenceProvider<TOptions>` implementations. Ollama is the first local provider, not a hard-coded application dependency.
- Ollama serves its local API at `http://localhost:11434/api`; coding-agent workloads should allocate at least 64K context and can be verified with `ollama ps`.
- Hermes review runs use non-interactive `hermes chat --quiet` with explicit `terminal,skills` toolsets.
- OpenClaw Gateway is configured for `gateway.mode=local`, loopback binding, schema-validated config writes, and `provider/model` references such as `ollama/qwen25-coder-14b-64k`.
