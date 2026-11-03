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

## Step 4: application (booking use case)

**Prompt summary:** AI presented the step 4 plan; approved. OQ-15 (mapping library) still open,
needed before step 5.

**What the AI generated:** ports `IReferenceDataStore` / `IAppointmentStore` and `InsertResult`
(the database outcome per constraint), `AppointmentDto` (times converted to the dealership's
offset), `BookAppointmentValidator` (steps 1–2 of the error order: all field errors at once,
then explicit offset and 15-minute alignment), `RequestHasher` (SHA-256 over a canonical request,
start hashed as a UTC instant), `BookAppointmentHandler` (steps 3–9: idempotency replay / reuse,
not-found order, ownership, horizon, opening hours, vehicle overlap, allocation, insert with up to
3 attempts on bay/technician conflicts, no retry on vehicle conflict, replay of a concurrent
winner on key conflict, 409 with same-day suggestions), `ValidationFailedError` and
`SlotUnavailableError` (errors that carry data; `DomainError` unsealed for this).
Unit tests with in-memory fake stores that can simulate constraint conflicts on the next insert.

**Decision during this step (OQ-15):** AutoMapper 16.2.0 without a license key. Checked before
applying: 16.x contains the fix for GHSA-rvv3-g6hj-g44x; without a key the library works normally
and only logs a warning (category `LuckyPennySoftware.AutoMapper.License`); production needs a key
(free Community licence for organisations under $5M revenue). API changes to handle in step 5:
`MapperConfiguration` now needs an `ILoggerFactory`, and `AddAutoMapper` requires a config action.

**Notes for review:**
- No `AddApplication()` extension in the Application project: `IServiceCollection` lives in a NuGet
  package and SPEC §3 keeps Application BCL-only. Handlers are registered in the Api in step 5.
- `StartTime` reaches the validator as the raw string so "missing offset" (INVALID_START_TIME,
  OQ-09) can be told apart from "malformed date" (VALIDATION_FAILED).
- Retrying is only meaningful because the snapshot is re-read on each attempt: another bay or
  technician may still be free after a conflict.

**What I corrected and why:** _(to fill in after review)_

**Verification:** on my machine: `dotnet build -warnaserror` succeeded (17.1 s), including the
restore of AutoMapper 16.2.0 with the NuGet audit as an error gate (no advisories);
`dotnet test --logger "trx;LogFilePrefix=step4"`: total 110, failed 0, succeeded 110, skipped 0
(15.7 s), up from 77 in step 3.

## Step 5: API (book and read appointments)

**Prompt summary:** AI presented the step 5 plan, including the choice to keep database conflict
mapping in step 6 as SPEC §13 orders it; approved as proposed.

**What the AI generated:** EF stores (`ReferenceDataStore`, `AppointmentStore`: one query per
resource type for the day snapshot, vehicle bookings across dealerships, idempotency lookup),
`GetAppointmentHandler` (FR-04) and a shared `AppointmentDtoLoader`, `AppointmentsController`
(POST with `Idempotency-Key`, 201 + Location, `Idempotency-Replayed` on replay; GET by id),
request / response contracts, `AppointmentProfile` (AutoMapper 16), `ProblemResults` (the error
catalog in one place, ProblemDetails with `code`, `correlationId`, `errors`, `suggestedStartTimes`),
`ValidationProblemFactory` (framework 400s such as an invalid GUID become VALIDATION_FAILED),
enum values serialised as strings, `INTERNAL_ERROR` code on 500s, sample requests in `.http`.
Tests: AutoMapper configuration validity and mapping, error catalog → status for every code,
API tests for AC-01.1 – 01.3, 01.6, 01.11, 01.13, 01.15 – 01.21 and the correlation id in errors.

**Notes for review:**
- API tests freeze "now" (`FixedTimeProvider` via `ConfigureTestServices`) at the SPEC §7 instant.
- Each API test builds its own dealership, bays, technicians, customer and vehicle: the ACs all
  book "Tuesday 09:00", so sharing seed data would make tests block each other through the
  exclusion constraints.
