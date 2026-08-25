# Verification Report — Patient Management App

> **This gate is being introduced retroactively.** Steps 1–11 have **no entries** in this file and were
> never independently verified by `verification-brd`; their test claims in `docs/implementation-progress.md`
> are the implementer's own, unconfirmed by this gate. Verification starts at Step 12. Nothing is
> backfilled — an absent row means "not verified", not "verified clean".

| Step | Plan Phase | Suites Run | Total | Passed | Failed | Skipped (flagged) | Verdict | Commit Verified | Notes |
|---|---|---|---|---|---|---|---|---|---|
| 12 | Appointment Management — `Modules/04-appointment-management.md` full build checklist | `dotnet test` (82), `ng test` ChromeHeadless (54), `ng build`, EF migration smoke test (inside `dotnet test`), live API end-to-end vs. real LocalDB | 136 | 136 | 0 | 0 | **PASS** | `9f7e9c0` | All four fixed-spec Appointment hard gates re-verified independently, in code, in tests, and live over HTTP. Implementer's counts reproduced exactly (47 Infrastructure + 35 Api = 82; 54 frontend). Zero skipped/disabled/focused tests repo-wide. Six non-blocking findings below — two (F-1, F-2) concern the *semantics* of the double-booking rule and need a spec decision before Module 5, but neither contradicts what this module locked. |

---

## Step 12 — verification detail

**Verdict: PASS.** Every locked rule in `Modules/04-appointment-management.md` is implemented, asserted by a
test that actually checks the requirement, and independently reproduced live against real SQL Server LocalDB.

### Suites executed (full repo, not diff-only)

| Suite | Result |
|---|---|
| `dotnet test` → `PatientManagement.Infrastructure.Tests` | 47 passed, 0 failed, 0 skipped |
| `dotnet test` → `PatientManagement.Api.Tests` | 35 passed, 0 failed, 0 skipped |
| `ng test --watch=false --browsers=ChromeHeadless` | `TOTAL: 54 SUCCESS`, 0 failed |
| `ng build` | succeeded — initial total **656.80 kB** (within the 700 kB budget); only 4 pre-existing Bootstrap CSS selector warnings, unrelated to this step |
| EF migration smoke (`MigrationSmokeTests.All_migrations_apply_cleanly_to_a_fresh_database`) | passed (inside the 47) |
| Live API (`dotnet run`, throwaway port 5199, real `(localdb)\MSSQLLocalDB`) | all probes matched expected |

Skip/disable audit: `grep` for `Skip =`, `[Ignore]`, `xit(`, `xdescribe(`, `fit(`, `fdescribe(`, `.skip(`,
`.only(` across `src/backend` and `src/frontend/src` returned **zero matches**. No empty test bodies or
commented-out assertions found in the Step 12 test files.

### Hard-gate re-verification (fixed spec, `implementation-brd.md`)

| Gate | Verdict | Evidence |
|---|---|---|
| **Slot duration is doctor-entered per appointment, never a fixed/system default** | PASS | `CreateAppointmentRequest.DurationMinutes` is `int?` + `[Required]` + `[Range(1,…)]` — deliberately nullable so an omitted value 400s instead of binding silently to `0`. No default anywhere in `AppointmentService.cs`, `Appointment.cs`, or either Angular form (`appointment-form.component.ts` and `walk-in-registration.component.ts` both initialise `durationMinutes: ['']`). Live: `durationMinutes: 45` → `201` returning `45`; `7` → `201` returning `7`; omitted → `400 {"DurationMinutes":["The DurationMinutes field is required."]}` with nothing written. Asserted by `AppointmentServiceTests.cs:30`, `AppointmentsControllerTests.cs:38` and `:54`, and `appointment-form.component.spec.ts:34`. |
| **Walk-ins supported; daily list is one merged, time-ordered list, not two feeds** | PASS | There is no `IsWalkIn` discriminator — walk-ins are ordinary `Appointment` rows, so `GetDailyAsync` is a single `OrderBy(a => a.ScheduledTime)` query and the merge is structural, not assembled. Live daily list for 2026-08-25 returned five rows in strict time order interleaving three walk-ins (`status: "Completed"`, `visitId` populated) with a scheduled 14:00 row (`status: "Scheduled"`, `visitId: null`) — one list, one table. UI renders one `<tbody>`. Asserted by `AppointmentServiceTests.cs:120`, `AppointmentsControllerTests.cs:137`, `daily-schedule.component.spec.ts:61`. |
| **Walk-in creates exactly one `Appointment` + one linked `Visit` in a single flow; `Visit.AppointmentId` non-nullable** | PASS | `WalkInService.CreateWalkInVisitAsync` inserts both inside one `BeginTransactionAsync`. `Visit.AppointmentId` is `int` (non-nullable) with `nullable: false` in migration `20260820170110_AddAppointmentsAndVisits.cs:43` and a **unique** index (`:82–85`) enforcing 1:1. Live: one `POST /api/appointments/walk-in` → `201 {"visitId":3,"appointmentId":7,"patientId":2,"visitNumber":1}`; a second for the same patient → `visitNumber: 2` (per-patient, not global). No pre-booking call needed — `walk-in-registration.component.spec.ts:47` proves the single request via `httpMock.verify()`. |
| **Double booking rejected outright (uniformly, both paths)** | PASS *(with F-1/F-2 on semantics)* | One shared `AppointmentSlotGuard.EnsureSlotIsFreeAsync` is called by **both** `AppointmentService.CreateAsync:23` and `WalkInService:17`, so the rule cannot drift between paths; a unique index on `Appointment.ScheduledTime` (migration `:76–79`) is the DB backstop. Rejection is outright — `409 Conflict`, nothing inserted — not a warning. Live: duplicate 09:00 → `409`, daily list still one row. Asserted by `AppointmentServiceTests.cs:94`, `AppointmentsControllerTests.cs:74` (scheduled) and `:113` (walk-in, clock-pinned), `WalkInServiceTests.Rejects_a_second_appointment_at_an_already_occupied_instant` (real LocalDB). **See F-1/F-2: "occupied" means the identical instant, not an overlapping window.** |

