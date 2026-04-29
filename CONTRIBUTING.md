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
- A small console helper is available at `src/aistack-dev-launcher`. It can run the API host or trigger the management start/stop endpoints.

Developer scripts
- Use the convenience scripts under `scripts/` for a predictable local dev workflow. The API development host binds to `127.0.0.1:5126` to avoid port confusion.

Hermetic (default) startup
- Start the API on the locked dev port:

```bash
./scripts/dev-api-run.sh
```

- Run the hermetic bootstrap + start (no system changes, safe):

```bash
./scripts/dev-start-hermetic.sh
```

Real startup (opt-in)
- To enable real system commands and perform actual `ollama`, `hermes`, and `openclaw` operations, use:

```bash
./scripts/dev-start-real.sh --token <management-token>
```

- Real startup sets `AISTACK_RUN_REAL_COMMANDS=true` and will call user-level services via `systemctl --user` (no interactive sudo required).

Notes
- Do not commit secrets. `AiStack:ManagementToken` should be set via environment variables or secret store in CI.
- Prefer adding providers as separate packages; implement `IInferenceProvider` and register via the Autofac module.

- [ ] Add/update tests for new behavior.
- [ ] Update `docs/ARCHITECTURE.md` or `README.md` with design changes.
- [ ] Keep commits small and focused.
Pull request checklist
- [ ] Add/update tests for new behavior.
- [ ] Update `docs/ARCHITECTURE.md` or `README.md` with design changes.
- [ ] Keep commits small and focused.
- [ ] Add/update tests for new behavior.
- [ ] Update `docs/ARCHITECTURE.md` or `README.md` with design changes.
- [ ] Keep commits small and focused.