- Known gap until step 6: two truly concurrent requests for the same bay can make the loser get a
  500 (the constraint holds, but its violation is not yet mapped to 409).

**What I corrected and why:** _(to fill in after review)_

**Verification:** on my machine: `dotnet build -warnaserror` succeeded (12.6 s);
`dotnet test --logger "trx;LogFilePrefix=step5"`: total 149, failed 0, succeeded 149, skipped 0
(11.2 s), up from 110 in step 4.

## Step 6: concurrency and observability

**Prompt summary:** AI presented the step 6 plan with its reasoning for why AC-02.2 always yields
exactly two successes within three attempts and why AC-02.3 always ends in
VEHICLE_ALREADY_BOOKED; approved as proposed.

**What the AI generated:** `AppointmentStore.InsertAsync` now maps `PostgresException` by SQLSTATE
and constraint name (bay, technician, vehicle, idempotency key, deadlock) and clears the change
tracker after a conflict; `InsertResult` split per constraint so metrics can tag it;
`SchedulerInstrumentation` (BCL `Meter` / `ActivitySource` named "Scheduler"):
`scheduler.booking.requests{result}`, `scheduler.booking.duration`,
`scheduler.concurrency.conflicts{constraint}` and an `AvailabilityEngine.Allocate` span per attempt.
Tests: store-level mapping per constraint and recovery in the same scope; parallel HTTP tests for
AC-02.1 (10 requests, 1 bay → 1×201, 9×409), AC-02.2 (2 bays → 2×201, no shared bay or
technician), AC-02.3 (same vehicle → 1×201, 4×409 VEHICLE_ALREADY_BOOKED), none returning 500;
MeterListener tests for every result tag and the conflict tag.

**Notes for review:**
- Handler tests and instrumentation tests share one xUnit collection: the meter is process-wide,
  so running them sequentially keeps the metric assertions exact.
- Unknown database errors still throw (→ 500 with `INTERNAL_ERROR` and correlation id): only the
  conflicts the design expects are turned into results.

**First run: 162/164, two concurrency tests red.** AC-02.1 passed, but AC-02.2 (8 of 10
requests) and AC-02.3 (2 of 5) returned 500 instead of 409. The server logs captured in the
`.trx` file showed the cause: `40P01 deadlock detected`, wrapped by EF Core in
`InvalidOperationException("…likely due to a transient failure")` around the
`DbUpdateException`. The store only inspected `DbUpdateException.InnerException`, so deadlocks
escaped the mapping. Deadlocks appear once two resources are contended: each insert checks three
exclusion constraints, and two waiting transactions can block each other across the bay and
technician indexes. AC-02.1 (one bay, one technician) never produced the cycle, which is why it
passed. Fix: search the whole inner-exception chain for the `PostgresException`. The tests were
not changed; they caught a real defect.

**Second run: 162/164, still red, no more 500s but wrong answers.** AC-02.2 booked only one of
the two bays and took 29 s; AC-02.3 answered SLOT_UNAVAILABLE instead of VEHICLE_ALREADY_BOOKED.
The AI's plan had assumed conflicts resolve cleanly in "rounds"; in reality the losing inserts
deadlocked against each other again and again (each detection costs about 1 s), so requests used up
their three attempts on deadlocks while a bay was still free. Integrity was never broken (no double
booking), but the outcome depended on timing, and more retries would only hide it.

**Design change (approved by me before it was made):** each allocation attempt now runs in one
transaction holding `pg_advisory_xact_lock(dealership, local day)`, via a new store method
`AllocateAndInsertAsync` that calls back into the handler for the allocation decision, so the
business logic stays in Application. Same dealership-day requests are serialised; exclusion
constraints remain the final guarantee. SPEC §10 and SDD §6 updated, including why "retries alone"
was rejected. The tests were again left unchanged.

