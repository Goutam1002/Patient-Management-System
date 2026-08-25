# Code Review Report — Patient Management App

> **This gate is being introduced retroactively.** Steps 1–11 have **no entries** in this file and were
> never reviewed by `codereview-brd`. Nothing is backfilled — an absent row means "not reviewed", not
> "reviewed clean". Code review starts at Step 12. Pre-existing defects in earlier steps are only
> reported here when Step 12's code depends on, exposes, or extends them.

| Step | Plan Phase | Verdict | Critical | High | Medium | Low | Commit Reviewed | Notes |
|---|---|---|---|---|---|---|---|---|
| 12 | Appointment Management — `Modules/04-appointment-management.md` full build checklist | **CHANGES REQUESTED** | 0 | 4 | 4 | 7 | `9f7e9c0` | Four High findings: two unvalidated request DTOs that let the *wrong patient* and *zero-valued vitals* be written silently, a status guard enforced in only one direction, and the overlap-blind double-booking rule. Verification's PASS stands — every one of these is outside what the existing tests exercise. See below. |
| 13 | Consultation Workflow — `Modules/05-consultation-workflow.md` full build checklist | **APPROVED** | 0 | 0 | 1 | 1 | `f9aceb9` | Vitals mandatory-at-entry is enforced correctly server-side (unlike Step 12's walk-in CR-2), and the post-creation edit boundary is a clean "no property on the DTO" proof. One new Medium (TOCTOU race on the visit-uniqueness check, same unaddressed shape as Step 12's CR-6) and one Low (own verdict on the CR-1 connection verification flagged). See below. |
| 14 | Prescription / Medication — `Modules/06-prescription-medication.md` full build checklist | **APPROVED** | 0 | 0 | 1 | 0 | `688802e` | Immutability is proven by absence (no PUT/PATCH/DELETE action exists), snapshot isolation and free-text autocomplete both hold. One new Medium, concurring with and extending verification's F-8: the snapshotted `Logo`/`Signature` bytes reach the Angular `Prescription` interface but the printable template never renders them, despite the fixed spec tying those exact fields to this exact artifact. See below. |

---

## Step 12 — code review detail

**Verdict: CHANGES REQUESTED.** Do not mark Step 12 finished, do not proceed to Module 5
(Consultation Workflow), and do not merge `impl/appointment-management` until CR-1 through CR-4 are
addressed and re-reviewed.

Reviewed at commit `9f7e9c0` on branch `impl/appointment-management` (confirmed via
`git branch --show-current` — the branch had *not* drifted this time). `docs/verification-report.md`
(`c4b5871`, PASS, 136/136) was read in full first; findings that restate its F-1…F-5 are marked as
concurrences and attributed.

The code is, on the whole, careful and unusually well-commented — the shared `AppointmentSlotGuard`,
the deliberately-nullable `DurationMinutes`, the half-open date range in `GetDailyAsync`, the absence
of an `IsWalkIn` discriminator, and the six-place documentation of the `Completed` decision are all
good engineering. The findings below are concentrated in one blind spot: **input validation on the
request DTOs**, where the reasoning applied so carefully to `DurationMinutes` was not applied to the
fields next to it.

---

## Correctness Review

### CR-1 — `patientId` is unvalidated; an omitted or malformed value silently books patient `0`. Severity: **High**

**Location:** `src/backend/PatientManagement.Application/DTOs/CreateAppointmentRequest.cs:7`
(consumed at `src/backend/PatientManagement.Infrastructure/Services/AppointmentService.cs:13`).

**What's wrong:** `public int PatientId { get; set; }` carries no `[Required]`, no `[Range]`, and is a
non-nullable `int`. A `POST /api/appointments` body that omits `patientId` — or sends `null`, or a
non-numeric value that fails to bind — produces `PatientId = 0`, and `db.Patients.FindAsync(0)` then
*succeeds*, because `implementation-brd.md`'s fixed Patient spec mandates
`.UseIdentityColumn(seed: 0, increment: 1)` — patient id `0` is a real patient, the clinic's first one.
The request returns `201 Created` with an appointment booked against the wrong human being.

The same file, ten lines further down, contains the exact reasoning that should have covered this
field: *"A non-nullable int here would bind an omitted field to 0 silently, which is exactly the
hardcoded-default behaviour the spec forbids."* That reasoning was applied to `DurationMinutes` and
not to `PatientId`, where the consequence is worse — a wrong duration is visible in the daily list, a
wrong patient is not.

No test covers this: `AppointmentServiceTests.Create_for_an_unknown_patient_returns_null` uses an id
that genuinely doesn't exist, and `AppointmentsControllerTests.Endpoints_require_authentication`
actually passes `PatientId = 0` as a valid-looking value (line ~215), which shows the id is
indistinguishable from a real one.

**Why it matters:** silent wrong-patient booking in a clinical system, returned as a success. It is
reachable from the UI too — `appointment-form.component.html:9` is a free-text numeric input, so a
cleared field or a stray keystroke lands on patient `0` rather than erroring.

**Suggested fix:** mirror the `DurationMinutes` treatment exactly —
`[Required] [Range(0, int.MaxValue)] public int? PatientId { get; set; }` — and unwrap with
`request.PatientId!.Value` in `AppointmentService.CreateAsync`. Add an API test asserting that a body
omitting `patientId` returns `400` **and** that no appointment exists for patient `0` afterwards.
Apply the identical change to `WalkInVisitRequest.PatientId` (see CR-2).

