# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A single-physician Patient Management Application. The repository holds the Business Requirements Document (`BRD/Doc_BRD.md`), a pipeline of specialized subagents (`.claude/agents/`) plus their review artifacts (`docs/`) that carry the project from requirements through implementation, verification, code review, and landing, and — as of `docs/implementation-progress.md` Steps 1–6 — a working Angular + .NET Web API + EF Core scaffold with the full Phase 1 data model (Users, DoctorDetails, Patient, Appointment/Visit, Prescription) under `src/`. No HTTP controllers/UI exist yet — everything built so far is schema and service-layer only; check `docs/implementation-progress.md` for the current step before assuming a feature area is done.

## Source of truth — and where it's actually kept

`BRD/Doc_BRD.md` is the nominal requirements document, but its text lags behind the real, accepted decisions the project has since made. Those decisions live in two agent-config files that override a literal reading of the BRD:

- `.claude/agents/implementation-brd.md` — the fixed tech stack and every locked feature spec (data-model field lists, business rules, interpretations of vague BRD language) a developer must build to.
- `.claude/agents/verification-brd.md` — the exact hard gates any implementation must satisfy to be considered correct.

Before asserting "what the requirements say," check both of these files, not just the BRD text. `docs/worktree-brd-review.md` tracks this exact gap (decided-but-not-written-back-into-the-BRD) as its own finding category — read its Executive Summary for the current state of what's settled vs. still genuinely open.

## Fixed technology stack

Locked in `.claude/agents/implementation-brd.md`; do not deviate without being told to:
- **Frontend:** Angular (latest stable), standalone components, reactive forms.
- **Backend:** .NET Web API (C#) — thin controllers, business logic in services, DTOs kept separate from EF entities.
- **Database:** SQL Server, managed/inspected via SSMS. Schema changes only ever go through EF Core migrations.
- **ORM:** Entity Framework Core, code-first — every entity change ships with its migration in the same commit.
- **Deployment:** local-only, permanently, on the doctor's own machine (Kestrel on `localhost`, no reverse proxy) — no hosting, no CI/CD, no staging/production split. Don't build for infrastructure that doesn't exist.

## The agent pipeline

The project is organized around a sequence of purpose-built subagents in `.claude/agents/`, each owning one phase and one report file, meant to run in order. Their file-ownership is deliberately partitioned — each writes only its own report — so two agents never race on the same file, and most isolate their work in a dedicated git worktree via `EnterWorktree`/`ExitWorktree` rather than touching `main` directly.

1. `brainstorm` — read-only ideation against the BRD.
2. `plan-brd` — read-only implementation planning.
3. `worktree-brd` — BRD edits and the four-analysis quality review (contradiction, healthcare-domain-completeness, architecture-completeness, measurability). Owns `docs/worktree-brd-review.md`.
4. `implementation-brd` — writes the actual Angular/.NET/EF Core code and tests, one plan step at a time. Owns `docs/implementation-progress.md`.
5. `verification-brd` — independent PASS/FAIL test-execution gate. Owns `docs/verification-report.md`.
6. `codereview-brd` — code quality/correctness/consistency review; APPROVED/CHANGES REQUESTED gate. Owns `docs/codereview-report.md`.
7. `gapanalysis` — scores the built implementation against the BRD's original requirements; GO requires ≥95% coverage and no unresolved Critical miss, otherwise LOOP BACK to the agent that owns the gap. Owns `docs/gap-analysis-report.md`.
8. `finishing-brd` — checks the three gates above, then presents merge / create-PR / clean-up-worktree options. Takes no git action without a fresh, explicit per-action confirmation — a prior "yes" never authorizes the next action.

All review-writing agents share the same finding format (Severity: Critical/High/Medium/Low, plus a fixed set of required fields per finding) and the same commit-message convention shape, scoped per agent (`brd(...)`, `impl(...)`, `verify(...)`, `codereview(...)`, `gap(...)`).

## The `docs/*-brd-review.md` files

Several `docs/*-brd-review.md` documents exist as **baseline snapshots** — produced by asking each pipeline agent what it would find given the repo state at the time they were written (most were written pre-build). They are not the same as the real gate-report files those agents own during actual operation (`docs/verification-report.md`, `docs/codereview-report.md`, `docs/gap-analysis-report.md`). `docs/implementation-progress.md` now exists and is current (Steps 1–6 done), but the other three gate-report files still don't — `verification-brd`, `codereview-brd`, and `gapanalysis` haven't been run against the implemented steps yet. Don't treat a `*-brd-review.md` file's existence as evidence its corresponding gate has run against real code.

## Commands

Run from `src/backend/` unless noted:
- `dotnet build` — build the solution (`PatientManagement.Api` + `PatientManagement.Api.Tests`)
- `dotnet test` — run the full xUnit suite (currently 16 tests; several require a real `(localdb)\MSSQLLocalDB` connection, not just EF Core InMemory — see `docs/implementation-progress.md` Step 2 for where the dev connection string/encryption key live)
- `dotnet ef migrations add <Name>` / `dotnet ef database update` — requires the `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef`) on `PATH`

Run from `src/frontend/`:
- `ng build` / `ng test` (Angular CLI 19 — install pinned via `npx @angular/cli@19 <command>` rather than a global `ng` if one isn't already on `PATH`)

## Global Rules

- Never commit directly to main.
- Prefer git worktrees for modifications.
- One logical change per commit.
- Follow report ownership strictly.
- Do not bypass verification, code review, or gap analysis gates.

## Frontend–Backend Integration

The application consists of two connected projects:

- src/frontend/ (Angular 19)
- src/backend/ (.NET 10 Web API)

Rules:

- Angular must consume .NET Web API endpoints.
- Angular components must use Angular services for API calls.
- Direct database access from Angular is prohibited.
- Mock data should not be used unless explicitly requested.
- Backend must configure CORS for Angular.
- API URLs must be configured through Angular environment files.
- A feature is only considered complete when:
  Database
  → EF Entity
  → Service
  → Controller
  → Angular Service
  → Angular Component
  → Bootstrap UI
  all work together.