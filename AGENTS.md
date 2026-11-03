# AGENTS.md

Rules for any AI agent working in this repository. `SPEC.md` is the source of truth; this file only
summarises how to work with it.

## Before writing code
1. Read `SPEC.md` in full, especially §3 (stack and constraints), §5 (business rules), §11 (conventions)
   and §17 (instructions for the AI).
2. For the next step in §13, state: files to create/change, new assumptions, new open questions.
   Wait for the human to confirm.

## While coding
- Tests first, written from the acceptance criteria; tag each with `[Trait("AC", "AC-xx.x")]`.
- Never modify, skip or weaken a test to make it pass. If a test looks wrong, stop and explain.
- Ambiguity → add a row to SPEC §16 and stop. Do not guess.
- No packages outside SPEC §3 without asking. Versions live only in `Directory.Packages.props`.
- Domain and Application stay BCL-only. Controllers stay thin. No MediatR.
- AutoMapper only in `Scheduler.Api/Mapping`, never targeting Domain entities.
- The PostgreSQL exclusion constraints are mandatory; never replace them with application locks.

## Verifying
- Never claim a build or test passes without showing its actual output.
- Commands: `dotnet build -warnaserror`, `dotnet test`, `docker compose up --build`.
- Integration tests need Docker running (Testcontainers).

## After each step
- One commit per step: `step N: <summary>`.
- Append to `docs/ai-collaboration-log.md`: prompt summary, what was generated,
  what the human corrected and why, and how it was verified.