---

### CR-2 — The walk-in endpoint has no server-side validation at all; zero-valued vitals and a zero duration are persisted as a `201`. Severity: **High**

**Location:** `src/backend/PatientManagement.Application/DTOs/WalkInVisitRequest.cs:8-17`, newly
exposed over HTTP by `src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs:60-65`
and written unchecked by `src/backend/PatientManagement.Infrastructure/Services/WalkInService.cs:27-51`.

**What's wrong:** `WalkInVisitRequest` is a positional record of non-nullable value types with **zero**
`DataAnnotations` and no guard clause anywhere on the path. A request body of `{"patientId": 1}` binds
every other field to its default and is accepted: a `Visit` row is written with `Temperature = 0.0`,
`BpSystolic = 0`, `BpDiastolic = 0`, `Pulse = 0`, `Weight = 0.000`, and its `Appointment` is written
with `DurationMinutes = 0`. HTTP `201`.

This breaks two fixed specs at once:

1. **Consultation vitals** — *"Vitals (temperature, blood pressure, pulse) are mandatory at data-entry
   time... Enforce with `Validators.required` on the Angular form **and** a non-nullable column plus
   service-layer check on the API side."* The Angular half exists
   (`walk-in-registration.component.ts:19-29`). The non-nullable columns exist (`Visit.cs:18-22`). The
   **service-layer check does not exist** — and non-nullable columns do not reject `0`. This is the
   client-side-validation-only pattern the review brief calls out explicitly. As of Step 12 this is the
   *only* HTTP path in the application that creates a `Visit`, so this is where that gate lives today.