**Third run: 163/164.** All three API concurrency tests (AC-02.1 – 02.3) green. The remaining
failure was the step 3 database test with 10 raw concurrent transactions: still exactly one commit,
but none of the nine losers reported 23P01 (run time about 9 s, consistent with deadlock detection).
This test does not touch application code and had passed in step 3, so it was timing-dependent.
Following SPEC §17 the AI stopped and explained instead of editing the test.

**Test correction (approved by me):** the test asserted something PostgreSQL does not promise.
Under concurrency it guarantees a single winner, but a loser can be rejected by the exclusion
constraint (23P01) or by deadlock detection (40P01). The assertion now requires exactly one commit,
nine rejections that are each 23P01 or 40P01 (anything else still fails), and one row in the
table, and it prints every outcome on failure. This is the only test changed in step 6, and it was
changed because its expectation was wrong, not to make code pass.

**What I corrected and why:** _(to fill in after review)_

**Verification:** fourth run on my machine (`dotnet test --logger "trx;LogFilePrefix=step6d"`,
read back from the `.trx` files): unit tests 122/122, integration tests 42/42, total 164/164.
AC-02.2 now completes in 0.10 s (29 s with retries alone), AC-02.3 in 0.09 s, AC-02.1 in 1.5 s,
the raw 10-transaction database test in 0.04 s.

## Fix: local run against the compose database

**Symptom:** `dotnet run` (Development) crashed at start-up in `DatabaseMigrator`:
`28P01: password authentication failed for user "scheduler"`. Tests were unaffected because
Testcontainers maps a random free port.

**Cause:** the Development connection string pointed at `localhost:5432`, the same host port the
compose file published. With a PostgreSQL already installed on the machine, `localhost:5432` reaches
that server, which has no `scheduler` user with this password.

**Fix:** compose publishes PostgreSQL on host port 5433 and `appsettings.Development.json` uses 5433.
The API container still talks to `postgres:5432` on the compose network. README updated.

## Steps 7–9: availability, cancellation, idempotency over HTTP, reference data

**Prompt summary:** I asked the AI to implement all remaining functional requirements in one go
instead of one plan per step. The code was written and verified together, and committed as one
commit: the three steps share changes (store interfaces, test fakes), so splitting them would
have produced intermediate commits that do not build.

**What the AI generated:**
- Step 7 (FR-06): `GetAvailabilityHandler` (validation, not-found, engine, local offsets),
  `AvailabilityController` (`GET /api/v1/availability`), mapping, unit and API tests AC-06.1 – 06.5.
- Step 8 (FR-07, FR-08): `CancelAppointmentHandler`, `POST /appointments/{id}/cancel`,
  `IAppointmentStore.UpdateAsync` with optimistic concurrency on PostgreSQL `xmin` (new AC-07.5: two
  parallel cancels → one 200, one 409), unit and API tests AC-07.1 – 07.5, API tests AC-08.1 – 08.4.
  Handler change: when a request with an `Idempotency-Key` fails, the key is checked once more
  before answering, because a concurrent identical request may have committed in between and
  caused the failure (it would otherwise answer VEHICLE_ALREADY_BOOKED to a retry of a booking that
  succeeded). Found while reasoning about AC-08.4 under the new per-dealership-day lock.
- Step 9 (FR-09): list methods on `IReferenceDataStore`, `ReferenceDataQueries`,
  `ReferenceDataController` (`/dealerships`, `/service-types`, `/customers/{id}/vehicles`), mapping
  with enums as names, API tests; the mapping test now loads every profile like Program.cs does.
- SPEC: AC-07.5, §10 notes on the idempotency re-check and cancel row version, traceability rows.

**What I corrected and why:** _(to fill in after review)_

**Verification:** on my machine: `dotnet build -warnaserror` succeeded (9.8 s);
`dotnet test --logger "trx;LogFilePrefix=step7to9"`: total 199, failed 0, succeeded 199, skipped 0
(8.4 s), up from 164 in step 6, first run green.

