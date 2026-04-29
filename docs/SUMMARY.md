# Summary: What We Were Doing, What We Want Now, and Why

## What we were doing

We were trying to make Hermes CLI act like Codex for a local code-review workflow inside Debian on WSL.

The desired review behavior was:

1. Use Ollama as the local model runtime.
2. Use `qwen25-coder-14b-64k` as the shared coding model.
3. Configure Hermes to use that model.
4. Configure OpenClaw to use that model.
5. Run a strict pre-push review:
   - `git status`
   - current branch
   - fetch
   - diff stat
   - full diff
   - staged diff
   - unstaged diff
   - inspect changed files
   - find solution file
   - run restore/build/tests
   - return Critical, Warnings, Suggestions, Looks Good, Fix Plan, Verdict

## What we learned

`qwen2.5-coder:14b` reported only 32,768 context tokens, and Hermes requires at least 64K context for agent workflows.

We created this model tag:

```bash
qwen25-coder-14b-64k
```

That fixed the first failure. Hermes then launched with the correct model.

The next failure was tool execution. Hermes printed raw JSON:

```json
{
  "name": "terminal",
  "arguments": {
    "command": "git status"
  }
}
```

But it reported `0 tool calls`. So the model/config was accepted, but real terminal tool execution did not happen.

## What we want now

We want a .NET architecture that owns this AI stack properly:

- `IHostedLifecycleService` for startup/shutdown phases
- `BackgroundService` for health snapshots
- Minimal API for simple stack commands
- Controllers for richer review/command workflows
- AppHost/bridge mindset
- Clean Architecture
- Non-anemic domain model
- Strong seams around external effects
- Serilog + OpenTelemetry
- Tests

## Why this is better

The bash script taught us the operations, but it was becoming a god controller. It owned too many decisions:

- model selection
- Ollama service management
- OpenClaw setup
- Hermes setup
- review orchestration
- diagnostics
- logs
- status
- shutdown

That belongs in a typed, testable, observable .NET service, not a giant shell switch statement.

## Derek Comartin seams mindset

The seam is not “interface everything.” The seam is the boundary where policy meets external effects.

Good seams here:

- process execution
- systemd
- Ollama CLI/API
- Hermes CLI
- OpenClaw CLI
- git/dotnet commands
- file config writes
- health snapshots

The domain should not be anemic. It should own state transitions and rules:

- context below 64K is invalid
- review workflows do not allow edits
- stack cannot start twice
- failed component degrades or fails the stack
- Hermes review must pass a tool-call smoke test before full review is trusted