2. **Appointment slot duration** — the Appointment hard gate ("doctor-entered per appointment, never a
   fixed/system default") is enforced with `[Range(1, int.MaxValue)]` on the scheduled path and
   **not at all** on the walk-in path. The same rule now behaves differently depending on which
   endpoint created the row — precisely the drift the shared `AppointmentSlotGuard` was introduced to
   prevent for the neighbouring rule.

Every existing test posts the fully-populated `WalkInPayload` helper
(`AppointmentsControllerTests.cs:229-240`), so nothing exercises a partial body.

**Why it matters:** a permanent clinical record containing a body temperature of 0 °C and a blood
pressure of 0/0, accepted as valid, in a system whose own spec calls these mandatory. Visits are never
deleted, so the bad row persists and will flow into Module 5's read surface and the CSV/PDF exports.

**Suggested fix:** make each field nullable with `[Required]` plus a plausible `[Range]`
(`durationMinutes` ≥ 1, `temperature` ~25–45, `bpSystolic`/`bpDiastolic` ~30–300, `pulse` ~20–250,
`weight` > 0), or — if the positional record shape is worth keeping — add an explicit guard clause at
the top of `CreateWalkInVisitAsync` throwing a named validation exception the controller maps to `400`.
Add API tests for: a body omitting vitals → `400` with nothing written; `durationMinutes: 0` → `400`.
Prefer the DTO-attribute route, since it matches how `CreateAppointmentRequest` and
`CreatePatientRequest` already work.

---

### CR-3 — The `Completed` guard is enforced in only one direction: a completed appointment with a recorded visit can be flipped to `Cancelled`/`NoShow` over HTTP. Severity: **High**

**Location:** `src/backend/PatientManagement.Infrastructure/Services/AppointmentService.cs:72-97`
(guard at `:81`, load at `:88`, unconditional assignment at `:96`).

**What's wrong:** `UpdateStatusAsync` rejects transitions *into* `Completed` but places no constraint on
transitions *out of* it. `PUT /api/appointments/{id}/status` with `{"status":"NoShow"}` against a
walk-in appointment — which `WalkInService.cs:32` creates as `Status = Completed` with a linked `Visit`
already written — succeeds with `200`.

The result is a self-contradictory clinical record: an appointment marked `NoShow` that has a `Visit`
row with vitals, complaints, and a diagnosis attached to it. The daily list will then render that row
as `NoShow` **and** "Visit recorded" simultaneously (`daily-schedule.component.html:52-75`).

The only thing preventing this today is the Angular template: `daily-schedule.component.html:52-54`
renders a read-only `<span class="badge">` instead of a `<select>` for `Completed` rows. That is
client-side-only enforcement of a data-integrity rule — the API is open. Nothing in either test suite
attempts a status change on a `Completed` appointment; `AppointmentServiceTests` line ~182's `[Theory]`
only exercises transitions *from* `Scheduled`.

**Why it matters:** the module's central invariant — "completion happens only as a side effect of
recording a visit" — is only half-implemented. The half that was built (blocking manual `Completed`)
is the cosmetic half; the half that was not (protecting an appointment that already *has* a visit) is
the one that guards data integrity. A single stray request permanently desynchronises the appointment
record from the clinical record, and there is no delete/repair path.

**Suggested fix:** move the existing guard below the `FirstOrDefaultAsync` at `:88` (which also closes
CR-8/verification F-4), then add a second guard: if `appointment.Status == AppointmentStatus.Completed`
— or, more robustly, if a `Visit` exists for `appointment.Id` — throw
`AppointmentStatusTransitionException` with a message naming the recorded visit. Add a service test and
an API test asserting `400` and an unchanged row for `Completed → Cancelled` on a walk-in appointment.

---

### CR-4 — Double booking is exact-instant equality; `DurationMinutes` is captured, validated, persisted, displayed — and never consulted by any decision in the codebase. Severity: **High**

**Location:** `src/backend/PatientManagement.Infrastructure/Services/AppointmentSlotGuard.cs:19`;
DB backstop at `src/backend/PatientManagement.Infrastructure/Data/AppDbContext.cs:55` and migration
`20260820170110_AddAppointmentsAndVisits.cs`.

**What's wrong:** concurs with verification F-1 and F-2 — booking `09:00` for 60 minutes and then
`09:30` for 30 minutes both return `201`, because the guard is `a.ScheduledTime == scheduledTime`.
Two additional observations from reading the code rather than testing it:

- `DurationMinutes` has **no reader anywhere in the solution**. `grep` finds it only in assignment,
  DTO projection, and display. The one decision it could possibly inform is this one. A field that is
  required, range-validated, hard-gated by the fixed spec, and then never read by any logic is a
  strong signal the rule it exists to serve was not finished.
- On the walk-in path the guard is effectively inert in production (verification F-2):
  `WalkInService.cs:13` stamps sub-second local time, so exact-instant equality against a round-numbered
  scheduled slot can essentially never fire. Both covering tests pin the clock, which is what makes them
  pass — the tests are correct, but they document a rule that does not engage in real use.

`Modules/04-appointment-management.md:19` does lock the mechanism as "a unique index on
`Appointment.ScheduledTime` plus a service-layer pre-check", and the implementation matches that
literally — so this is a *faithfully-implemented-but-under-specified rule*, not an implementation
defect. It is rated High because the resulting behaviour still fails the fixed spec's own prose
("a date/time already **occupied** by an existing appointment must be rejected outright") in the plain
clinical reading, and because Module 5 will build consultation flows on top of whatever "occupied"
means.

**Why it matters:** the doctor can be double-booked with two overlapping patients and the system will
report both as fine. The rule the BRD review resolved as Critical (MR-1) is satisfied only for the
exact-collision case.

**Suggested fix — spec decision first, then code:**
1. Record an explicit decision in `.claude/agents/implementation-brd.md`'s "Fixed feature spec:
   Appointment & Visit": exact-instant or overlap-aware. This is `worktree-brd`/user territory, not
   `implementation-brd`'s to invent.
2. If overlap-aware: the guard becomes an interval-intersection check
   (`existing.Start < newEnd && newStart < existing.End`). Note two traps — `DateTime.AddMinutes(column)`
   does not translate to SQL Server, so use `EF.Functions.DateDiffMinute` or a persisted computed
   `EndTime` column; and a unique index on `ScheduledTime` can no longer serve as the DB backstop, so
   the migration needs rethinking rather than deleting.
3. Either way, decide what a walk-in's `ScheduledTime` precision should be — truncating to the minute
   would at least make the current rule capable of firing.

---

### CR-5 — Walk-in with an unknown `patientId` returns `500`. Severity: Medium

**Location:** `src/backend/PatientManagement.Infrastructure/Services/WalkInService.cs:11-51`;
`src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs:60-78`.

Concurs with verification F-3 and with the implementer's own disclosure in the Step 12 tracker row.
Adding one thing the verification report did not: this cannot be caught by the API test suite as
built, because `AuthApiFactory.cs:50-64` uses the EF Core **InMemory** provider, which enforces no
foreign keys — the request that 500s against LocalDB returns `201` under test. So the gap is not just
untested, it is structurally untestable at that layer until the check moves into the service.

**Suggested fix:** fold into CR-2's validation pass — check `db.Patients.FindAsync` at the top of
`CreateWalkInVisitAsync` exactly as `AppointmentService.CreateAsync:13` does, and reshape
`IWalkInService` to return `Task<WalkInVisitDto?>` (see CQ-2) so the controller can `NotFound()`.

### CR-6 — Time-of-check/time-of-use between the slot guard and the insert; visit-number `MAX` read outside the transaction. Severity: Medium

**Location:** `src/backend/PatientManagement.Infrastructure/Services/AppointmentSlotGuard.cs:19` →
`AppointmentService.cs:33-34`; `WalkInService.cs:19-25` (the `MaxAsync` sits *above*
`BeginTransactionAsync` at `:25`).

Two concurrent inserts on the same slot both pass the pre-check; the unique index then rejects the
loser with a `DbUpdateException` that nothing catches, so the caller gets `500` instead of the `409`
the guard exists to produce — the guard's own XML doc claims the index "is the real backstop", but the
backstop's failure mode is unhandled. Separately, the per-patient visit-number `MAX` is read before the
transaction opens, so it is not covered by the transaction's isolation. Low likelihood on a
single-user localhost app (a double-submitted form is the realistic trigger), cheap to close.

**Suggested fix:** wrap the `SaveChangesAsync` in `catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))`
→ `throw new AppointmentSlotConflictException(...)`; move the `MaxAsync` inside the transaction in
`WalkInService`.

### CR-7 — Out-of-range numeric enum values bind and persist. Severity: Medium

**Location:** `src/backend/PatientManagement.Api/Program.cs:18-19`;
`src/backend/PatientManagement.Application/DTOs/UpdateAppointmentStatusRequest.cs:13`.

`new JsonStringEnumConverter()` still accepts integers. `PUT .../status` with `{"status": 99}` binds to
`(AppointmentStatus)99`, passes `[Required]`, misses the `== Completed` guard, and is written to the
column. The Angular `AppointmentStatus` union type then receives a value it cannot render or map to a
badge class (`daily-schedule.component.ts:77-88` falls through to `bg-primary`).

**Suggested fix:** `new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)`, and/or
`[EnumDataType(typeof(AppointmentStatus))]` on the DTO property. Add a test for `{"status": 99}` → `400`.

### CR-8 — `Completed` guard evaluated before the existence check. Severity: Low

**Location:** `src/backend/PatientManagement.Infrastructure/Services/AppointmentService.cs:81` (guard)
vs `:88` (load). Concurs with verification F-4 — a nonexistent id returns `400` for `Completed` and
`404` for anything else. Fixed for free by CR-3's reordering.

### CR-9 — One backend test is timing-dependent near midnight. Severity: Low

**Location:** `src/backend/PatientManagement.Api.Tests/AppointmentsControllerTests.cs:137-156`.
Concurs with verification F-5. Pin the clock as the neighbouring test at `:113` already does.

### CR-10 — The status `<select>` keeps the rejected value after a failed update. Severity: Low

**Location:** `src/frontend/src/app/features/appointments/daily-schedule/daily-schedule.component.ts:70-73`
with `daily-schedule.component.html:56-67`.

On error the component sets `errorMessage` but never restores the row, and `[value]`/`[selected]` are
not re-evaluated because the `appointments` signal did not change — so the dropdown displays the status
the server just refused (e.g. it shows `Cancelled` while the row is still `Scheduled`) until the page
is reloaded. Given CR-3, this is also how a doctor could be shown a status the API never accepted.

**Suggested fix:** in the `error` branch, force a rebind — reassign the signal
(`this.appointments.update(list => [...list])`) or call `this.load()`.

---

## Quality Review

### CQ-1 — Both appointment screens require the doctor to type a raw numeric patient id, with no lookup and no name confirmation. Severity: Medium

**Location:** `src/frontend/src/app/features/appointments/appointment-form/appointment-form.component.html:8-10`
and `.../walk-in-registration/walk-in-registration.component.html:21-22`.

`GET /api/patients/search?name=&phone=` has existed since Step 9 and is not used by either form. The
doctor must know and correctly type an integer id before booking anything, and nothing echoes back
*which patient* that id resolved to before the record is written — the walk-in success alert
(`walk-in-registration.component.html:11`) shows "patient 7", not a name.

Two consequences: (a) real friction on the consultation entry path, which the fixed interpretation of
the 2–3 minute criterion says to judge on step count and unnecessary round-trips — the doctor's
"lookup the id" step happens outside the app entirely; (b) wrong-patient entry has no safety net, which
compounds CR-1 directly.

**Suggested fix:** replace the numeric input with a typeahead bound to the existing patient-search
endpoint that resolves to the id, and render the resolved patient's name next to the field before
submit. This is a genuine scope question for Module 5's consultation form too — worth deciding once.

### CQ-2 — `IWalkInService` returns the EF entity `Visit` across the Application boundary. Severity: Low

**Location:** `src/backend/PatientManagement.Application/Services/IWalkInService.cs:15`; hand-mapped in
`src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs:66-72`.

Every other service in the codebase returns a DTO (`IPatientService`, `IDoctorDetailsService`,
`IAppointmentService`); this one alone hands a mutable, navigation-property-bearing domain entity to a
controller, which then does the DTO mapping itself — business-shape knowledge living in the controller.
The signature predates Step 12, but Step 12 is what put it on the wire. Nothing leaks to the client
today (the controller maps to `WalkInVisitDto` correctly), so this is Low.

**Suggested fix:** change the signature to `Task<WalkInVisitDto?>` and move the mapping into
`WalkInService`. The nullable return is also the natural place to signal "no such patient", closing CR-5
in the same edit.

### CQ-3 — Per-action `try`/`catch` for exception→status mapping, unlike every other controller. Severity: Low

**Location:** `src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs:17-24, 48-55, 63-77`.

`PatientsController` and `DoctorDetailsController` contain no exception handling; `AppointmentsController`
has three blocks, and CR-2/CR-5's fixes would add more. Not wrong today — the controller stays thin and
the mapping is explicit — but it will not scale past a few exception types.

**Suggested fix:** consider a single `IExceptionHandler` mapping the named Application exceptions to
`ProblemDetails` status codes once, registered in `Program.cs`. Not blocking; flagging before the
pattern is copied into Module 5.

### CQ-4 — Service implementations live in `Infrastructure`, not `Application`, contrary to the fixed project-layout spec. Severity: Low (pre-existing, consistent)

**Location:** `src/backend/PatientManagement.Infrastructure/Services/` — `AppointmentService.cs`,
`AppointmentSlotGuard.cs`, `WalkInService.cs`, alongside `PatientService`, `DoctorDetailsService`,
`LoginService`.

`implementation-brd.md`'s stack spec assigns "service interfaces **and implementations** (business
logic)" to `PatientManagement.Application`, with `Infrastructure` holding "`DbContext`, entity
configurations, migrations, repository implementations", and states `Application` has "no direct EF Core
`DbContext` usage". Every service in this repo takes `AppDbContext` directly and therefore lives in
`Infrastructure`. This is *fully consistent across all six services* and predates Step 12 by many steps —
Step 12 followed the house pattern correctly, and its tests correctly land in `Infrastructure.Tests`.

