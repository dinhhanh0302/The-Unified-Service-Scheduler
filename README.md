# Unified Service Scheduler

Backend API for booking dealership service appointments with real-time bay and technician
availability (Keyloop Technical Assessment, Scenario A).

> **Status:** specification only. No code yet. Implementation follows `SPEC.md` §13, one commit per step.

## Repository contents (this commit)
| File | Purpose |
|---|---|
| `SPEC.md` | Source of truth: scope, business rules, acceptance criteria, API contract, NFRs, plan, assumptions, open questions |
| `docs/system-design.md` | System Design Document: architecture, components, data flow, technology choices, observability, assumptions, GenAI usage in design |
| `AGENTS.md` | Working rules for AI agents in this repository |
| `docs/ai-collaboration-log.md` | Step-by-step record of how the AI was directed and corrected |

## Planned stack
.NET 10, ASP.NET Core Web API (Controllers), PostgreSQL 16 with exclusion constraints, EF Core 10,
AutoMapper (Api layer only), Serilog, OpenTelemetry, xUnit + Testcontainers, Docker Compose.
Details and justifications: `SPEC.md` §3.

## Build, run and test
_Added in step 1 of `SPEC.md` §13._

## AI Collaboration Narrative
_Written in the final step. The step-by-step record is in `docs/ai-collaboration-log.md`._
