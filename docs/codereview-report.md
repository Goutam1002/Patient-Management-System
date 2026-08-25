# Code Review Report — Patient Management App

> **This gate is being introduced retroactively.** Steps 1–11 have **no entries** in this file and were
> never reviewed by `codereview-brd`. Nothing is backfilled — an absent row means "not reviewed", not
> "reviewed clean". Code review starts at Step 12. Pre-existing defects in earlier steps are only
> reported here when Step 12's code depends on, exposes, or extends them.

| Step | Plan Phase | Verdict | Critical | High | Medium | Low | Commit Reviewed | Notes |
|---|---|---|---|---|---|---|---|---|
| 12 | Appointment Management — `Modules/04-appointment-management.md` full build checklist | **CHANGES REQUESTED** | 0 | 4 | 4 | 7 | `9f7e9c0` | Four High findings: two unvalidated request DTOs that let the *wrong patient* and *zero-valued vitals* be written silently, a status guard enforced in only one direction, and the overlap-blind double-booking rule. Verification's PASS stands — every one of these is outside what the existing tests exercise. See below. |

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