Raised only so it is recorded as a knowingly accepted deviation rather than silent drift. Reversing it
would mean introducing repository abstractions across the whole backend — a plan step of its own, not
something to do mid-feature.

---

## Consistency Review

Checked against all three references. No blocking findings beyond those already raised.

**Against `implementation-brd.md`'s fixed specs:**

| Fixed rule | Verdict |
|---|---|
| Slot duration doctor-entered per appointment, never defaulted | Held on `POST /api/appointments`; **broken on `POST /api/appointments/walk-in`** — see CR-2. Same rule, two behaviours. |
| Walk-in creates one `Appointment` + one linked `Visit` in a single flow | Held. `WalkInService.cs:25-53`, one transaction, one HTTP call, `Visit.AppointmentId` non-nullable with a unique index. Correctly implemented. |
| Daily list merges scheduled + walk-in, time-ordered, not two feeds | Held, and held *structurally* — no `IsWalkIn` discriminator means `GetDailyAsync` is one `OrderBy` over one table. Good design call, documented in the tracker. |
| Double booking rejected outright, uniformly on both paths | Uniform (one shared guard), outright (`409`, nothing written) — but exact-instant only. See CR-4. |
| Vitals mandatory at data-entry, enforced server-side as well as client-side | **Not held** on the only endpoint that writes a `Visit`. See CR-2. |
| Weight `decimal(6,3)`, temperature Celsius, BP two numeric columns | Held (`AppDbContext.cs:65-66`, `Visit.cs:18-22`). Unchanged by this step. |
| Visits numbered sequentially **per patient** | Held (`WalkInService.cs:19-23`), and asserted live by verification. |
| No hosting-shaped infrastructure | Held. CORS still targets a configurable `localhost:4200` only; nothing environment-specific added. |
| Auth gates every new endpoint | Held for free via the global `FallbackPolicy`; `AppointmentsController` correctly declares no `[AllowAnonymous]`, and `Endpoints_require_authentication` proves it. |

