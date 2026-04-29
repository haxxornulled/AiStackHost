# Contributing to AiStackHost

Thank you for your interest in contributing! This file explains how to run the code locally, run tests, and add providers or integrations.

Quick start
- Clone the repo and open in .NET 10 SDK.
- Run unit and integration tests:

```bash
dotnet test ./tests/AiStackManager.UnitTests -c Release
dotnet test ./tests/AiStackManager.IntegrationTests -c Release
```

Running integration tests with real system commands
- By default, integration tests are hermetic and will not execute system commands. To enable real command execution (e.g., to exercise `ollama`, `openclaw`, or `systemctl`), set an environment variable when running tests:

```bash
export AISTACK_RUN_REAL_COMMANDS=true
dotnet test ./tests/AiStackManager.IntegrationTests -c Release
```

Developer launcher
- A small console helper is available at `src/aistack-dev-launcher`. It can run the API host or trigger the management start/stop endpoints:

```bash
dotnet run --project src/aistack-dev-launcher -- api --urls http://localhost:5000
dotnet run --project src/aistack-dev-launcher -- start --token <token>
dotnet run --project src/aistack-dev-launcher -- stop --token <token>
```

Notes
- Do not commit secrets. `AiStack:ManagementToken` should be set via environment variables or secret store in CI.
- Prefer adding providers as separate packages; implement `IInferenceProvider` and register via the Autofac module.

Pull request checklist
- [ ] Add/update tests for new behavior.
- [ ] Update `docs/ARCHITECTURE.md` or `README.md` with design changes.
- [ ] Keep commits small and focused.