### Other fixed-spec gates — regression spot-check (full suite run)

No regressions. Authentication: `SessionTokenGateTests` (4), `AuthControllerTests` (7, incl. `No_registration_endpoint_exists` / `No_password_reset_endpoint_exists`), `AesPasswordCryptoTests` (4, reversible encryption), `DoctorAccountSeederTests` (2), `UsersSchemaTests` (exactly `Id`/`Username`/`Password`) all pass; live `GET /api/appointments/daily` without a token returned `401`, confirming the global `FallbackPolicy` still gates new controllers automatically. Patient/Vitals: `PatientIdentitySeedTests` (id-from-zero, age/DOB independent, shared phone allowed), `PatientServiceTests`/`PatientsControllerTests` (incl. `No_delete_endpoint_exists`) pass. Search: substring-anywhere on both name and phone, case-insensitive, empty-criteria-returns-nothing — all four assertions pass. Prescription snapshotting: `PrescriptionSnapshotTests` (2) pass.

### Flagged-assumption audit — `Completed` cannot be set by hand

The task required confirming this was **documented, not silently invented**, and **internally consistent**. Both confirmed.

*Documented in six places*, consistently: `Modules/04-appointment-management.md:45`; `docs/implementation-progress.md`
Step 12 Notes (with exact rollback instructions); the `9f7e9c0` commit-message body; a code comment at
`PatientManagement.Infrastructure/Services/AppointmentService.cs:74–80`; XML doc on
`IAppointmentService.UpdateStatusAsync`; and the `MANUALLY_SETTABLE_STATUSES` doc block in
`src/frontend/src/app/features/appointments/appointment.service.ts`.

*Enforced consistently* across every layer — service guard (`AppointmentService.cs:81`) → `400` via
`AppointmentsController.cs:52` → backend tests (`AppointmentServiceTests.cs:205`, `AppointmentsControllerTests.cs:183`)
→ Angular constant excludes `'Completed'` → template renders a read-only badge instead of a `<select>` for
Completed rows (`daily-schedule.component.html`), asserted by `daily-schedule.component.spec.ts:72`.
Live: `PUT .../status {"status":"Completed"}` → `400` with the explanatory detail, row still `Scheduled`;
`Cancelled` → `200`; `NoShow` → `200`. The `[Theory]` at `AppointmentServiceTests.cs:182` covers all three
permitted targets. **No inconsistency found.** The rollback path described in the tracker is accurate.

### Findings — none blocking, all recorded for the owning agent