**Against the rest of the codebase:** DTO naming/placement (`{Verb}{Entity}Request` / `{Entity}Dto` in
`Application/DTOs`), primary-constructor services, `ToDto` private static mapper, one test class per
controller with a fresh `AuthApiFactory` database per test, Angular standalone components with
`inject()`, signals for view state, and `nonNullable` reactive forms — all match Steps 9 and 11
exactly. One cosmetic divergence: `AppointmentsController.cs:34` builds its `Location` header as a
string where `PatientsController.cs:15` uses `CreatedAtAction`; justified in-comment by the absence of
a GET-by-id route, and the URL it emits is genuinely resolvable. **Low, no action needed.**

**Against the plan (`Modules/04-appointment-management.md`):** every file path matches the module's
declared API and Frontend surfaces exactly — four routes as listed at `:32-35`,
`src/frontend/src/app/features/appointments/` with all four named artifacts at `:39`. All nine build
checklist items map to real, present code. No migration was added, correctly — no entity changed this
step. **No undocumented deviation found.**

**On the flagged design decision (`Completed` not manually settable) — judged as a design choice, not
re-litigated:** it is a *sound* choice and unusually well handled. It is documented in six places
consistently, enforced end-to-end (service → `400` → tests → Angular constant → template), and the
tracker records exactly what to delete to reverse it. The one thing wrong with it is that it is
enforced in only one direction (**CR-3**) — fix that and the design is coherent. Recommendation to the
user/`worktree-brd`: promote it from "flagged assumption" to a locked line in `implementation-brd.md`.
Leaving a rule this load-bearing in "overturnable" state while Module 5 builds visit-creation on top of
it invites exactly the kind of half-implementation CR-3 already is.

---

## Routing — what `implementation-brd` must fix before Step 12 can be marked finished

1. **CR-1** — `[Required]`/`[Range]` on `CreateAppointmentRequest.PatientId` (+ test: omitted `patientId` → `400`, no appointment for patient `0`).
2. **CR-2** — server-side validation for the entire walk-in payload: patient id, duration ≥ 1, and all five vitals (+ tests: partial body → `400`, `durationMinutes: 0` → `400`).
3. **CR-3** — reject status changes on an appointment that is already `Completed` / already has a `Visit`, and move the guard below the existence check (+ service and API tests).
4. **CR-4** — **spec decision required first** (user / `worktree-brd`): exact-instant vs. overlap-aware double booking, recorded in `implementation-brd.md`. `implementation-brd` implements whichever is chosen; if exact-instant is confirmed as intentional, the fixed spec's prose needs amending to say so, and `DurationMinutes`' status as a display-only field should be stated explicitly.

CR-5 through CR-10 and CQ-1 through CQ-4 are tracked debt and do not block, but CR-5 and CR-6 should be
closed in the same pass as CR-2 since they touch the same method, and CQ-1 should be decided before
Module 5 builds a second screen with the same raw-id input.

Nothing here contradicts `verification-brd`'s PASS — every High above is outside what the test suite
exercises, which is exactly the gap this gate exists to close.

---

## Steps 13 & 14 — code review detail

