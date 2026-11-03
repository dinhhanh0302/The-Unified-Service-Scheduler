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

**Verification:** after the CS0618 fix, a full solution build on my machine (14:13, warnings as
errors) produced every assembly, including both test projects. `dotnet test` with Docker running:
all tests passed (reported by me; console output not captured). From step 3 on, test results are
exported to `TestResults/*.trx` so the counts are recorded.

## Step 2: domain

**Prompt summary:** AI presented the step 2 plan (files, design decisions, new assumption A-12,
TDD in two passes). I approved it but asked to write tests and implementation in one pass.

**What the AI generated:** BCL-only domain model: enums, immutable entities created through
constructors (`Appointment.Confirm` enforces End = Start + Duration), `TimeSlot` (half-open),
`Result<T>` and the error catalog, `BookingPolicy` (BR-05 opening hours in the dealership time
zone incl. DST, BR-06 horizon, BR-07 alignment) and `AvailabilityEngine` (allocation with BR-10
tie-breaks, available start times, suggestions that respect the vehicle's other bookings).
Unit tests tagged with AC/BR ids: TimeSlot, BookingPolicy, Appointment, AvailabilityEngine.

**Notes for review:**
- Tests and implementation were written together at my request, so there is no "red first" run
  for this step; the tests are derived from the ACs in SPEC §7, not from the implementation.
- `AvailabilityEngine` never reads the clock: the booking window is computed from "now" by
  `BookingPolicy` and passed in, so tests are independent of when they run.
- Expected-result data for AC-05.1 was designed by hand (bay bookings 08:00–10:00, 11:00–11:15,
  12:15–13:00) so that 10:00, 11:15 and 13:00 are provably the first free starts after 09:00.

**What I corrected and why:** _(to fill in after review)_

**Verification:** same run as step 1: the 14:13 build compiled all step 2 sources (newest source
file 14:13:02), and `dotnet test` passed, including the domain unit tests (reported by me; console
output not captured).

## Step 3: infrastructure

**Prompt summary:** AI presented the step 3 plan (schema, seed, migrator, EF mapping, DB-level
tests); approved as proposed.

**What the AI generated:** `0001_initial_schema.sql` (tables, FKs, checks, `btree_gist`, the three
named exclusion constraints, partial unique index on `idempotency_key`, day-snapshot index),
`0002_seed_data.sql` (D1 per SPEC §7, D2 for cross-dealership tests, 4 service types,
customers, vehicles) with matching `SeedIds`, `DatabaseMigrator` (embedded scripts, once each,
one transaction, advisory lock) run by a hosted service, `SchedulerDbContext` with explicit
snake_case configurations and enum-array converters.
Integration tests: exclusion constraints per resource (bay, technician, vehicle), back-to-back
allowed, cancelled rows not blocking, 10 concurrent transactions → exactly 1 commit,
migrations applied and idempotent, seed matches SPEC §7, EF round trip of every entity.

**Notes for review:**
- Migrations run from an `IHostedService`, not from code after `builder.Build()` in Program.cs:
  `WebApplicationFactory` aborts the entry point right after the host is built, so code placed
  there would never run in integration tests.
- EF Core binds most immutable entities through their public constructors. `Dealership` and
  `Technician` expose enum lists that EF cannot bind that way, so they gained a private
  parameterless constructor for materialisation only (documented in SPEC §11); the time zone is
  resolved lazily after EF sets `TimeZoneId`.
- FK relationships are declared in EF (without navigations) only so EF orders inserts correctly;
  the schema itself is owned by the SQL scripts.
- The round-trip test books an appointment in 2030 so the cancellation rule never depends on the
  date the tests run.

**What I corrected and why:** _(to fill in after review)_

**Verification:** on my machine, Docker running: `dotnet build -warnaserror` succeeded (15.0 s);
`dotnet test --logger "trx;LogFilePrefix=step3"`: total 77, failed 0, succeeded 77, skipped 0
(13.7 s). Includes the database-level proof of BR-11 (10 concurrent transactions, 1 commit).

