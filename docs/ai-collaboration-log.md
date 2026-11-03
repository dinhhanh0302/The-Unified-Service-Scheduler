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
