# Unified Service Scheduler

Backend API that replaces manual dealership service booking. A booking is confirmed only when a
**service bay of the right type** and a **technician with the right qualification** are both free for
the entire service window, and **no bay, technician or vehicle can ever be double-booked**, even under
concurrent requests and with several API instances.

Keyloop Technical Assessment, Scenario A. Implemented layer: **backend** (REST API + PostgreSQL); the
client is stubbed with OpenAPI / Swagger UI, an `.http` file and the cURL examples below.

| Document | Content |
|---|---|
| [`docs/system-design.md`](docs/system-design.md) | System Design Document: architecture, components, data flow, technology choices, observability, assumptions, GenAI in design |
| [`SPEC.md`](SPEC.md) | Specification: business rules, functional requirements, acceptance criteria (AC), API contract, error catalog, NFRs, traceability |
| [`docs/ai-collaboration-log.md`](docs/ai-collaboration-log.md) | Step-by-step record of how the AI was directed, what failed and how it was fixed |
| [`AGENTS.md`](AGENTS.md) | Rules for AI agents working in this repository |

## Contents
1. [Features](#features)
2. [Quick start](#quick-start)
3. [Build and test](#build-and-test)
4. [API and cURL examples](#api-and-curl-examples)
5. [How double-booking is prevented](#how-double-booking-is-prevented)
6. [Observability](#observability)
7. [Performance](#performance)
8. [Project structure](#project-structure)
9. [Assumptions and known limitations](#assumptions-and-known-limitations)
10. [AI Collaboration Narrative](#ai-collaboration-narrative)

## Features

| Requirement | Endpoint |
|---|---|
| Book an appointment for a vehicle, service type, dealership and start time; bay and technician assigned automatically (Keyloop requirements 1–3) | `POST /api/v1/appointments` |
| Alternative start times when the slot is taken (up to 3, same day) | in the `409 SLOT_UNAVAILABLE` body |
| Retrieve an appointment | `GET /api/v1/appointments/{id}` |
| Cancel an appointment, freeing its bay and technician | `POST /api/v1/appointments/{id}/cancel` |
| Safe retries with `Idempotency-Key` | header on `POST /api/v1/appointments` |
| Open start times for a day | `GET /api/v1/availability` |
| Reference data | `GET /api/v1/dealerships`, `/service-types`, `/customers/{id}/vehicles` |
| Health | `GET /health/live`, `GET /health/ready` |

Booking rules (SPEC §5): windows are half-open `[start, end)`; the whole service must fit within
opening hours in the dealership's time zone; start 60 minutes to 60 days ahead, on a 15-minute boundary,
with an explicit UTC offset; the vehicle must belong to the customer and must not have an overlapping
appointment at any dealership.

## Quick start

Prerequisites: Docker Desktop.

```bash
docker compose up --build
```

- Swagger UI: http://localhost:8080/swagger
- OpenAPI document: http://localhost:8080/openapi/v1.json
- Health: http://localhost:8080/health/ready

The schema and seed data (two dealerships, bays, technicians, four service types, customers, vehicles)
are applied automatically on start-up. Seed ids are in
[`SeedIds.cs`](src/Scheduler.Infrastructure/Persistence/SeedIds.cs). Reset the database with
`docker compose down -v`.

Run the API from source against the compose database (published on host port **5433**, so a locally
installed PostgreSQL on 5432 does not interfere):

```bash
docker compose up -d postgres
dotnet run --project src/Scheduler.Api --launch-profile http   # http://localhost:5080/swagger
```

## Build and test

Prerequisites: .NET 10 SDK, Docker Desktop (integration tests start a disposable PostgreSQL 16 container).

```bash
dotnet build -warnaserror
dotnet test --logger "trx;LogFilePrefix=run"
```

| Test project | What it covers |
|---|---|
| `Scheduler.UnitTests` | Domain rules (`TimeSlot`, `BookingPolicy`, `AvailabilityEngine`, `Appointment`), use cases with in-memory stores (error order, retries, idempotency, metrics), validator, AutoMapper configuration, error catalog |
| `Scheduler.IntegrationTests` | Real HTTP (`WebApplicationFactory`) against real PostgreSQL (Testcontainers): every AC in SPEC §7, exclusion constraints, migrations, EF mapping, concurrency |

Every test carries its acceptance criterion: `[Trait("AC", "AC-01.3")]`. Useful filters:

```bash
dotnet test --filter "AC=AC-02.1"                                # one acceptance criterion
dotnet test --filter "Category=Concurrency|Category=Database"    # concurrency and database tests
```

The SPEC §12 traceability matrix maps every requirement to its tests.

## API and cURL examples

Errors are RFC 9457 ProblemDetails with a stable `code` (catalog in SPEC §8) and the request's
`correlationId`. Times in responses carry the dealership's UTC offset.

```bash
BASE=http://localhost:8080/api/v1
D1=d1000000-0000-0000-0000-000000000001
OIL=5e000000-0000-0000-0000-000000000001
AN=c0000000-0000-0000-0000-000000000001
ACCORD=ae000000-0000-0000-0000-000000000001
DAY=2026-12-22          # change to any Monday–Saturday 1–60 days ahead if this date has passed

# Open start times for an oil change
curl -s "$BASE/availability?dealershipId=$D1&serviceTypeId=$OIL&date=$DAY"

# Book (201 Created, Location header); repeat with the same key → same appointment, Idempotency-Replayed: true
curl -si -X POST "$BASE/appointments" \
  -H "Content-Type: application/json" -H "Idempotency-Key: demo-001" \
  -d "{\"customerId\":\"$AN\",\"vehicleId\":\"$ACCORD\",\"dealershipId\":\"$D1\",\"serviceTypeId\":\"$OIL\",\"startTime\":\"${DAY}T09:00:00+07:00\",\"notes\":\"Strange noise\"}"

# Read it (use the id from the response)
curl -s "$BASE/appointments/<id>"

# Same vehicle, overlapping window → 409 VEHICLE_ALREADY_BOOKED
curl -s -X POST "$BASE/appointments" -H "Content-Type: application/json" \
  -d "{\"customerId\":\"$AN\",\"vehicleId\":\"$ACCORD\",\"dealershipId\":\"$D1\",\"serviceTypeId\":\"$OIL\",\"startTime\":\"${DAY}T09:30:00+07:00\"}"

# Cancel (200), cancel again (409 APPOINTMENT_NOT_CANCELLABLE)
curl -s -X POST "$BASE/appointments/<id>/cancel"

# Reference data
curl -s "$BASE/dealerships"
curl -s "$BASE/service-types"
curl -s "$BASE/customers/$AN/vehicles"
```

On Windows PowerShell use `curl.exe` instead of `curl`. The same requests, plus error cases, are in
[`http/scheduler.http`](http/scheduler.http) (Visual Studio, Rider, VS Code REST Client).

## How double-booking is prevented

1. **Database guarantee.** Three PostgreSQL exclusion constraints reject any two `Confirmed`
   appointments that overlap on the same bay, technician or vehicle:
   `EXCLUDE USING gist (service_bay_id WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&) WHERE (status = 'Confirmed')`.
   This holds whatever the application does and however many API instances run.
2. **Serialisation per dealership-day.** Snapshot, allocation and insert run in one transaction under
   `pg_advisory_xact_lock(dealership, local day)`, so concurrent bookings for the same dealership and day
   queue instead of racing (no deadlocks, every request sees the latest state).
3. **Mapping, never 500.** Constraint violations and deadlocks are mapped by constraint name to 409
   answers, with a retry for bay/technician conflicts.

Proven by tests: 10 parallel transactions on one bay → exactly 1 commit; 10 parallel HTTP bookings for
1 bay → 1×201, 9×409; for 2 bays → exactly 2×201; 5 parallel bookings of one vehicle → 1×201,
4×VEHICLE_ALREADY_BOOKED. Details and the alternatives considered: SDD §6.

## Observability

| Signal | Implementation |
|---|---|
| Logs | Serilog JSON to stdout; every entry has `correlationId` (from `X-Correlation-Id` or generated, echoed in the response and in error bodies); no customer PII |
| Request timing | One request log line per HTTP request with method, path, status code and elapsed time, from which latency and error rates can be derived |
| Health | `/health/live` (process), `/health/ready` (database) |

No metrics or tracing SDK is included, to keep the solution small; the production path (OpenTelemetry
with the built-in ASP.NET Core and Npgsql instrumentation) is described in SDD §8.

## Performance

Targets (SPEC §10): booking p95 < 500 ms, availability p95 < 300 ms. Load script with thresholds on
both: [`perf/booking.k6.js`](perf/booking.k6.js) (20 availability + 10 booking requests per second for
30 s against the seed data).

```bash
docker compose up -d --build
k6 run perf/booking.k6.js
# or without installing k6:
docker run --rm -i -e BASE_URL=http://host.docker.internal:8080 grafana/k6 run - < perf/booking.k6.js
docker compose down -v      # the run creates real appointments
```

| Run | Booking p95 | Availability p95 | Result |
|---|---|---|---|
| _not yet run_ | | | |

The SPEC sizing (50 bays, 100 technicians, 10k appointments) is larger than the seed data; the script
measures the seed dataset.

## Project structure

```
src/
  Scheduler.Domain/          Entities, TimeSlot, BookingPolicy, AvailabilityEngine, error catalog (BCL only)
  Scheduler.Application/     Use cases, validator, ports, DTOs, metrics/tracing (BCL only)
  Scheduler.Infrastructure/  EF Core, SQL migrations + seed, migrator, stores (constraint mapping, lock)
  Scheduler.Api/             Controllers, AutoMapper profiles, ProblemDetails, middleware
tests/
  Scheduler.UnitTests/
  Scheduler.IntegrationTests/
docs/  http/  perf/
```

Dependencies point inwards: Api → Application → Domain ← Infrastructure.

## Assumptions and known limitations

The brief leaves several rules open; the assumptions taken are listed with their impact in SPEC §15 and
SDD §11 (instant confirmation, no technician shifts, no buffer between jobs, automatic assignment, no
authentication, same hours every open day). Known limitations:

- **No authentication** (A-07): any caller can read or cancel an appointment by id.
- **AutoMapper 16 runs without a license key** (OQ-15): it works and logs a licence warning; production
  use needs a key (free Community licence below USD 5M revenue).
- Bookings for the same dealership and day are serialised; throughput per dealership-day is bounded,
  which is acceptable for workshop volumes.

## AI Collaboration Narrative

<!-- DRAFT prepared from docs/ai-collaboration-log.md. Review and adjust wording before submission. -->

**Strategy: specification first, AI under explicit rules.** Before any code, I worked with an AI
assistant (Claude) on a specification: business rules, functional requirements and Given/When/Then
acceptance criteria, each with an ID, plus an API contract, error catalog and an implementation plan.
The AI first reviewed my own draft and found real gaps (contradictory status, constraint errors not
distinguished per resource, missing idempotency cases, tests that depended on the run date). The final
spec ends with instructions for the AI: present the files and assumptions of each step and wait for my
approval, write tests from the acceptance criteria, never weaken a test to make it pass, and never claim
a result without showing its output. `AGENTS.md` repeats these rules.

**Directing, not accepting.** I changed proposals where they did not fit how I want to own the code:
Controllers instead of Minimal APIs, AutoMapper instead of hand-written mapping (with guard rails), and I
challenged the concurrency design (why not a version column checked in the API?) until the reasoning was
clear. When the AI started generating code before the spec was committed, I stopped it and kept the first
commit specification-only.

**Verification loop.** The AI's environment had no .NET SDK, so every build and test ran on my machine and
the real output went back to the AI. Three gates did most of the work:
- *Warnings as errors with a NuGet vulnerability audit* caught four vulnerable packages the AI had chosen
  from memory, including AutoMapper 14 (which reopened a design decision I then made explicitly).
- *Acceptance-criteria tests against real PostgreSQL* (Testcontainers, no in-memory fakes for the
  database), each tagged with its AC id.
- *Concurrency tests over HTTP*, which failed twice: first with 500s (EF wraps deadlocks in a different
  exception type), then with wrong answers (inserts deadlocking until retries ran out). The AI diagnosed
  both from server logs captured in the test result files; the second led to a design change I approved
  (per dealership-day advisory lock). The only test changed afterwards was one whose expectation was
  wrong about PostgreSQL itself, and that change was explained and approved before it was made.

**Ensuring final quality.** Every acceptance criterion in SPEC §7 has at least one automated test and the
traceability matrix names them; the build treats warnings and vulnerable dependencies as errors; the
domain and application layers have no third-party dependencies; and the log records every failed run, its
cause and the fix, so the history can be checked rather than taken on trust.