## Step 10–11: completeness review, missing API tests, load script, final documentation

**Prompt summary:** I asked the AI whether the code was ready to submit. It answered no and listed
the gaps against SPEC §14 and the Keyloop brief; I accepted its plan for the parts it could do.

**What the AI generated:**
- Review result: every AC in SPEC §7 had a test, but 8 Must ACs were covered only by unit tests,
  while the Definition of Done asks for an API-level test for each. Added to
  `BookAppointmentApiTests`: AC-01.4 (technician busy for part of the window), AC-01.5 (only free
  technician unqualified), AC-01.7 (only free bay of the wrong type), AC-01.8 (least booked
  minutes wins), AC-01.9 (lowest bay name wins), AC-01.10 (inactive resources ignored), AC-01.12
  (Sunday), AC-01.14 (61 days ahead). `ApiTestData` gained helpers to add bays and technicians.
- `perf/booking.k6.js`: availability 20 req/s and booking 10 req/s for 30 s with the SPEC §10
  p95 targets as thresholds; 409/422 counted as expected answers.
- README rewritten: quick start, tests and filters, cURL examples, concurrency summary,
  observability, performance section, a draft of the AI Collaboration Narrative.
- SPEC (revision entry for the changes made during implementation, Definition of Done ticked
  only where evidence exists) and final status in the System Design Document.

**Not done by the AI (open):** `docker compose up --build` on a clean machine, the k6 run and its
numbers, my review of the narrative and of the "What I corrected and why" entries, the video.

**Verification:** on my machine: `dotnet test --logger "trx;LogFilePrefix=final"` (warnings as
errors via `Directory.Build.props`): build succeeded (8.6 s); total 207, failed 0, succeeded 207,
skipped 0 (7.3 s), up from 199; the 8 new API tests were green on the first run.

## Note on commit history

SPEC §13 asks for one commit per step named `step N: <summary>`. Steps 0–4 follow it. Steps 5–11 are
one commit (`steps 5-11: ...`), squashed at my request: step 6 needed a follow-up fix (port clash),
steps 7–9 share store interfaces and test fakes so separate commits did not each build on their own,
and steps 10–11 plus the removal of the custom metrics and OpenTelemetry changed the same files again.

## Change: custom metrics, span and OpenTelemetry removed

**Prompt summary:** after reviewing what the observability code was for, I checked the Keyloop brief:
it asks for an observability *strategy* and for observability to be considered, with no specific
metrics or latency numbers. I decided to remove the custom instrumentation and keep the design simpler.

**What the AI changed:**
- Removed `SchedulerInstrumentation` (`scheduler.booking.requests`, `scheduler.booking.duration`,
  `scheduler.concurrency.conflicts`, `AvailabilityEngine.Allocate` span), its calls in
  `BookAppointmentHandler`, `SchedulerTelemetry`, and their registration with OpenTelemetry.
- Removed `BookAppointmentInstrumentationTests` (5 tests of the removed metrics) and the xUnit
  collection that only existed to isolate the shared meter. No other test changed.
- Kept: Serilog JSON logs with correlation id, OpenTelemetry built-in ASP.NET Core and Npgsql
  metrics and traces, health endpoints.
- SPEC (§10, revision entry), SDD §8/§9 and README updated. While doing so the AI found that SPEC §10 and SDD §8
  described log fields (`dealershipId`, `appointmentId`, booking outcomes) that the code never wrote;
  the documents now describe what the code does.

Follow-up decision: I also removed OpenTelemetry. With no exporter configured by default it did
nothing visible in a `docker compose up` run, and its four packages had needed security pins. Removed
the packages, `ObservabilityExtensions`, the `Telemetry` setting, the OTLP comment in compose and the
`Activity` tag in `CorrelationIdMiddleware`. The SDD now describes OpenTelemetry as the production path,
and request logs (status, elapsed time, correlation id) as the implemented signal.

**Verification:** _(pending: build and test run; expected 202 tests = 207 − 5)_