**Verdict: APPROVED (both steps).** Reviewed together, as instructed — both are `Done` in
`docs/implementation-progress.md`, both have a fresh `docs/verification-report.md` PASS entry
(209/209, commit `f14a249`), and both live in the single worktree
`.claude/worktrees/impl-prescription-medication` (branch `impl/prescription-medication`, tip `f14a249`
— Step 14's own `688802e` plus verification's report commit on top, built off `main`@`7a23d41`, which is
Step 13's own merge commit, so Step 13's code (`f9aceb9`) is present unchanged and Step 14's commit sits
on top). `EnterWorktree` with `path` was refused ("the current working directory ... is the repository
root, not an isolated worktree"), so this review ran directly against the worktree's absolute paths
instead, per the same documented fallback `verification-brd` used. This worktree was created by
`implementation-brd`, not this session — no `ExitWorktree` action of any kind was taken.

Read in full before starting: `Modules/05-consultation-workflow.md`, `Modules/06-prescription-medication.md`,
`docs/implementation-progress.md` Steps 13/14 rows, `docs/verification-report.md`'s Steps 13 & 14 detail
section (PASS, `f14a249`), and `docs/codereview-report.md`'s own Step 12 entry above (CR-1 through CR-4,
still open). `.claude/agents/implementation-brd.md` was read in full for the fixed specs this review
checks against.

Files actually read (not just the diff from `main`): `StartConsultationRequest.cs`, `UpdateVisitRequest.cs`,
`VisitDto.cs`, `IConsultationService.cs`, `ConsultationAlreadyStartedException.cs`, `ConsultationService.cs`,
`VisitsController.cs`, `CreatePrescriptionRequest.cs`, `PrescriptionDto.cs`, `PrescriptionItemDto.cs`,
`IPrescriptionService.cs`, `IDrugSuggestionService.cs`, `PrescriptionService.cs`, `DrugSuggestionService.cs`,
`PrescriptionsController.cs`, `Prescription.cs` (domain), `AppDbContext.cs` (Weight/Temperature/index
config), `Program.cs` (DI registration), `app.routes.ts`, `consultation.service.ts`,
`consultation-workflow.component.ts`, `vitals-form.component.ts`, `prescription.service.ts`,
`prescription-form.component.ts`, `printable-prescription.component.ts`/`.html`, and the full test bodies
of `VisitsControllerTests.cs`, `PrescriptionsControllerTests.cs`, `ConsultationServiceTests.cs`,
`PrescriptionServiceTests.cs`, `DrugSuggestionServiceTests.cs`.

**Overall assessment:** this is careful, well-reasoned work, and it visibly learned from Step 12's code
review. `StartConsultationRequest` deliberately mirrors `CreateAppointmentRequest.DurationMinutes`'s
nullable-plus-`[Required]` pattern specifically to close the exact client-validation-only gap CR-2 found —
that is the correct fix, applied proactively, to a sibling code path Step 12 got wrong. `UpdateVisitRequest`
and the "no PUT/PATCH/DELETE action" technique on `PrescriptionsController` both enforce their invariants
by construction (absence of a property, absence of a route) rather than by a runtime check that could have
a bug — the same durable pattern already established for `Patient`'s no-delete guarantee. Every fixed
hard gate for these two modules was independently re-read in the actual code, not inferred from a passing
test: mandatory vitals, the vitals-locked/complaints-diagnosis-editable boundary, prescription immutability,
`DoctorDetails` snapshot-not-live-join, and `Contains` autocomplete semantics all hold exactly as specified.

---

### Correctness Review

**No Critical/High findings.**

**CR-11 — `ConsultationService.StartConsultationAsync`'s "at most one visit per appointment" check is a
TOCTOU race with no handler for the DB constraint that actually backstops it. Severity: Medium.**

**Location:** `src/backend/PatientManagement.Infrastructure/Services/ConsultationService.cs:13-26` (the
`FirstOrDefaultAsync` load + `AnyAsync` pre-check) through `:63` (`SaveChangesAsync`); the unique index
that is the real backstop is `AppDbContext.cs:75` (`entity.HasIndex(v => v.AppointmentId).IsUnique()`).

**What's wrong:** the same shape Step 12's code review already flagged as **CR-6** (`AppointmentSlotGuard`
vs. its own unique-index backstop) is present again here, unaddressed, in a second service: two concurrent
`POST /api/appointments/{id}/start-consultation` requests for the same appointment can both pass the
`alreadyHasVisit` check before either has inserted, and the loser's `SaveChangesAsync` then fails on the
unique index with an unhandled `DbUpdateException` — nothing in `VisitsController.StartConsultation`
catches that exception type, so the caller gets an unhandled `500`, not the `409`
(`ConsultationAlreadyStartedException`) the guard exists to produce for the same logical conflict when it's
detected in time. This is not a new pattern being invented — `PrescriptionsController`/`AppointmentsController`
don't need this at all (prescriptions have no uniqueness rule; appointments' equivalent is exactly CR-6) —
it's the identical unaddressed gap recurring in code this step wrote fresh.

**Why it matters:** low likelihood on a single-user localhost app (CR-6's own reasoning applies
identically) — the realistic trigger is a doctor double-clicking "Start Consultation" on a slow connection,
not concurrent multi-user traffic. But the failure mode is a raw `500` with no explanatory body, worse
than the `409` a doctor would actually understand, and it is the second occurrence of a fix CR-6 already
specified — worth closing both in the same pass rather than accumulating a third instance in a future
module.

**Suggested fix:** same remedy as CR-6 — wrap `SaveChangesAsync` in
`catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex)) { throw new ConsultationAlreadyStartedException(appointmentId); }`.
Add a test that starts two consultations for the same appointment concurrently (or mocks the race) and
asserts `409`, not `500`.

**Low — `ConsultationService.StartConsultationAsync` trusts `appointment.PatientId` with no re-validation,
confirmed as a second consumer of Step 12's still-open CR-1, not a new defect of its own. Severity: Low
(informational, no action routed).**

