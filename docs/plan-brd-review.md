# Plan: Patient Management Application — Phase 1, full build

**Source:** `BRD/Doc_BRD.md`, grounded against the fixed stack/spec in `.claude/agents/implementation-brd.md` and the open decisions raised in `docs/brainstorm-brd-review.md`.
**Method:** produced per `.claude/agents/plan-brd.md` — ordered, file-targeted steps a developer (or `implementation-brd`) can execute without re-deriving decisions.
**Date:** 2026-08-19
**Repo state confirmed:** pre-build — no `angular.json`, no `*.csproj`/`*.sln` exist yet. This plan starts from scaffolding, not from an existing codebase.

---

## Scope & assumptions

- **In scope:** the whole Phase 1 BRD — patient management, appointment scheduling, consultation workflow (vitals → complaints → diagnosis → medication), printable prescriptions, visit history, search, CSV/PDF export, single-user auth.
- **Explicitly excluded** (per BRD Out of Scope): receptionist/multi-user access, billing, insurance, lab/pharmacy integration, AI diagnosis, offline mode, mobile app, advanced analytics, multi-doctor/multi-clinic, follow-up reminders. No step below builds toward any of these.
- **Project layout** (updated 2026-08-24 at the user's request — both projects now live under a common `src/`): `src/frontend/` for the Angular workspace, `src/backend/PatientManagement.Api/` for the .NET Web API project, `src/backend/PatientManagement.slnx` at the solution root (also renamed from `.sln` to match the actual `.slnx` the .NET 10 SDK generates by default — see `docs/implementation-progress.md` Step 1). File targets below are updated to match.
- **Two decisions this plan builds against are recommendations from `docs/brainstorm-brd-review.md`, not yet stakeholder-confirmed — flagging per plan-brd's rule against silently resolving open questions:**
  - **Walk-ins:** `implementation-brd.md` requires `Visit.AppointmentId` non-nullable and flags walk-in support as unresolved. This plan builds against brainstorm-review §1.1's recommendation (auto-create a same-moment `Appointment` row for a walk-in, `status = Completed`) so the schema doesn't need to change later. **If the stakeholder instead wants `Visit.AppointmentId` nullable, Step 5 and Step 16 both change** — flag before building Step 5.
  - **Backups:** the BRD's "regular automated backups" NFR has no mechanism named anywhere. This plan builds against brainstorm-review §3.5's recommendation (Windows Task Scheduler + `sqlcmd BACKUP DATABASE`, compatible with SQL Server Express). **If a different SQL Server edition (with Agent) is chosen instead, Step 19 changes.**
- **Everything else assumed** is either directly stated in the BRD or already fixed in `implementation-brd.md` (Patient keeps both Age and DOB, `PatientId` is a 0-based sequential integer, `Phone` optional/non-unique, prescriptions immutable, auth uses reversible encryption not hashing, exports follow the two-file CSV split) — not re-derived here.

---

## Steps

### Phase 0 — Scaffold

**1. Scaffold Angular workspace + .NET Web API project + local SQL Server wiring**
- What it does: `ng new frontend --standalone --routing` (run inside `src/`); `dotnet new webapi -o PatientManagement.Api` (run inside `src/backend/`); add a solution file at `src/backend/`; install `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Tools`; add an empty `AppDbContext` and a `localhost`-only connection string in `appsettings.json` pointing at a local SQL Server Express named instance (per brainstorm-review §3.3).
- Files: `src/frontend/angular.json` (new), `src/backend/PatientManagement.slnx` (new), `src/backend/PatientManagement.Api/PatientManagement.Api.csproj` (new), `src/backend/PatientManagement.Api/Data/AppDbContext.cs` (new), `src/backend/PatientManagement.Api/appsettings.json` (new)
- Depends on: none
- Tests: manual — `dotnet build` succeeds, `ng build` succeeds, API starts on Kestrel and returns its default route, EF Core can open a connection to the local SQL Server instance (`dotnet ef database update` on the still-empty context succeeds with zero migrations).

### Phase 1 — Data model & migrations

**2. `Users` entity + auth secret**
- What it does: `Users` table with exactly `Id`, `Username`, `Password` (no extra columns — no `PasswordHash`/`Salt`); an AES encryption helper (`Services/PasswordCrypto.cs`) using a key from `appsettings.json`; a one-time seed inserting the single doctor account with an encrypted password.
- Files: `src/backend/PatientManagement.Api/Models/User.cs` (new), `src/backend/PatientManagement.Api/Services/PasswordCrypto.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddUsers.cs` (new), `src/backend/PatientManagement.Api/Data/AppDbContext.cs` (modified)
- Depends on: Step 1
- Tests: unit — encrypting then decrypting a password returns the original value; encrypted value is never equal to the plaintext. Migration smoke test — applies cleanly to a fresh database.

**3. `DoctorDetails` entity**
- What it does: `DoctorDetails` table (`ClinicName`, `DoctorName`, `Qualifications`, `RegistrationNumber`, `Logo`, `Signature`) — the source for prescription header/footer, snapshotted (not joined live) at print time per the fixed spec.
- Files: `src/backend/PatientManagement.Api/Models/DoctorDetails.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddDoctorDetails.cs` (new)
- Depends on: Step 1
- Tests: migration smoke test only at this step — no consumer yet (consumed in Step 10).

**4. `Patient` entity**
- What it does: `Patient` table per the fixed spec — `Age` and `DateOfBirth` both stored independently, `Allergies`/`CurrentMedications`/`ChronicConditions`, `Phone` optional and non-unique (no unique index), `PatientId` as `IDENTITY(0,1)` (explicit seed 0, not EF's default seed 1 — configure via `.UseIdentityColumn(seed: 0, increment: 1)` in `OnModelCreating`), no delete/archive column of any kind.
- Files: `src/backend/PatientManagement.Api/Models/Patient.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddPatients.cs` (new)
- Depends on: Step 1
- Tests: integration — inserting the first patient yields `PatientId == 0`, the second `PatientId == 1`; inserting two patients with the same phone number succeeds (no unique-constraint violation); no delete endpoint exists yet to test against (negative test lands with Step 4's controller in Step 11, not here).

**5. `Appointment` + `Visit` entities**
- What it does: `Appointment` (status enum `Scheduled/Completed/Cancelled/NoShow`, `ScheduledTime`, `PatientId` FK); `Visit` with `AppointmentId` **non-nullable** FK, vitals columns non-nullable (`Temperature` in °C, `BpSystolic`/`BpDiastolic` as separate smallints — never a formatted string, `Pulse`, `Weight` as `decimal(6,3)`), `Complaints`/`Diagnosis` free text, sequential per-patient visit numbering. A service-layer `CreateWalkInVisit(patientId)` helper that inserts a same-moment `Appointment` (`status = Completed`) then the `Visit` in one transaction (brainstorm-review §1.1, Option 2 — flagged as an assumption above).
- Files: `src/backend/PatientManagement.Api/Models/Appointment.cs` (new), `src/backend/PatientManagement.Api/Models/Visit.cs` (new), `src/backend/PatientManagement.Api/Services/WalkInService.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddAppointmentsAndVisits.cs` (new)
- Depends on: Step 4
- Tests: integration — a `Visit` cannot be saved with a missing temperature/BP/pulse (server-side, non-nullable columns); `Weight` round-trips at three decimal places without rounding (e.g. `52.850`); `CreateWalkInVisit` produces exactly one `Appointment` row (`status = Completed`) and one linked `Visit` row in a single transaction; visit numbers increment per-patient, not globally.

**6. `Prescription` + `PrescriptionItem` entities**
- What it does: `Prescription` (header, snapshotted `DoctorDetails` fields at creation, linked to one `Visit`) + `PrescriptionItem` lines (`DrugName`, `Dosage`, `Frequency`, `Duration`, `Instructions` — free text, no drug-dictionary FK). No update endpoint will ever target these once created (enforced in Step 8's controller, not here) — a correction creates a new `Prescription` row.
- Files: `src/backend/PatientManagement.Api/Models/Prescription.cs` (new), `src/backend/PatientManagement.Api/Models/PrescriptionItem.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddPrescriptions.cs` (new)
- Depends on: Step 3, Step 5
- Tests: migration smoke test; integration — creating a `Prescription` copies the current `DoctorDetails` values onto it rather than storing a live FK (verified by changing `DoctorDetails` after creation and confirming the existing prescription's snapshot is unchanged).

### Phase 2 — Consultation workflow (critical path for the 2–3 minute target)

**7. Visit-creation API (vitals + complaints/diagnosis + medication)**
- What it does: `POST /api/visits` accepting vitals (server-side non-nullable validation, independent of the Angular form), `Complaints`/`Diagnosis` free text, and a list of `PrescriptionItem`s; a companion `GET /api/patients/{id}/suggestions?field=diagnosis&prefix=...` endpoint returning the doctor's own prior free-text entries ranked by recency + frequency, reused for both `Complaints`/`Diagnosis` and medication name autocomplete (brainstorm-review §1.3, §2.2 — one shared mechanism, not three).
- Files: `src/backend/PatientManagement.Api/Controllers/VisitsController.cs` (new), `src/backend/PatientManagement.Api/Controllers/SuggestionsController.cs` (new), `src/backend/PatientManagement.Api/Services/VisitService.cs` (new), `src/backend/PatientManagement.Api/DTOs/CreateVisitDto.cs` (new)
- Depends on: Step 5, Step 6
- Tests: integration — a visit request missing temperature/BP/pulse is rejected (400, not silently defaulted); a valid visit creates the `Visit` + `Prescription` + `PrescriptionItem` rows in one transaction; the suggestions endpoint returns prior entries ranked by recency for a repeat prefix.

**8. Angular consultation component**
- What it does: single scrollable reactive-form page (brainstorm-review §2.3 — no wizard) covering vitals (fixed tab order, `Validators.required`, no unit toggle) → complaints/diagnosis (autocomplete from Step 7's suggestions endpoint) → medication entry (free text, autocomplete, prefix-match + frequency-ranked dosage/frequency pre-fill per brainstorm-review §2.2).
- Files: `src/frontend/src/app/consultation/consultation-form.component.ts` (new), `src/frontend/src/app/consultation/consultation-form.component.html` (new), `src/frontend/src/app/consultation/vitals-input.component.ts` (new), `src/frontend/src/app/shared/autocomplete.component.ts` (new), `src/frontend/src/app/consultation/consultation.service.ts` (new)
- Depends on: Step 7
- Tests: component — form submission is blocked when temperature/BP/pulse are empty; tab order is temperature → systolic → diastolic → pulse with no intervening stop; autocomplete suggestion list appears after the configured prefix length and selecting one fills the field without an extra click.
- Cost against the speed target: this is the step where every extra click/round-trip directly costs seconds against the 2–3 minute criterion — no field beyond the BRD's stated set, no confirmation modal on save.

**9. "Repeat last Rx"**
- What it does: a button on the consultation form that, for a patient with a prior visit, clones the most recent `Prescription`'s line items into the new visit's medication section as an editable starting point (brainstorm-review §2.2, Option 2 — flagged as a fast-follow, not deferred to Phase 2+).
- Files: `src/frontend/src/app/consultation/consultation-form.component.ts` (modified), `src/backend/PatientManagement.Api/Controllers/VisitsController.cs` (modified — add `GET /api/patients/{id}/last-prescription`)
- Depends on: Step 8
- Tests: integration — endpoint returns the most recent prescription's items for a patient with visit history, and `404`/empty for a first-time patient; component — clicking the button populates the medication rows without submitting the form.

### Phase 3 — Printing

**10. Prescription print view**
- What it does: a dedicated print-styled Angular route rendering the finalized prescription (clinic/doctor header from the snapshotted `DoctorDetails`, patient details, vitals, diagnosis, medications, footer/signature area) using `window.print()` — no server-side PDF round-trip for printing (brainstorm-review §6.5: print is a different concern from the PDF *export* feature in Step 14).
- Files: `src/frontend/src/app/prescription/prescription-print.component.ts` (new), `src/frontend/src/app/prescription/prescription-print.component.html` (new), print stylesheet in the same component
- Depends on: Step 6, Step 8
- Tests: manual/UI — print preview shows all required sections (header, patient, vitals, diagnosis, medications, footer); a prescription printed, then the underlying `DoctorDetails` changed, then reprinted from history (Step 12) still shows the original snapshotted header — proves immutability holds through print, not just through the API.

### Phase 4 — Search & History

**11. Patient search**
- What it does: `GET /api/patients/search?q=...` matching name (prefix/contains) and normalized phone digits, ranked by most-recently-visited first (brainstorm-review §4.1); Angular search bar + results list.
- Files: `src/backend/PatientManagement.Api/Controllers/PatientsController.cs` (new — also houses add/edit/view from the BRD's Patient Management requirement), `src/backend/PatientManagement.Api/Services/PatientSearchService.cs` (new), `src/frontend/src/app/patients/patient-search.component.ts` (new)
- Depends on: Step 4
- Tests: integration — search by partial name returns matches; search by partial phone digits returns matches regardless of formatting (`+91 98765` vs `9876543210`); result ordering favors a recently-visited patient over an equally-matching one with no recent visit; a delete/archive endpoint for `Patient` does not exist (negative test, fixed-spec gate).

**12. Patient history view**
- What it does: reverse-chronological visit list on the patient profile, each row expandable to vitals/complaints/diagnosis/prescription, with a date-range filter (BRD's Patient History requirement, literally).
- Files: `src/frontend/src/app/patients/patient-history.component.ts` (new), `src/backend/PatientManagement.Api/Controllers/PatientsController.cs` (modified — add `GET /api/patients/{id}/visits?from=&to=`)
- Depends on: Step 5, Step 11
- Tests: integration — date-range filter excludes visits outside the range; a patient with zero visits returns an empty list, not an error.

### Phase 5 — Export

**13. CSV export**
- What it does: `patients.csv` (one row per visit within the selected scope, every current `Patient` entity field via reflection over the EF model — not a hardcoded column list — plus `VisitDate`/`Diagnosis`/`Prescriptions` semicolon-encoded) and `visits.csv` (one row per visit, fixed column order) per the locked spec in `implementation-brd.md`. Scope is selected patients or a date range only — no code path produces an unbounded export. Every export writes an audit-log entry.
- Files: `src/backend/PatientManagement.Api/Controllers/ExportController.cs` (new), `src/backend/PatientManagement.Api/Services/CsvExportService.cs` (new), `src/backend/PatientManagement.Api/Models/ExportAuditLog.cs` (new), `src/backend/PatientManagement.Api/Migrations/xxxx_AddExportAuditLog.cs` (new)
- Depends on: Step 5, Step 6, Step 11
- Tests: negative test proving no unbounded/all-patients export path exists; `patients.csv` produces one row per visit with a column set that is a strict superset of the `Patient` entity's current mapped properties (reflection-checked, so it can't silently drift behind the entity); `visits.csv` produces exactly `PatientId, Name, DOB, Phone, VisitDate, Diagnosis, Prescriptions` in that order; an export request without confirmation is rejected (400); a completed export writes exactly one audit-log row (who/scope/format/when).

**14. PDF export**
- What it does: single-patient summary PDF (demographics, visit history, prescriptions), date range selectable, same confirmation-gate and audit-log requirement as CSV.
- Files: `src/backend/PatientManagement.Api/Services/PdfExportService.cs` (new, using a PDF-generation library added to the `.csproj`), `src/backend/PatientManagement.Api/Controllers/ExportController.cs` (modified)
- Depends on: Step 13
- Tests: integration — generated PDF byte stream is non-empty and correctly scoped to one patient's date-filtered visits; confirmation-gate and audit-log tests mirror Step 13's.

**15. Export UX**
- What it does: single "Export" screen — scope picker (patients or date range) → format choice → explicit "Confirm Export" button → download (brainstorm-review §5, Option 1 — one enforcement point for the hard gates, not scattered export buttons).
- Files: `src/frontend/src/app/export/export.component.ts` (new)
- Depends on: Step 13, Step 14
- Tests: component — the confirm button is disabled until a scope and format are chosen; download only fires after explicit confirmation.

### Phase 6 — Appointments

**16. Appointment scheduling**
- What it does: `POST/GET /api/appointments` (create, daily list), status updates restricted to the legal transitions in Step 5's enum (a manual status PATCH cannot set `Completed` directly — only finalizing a `Visit` does, per brainstorm-review §1.4); Angular daily appointment list + a "New Walk-in" button wired to Step 5's `CreateWalkInVisit`.
- Files: `src/backend/PatientManagement.Api/Controllers/AppointmentsController.cs` (new), `src/frontend/src/app/appointments/appointment-list.component.ts` (new), `src/frontend/src/app/appointments/walk-in-button.component.ts` (new)
- Depends on: Step 5, Step 8
- Tests: integration — a direct PATCH attempting to set `status = Completed` without a linked `Visit` is rejected; a `Cancelled`/`NoShow` appointment has no `Visit`; the walk-in button end-to-end creates both rows and lands the doctor directly in Step 8's consultation form.

### Phase 7 — Auth & session

**17. Login + session timeout**
- What it does: `POST /api/auth/login` — decrypt-and-compare, not hashed comparison, no registration endpoint; Angular login screen + route guard; 30-minute idle session timeout returning to login (brainstorm-review §3.6).
- Files: `src/backend/PatientManagement.Api/Controllers/AuthController.cs` (new), `src/frontend/src/app/auth/login.component.ts` (new), `src/frontend/src/app/auth/auth.guard.ts` (new), `src/frontend/src/app/auth/idle-timeout.service.ts` (new)
- Depends on: Step 2
- Tests: integration — login succeeds only on exact username/decrypted-password match; wrong password, wrong username, and empty credentials all fail; no registration/self-signup endpoint exists (route absence test); component — idle timeout redirects to login after the configured window with no user activity.

### Phase 8 — Backup & local ops

**18. Backup mechanism**
- What it does: a `sqlcmd -Q "BACKUP DATABASE..."` script + a documented Windows Task Scheduler entry running it nightly, writing `.bak` files to a local (or external-drive) folder (brainstorm-review §3.5, Option 2 — the option compatible with SQL Server Express, flagged as an assumption above). Restore steps documented alongside it.
- Files: `src/backend/scripts/backup.sql` (new), `src/backend/scripts/backup-setup.md` (new, setup instructions — not app code, but needed for the NFR to be real rather than implied)
- Depends on: Step 1
- Tests: manual — running the script against a populated local database produces a valid `.bak` file restorable via SSMS.

**19. Local start script**
- What it does: a batch/shortcut script that starts the .NET Web API (Kestrel) and serves the Angular production build, then opens the default browser to the app (brainstorm-review §3.4, Option 1 — no installer framework, no Windows Service).
- Files: `src/backend/scripts/start-clinic-app.bat` (new)
- Depends on: Step 1
- Tests: manual — double-clicking the script on a clean machine starts both processes and opens the app in the browser within a few seconds.

---

## Sequencing rationale

- **Phase 1 (data model) comes before every feature phase** because every downstream step reads or writes these tables — building UI against an unstable schema is the single most expensive kind of rework. Appointment and Visit are modeled together in Step 5 even though appointment *scheduling UI* doesn't land until Phase 6, because Visit's required `AppointmentId` FK means the schema decision can't be deferred, only the UI can.
- **Phase 2 (consultation workflow) is the critical path.** It's where the BRD's headline success criterion (2–3 minute consultation) is actually won or lost — prescription-entry autocomplete quality (Step 8) matters more to that number than any other single decision in this plan, which is why Step 9 ("Repeat last Rx") is sequenced as an immediate fast-follow rather than deferred.
- **Printing (Phase 3) follows consultation directly** because a prescription with nothing to print is untestable — it needs Step 6's data and Step 8's UI to exist first.
- **Search/History (Phase 4) and Export (Phase 5) are next** because they depend on visit data existing (a patient with zero visits can't meaningfully demonstrate search ranking or CSV row-per-visit shape) but don't block the consultation critical path — safe to build in parallel with Phase 6 if resourcing allows.
- **Appointments (Phase 6) is deliberately last among the feature phases**, matching `plan-brd.md`'s own guidance to weight toward the consultation loop first — a doctor can be given a manual/ad hoc way to create visits for early testing (directly via Step 7's API) before the full scheduling UI exists.
- **Auth (Phase 7) can be built in parallel with Phases 2–6** — it has no data dependency on Patient/Visit, only on Step 2's `Users` table — but is sequenced late here only because it's not on the consultation-speed critical path; a real team could pull it forward.
- **Backup and local-ops (Phase 8) are last** because they're operational concerns, not features — but they are **not optional**: Step 18 is the only thing that makes the BRD's "no data loss" / "regular automated backups" NFR real, and shipping without it is shipping an NFR that was never implemented.

---

## Deferred / Phase 2+

- **Duplicate patient detection** (brainstorm-review §1.2) — a soft warning at registration is valuable but not on the critical path for any Phase 1 success criterion; defer until Phases 1–7 are stable.
- **DoctorDetails capture UX** (brainstorm-review §6.6) — a settings screen for uploading `Logo`/`Signature` is needed before Step 10 is truly usable end-to-end in a real clinic, but is small enough to fold into Step 3/10 at implementation time rather than reserving a separate phase here; call it out if it's dropped.
- **A Windows Service / auto-start on machine boot** (brainstorm-review §3.4, Option 2) — real polish, not a Phase 1 requirement; the batch-script approach (Step 19) is sufficient for launch.
- **Full-text/fuzzy search** (brainstorm-review §4.1, Option 2) — only worth revisiting if real usage shows prefix/contains search missing matches.
- **Everything in the BRD's own Out of Scope list** — receptionist/multi-user, billing, insurance, lab/pharmacy integration, AI diagnosis, offline mode, mobile app, advanced analytics, multi-doctor/multi-clinic, follow-up reminders. No step above builds toward any of these; naming them here so a future plan revision doesn't fold one in silently.
