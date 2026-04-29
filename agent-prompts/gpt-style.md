# GPT-style — PR Summaries & Rationale

Use-case: generate PR descriptions, short design rationales, and user-facing docs.

Prompt template:

"Summarize the change in 3–5 bullets: Problem, Approach, Files changed, Tests added, Migration/compat notes. Use `01-core.md` rules to evaluate quality and list any remaining risks in 1–2 bullets." 

Response expectations:
- 3–5 concise bullets.
- One-line risk/compat note if applicable.
- Reference repository files using relative paths.