**F-1 — Double-booking is exact-instant equality, not overlap detection. Severity: High. Owner: spec decision (BRD/`implementation-brd.md`), then `implementation-brd` for Module 5.**
`AppointmentSlotGuard` tests `a.ScheduledTime == scheduledTime` only. `DurationMinutes` is captured and
persisted faithfully but is **never consulted** when deciding whether a slot is occupied.
Live-proven on a clean day: `09:00` for **60 minutes** → `201`; then `09:30` for 30 minutes → `201` (accepted,
squarely inside the first appointment's window); `09:00:01` → `201`.
Expected under the fixed-spec gate's wording ("a date/time already **occupied** by an existing appointment"):
09:30 is occupied. Actual: accepted.
**Why this is not a FAIL:** `Modules/04-appointment-management.md:19` — the authoritative spec for this step —
explicitly locks the mechanism as "a unique index on `Appointment.ScheduledTime` plus a service-layer
pre-check", which is exactly what was built. The implementation matches its written spec; the *spec* is
narrower than the hard gate's prose. This needs an explicit decision (exact-instant vs. overlap-aware)
recorded in `implementation-brd.md` before Module 5 builds consultation flows on top of it.

**F-2 — The walk-in path's double-booking protection is practically inert in production. Severity: Medium. Owner: same decision as F-1.**
`WalkInService.cs:13` stamps `ScheduledTime` from `TimeProvider.GetLocalNow().LocalDateTime` at full
sub-second precision — live-observed values include `2026-08-25T12:10:08.4177363`. Exact-instant equality
against a scheduled appointment at a round time (`09:00:00`) can essentially never match, so on the walk-in
path the guard fires only when the clock is pinned — which is precisely what both covering tests do
(`AppointmentsControllerTests.cs:116`, `WalkInServiceTests`). The rule is genuinely *uniform* and cannot be
bypassed, so the gate's "applies uniformly regardless of which path" clause holds; but the real-world
protection it delivers on that path is near zero. Same root cause as F-1 and fixed by the same decision.

**F-3 — `POST /api/appointments/walk-in` with an unknown `patientId` returns `500`. Severity: Medium. Owner: `implementation-brd`, next step touching `WalkInService`.**
Live-confirmed: `patientId: 99999` → `500` (FK violation surfacing raw). The scheduled path handles this
correctly (`404`, verified live). Disclosed honestly in the Step 12 Notes as a known gap, with the reason
(`IWalkInService`'s `Task<Visit>` non-nullable signature predates this step) — it is outside this module's
build checklist, so it does not fail Step 12, but it is an unhandled server error on a reachable path and
should not survive Module 5.

**F-4 — `UpdateStatusAsync` evaluates the `Completed` guard before checking the appointment exists. Severity: Low.**
`AppointmentService.cs:81` throws before the `FirstOrDefaultAsync` at `:88`. Live: `PUT /api/appointments/4242/status`
(nonexistent) with `Completed` → `400`; the same unknown id with `Cancelled` → `404`. Cosmetic
status-code ordering, no data impact; noted only so it isn't mistaken for a bug later.

**F-5 — One backend test is timing-dependent near midnight. Severity: Low. Owner: `implementation-brd`.**
`PatientManagement.Api.Tests/AppointmentsControllerTests.cs:137–156`
(`Daily_list_returns_scheduled_and_walk_in_entries_together_ordered_by_time`) derives slots from
`DateTime.Now` via `today.Date.AddHours(1)` / `.AddHours(23)` and issues a real-clock walk-in. Executed
between 23:00 and midnight the walk-in would collide with the 23:00 booking and return `409`, failing the
test. Deterministic in this run; consider pinning the clock as the neighbouring test at `:113` already does.

**F-6 — Process: `docs/` and `Modules/` are gitignored, so gate reports are untracked. Severity: Low. Owner: user.**
`.gitignore` lists `.claude/agents/`, `BRD/`, `docs/`, `Modules/`. This report had to be committed with
`git add -f`. Already flagged by the implementer at `docs/implementation-progress.md:11`. Worth reconciling —
otherwise verification/code-review/gap reports live outside version control while the gates they guard do not.

**F-7 — Process: the checkout was on `main`, not `impl/appointment-management`, at the start of this run. Severity: Low (informational).**
The verification briefing stated the primary checkout was already on the implementation branch; it was on
`main` at `125f74d`. The working tree was clean, so `git checkout impl/appointment-management` (tip
`9f7e9c0`) was performed with no loss of state, and all results above are from that branch. Recorded so the
discrepancy isn't silently absorbed. Because `docs/` is untracked (F-6), the progress tracker and module
files are shared across branches rather than branch-scoped.

### What happens next

**Proceed.** Step 12 is verified and may be treated as Done. Before Module 5 (Consultation Workflow) builds
on this foundation, F-1 needs an explicit recorded decision (it changes what "double booked" means for every
later step) and F-3 should be closed by whichever step next reshapes `IWalkInService`. Neither blocks
landing Step 12 itself.
