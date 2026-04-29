# Copilot — Quick Implementation Snippets

Use-case: in-editor quick implementations and Autofill suggestions.

Prompt template:

"Provide a complete, minimal implementation for the requested task consistent with `01-core.md`. Prefer explicitness and composition. If DI changes are needed, include an Autofac module example showing registrations. Include xUnit tests. Keep edits confined to the smallest set of files and produce short code blocks with file paths." 

Response expectations:
- Short, runnable code snippets.
- Autofac registration examples when DI changes required.
- A short test or usage example.
