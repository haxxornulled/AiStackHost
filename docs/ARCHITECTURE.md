# AiStackManager — One-page Architecture

```mermaid
flowchart LR
  API["API (ASP.NET Core)\nControllers + Minimal Endpoints"]
  Orchestrator["AiStackOrchestrator\n(start/stop/status)"]
  Models["InferenceModelManager\n(model selection/download)"]
  Providers["IInferenceProvider Registry\n(Ollama, OpenClaw, etc)"]
  Hermes["HermesService\n(review & tooling)"]
  OpenClaw["OpenClawService\n(agent gateway)"]
  State["IAiStackStateStore\n(persisted or in-memory)"]
  Cmd["ICommandRunner\n(Process runner / test runner)"]

  API --> Orchestrator
  API --> Models
  Models --> Providers
  Orchestrator --> Providers
  Orchestrator --> Hermes
  Orchestrator --> OpenClaw
  Orchestrator --> State
  Providers --> Cmd
  Hermes --> Cmd
  OpenClaw --> Cmd
  Cmd --> System["OS / Shell"]

  subgraph "Providers"
    Ollama((Ollama))
    LocalRuntime((Local Runtime))
  end

  Providers --> Ollama
  Providers --> LocalRuntime
```

Key points
- `ICommandRunner` is the single adapter for all shell operations. Tests can override it.
- `IInferenceProvider` is the provider abstraction. Implementations should be side-effect free where possible and expose idempotent `Download/Ensure/Warm` operations.
- `AiStackOrchestrator` coordinates start/stop flows and records state via `IAiStackStateStore`.

Recommended next steps
- Add a durable `IAiStackStateStore` (SQLite/file) for multi-process resilience.
- Publish a provider template with a CONTRIBUTING guide for adding new providers.
