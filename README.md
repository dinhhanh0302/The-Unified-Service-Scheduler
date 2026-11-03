# Unified Service Scheduler

Backend API for booking dealership service appointments with real-time bay and technician
availability (Keyloop Technical Assessment, Scenario A).

> **Status:** step 4 of `SPEC.md` §13 (booking use case). HTTP endpoints arrive in step 5.

## Repository contents
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

## Prerequisites
- .NET 10 SDK
- Docker Desktop (for `docker compose` and the Testcontainers integration tests)

## Build
```bash
dotnet build -warnaserror
```

## Run
With Docker (API + PostgreSQL):
```bash
docker compose up --build
```
- Swagger UI: http://localhost:8080/swagger
- OpenAPI document: http://localhost:8080/openapi/v1.json
- Health: http://localhost:8080/health/live, http://localhost:8080/health/ready

Locally against the compose database:
```bash
docker compose up -d postgres
dotnet run --project src/Scheduler.Api --launch-profile http
```
Swagger UI is then at http://localhost:5080/swagger.

## Test
```bash
dotnet test
```
Integration tests start a disposable PostgreSQL 16 container, so Docker must be running.
To keep a record of the results:
```bash
dotnet test --logger "trx;LogFilePrefix=run"
```
The database schema and seed data are applied automatically on start-up (`DatabaseMigrator`);
seed ids are listed in `src/Scheduler.Infrastructure/Persistence/SeedIds.cs`.
Sample requests are in `http/scheduler.http`.

## AI Collaboration Narrative
_Written in the final step. The step-by-step record is in `docs/ai-collaboration-log.md`._
