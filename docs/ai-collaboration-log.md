# AI Collaboration Log

One entry per step of SPEC §13. Source material for the README "AI Collaboration Narrative".

## Step 0: specification (first commit)

**Prompt summary:** Asked the AI for a standard AI-spec structure, had it review my first spec
draft, then had it rewrite the spec for Keyloop Scenario A.

**What the AI generated:** `SPEC.md` (17 sections: business rules, functional requirements,
Given/When/Then acceptance criteria, API contract, error catalog, NFRs, traceability matrix,
implementation plan, Definition of Done, assumptions, open questions, instructions for the AI),
plus `docs/system-design.md` (Keyloop Part 1: architecture diagram, component roles, data model,
data flow, concurrency design, technology choices with alternatives, observability, quality
attributes, assumptions, GenAI usage in design), `AGENTS.md` and this log.

**What I corrected / decided, and why:**
- Reset the spec version to 1.0 for the first approved version.
- Switched the API layer from Minimal APIs to Controllers: a conventional structure I can review
  and defend, built-in model validation and richer OpenAPI metadata.
- Chose AutoMapper (14.x, MIT) over hand-written mapping for familiarity, but limited it to the
  Api layer and required a configuration-validity test (OQ-14).
- Challenged the concurrency design: why not a version column checked in the API server?
  Kept PostgreSQL exclusion constraints, because the conflict is between two INSERTs of new rows
  (no shared row for a version to protect) and a check-then-write in the API is racy across
  instances. A version token (`xmin`) remains the right tool for cancelling an existing row.
- The AI started generating the step 1 code skeleton straight away. I stopped it and had it
  remove the code: the first commit must contain only the specification, so the spec can be
  reviewed on its own before any implementation.
- The AI's first System Design Document was long (12 detailed sections, several diagrams). I
  replaced it with a compact format of my own and had the AI fill it from the spec. The AI kept the
  format but corrected content that no longer matched this project (Minimal APIs → Controllers,
  store names, and verification claims that had not happened yet).

**Verification:** _(to fill in: how I reviewed `SPEC.md` against the Keyloop brief)_

## Step 1: solution skeleton

**Prompt summary:** Plan for step 1 of SPEC §13 presented by the AI (files, assumptions, open
questions) and approved before any file was created.

**What the AI generated:** `.slnx` solution, central package management, 4 src projects and
2 test projects, `Program.cs` (Controllers, Serilog JSON, OpenTelemetry, health checks,
OpenAPI + Swagger UI, correlation id middleware), database health check, Dockerfile,
docker-compose, `.http` stub, README build/run/test section.
Tests: correlation id unit tests; integration tests for `/health/live`, `/health/ready`
(real PostgreSQL via Testcontainers), correlation id echo and the OpenAPI document.

**Notes for review:**
- The AI's environment had no .NET SDK or NuGet access, so it could not build or run tests.
  Package versions in `Directory.Packages.props` were chosen without checking NuGet and are
  confirmed by the first restore on my machine.
- No extra packages: the database health check uses `NpgsqlDataSource` directly, and Npgsql
  tracing/metrics use its built-in "Npgsql" source instead of an instrumentation package.

**Dependency audit on first restore** (Visual Studio restore on my machine, read back from
`obj/project.assets.json`; NU190x are build errors per SPEC §3):

| Advisory | Package (as chosen by the AI) | Fix applied |
|---|---|---|
| GHSA-rvv3-g6hj-g44x (high) | AutoMapper 14.0.0 | Fixed only in 15.1.1 / 16.1.1 (commercial). Package removed from step 1 (not used yet) and raised as **OQ-15** instead of suppressing the warning |
| GHSA-v5pm-xwqc-g5wc (high) | Microsoft.OpenApi 2.0.0 (transitive) | Pinned to 2.7.5 |
| GHSA-g94r-2vxg-569j, GHSA-4625-4j76-fww9 (moderate) | OpenTelemetry.Api / OTLP exporter 1.12.0 | OpenTelemetry packages to 1.15.3 |
| GHSA-mggc-4xg6-vcxf, GHSA-q939-rpr3-3284 (high) | SSH.NET 2024.2.0 (via Testcontainers 4.6.0) | Testcontainers 4.14.0 and SSH.NET pinned to 2026.0.0 |

Second restore: no audit warnings, all versions resolved.

Lesson: the AI picked package versions from memory; the audit gate in `Directory.Build.props`
caught four vulnerable dependencies before any code ran. The AutoMapper finding overturns an
earlier design decision, so it goes back to me as an open question rather than being patched over.

**First build on my machine** (`dotnet build -warnaserror`): 5 of 6 projects green;
`Scheduler.IntegrationTests` failed with CS0618: `PostgreSqlBuilder()` is obsolete in
Testcontainers 4.14 (the version bump from the audit fix). Fixed by passing the image to the
constructor: `new PostgreSqlBuilder("postgres:16-alpine")`. Warnings-as-errors caught an API
deprecation the AI did not anticipate when it bumped the package.

**What I corrected and why:** _(to fill in after review)_

**Verification:** _(paste `dotnet build` and `dotnet test` results here)_