**Location:** `src/backend/PatientManagement.Infrastructure/Services/ConsultationService.cs:39`
(`PatientId = appointment.PatientId`).

Forming an independent verdict on this, as asked, rather than restating verification's framing: this is
**not** a new Correctness finding against Step 13's own code, and it does not get a CR number. The reason
is structural, not just "it's pre-existing" — `StartConsultationRequest` has no `PatientId` property at
all (compare `CreateAppointmentRequest.PatientId`, the actual site of CR-1's defect); there is no new
attacker/user-controlled input here for Step 13 to have validated or failed to validate. The service reads
an already-persisted foreign key off a row it just loaded, which is ordinary, correct service design — the
entire defect surface is CR-1's own `[Required]`/`[Range]` gap on `CreateAppointmentRequest.PatientId`, and
fixing CR-1 closes this consumer automatically with no separate change needed here. Recorded because the
task asked for it to be, and because it is a legitimate reason to prioritize CR-1 (two consumers now, not
one) — not because Step 13 did anything wrong.

**Confirmed: Step 14 does not repeat CR-1's mistake.** `CreatePrescriptionRequest` takes no patient/visit
id in its body at all (`visitId` comes from the route and is checked with `db.Visits.AnyAsync` → `404` if
absent); its only body field, `Items`, is `[Required]`/`[MinLength(1)]` and each item's `DrugName` is
`[Required]`. No unvalidated-id pattern was introduced.

---

### Quality Review

**No Critical/High findings.**

No dead code, no misleading naming, no duplicated logic that should share an implementation. Controllers
stay thin — all branching logic (the `alreadyHasVisit` guard, the `DoctorDetails`-fallback default, the
`Contains` query) lives in the two services, not in `VisitsController`/`PrescriptionsController`. DTOs
don't leak EF entity shapes: `VisitDto`/`PrescriptionDto`/`PrescriptionItemDto` are hand-shaped, and
`PrescriptionService.ToDto` converts `Logo`/`Signature` `byte[]?` to base64 `string?` rather than exposing
raw bytes, mirroring `DoctorDetailsDto`'s own convention. No unrequested scope: `[MinLength(1)]` on
`CreatePrescriptionRequest.Items` is the one addition beyond the module's own checklist, and it's
justified (an empty prescription isn't a meaningful printed document) and cheap to relax, not
speculative infrastructure.

**CQ-5 — `PrintablePrescriptionComponent` never renders the snapshotted `Logo`/`Signature` bytes it
already has on hand. Severity: Medium. (Independent verdict, concurring with verification's F-8.)**

**Location:** `src/frontend/src/app/features/prescriptions/printable-prescription/printable-prescription.component.html:16-25`
(header) and `:89-96` (footer); the data is present and typed on the component's own model at
`src/frontend/src/app/features/prescriptions/prescription.service.ts:24-25`
(`Prescription.logo`/`.signature: string | null`, populated from the backend's base64-encoded snapshot)
but never referenced anywhere in the template.

Forming an independent verdict rather than restating verification's F-8: I agree this is real and Medium,
not higher and not nothing. `implementation-brd.md`'s Doctor/clinic details spec is unambiguous about
*why* `Logo`/`Signature` exist as columns at all — *"This is the source for the header/footer of printed
prescriptions"* — and `Prescription.CreateFromDoctorDetails` was built specifically to carry both fields
into exactly this artifact (`Prescription.cs:22-23,41-42`), snapshot-isolated and unit-tested since Step 6.
The data pipeline from `DoctorDetails` upload through to the Angular `Prescription` model is fully wired
and correct; only the last rendering step is missing. It does not corrode data, does not violate any
hard gate `verification-brd.md` names for this module (that list is immutability-only), and is arguably
satisfied by the BRD's own looser wording ("Footer (basic notes/signature area)") — which is why this
stays Medium and does not gate the step. But it is a genuine, cheap-to-close gap against a fixed spec's
explicit stated purpose for these two fields, not a style nitpick: a doctor who uploads a clinic logo via
Module 2 has no way to see it appear on a printed prescription today.

**Suggested fix:** in the header/footer sections already present in the template, conditionally render
`<img [src]="'data:image/png;base64,' + rx.logo" *ngIf="rx.logo">` (and the equivalent for `rx.signature`
in the footer's signature area) when the corresponding field is non-null. No backend change needed — the
data already round-trips correctly end to end.

**No new instance of Step 12's CQ-1 (raw numeric id, no lookup/confirmation) in this scope.** Neither
`ConsultationWorkflowComponent` nor `PrescriptionFormComponent` asks the doctor to type a patient/visit id
by hand — `appointmentId`/`visitId` arrive as route parameters from links the daily schedule and the
consultation success panel already generate, so this scope doesn't reintroduce the pattern CQ-1 flagged.

---

### Consistency Review

Checked against all three references. No blocking findings.

**Against `implementation-brd.md`'s fixed specs:**

| Fixed rule | Verdict |
|---|---|
| Vitals mandatory at data-entry, non-nullable column + service-layer/API check | **Held**, and correctly — `StartConsultationRequest`'s nullable-`+[Required]` fields 400 a missing vital before `ConsultationService` runs at all. Closes CR-2's gap for this path (walk-in's own gap is untouched, as instructed, out of this scope). |
| Temperature Celsius, BP two numeric columns, Weight `decimal(6,3)` | Held, unchanged (`AppDbContext.cs:65-66`). |
| No draft/autosave path for an incomplete Visit | Held — `ConsultationService` has exactly one Visit-creating method, and it requires the full validated request. |
| Post-creation edit boundary (vitals locked, complaints/diagnosis editable) | Held, enforced by DTO shape (`UpdateVisitRequest` has no vitals property) — confirmed live by the "smuggled `temperature: 999`" test, which I read in full. |
| Prescription immutable — no update endpoint ever targets it | Held, enforced by *absence* of any `[HttpPut]`/`[HttpPatch]`/`[HttpDelete]` action — confirmed by reading `PrescriptionsController.cs` directly, not just trusting the 405 tests. |
| `DoctorDetails` snapshotted at creation, never joined live | Held, unchanged since Step 6 (`Prescription.CreateFromDoctorDetails`). |
| Medication free text + autocomplete, not a validation constraint | Held — `DrugName` is `required string`, no dictionary/enum; `DrugSuggestionService` is read-only. |
| Autocomplete match semantics (open item in the module file) | Resolved as `Contains`, case-insensitive — matches the codebase's only other free-text-lookup precedent (Patient search). Reasonable, documented, cheap to revisit. |
| No hosting-shaped infrastructure added | Held. |
| Auth gates every new endpoint | Held for free via the global `FallbackPolicy`; confirmed by each controller's own `Endpoints_require_authentication` test, read in full. |

**Against the rest of the codebase:** service-in-`Infrastructure`-not-`Application` placement (`CQ-4`,
already an accepted, consistently-applied deviation as of Step 12) continues unchanged —
`ConsultationService`/`PrescriptionService`/`DrugSuggestionService` all take `AppDbContext` directly and
live in `Infrastructure/Services/`, matching every other service in the codebase. DTO
naming/placement, primary-constructor services, private static `ToDto` mappers, one test class per
controller with a fresh `AuthApiFactory` database per test, Angular standalone components with `inject()`
and signals, `nonNullable` reactive forms — all match Steps 9/11/12 exactly. `VisitsController`'s absolute
route-template override for `start-consultation` is a deliberate, documented, well-precedented ASP.NET
Core technique (not a hack), chosen so Module 5 owns its whole HTTP surface without editing Module 4's
controller — a reasonable call, consistent with how this codebase already keeps each module's controller
self-contained.

**Against the plan (`Modules/05-consultation-workflow.md`, `Modules/06-prescription-medication.md`):**
every file path matches each module's declared API/Frontend surface exactly. Both modules' full build
checklists map to real, present code — verified by reading the files, not by trusting the checked boxes.
No undocumented deviation found. The two implementation-time decisions each module's own text explicitly
left open (Step 13's post-creation edit boundary; Step 14's autocomplete match semantics) were both
resolved with documented reasoning rather than escalated or silently invented — reviewed as design choices
on their merits, not re-litigated, per the same standard Step 12's `Completed`-decision was judged against.
Two further undocumented-but-reasonable implementation-time calls, noted for completeness rather than as
findings: no status precondition on starting a consultation (any non-completed appointment can have a
consultation started regardless of `Scheduled`/`Cancelled`/`NoShow`), and no explicit DB transaction
wrapping `ConsultationService.StartConsultationAsync`'s single `SaveChangesAsync` call (correctly reasoned
as unnecessary, since one call is already atomic — unlike `WalkInService`, which spans two).

**On Step 12's still-open CR-1 through CR-4 (context, not re-reviewed):** confirmed independently via
`git diff --stat 7a23d41 688802e -- src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs src/backend/PatientManagement.Infrastructure/Services/AppointmentService.cs src/backend/PatientManagement.Infrastructure/Services/WalkInService.cs src/backend/PatientManagement.Infrastructure/Services/AppointmentSlotGuard.cs`
that none of these four files changed between Step 13's merge and Step 14's tip — Steps 13/14 neither fix
nor worsen CR-1 through CR-4. The one genuine interaction (ConsultationService trusting
`appointment.PatientId`) is addressed above under Correctness as a Low, not routed as new work.

---

## Routing — what `implementation-brd` should address (tracked debt, does not block either step)

Neither step requires any fix before being marked finished — both are **APPROVED**. For the record,
carried forward as tracked debt (same non-blocking status Step 12's CR-5 through CR-10 and CQ-1 through
CQ-4 already have):

1. **CR-11** (Medium) — catch the unique-index `DbUpdateException` in `ConsultationService.StartConsultationAsync`
   and rethrow as `ConsultationAlreadyStartedException`, same fix as Step 12's still-open CR-6, now needed
   in two places.
2. **CQ-5** (Medium) — render `Logo`/`Signature` in `PrintablePrescriptionComponent`'s header/footer when
   non-null; concurs with and formally tracks `docs/verification-report.md`'s F-8.
3. Step 12's **CR-1** (High, still open) now has a second consumer (`ConsultationService.StartConsultationAsync`,
   noted above) in addition to `AppointmentService.CreateAsync` — worth weighting when CR-1 is next
   prioritized, though this does not change CR-1's own severity or add a new CR number.

Nothing here contradicts `verification-brd`'s PASS for Steps 13/14 — every finding above is either Medium/
Low tracked debt outside what the test suite exercises, or an explicit, reasoned non-finding (the CR-1
connection). Proceed: Step 15, and merging `impl/prescription-medication`, are both clear as far as this
gate is concerned.
