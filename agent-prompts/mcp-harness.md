# MCP Harness & Local Agent Safety

Purpose: define safe operating rules for running local C# coding agents inside an MCP-style harness.

Recommended toolset (read-only first, guarded writes):
- `analyze_file`
- `analyze_solution`
- `analyze_workspace`
- `review_diff`
- `explain_build_errors`
- `generate_patch`
- `apply_patch` (write; guarded by approval)
- `pack_context`
- `list_models`
- `health_check`

Security & operational rules:
- Canonicalize and validate all paths; only operate inside allowed workspace roots.
- Reject paths containing traversal components (`..`) that leave the workspace.
- Limit max file size for reads and disallow binary files unless explicitly permitted.
- Require a generate->review->approve workflow before any `apply_patch` call that writes to disk.
- Log all agent actions (reads/writes/patches/decisions) to Serilog with an audit channel.
- Default to no shell execution; any exec must be explicitly approved and sandboxed.
- Keep a human-approver hook for destructive or broad refactors.

Auditing & telemetry:
- Emit structured events for each patch with: agent-id, model, files-changed, timestamp, user-approval-id.
- Capture verification results (compile/test) and include them in the audit log.

Fail-safe:
- If verification fails (compile/test), do not apply patches automatically; require human review.
