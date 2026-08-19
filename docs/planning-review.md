# Planning Review — Patient Management Application

**Source:** `BRD/Doc_BRD.md`, `docs/brainstorm-review.md`
**Method:** full end-to-end Phase 1 implementation plan derived from the BRD, reusing the data-model, UX, and roadmap decisions already reconciled in the prior brainstorm review rather than re-deriving them.
**Date:** 2026-08-18
**Status:** pre-build. The repo contains the BRD and prior analysis only — no code, no schema, no stack committed yet. This plan proposes a stack and a full work breakdown against it.

---

## Plan: Patient Management Application — full Phase 1 (BRD `Doc_BRD.md`), pre-build to go-live

Repo root: `D:\Projects\Patient Management\`. All file targets below are relative to that root. Sources read in full: `BRD/Doc_BRD.md`, `docs/brainstorm-review.md` (referenced below as BR §n).

---

### Scope & assumptions

**In scope (traced to BRD):** patient registration/profile/search; appointment scheduling + daily list + statuses; consultation workflow (mandatory vitals → complaints → diagnosis → medication); printable prescription with header/footer; patient visit history with date filter; quick search + recent patients; CSV and PDF export; NFRs — page load <2s, fast search, no-data-loss/automated backups, single-user secure login, encryption at rest and in transit, Chrome/Edge/Safari.

**Explicitly excluded (BRD "Out of Scope"), and not smuggled in anywhere below:** receptionist/multi-user access, billing/invoicing, insurance, lab/pharmacy integration, AI diagnosis, offline mode, mobile app, advanced analytics, multi-doctor/multi-clinic, follow-up alerts/reminders. Where BR §9 lists cheap "doors to keep open" that touch these areas (e.g. `created_by` columns, `follow_up_after_days` as a stored-and-printed field with **no notification**, `amount` on visit), I include only the schema-level hooks and mark each `[hook]`; any hook can be dropped without affecting Phase 1 behaviour. Fee capture (`amount`/`payment_mode`) is **omitted entirely** — it is billing, which the BRD excludes.

**Decisions reused from `docs/brainstorm-review.md` (not re-derived):**
- **Visit is the root clinical record; Appointment is the booking lifecycle** (BR §1.1). Walk-ins auto-create an appointment row. Finalizing a visit sets `appointment.status='completed'`.
- **Mandatory vitals enforced at finalize, not at column level** — conditional CHECK, so drafts can autosave (BR §1.2).
- **Vitals stored as typed columns** with separate systolic/diastolic smallints and a stored temperature unit (BR §1.2).
- **Complaints/diagnosis stay free text**, with a `phrase_suggestion` corpus feeding typeahead (BR §1.3).
- **A finalized prescription is immutable**; edits create version *n+1* with `supersedes_prescription_id`, and a frozen `rendered_snapshot` including letterhead fields (BR §1.4). This is BR §10's "get right before the first migration" item.
- **Identity:** surrogate PK + printed `patient_code`; phone neither required nor unique; `phone_digits` normalized+indexed; `dob` + `dob_precision`; `patient_age_years_at_visit` snapshotted on the visit (BR §1.5).
- **Deletion:** hard delete only for zero-visit patients; archive otherwise; visits/prescriptions never deletable, only voided (BR §1.6).
- **Speed levers, in priority order:** drug autocomplete recalling the doctor's own last-used regimen, "repeat last Rx", personal-corpus diagnosis autocomplete (BR §2.4, §10). These are treated as critical path, not stretch.
- **Print:** `window.print()` + `@media print` for the daily prescription; QuestPDF server-side for exports/archival (BR §2.6, §3.9).
- **Stack:** ASP.NET Core Razor Pages + htmx + SQLite (WAL + FTS5) + EF Core, BR §3.2 option 1 — chosen because it is the only option that both meets the <2s first-paint budget with a 30–60KB server-rendered payload (BR §3.8 item 2) and keeps ops to one process and one file. Used consistently for every file path below. .NET 8 LTS.

**Assumptions I am planning against, because the BRD/BR leave them open and the plan cannot proceed without them:**

| # | BR §8 ref | Assumption | Why reasonable | Cost if wrong |
|---|---|---|---|---|
| A1 | Q1 hosting | **On-premise**: the app runs on the doctor's consulting-room PC or a ~$200 mini-PC on the clinic LAN; continuous encrypted replication offsite. | BR §3.3's recommendation; it is also the only answer that resolves the BRD's own "offline out of scope" vs. reliability contradiction (BR §7.3) without building sync. | Config + deploy only. App code identical for cloud (BR §3.3). Steps 35–37 change; steps 1–34 do not. |
| A2 | Q3 second user | **Single user, single credential row**, per BRD. But `created_by`/`updated_by` on every write, a real `app_user` table (never a hardcoded credential), and Registration + Vitals built as their own partials/endpoints so they could be permissioned later. `[hook]` | BRD is explicit; BR §9 warns the hooks cost ~0 and the retrofit is unbackfillable. | Without hooks, adding a second user later means an audit gap that cannot be backfilled. |
| A3 | Q11 vitals escape hatch | Vitals may be finalized as **"Unable to record — reason"** per-vital, which prints as "not recorded". | BR §2.3: a hard mandate with no escape hatch gets satisfied by reflexively typing `120/80`, which is a false clinical record. | **Needs the doctor's sign-off before Step 19 starts.** Schema impact is one nullable text column per vital group; behaviour impact is the finalize CHECK. Flagged as a real gate below. |
| A4 | Q12 units | Temperature unit is a **clinic-level setting** (°C or °F), stored per reading anyway. | BR §1.2 + §2.3.2 — a per-field dropdown is a per-consultation tax for a once-per-clinic decision. | Cheap: the stored-per-reading column already covers a later switch. |
| A5 | Q13 clinic day | All timestamps stored UTC; every daily list/filter uses a derived `clinic_date` with an explicit **04:00–04:00** boundary, configurable. Backdated entry allowed with an `entered_late` flag. | BR §5 "Date boundaries"; also BR §3.3.3's paper-fallback requirement. | Rework of every "today" query. Cheap now, expensive after export contracts ship. |
| A6 | Q6 paper | **A5 portrait default, A4 configurable, pre-printed-letterhead toggle.** | BR §2.6. | Setting, not code — provided the toggle exists from step 27. |
| A7 | Q14 script | Patient names and prescriptions are **Latin + one Indic script**; PDF font is **Noto Sans + Noto Sans Devanagari, embedded**, tested with real names on day one. | BR §5 "PDF font embedding" fails visibly and late. | Font swap only, *if* caught in the step-2 spike. If not caught, it surfaces after go-live on a printed medical document. |
| A8 | Q17 browser | Daily driver is **Chrome or Edge on Windows**; Safari is verified functionally but print alignment is validated only on the clinic's actual printer. | BR §8 Q17 — nobody has confirmed a Mac exists. | Extra print-testing pass only. |
| A9 | Q9/Q19 backup | Destination is **Backblaze B2 (or S3) with Object Lock**, encrypted before upload, key printed and stored in the clinic safe. Retention 7 daily / 4 weekly / 12 monthly / yearly. | BR §3.7. | **Needs an account + a payer named before Step 35.** Yearly-tier duration is a legal question (BR §3.7) — flag to stakeholder, do not assert. |
| A10 | Q18 support | A named developer/vendor with a written runbook + a one-command restore script on a card taped to the machine. | BR §5 operational table; BR §8 Q18. | Not code. Names must exist before go-live (Step 42). |

**Hard gates before the referenced step can start:** A3 (blocks Step 19), A9 (blocks Step 35), A10 (blocks Step 42). Q10 (does a digital patient list exist to import?) gates the optional Step 16 only.

**Targets this plan is designed against, replacing untestable BRD wording:** consultation record ≤150s with slack (BR §2.1); search p95 <500ms at 20k patients / 100k visits; first paint <2s cold; **RPO ≤5 min, RTO ≤4h** in place of "no data loss" (BR §3.7).

---

### Steps

#### Milestone 0 — Close decisions, retire risk (before any production code)

**1. Decision log — close BR §8 questions 1–8 and the gates above**
- What: a 45-minute session at the doctor's desk, with his pad and printer in the room (BR §10). Record each answer as a one-page ADR. Also confirm A1/A3/A5/A6/A9/A10 and Q10 (importable patient list?), Q19 (retention period — legal, ask, don't decide).
- Files: `docs/decisions/0001-hosting.md`, `0002-visit-vs-appointment.md`, `0003-single-user-and-hooks.md`, `0004-prescription-immutability.md`, `0005-age-and-dob.md`, `0006-print-stack-and-paper.md`, `0007-medication-entry.md`, `0008-patient-identity.md`, `0009-vitals-escape-hatch.md`, `0010-clinic-day-and-timezone.md`, `0011-backup-destination-and-retention.md`, `0012-support-and-restorer.md` (all new)
- Depends on: none
- Tests: **manual gate.** Checklist: each ADR has a decision, a date, and a named decider. Reviewer confirms no ADR says "TBD". No migration is written until 0002, 0004, 0005, 0008 are decided — BR §10 flags 0004 as the one whose retrofit is a rewrite.

**2. Spike — performance and print, thrown away afterward**
- What: throwaway Razor Pages app + SQLite seeded with 20,000 patients / 100,000 visits; one search page; one hardcoded A5 prescription printed on the clinic's actual printer and paper, with a real non-Latin patient name. Retires the entire perf question in a day (BR §10) and catches A7/A8 before they are expensive.
- Files: `spikes/perf-print-spike/` (new, deleted after step 3)
- Depends on: 1 (needs A6 paper size, A7 script)
- Tests: **manual + measured.** (a) name-substring search p95 <500ms on hardware comparable to the clinic's; (b) printed A5 aligns to the pad within 2mm with driver "fit to page" **off**, and again with it on — BR §2.6 names this the usual cause of a 4mm shift; (c) the non-Latin name renders with no tofu boxes; (d) cold first paint on the clinic LAN <2s. Record numbers in `docs/decisions/0013-spike-results.md`.

#### Milestone 1 — Scaffolding

**3. Solution, projects, CI**
- What: create the solution and four projects; pin .NET 8; add EditorConfig, Directory.Build.props, `.gitignore`; CI that builds, runs unit + integration tests, and fails on warnings.
- Files (new): `PatientManagement.sln`; `src/PatientManagement.Domain/PatientManagement.Domain.csproj`; `src/PatientManagement.Data/PatientManagement.Data.csproj`; `src/PatientManagement.Web/PatientManagement.Web.csproj`; `tests/PatientManagement.UnitTests/`, `tests/PatientManagement.IntegrationTests/`, `tests/PatientManagement.E2ETests/`; `.editorconfig`; `Directory.Build.props`; `.github/workflows/ci.yml` (or `azure-pipelines.yml`)
- Depends on: 1
- Tests: CI green on an empty solution. One trivial passing unit test proves the harness runs. **Do not** add a fourth project or an abstraction layer beyond these — BR §3.2 chose this stack for low ops burden.

**4. Web host baseline: layout, htmx, CSS, error handling, logging**
- What: `Program.cs` with Serilog (rotating file), global exception handler, `_Layout.cshtml` with the app shell (header, global search slot, footer slot), htmx 1.9 vendored locally (no CDN — it's a PHI app on a LAN that may have no internet), a small hand-written CSS with design tokens, and a `no-store` response header filter applied to every page.
- Files (new): `src/PatientManagement.Web/Program.cs`; `Pages/Shared/_Layout.cshtml`, `Pages/Shared/_ValidationScripts.cshtml`, `Pages/Error.cshtml(.cs)`; `wwwroot/css/app.css`, `wwwroot/css/print.css` (stub); `wwwroot/lib/htmx/htmx.min.js`; `Infrastructure/NoStoreHeaderFilter.cs`; `Infrastructure/SerilogConfig.cs`
- Depends on: 3
- Tests: integration — `GET /` returns `Cache-Control: no-store` (BR §3.5: without it, the back button after logout re-renders a patient record on a shared desktop); unhandled exception renders the error page and writes exactly one log line with a correlation id and **no** request query string.
- Privacy note: the log configuration here is where PHI leaks first (BR §3.9). Query strings must be excluded from request logging in this step, not retrofitted in step 39.

**5. Config, health endpoint, Server-Timing**
- What: strongly-typed options (`ClinicOptions`, `BackupOptions`, `PrintOptions`), `/healthz` returning DB reachability + migration version, and a middleware emitting a `Server-Timing` header logged per request. Makes the BRD's fuzzy <2s number measurable (BR §3.8).
- Files (new): `src/PatientManagement.Web/Infrastructure/ServerTimingMiddleware.cs`, `Infrastructure/Options/*.cs`, `Pages/Health.cshtml.cs`, `appsettings.json`, `appsettings.Development.json`
- Depends on: 4
- Tests: integration — `/healthz` returns 200 with the current migration id; every response carries `Server-Timing`; `/healthz` is reachable **without** auth while every other route is not (asserted again in step 11).

#### Milestone 2 — Data model

**6. Domain primitives (pure, no EF) — the parsing layer the whole UI leans on**
- What: `BloodPressure.TryParse` accepting `12080`, `120/80`, `120 80`, `120-80` → two smallints (BR §2.3.1, the single highest-value vitals decision); `Temperature.TryParse` handling `38,5` vs `38.5` locale forms (BR §5); `PhoneNumber.Normalize` → `phone_digits` stripped of `+`/spaces/dashes (BR §1.5); `PatientAge.FromDobOrAge` with `DobPrecision` and leap-year handling; `NameNormalizer` (lower + unaccent) for the generated search column; `FrequencyToken.Parse` for `1-0-1`, `1-1-1`, `BD`, `TDS`, `HS`, `SOS`, `STAT` → `frequency_code` + `frequency_text`; `DurationText.Format`; `ClinicDate.From(utcInstant, boundaryHour)`.
- Files (new): `src/PatientManagement.Domain/Values/BloodPressure.cs`, `Temperature.cs`, `PhoneNumber.cs`, `PatientAge.cs`, `DobPrecision.cs`, `NameNormalizer.cs`, `FrequencyToken.cs`, `DurationText.cs`, `ClinicDate.cs`
- Depends on: 3
- Tests: **unit, heavy — this is where BR §3.10 says 60–100 unit tests earn their keep.** BP: all four input shapes; `12/80` and `1200/80` rejected/flagged; `120/` partial. Temperature: `38,5`, `38.5`, `986` (decimal typo → plausibility flag, not an error), °C/°F round-trip. Phone: `+91 98765 43210` → `919876543210`; empty phone allowed; searching `9876` matches as substring. Age: DOB on 29 Feb; age-only input synthesizes DOB and marks `estimated_from_age`; age at a *past* visit date ≠ age today. ClinicDate: a visit saved at 00:10 belongs to the previous clinic day under a 04:00 boundary; a wrong system clock is out of scope but noted.

**7. Entities + DbContext + initial migration (the 10-table schema)**
- What: implement BR §1.8 exactly — `clinic_profile` (CHECK id=1), `app_user`, `patient`, `appointment`, `visit`, `prescription`, `prescription_item`, `medication`, `phrase_suggestion`, `audit_log`, plus `export_log` and `print_log`. Include the finalize CHECK on vitals (BR §1.2), `UNIQUE(visit_id) WHERE status='finalized'`, `merged_into_patient_id` nullable self-FK, `diagnosis_code` reserved-unused, `follow_up_after_days` (stored + printed, **no reminder**), `patient_age_years_at_visit`, `row_version`, `created_by`/`updated_by` `[hook]`, `archived_at`, `voided_at`, `entered_late`, and the vitals "unable to record" reason columns from A3.
- Files (new): `src/PatientManagement.Data/Entities/*.cs` (12 files), `src/PatientManagement.Data/AppDbContext.cs`, `src/PatientManagement.Data/Configurations/*.cs`, `src/PatientManagement.Data/Migrations/0001_Initial.cs`
- Depends on: 1 (ADRs 0002/0004/0005/0008/0009), 6
- Tests: integration against a real SQLite file — (a) inserting a `visit` with `status='completed'` and a NULL `bp_systolic` **fails** the CHECK; the same row with `status='in_progress'` **succeeds** (this is the autosave-vs-mandate contract from BR §1.2); (b) two `finalized` prescriptions on one visit violate the partial unique index, `finalized` + `superseded` does not; (c) `patient` with neither DOB nor age fails the CHECK; (d) a patient with no phone inserts fine; (e) two visits for the same patient on the same date both insert — BR §5 calls `unique(patient,date)` a natural-looking mistake that permanently breaks the X-ray-return case.

**8. Indexes, generated columns, FTS5**
- What: the BR §1.9 index set — btree on `phone_digits`; `(patient_id, visit_date DESC, id DESC)`; `(scheduled_date, scheduled_at)`; partial `(last_visit_at DESC)` on non-archived; plus an FTS5 virtual table + triggers over `patient.name_normalized` for substring/typo-tolerant name search. Add denormalized `patient.last_visit_at` and `visit_count` maintained on finalize.
- Files: `src/PatientManagement.Data/Migrations/0002_SearchIndexes.cs` (new), `src/PatientManagement.Data/Search/PatientSearchRepository.cs` (new — **the single place provider-specific SQL is allowed to live**, per BR §3.4)
- Depends on: 7
- Tests: integration — FTS5 returns `Ramesh Kumar` for `kumar` and for `rames`; `LIMIT 20` always applied; denormalized `last_visit_at` matches `MAX(visit_date)` after a finalize (property test over 200 seeded visits). Perf assertion deferred to step 38.

**9. Audit log via triggers**
- What: one generic trigger set writing `audit_log(table, row_pk, action, before jsonb, after jsonb, changed_fields, actor, at)` for `patient`, `visit`, `prescription`, `prescription_item` (BR §1.7 — ~50 lines, invisible to the UI, zero clicks on the consultation path).
- Files: `src/PatientManagement.Data/Migrations/0003_AuditTriggers.cs` (new), `src/PatientManagement.Data/Audit/AuditActorAccessor.cs` (new)
- Depends on: 7
- Tests: integration — an `UPDATE` to `visit.diagnosis_text` writes one audit row with correct before/after and `changed_fields=['diagnosis_text']`; a hard-deleted zero-visit patient still leaves an audit row (BR §1.6: "log it anyway"); audit writes add <5ms to a visit save (measured, because this sits on the consultation path).

**10. Seed data generator**
- What: a `--seed` CLI flag generating Bogus data at two sizes: `--seed small` (60 patients, 200 visits, for local dev) and `--seed perf` (20k patients / 100k visits, for step 38). Includes duplicate-ish names, patients with no phone, families sharing a phone, one 5,000-character complaint, and non-Latin names.
- Files (new): `src/PatientManagement.Data/Seeding/SeedRunner.cs`, `Seeding/BogusFactories.cs`; `tests/PatientManagement.IntegrationTests/Fixtures/SeededDbFixture.cs`
- Depends on: 8
- Tests: unit — the generator is deterministic under a fixed seed. **Policy assertion, enforced in CI:** the connection string used by seeding must never point at a production path. BR §3.10: never put real patient data on a dev machine; the corollary is never put fake data in production.

#### Milestone 3 — Auth and clinic profile (both are prerequisites for printing)

**11. Single-user auth: Argon2id, cookie, soft lock, recovery codes**
- What: one `app_user` row. Argon2id (19 MiB, t=2, p=1) via `Konscious.Security.Cryptography` — **not** ASP.NET Core Identity (BR §3.5). Cookie `HttpOnly; Secure; SameSite=Strict`, absolute lifetime 12h (one clinic day), no persistent remember-me. Idle 10 min → **soft lock overlay cleared by PIN**, not full password re-entry, rendered *over the intact form* so a session expiry never eats a draft (BR §2.7). A prominent **Lock** button. First-run setup wizard generates 10 printed one-time recovery codes. `autocomplete="off"` on the search field. Failed-attempt counter + `locked_until`.
- Files (new): `src/PatientManagement.Web/Pages/Account/Login.cshtml(.cs)`, `Pages/Account/Setup.cshtml(.cs)`, `Pages/Account/RecoveryCodes.cshtml(.cs)`, `Pages/Shared/_LockOverlay.cshtml`, `Auth/PasswordHasher.cs`, `Auth/RecoveryCodeService.cs`, `Auth/SessionPolicy.cs`, `Auth/PinLockService.cs`; `tools/reset-password/Program.cs` (local CLI backstop)
- Depends on: 5, 7
- Tests: unit — Argon2id verify round-trip; a recovery code is single-use and invalidated on redemption; lockout after N failures. Integration — every route except `/healthz`, `/Account/Login` returns 302 when unauthenticated; the login cookie carries all three flags; after 10 min idle the lock overlay appears but a `POST` autosave from the still-open form **still succeeds** for the grace window, then requires unlock (this is the BR §2.7 requirement that a screen lock mid-consultation must not lose work). Manual — `tools/reset-password` recovers a locked-out account on a machine with no email.
- Security note: password reset with no second user and no email is BR §8 Q15, a launch-blocking lockout risk. Printed codes + CLI is the decided answer; the codes go in the clinic safe **next to the backup encryption key** (step 35).

**12. Clinic profile / settings screen**
- What: the feature BR §4 gap #1 notes the BRD implies but never creates. Single-row editor: clinic name, address, phone, email, doctor name, qualifications, **medical registration number** (legally expected), specialty, logo upload, signature image upload, header/footer notes, paper size (A5/A4), pre-printed-letterhead toggle + reserved top margin, temperature unit, clinic-day boundary hour, timezone.
- Files (new): `src/PatientManagement.Web/Pages/Settings/Clinic.cshtml(.cs)`, `Pages/Settings/_LetterheadPreview.cshtml`, `Services/ClinicProfileService.cs`, `Services/ImageUploadValidator.cs`
- Depends on: 7, 11
- Tests: unit — image validator rejects non-image content types, >2MB files, and SVG (script vector). Integration — only one `clinic_profile` row can ever exist (CHECK id=1); changing the clinic phone number **does not** alter any existing `prescription.rendered_snapshot` (BR §1.4 — this is the test that proves the snapshot design works, and it must exist before step 27 ships). Manual — the letterhead preview matches the printed output from step 27.

#### Milestone 4 — Patients and search

**13. Patient quick-add (<10 seconds)**
- What: BR §6's floor requirement. One compact form: name, phone, age *or* DOB, gender required; everything else optional and collapsed. Age-only entry synthesizes DOB with `dob_precision='estimated_from_age'`. **Search-before-create duplicate prevention inside the form** (BR §5): as the name/phone is typed, a soft "3 similar patients — is this one of them?" strip appears with last-visit dates; **warn, never block**. Assigns `patient_code` (`P-000142`). Allergies/chronic conditions as two optional free-text fields (BR §4 gap #14 — a prescribing system with no allergy field is a patient-safety gap; a few hours' work).
- Files (new): `src/PatientManagement.Web/Pages/Patients/New.cshtml(.cs)`, `Pages/Patients/_DuplicateHint.cshtml`, `Services/PatientService.cs`, `Services/PatientCodeGenerator.cs`
- Depends on: 8, 11
- Tests: unit — code generator is gapless and collision-free under concurrent-ish calls. Integration — creating two patients with identical names succeeds but surfaces the hint; a patient with no phone saves; `phone_digits` is populated from a formatted input. Manual/E2E — **timed**: keyboard-only add from empty form to saved in <10s. Edge cases: name with a leading/trailing space; duplicate name + duplicate phone (family) both allowed; the duplicate hint must not fire a request per keystroke (debounced ≥200ms).

**14. Global search + recent patients**
- What: always-present search box, `/` to focus, ~200ms debounce, search-as-you-type via htmx, **no submit step** (BR §2.8 — this is what actually meets the 2–5s criterion). One field searches name (any word, substring) and phone (substring, so last-4 works). Result rows carry age/gender/phone/last-visit-date — the fix for duplicate names and the reason family-shared phones become a feature. Empty query renders recent patients, satisfying the BRD's "view recent patients" with no extra screen. Always `LIMIT 20`.
- Files (new): `src/PatientManagement.Web/Pages/Search/Index.cshtml(.cs)`, `Pages/Shared/_SearchBox.cshtml`, `Pages/Shared/_SearchResults.cshtml`; modified: `src/PatientManagement.Data/Search/PatientSearchRepository.cs`
- Depends on: 8, 13
- Tests: integration on the search repository (BR §3.10 names this the one place provider-specific SQL lives, so it gets the most integration coverage): `kumar` matches `Ramesh Kumar`; `9876` matches `+91 98765 43210`; `rames` matches `Ramesh` (FTS prefix); archived patients excluded by default; results capped at 20; a query of `'` or `%` doesn't error. E2E — `/` focuses the box from anywhere; empty box shows recent patients. **Findability check (BR §2.8's real requirement):** a manual list of 20 real-world name variants the doctor supplies must all resolve; one failed search is what kills trust.
- Privacy note: the search term goes in a **POST body, never a query string** (BR §3.9 #1) — otherwise `/search?q=Ramesh+Kumar` lands in web logs. Assert this in an integration test.

**15. Patient profile + edit + archive**
- What: profile and visit list on **one page** — BR §2.8 satisfies the BRD's "easy navigation between profile and visits" by there being no navigation. Edit form reuses step 13's fields. Delete rules per BR §1.6: hard delete only when `visit_count=0` (logged); otherwise the button reads **"Archive"** with a reason and is restorable in one click. Previous names retained and searchable on rename.
- Files (new): `src/PatientManagement.Web/Pages/Patients/Details.cshtml(.cs)`, `Pages/Patients/Edit.cshtml(.cs)`, `Pages/Patients/_ArchiveDialog.cshtml`
- Depends on: 13, 14
- Tests: integration — deleting a patient with ≥1 visit is refused at the service layer, not just hidden in the UI; archiving removes them from default search but `?includeArchived=true` finds them; restore is idempotent. Unit — rename keeps the old name searchable and does not mutate any `prescription.rendered_snapshot`.
- Privacy note: archive means PHI is retained indefinitely (BR §1.6). If "erase this patient" is ever required, it needs a separate purge path reaching audit rows, snapshots and backups — **out of Phase 1 scope, recorded as a known gap in `docs/decisions/0014-retention-gap.md`.**

**16. [Optional — gated on BR §8 Q10] One-off CSV importer**
- What: import an existing patient list (name, phone, age/DOB, gender) from Excel/contacts. BR §6 calls this the highest-ROI item not in the BRD (~1 day): it turns week one from "everyone is new" into "he found his patients". Dry-run preview, per-row error report, duplicate hint applied in bulk.
- Files (new): `src/PatientManagement.Web/Pages/Settings/Import.cshtml(.cs)`, `Services/PatientImportService.cs`
- Depends on: 13
- Tests: unit — rows with a missing name are rejected with a line number; phone normalization applied; UTF-8 BOM input handled; a 1,000-row file imports without partial commit (single transaction). Manual — import the doctor's real file **on his machine only**, never on a dev box.
- Skip this step entirely if the answer to Q10 is "no digital list exists" — then BR §6's zero-dev alternative (an assistant types 200 names over a weekend) applies instead.

#### Milestone 5 — The consultation loop (critical path; every step here is latency-scored)

**17. Today's-visits home screen**
- What: the home screen from BR §2.2, built **visits-only** first per BR §9's explicit note — same layout, same one-click rule, no scheduling yet. Rows: time, name, age/gender, phone, "last visit · diagnosis" recognition cue, and a right-hand action (`▸START` / `▸RESUME` / `✓ Done`). Draft rows visually distinct. Counts strip. `[+ Walk-in]` button. Date nav with `[Today]`, driven by `clinic_date` (A5).
- Files (new): `src/PatientManagement.Web/Pages/Index.cshtml(.cs)`, `Pages/Shared/_DayList.cshtml`, `Pages/Shared/_DayListRow.cshtml`, `Services/DayListService.cs`
- Depends on: 8, 14
- Tests: integration — the day list is **one query with no N+1** (assert query count = 1 via an EF interceptor; BR §3.8 #5 names N+1 in list/history loads as a top realistic perf breach); a visit created at 00:10 appears under the previous clinic day; a draft from yesterday still shows as resumable. **Latency note:** this screen is the doctor's first request every morning; it must render <2s cold (BR §3.8 #1) — asserted in step 38.

**18. Open consultation → draft visit created immediately, with history preloaded**
- What: clicking a row body opens the cockpit in **one click** — never row → profile → "New consultation" (BR §2.2). The `visit` row is inserted as `status='in_progress'` **at open, not at first save** (BR §2.7), so any interruption has something to attach to. `patient_age_years_at_visit` snapshotted here. The **last 3 visits ship in the same payload** as the patient record (BR §2.5) — expanding a history card must never hit the network. Second-visit-same-day prompts "continue this morning's visit or start a new one?" (BR §5). Walk-in auto-creates the appointment row (BR §1.1).
- Files (new): `src/PatientManagement.Web/Pages/Consultation/Index.cshtml(.cs)`, `Pages/Consultation/_HistoryRail.cshtml`, `Services/ConsultationService.cs`, `Services/VisitOpenModel.cs`
- Depends on: 7, 17
- Tests: integration — opening a consultation issues **exactly one** round trip and returns patient + last-3-visits + medication shortlist in that payload; a second open for the same patient/day returns the prompt rather than silently creating a duplicate; a walk-in creates both `visit` and `appointment(source='walk_in')` in one transaction. **Latency note: budget 2s list→cockpit (BR §2.1). Any additional fetch added here is a direct hit to the 150s budget — reject it.**

**19. Vitals zone** — *gated on A3 (BR §8 Q11) being decided*
- What: BR §2.3 in full. **A single BP field with auto-format** (`12080` / `120/80` / `120 80` → two ints) — one input, one tab stop, the highest-value vitals decision. `inputmode="decimal"` numerics, no spinners, **no per-field unit dropdown** (unit is the clinic setting from step 12). Quick-chips showing clinical normals *and this patient's last recorded value* — **never auto-prefill**, because a prefilled vital nobody looked at is a fabricated medical record. Inline validation only (red field + focus move), **never a blocking modal**. Plausibility warnings that don't block, **age-aware** (pulse 130 is normal in an infant). Per-vital "Unable to record — reason" per A3, printing as "not recorded".
- Files (new): `src/PatientManagement.Web/Pages/Consultation/_Vitals.cshtml`, `wwwroot/js/bp-field.js`, `wwwroot/js/vitals-chips.js`; modified: `src/PatientManagement.Domain/Values/BloodPressure.cs`
- Depends on: 6, 18, ADR 0009
- Tests: unit (step 6 parsers reused) plus component-level: chip click fills the field and is undoable by typing ("typing always wins"); no field is pre-populated on a fresh visit. E2E — three vitals entered keyboard-only in **≤12s** (BR §2.1 budget); finalize with an empty BP turns the field red and focuses it without a dialog; `temp 986` shows a soft warning and still allows save; `BP 12/80` warns; an infant's pulse of 130 does **not** warn.
- **Latency note:** the single BP field halves the tab stops on the field doctors dictate as one number. Do not split it back into two inputs for validation convenience.

**20. Complaints and diagnosis with personal-corpus autocomplete**
- What: two free-text areas (schema unchanged, per BR §1.3). Typeahead fed by `phrase_suggestion` ranked by `use_count` + recency, populated from the doctor's own past entries on every finalize. Recent-phrase chips under each field. A `[same as last visit ⤾]` action on complaints. Free text is length-capped (BR §5: a 5,000-character complaint destroys the print layout) — cap at 2,000 with a soft counter, not a hard truncate.
- Files (new): `src/PatientManagement.Web/Pages/Consultation/_Complaints.cshtml`, `_Diagnosis.cshtml`, `Services/PhraseSuggestionService.cs`, `wwwroot/js/typeahead.js`
- Depends on: 18
- Tests: unit — suggestion ranking is recency-weighted; a phrase used once doesn't outrank one used 40 times. Integration — finalizing a visit upserts `phrase_suggestion` rows for complaints, diagnosis and advice; suggestions are served from the **preloaded payload for the top 20**, hitting the network only beyond that. **Latency note: keystroke-to-suggestion must be <50ms for the preloaded set** — BR §7.10 says this unstated number, not the 2–5s search figure, is what actually matters.

**21. Autosave, drafts, and concurrency**
- What: debounced autosave every ~3s idle + on field blur, writing a partial visit (legal because vitals are only CHECKed at `completed`). A quiet `⟳ saved 10:03` in the header — **not a toast, not a spinner** (BR §2.7). `row_version` optimistic concurrency with a visible "this visit was changed in another tab" warning rather than silent last-write-wins (BR §5). A "Resume in-progress consultation" strip on the home screen. Autosave must survive the step-11 lock overlay.
- Files (new): `src/PatientManagement.Web/Pages/Consultation/Autosave.cshtml.cs` (htmx endpoint), `wwwroot/js/autosave.js`; modified: `Services/ConsultationService.cs`, `Pages/Shared/_DayList.cshtml`
- Depends on: 18, 19, 20
- Tests: integration — autosave with missing vitals succeeds (the whole point); autosave with a stale `row_version` returns a conflict, not an overwrite; killing the process mid-consultation and reopening restores every entered field. E2E — close the tab after typing complaints, reopen from the day list, complaints are intact. **Latency note: autosave is fire-and-forget and must never block typing or steal focus.** BR §6 lists "half-entered records" as the #2 cause of week-3 abandonment; drafts are the mitigation.

**22. Medication dictionary + drug autocomplete with last-used regimen** — *highest-value step in the plan*
- What: BR §2.4 item ★★★. A self-building `medication` table. Typing in the drug field suggests from the doctor's own corpus **and the dropdown row displays the regimen it will apply** ("Naproxen 250mg — you last used: 250mg, 1-0-1, 5d") so it is never a surprise. Selecting fills dosage/frequency/duration/instructions, all editable. Empty field shows the **top 15 by usage** before any typing (zero-keystroke selection, also the tablet path). Ships with a **seeded starter list of ~200 common drugs** and a merge/rename tool in Settings — BR §2.4 names corpus decay (`Paracetamol`/`Paracetmol`/`PCM`/`Calpol` as four entries) as a known, dated failure.
- Files (new): `src/PatientManagement.Web/Pages/Consultation/_DrugAutocomplete.cshtml`, `Services/MedicationSuggestService.cs`, `src/PatientManagement.Data/Seeding/CommonDrugs.cs`, `src/PatientManagement.Web/Pages/Settings/Medications.cshtml(.cs)` (merge/rename)
- Depends on: 7, 18
- Tests: unit — last-used regimen resolution picks the most recent *finalized* prescription, ignoring drafts and voided ones; fuzzy suggest matches `paracetmol` → `Paracetamol`. Integration — merging two medication rows repoints future suggestions but **leaves every historical `prescription_item.drug_name` untouched** (BR §1.4: the FK is for autocomplete, the text is the record). E2E — a 3-drug prescription entered via autocomplete in **≤35s** vs. a measured baseline of the same three typed manually.
- **Latency note: BR §2.4 scores this as ~30–40s saved on a 3-drug Rx — alone the difference between 3:00 and 2:00.** Suggestions must come from the preloaded top-15 with no network hit for the common case.

**23. Prescription grid**
- What: an inline row grid — `#`, drug, strength, dosage, frequency, duration, instructions, `✕`. **Enter adds a row; never a "+ Add medicine" modal** (BR §2.4 item ★: modals for repeated entry are the classic 3× time multiplier). Frequency is a coded token field accepting `1-0-1`/`BD`/`TDS`/`SOS`/`STAT`/`HS` with chips for the top 5 and free text fallback. Duration = number + unit chips (3/5/7). Instructions = chips (After food / Before food / Bedtime / Empty stomach) + free text. Each line stores **both** the literal printed text and the nullable `medication_id`.
- Files (new): `src/PatientManagement.Web/Pages/Consultation/_RxGrid.cshtml`, `_RxRow.cshtml`, `wwwroot/js/rx-grid.js`; modified: `src/PatientManagement.Domain/Values/FrequencyToken.cs`
- Depends on: 6, 22
- Tests: unit — frequency parsing for all supported tokens plus an unrecognized string preserved verbatim as `frequency_text`; row renumbering after a middle-row delete. E2E — the full tab order runs unbroken: Temp → BP → Pulse → Complaints → Diagnosis → Drug1 → Dose1 → Freq1 → Dur1 → Instr1 → Enter → Drug2 → … → Advice → Ctrl+Enter, **with no mouse, no modal and no scroll at 1280px**. BR §2.2 calls this tab order the product's spine; it is a regression test, not a one-time check.

**24. "Repeat last Rx" and per-card copy from history**
- What: BR §2.4 item ★★★. A `[repeat last Rx ⤾]` button on the Rx header and a `⤾ copy Rx` / `⤾ copy complaints` action on each history rail card (BR §2.5 — this is what makes history a *speed* feature, not a reading feature). Copied rows are **marked until touched**, and it is **never auto-applied**. Data is already in the step-18 payload, so cost is ~zero.
- Files: `src/PatientManagement.Web/Pages/Consultation/_RxGrid.cshtml`, `_HistoryRail.cshtml` (modified); `Services/RxCopyService.cs` (new)
- Depends on: 18, 23
- Tests: integration — copy pulls from the last **finalized** prescription, skipping drafts/voided; copying does not carry over the source `prescription_id`. E2E — the chronic-repeat case (BR §5): open → repeat last Rx → vitals → Save & Print in **≤45s**. This is arguably the highest-leverage feature for the BRD's success criterion.

**25. Finalize: Save & Print**
- What: `Ctrl+Enter` → validate vitals (CHECK-aligned, inline errors), write `prescription` v1 with `status='finalized'`, build and freeze `rendered_snapshot` **including the letterhead fields** (BR §1.4), set `visit.status='completed'` + `finalized_at`, set `appointment.status='completed'` (BR §1.1's single permitted derived transition — implicit, so the doctor never has to remember to mark someone done, per BR §2.2), upsert phrase suggestions and medication regimens, update `patient.last_visit_at`/`visit_count`, then hand off to the print view. All in one transaction.
- Files (new): `src/PatientManagement.Web/Pages/Consultation/Finalize.cshtml.cs`, `Services/PrescriptionFinalizer.cs`, `Services/RenderedSnapshotBuilder.cs`
- Depends on: 12, 19, 21, 23
- Tests: integration — finalizing with a missing pulse (and no "unable to record" reason) fails validation and leaves the visit `in_progress`; finalizing twice creates exactly one finalized prescription; the snapshot contains clinic name/address/reg-no/doctor name/signature ref and is unaffected by later Settings edits (paired with step 12's test); the appointment flips to `completed`. E2E — the headline timed test: **open → vitals → complaints → diagnosis → 3 meds via autocomplete → Save & Print, keyboard-only, ≤150s**, run on the clinic-comparable hardware. This is the BRD success criterion turned into a gate.

**26. [Stretch] Named prescription templates**
- What: BR §2.4 item ★★ — "URI 5-day", "Gastritis" collapse a 3-drug Rx from ~50s to ~5s. Applied rows are **marked as needing review** (the stated risk is stale templates applied without reading).
- Files (new): `src/PatientManagement.Web/Pages/Settings/Templates.cshtml(.cs)`, `Pages/Consultation/_TemplatePicker.cshtml`, `Services/PrescriptionTemplateService.cs`
- Depends on: 23
- Tests: unit — applying a template appends rather than replacing existing rows. E2E — applied rows carry the review marker until edited. **Cut this first if time is short; steps 22 and 24 deliver most of the benefit.**

#### Milestone 6 — Printing

**27. Printable prescription (browser print)**
- What: BR §2.6 layout exactly. A dedicated print route rendering **from `rendered_snapshot`, not from live tables**. `@page { size: A5 portrait; margin: 10mm }` with A4 configurable; `break-inside: avoid` per medication row; `<thead>` repeat for multi-page with "Page 1 of 2" (BR §5: 8 medicines overflow A5); ~25mm of real whitespace for a pen signature; pre-printed-letterhead toggle suppressing header/footer and reserving margin instead. Rx number `#2026-1042-07` and printed-at timestamp for traceability. **Reprint is byte-identical with no "DUPLICATE" watermark** (a stamp can cause a pharmacist to refuse it) but **every print event is logged**.
- Files (new): `src/PatientManagement.Web/Pages/Prescriptions/Print.cshtml(.cs)`, `Pages/Prescriptions/_Letterhead.cshtml`, `_RxBody.cshtml`; modified: `wwwroot/css/print.css`; new `Services/PrintLogService.cs`
- Depends on: 12, 25
- Tests: integration — the print view for a visit whose patient was later renamed and whose clinic phone later changed renders the **original** name and phone (BR §5's reprint-weeks-later case); the print route writes one `print_log` row per render. Manual, **on the real printer and real paper, not print preview** (BR §2.6): A5 alignment; A4 fallback; letterhead-toggle output on the doctor's stationery with no double header; an 8-medicine Rx across two pages with a repeated header; a non-Latin name; browser-injected headers/footers disabled per the setup checklist.

**28. Amendment (version n+1) and void**
- What: BR §1.4/§1.7 — editing a finalized prescription creates v2 with `supersedes_prescription_id` and a `revision_reason`; the old row flips to `superseded`; the printed v2 carries a visible "Revised — supersedes #…07" line and its own Rx number. Visit text stays freely editable while in progress or within a grace window (end of clinic day); after that, saves stamp `amended_at` + `amendment_note`. Void-with-reason for a wrongly-issued prescription or a wrong-patient visit, **visible in both patients' histories**, never silently reassigned (BR §5).
- Files (new): `src/PatientManagement.Web/Pages/Prescriptions/Amend.cshtml(.cs)`, `Pages/Visits/Void.cshtml(.cs)`; modified: `Services/PrescriptionFinalizer.cs`, `Services/ConsultationService.cs`
- Depends on: 9, 27
- Tests: integration — amending produces exactly two prescription rows (`superseded` + `finalized`) and never violates the partial unique index; the v1 snapshot is byte-identical before and after amendment; a visit edited after the grace window writes `amended_at` + an audit row, one edited before it does not. Manual — the 8pm rash case from BR §5: patient calls, antibiotic swapped, v2 prints with the supersedes line, v1 still reprintable.
- **Latency note: zero cost on the consultation path** — amendment is a separate route, and the grace window means same-day corrections stay a plain UPDATE with no ceremony (BR §1.7).

**29. Test-print page and printer setup checklist**
- What: a permanent `Settings → Test print` page rendering a calibration sheet with the real letterhead and a millimetre ruler, plus a written one-time setup checklist (paper size, margins, headers/footers off, "fit to page" off, default printer). BR §5 lists browser printer setup as a top operational risk, and BR §6 lists a day-two printer jam as a week-3 abandonment cause.
- Files (new): `src/PatientManagement.Web/Pages/Settings/TestPrint.cshtml(.cs)`, `docs/runbooks/printer-setup.md`
- Depends on: 27
- Tests: manual — a Chrome update or driver change is detected by re-running this page; the checklist is verified against the clinic's actual printer **before go-live**, per BR §5.

#### Milestone 7 — History

**30. Patient visit history**
- What: BRD "Patient History". Reverse-chronological visit list on the patient page (step 15) plus a full history route with a **date-range filter**. Each row expands to vitals, complaints, diagnosis, advice, and the medication list; each finalized visit links to reprint (step 27) and amend (step 28). Voided visits shown struck through with their reason. Paginated — never load an unbounded history.
- Files (new): `src/PatientManagement.Web/Pages/Patients/History.cshtml(.cs)`, `Pages/Visits/Details.cshtml(.cs)`, `Pages/Shared/_VisitCard.cshtml`; modified: `Services/VisitQueryService.cs`
- Depends on: 8, 15, 25
- Tests: integration — history uses `.Include()` and issues a bounded query count regardless of visit count (BR §3.8 #5); the `(patient_id, visit_date DESC, id DESC)` index is used (assert via query plan); the date filter is inclusive at both ends and respects the clinic-day boundary; two visits on the same date both appear in a stable order. E2E — find a patient → open history → filter to last 6 months → open a visit → reprint.

#### Milestone 8 — Appointments (BRD Phase 1; BR §9 sequences it after the consultation loop)

**31. Appointment scheduling and status lifecycle**
- What: schedule form (patient, date, time, duration, reason); statuses `scheduled / arrived / completed / cancelled / no_show`; **only Cancelled and No-show are manual** — Completed fires implicitly on Save & Print (BR §2.2). Double-booking allowed with a warning, not blocked (a solo GP overbooks deliberately). **Auto-lapse:** yesterday's still-`scheduled` rows flip to `no_show` at the clinic-day boundary, or the list rots (BR §4 gap #15).
- Files (new): `src/PatientManagement.Web/Pages/Appointments/New.cshtml(.cs)`, `Pages/Appointments/_StatusMenu.cshtml`, `Services/AppointmentService.cs`, `Services/DayRolloverJob.cs` (hosted service)
- Depends on: 7, 17
- Tests: unit — the rollover job is idempotent and only touches rows strictly before today's clinic date; a walk-in appointment is never auto-lapsed. Integration — cancelling an appointment whose visit is already finalized is refused; scheduling for a past date is allowed (backdated entry, A5) and flagged `entered_late`. Edge case (BR §5): a patient books, walks in early as a walk-in, and the booked row must not become a phantom — the walk-in flow reuses today's scheduled appointment for that patient if one exists.

**32. Daily appointment list = the home screen, upgraded**
- What: layer scheduling onto step 17's screen — time column populated from appointments, the counts strip (`18 scheduled · 7 done · 1 no-show`), `[+ Schedule]`, per-row status dropdown, day navigation. BR §9's note holds: the layout does not change, only the data source widens.
- Files: `src/PatientManagement.Web/Pages/Index.cshtml(.cs)`, `Pages/Shared/_DayList.cshtml`, `Services/DayListService.cs` (all modified)
- Depends on: 17, 31
- Tests: integration — the list stays a **single indexed query** over `(scheduled_date, scheduled_at)` after the merge (this is the payoff for BR §1.1's walk-ins-auto-create-an-appointment decision; assert query count is unchanged from step 17). E2E — schedule → arrive → consult → the row shows `✓ Done` with no manual status change.

#### Milestone 9 — Export

**33. CSV export**
- What: BRD "Data Export". Contract (BR §3.9, since the BRD doesn't define one): `patients.csv`, `visits.csv` (vitals flattened to columns, medicines `;`-joined), `medications.csv` keyed by visit — joinable in Excel via the stable business keys `patient_code` and `visit_code`, **never bare surrogate PKs**. **UTF-8 with BOM** or Excel mangles every non-ASCII name. **CSV-injection guard**: any cell starting with `= + - @` is prefixed with `'` — complaints and diagnosis are free text, so this is a real vulnerability, not a theoretical one. Phone numbers and dates quoted and prefixed against Excel's mangling. Scope selector (date range / single patient), **no default "export everything" button**, one-line confirmation dialog, and every export written to `export_log` (timestamp, type, filter, row count).
- Files (new): `src/PatientManagement.Web/Pages/Export/Index.cshtml(.cs)`, `Pages/Export/Csv.cshtml.cs`, `src/PatientManagement.Domain/Export/CsvWriter.cs`, `Export/CsvInjectionGuard.cs`, `Services/ExportLogService.cs`
- Depends on: 25, 30
- Tests: unit — injection guard covers `=`, `+`, `-`, `@`, tab and CR leading characters; embedded quotes, commas and newlines in a diagnosis round-trip correctly; BOM present; `+91…` and a leading-zero phone survive an Excel round-trip; a `;`-joined medicine list containing a `;` in the drug name is escaped. Integration — every export writes exactly one `export_log` row; the patient/visit files rejoin on `patient_code` with zero orphans. Manual — open all three files in Excel on Windows with a real non-Latin name.
- **Privacy note (BR §3.9 #2, and BRD contradiction §7.6):** a CSV in `Downloads` is a complete unencrypted health extract that contradicts the encryption-at-rest requirement and stays there forever. **Friction is correct here, unlike on the consultation path** — keep the confirmation dialog, keep the scope selector, keep the log, and surface the export log in Settings.

**34. PDF export (QuestPDF)**
- What: server-side PDF for the export requirement and the archival prescription copy — **QuestPDF, not headless Chromium** (300MB of dependency whose classic failure is missing fonts on non-Latin names), not iText (AGPL), not `html2canvas` (raster output is unacceptable for a medical document). Two documents: a patient summary (profile + visit history) and an archival single-prescription PDF rendered from `rendered_snapshot`. Fonts embedded per A7.
- Files (new): `src/PatientManagement.Web/Pages/Export/Pdf.cshtml.cs`, `src/PatientManagement.Domain/Export/PatientSummaryDocument.cs`, `Export/PrescriptionDocument.cs`, `Export/PdfFonts.cs`; assets under `src/PatientManagement.Web/wwwroot/fonts/`
- Depends on: 27, 33
- Tests: unit — the PDF renders a non-Latin name with **no tofu boxes** (assert by extracting text from the generated PDF, not by eyeballing); text is selectable/searchable, proving it is not a raster; a 40-visit history paginates with a repeated header. Integration — generating the archival PDF for a visit whose clinic details later changed reproduces the original letterhead. **Perf note:** PDF generation is CPU-spiky (BR §3.8 #4); it runs off the consultation path only, and the endpoint is rate-limited to one concurrent render.

#### Milestone 10 — Non-functional hardening

**35. Backups: Litestream + nightly encrypted dump + visible status** — *gated on A9*
- What: BR §3.7's 3-2-1, concretely: live SQLite on the box → Litestream streaming the WAL continuously to B2/S3 (RPO in seconds) → a nightly dump encrypted with `age`/`restic` to a second local disk. **Object Lock / versioning on the bucket** — the realistic ransomware mode is a compromised box deleting its own backups, and immutable retention is the only counter (free, one setting). Encrypt **before** upload with a key printed and stored in the clinic safe next to the step-11 recovery codes. Retention 7/4/12/yearly. A **"last successful backup: 3 minutes ago" line in the UI footer** — BR §3.7 calls this the only way the doctor will ever notice replication silently stopped two months ago.
- Files (new): `ops/litestream.yml`, `ops/backup-nightly.ps1`, `src/PatientManagement.Web/Services/BackupStatusService.cs`, `Pages/Shared/_BackupStatus.cshtml`, `docs/runbooks/backup.md`
- Depends on: 7, 11, A9 decided
- Tests: integration — the footer reads from the actual replication timestamp and turns red past a 15-minute threshold (not a hardcoded "OK"). Manual — kill the network for 10 minutes and confirm the footer goes red and recovers; confirm Object Lock rejects a delete attempt from the box's own credentials.
- Security note: the backup encryption key and the auth recovery codes are the two secrets whose loss is unrecoverable. Both are printed, both live in the clinic safe, both are named in `docs/runbooks/backup.md`.

**36. One-command restore + automated monthly restore test (= the only staging environment)**
- What: BR §3.7's two highest-value items. A single restore script (also printed on a card taped to the machine). A scheduled job that pulls the latest backup into a scratch container, applies migrations, and asserts: row counts non-zero, most recent visit within 48h, a known seeded canary record readable, and a prescription snapshot renders. Reports pass/fail somewhere a human reads. Per BR §3.10, **staging *is* the restore drill** — ephemeral, monthly, destroyed after; no permanent staging environment for a one-doctor app.
- Files (new): `ops/restore.ps1`, `ops/restore-test.ps1`, `tools/restore-check/Program.cs`, `.github/workflows/restore-test.yml`, `docs/runbooks/restore.md`
- Depends on: 35
- Tests: **this step is itself the test — and BR §3.7 calls it the most important "test" in the project.** Verify by deliberately corrupting a scratch copy and confirming the check **fails loudly**; a restore test that can't fail proves nothing. Manual: a timed full restore drill against **RTO ≤4h**, performed by the person named in ADR 0012, not by the developer who wrote it.

**37. Encryption in transit and at rest**
- What: per A1 (on-prem). At rest: **BitLocker with TPM on the data drive** — the single most important on-prem control, because the realistic threat is the PC being stolen from the clinic. In transit: loopback if the app runs on the doctor's own PC (browsers treat `localhost` as a secure context, so `Secure` cookies still work); for a separate LAN box, either `mkcert` with the local CA trusted once, or a real Let's Encrypt cert via **DNS-01** with an A record pointing at the private IP — publicly-trusted, auto-renewing, no inbound internet. HSTS + 80→443 redirect where TLS is terminated. **Column-level PHI encryption is explicitly rejected for Phase 1** (BR §3.6): it breaks search and CSV export and creates key management with no admin.
- Files (new): `ops/tls-setup.md`, `ops/bitlocker-checklist.md`, `docs/decisions/0015-encryption-interpretation.md`; modified: `src/PatientManagement.Web/Program.cs` (HSTS, redirect)
- Depends on: 11, 35
- Tests: manual/verified — BitLocker reports the data volume as encrypted with the recovery key escrowed (printed, clinic safe); the browser shows no certificate warning on the clinic PC; cookies carry `Secure` even on loopback. Integration — HSTS header present when TLS is on.
- **Named conflict, recorded in ADR 0015 (BR §3.6):** "encryption at rest" and the search target collide — column-level encryption on `full_name`/`phone` destroys the trigram/prefix indexes search depends on. The requirement is being interpreted as **disk-level encryption**. This is a documented interpretation of a BRD requirement, not a silent omission.

**38. Performance gate in CI**
- What: turn the BRD's fuzzy numbers into a build gate. Seed 20k patients / 100k visits (step 10), assert **search p95 <500ms**, day-list render <300ms, consultation-open <500ms, and a query-count ceiling per page via an EF interceptor (N+1 guard). Log `Server-Timing` in production and surface the slowest 10 routes in Settings.
- Files (new): `tests/PatientManagement.IntegrationTests/Performance/SearchPerfTests.cs`, `Performance/QueryCountTests.cs`, `tests/PatientManagement.IntegrationTests/Fixtures/PerfSeedFixture.cs`
- Depends on: 10, 14, 17, 18, 30
- Tests: the step is the test. Also assert the **cold-start** path (BR §3.8 #1: the doctor's first patient of the day hits it every day and forms his entire opinion of the product) — on-prem this means the service is set to always-running with `Restart=always`, verified by a reboot test in step 42.

**39. PHI leak review: logs, third parties, browser**
- What: a single hardening pass over BR §3.9's ranked list. Serilog destructuring policy redacting `full_name`, `phone*`, `complaints_text`, `diagnosis_text`, `advice_text`, `drug_name` **at the logger**, not at every call site; 30-day log retention; log `patient_id` integers only. A CI check that fails the build if `EnableSensitiveDataLogging` appears outside a `#if DEBUG` — it writes patient names and diagnoses into logs. **Ship zero analytics** (BR §3.9 #4). `autocomplete="off"` audit on all PHI inputs. Confirm `no-store` on every PHI page (step 4) still holds.
- Files (new): `src/PatientManagement.Web/Infrastructure/PhiRedactionPolicy.cs`, `.github/workflows/phi-guard.yml`, `docs/runbooks/phi-checklist.md`; modified: `Infrastructure/SerilogConfig.cs`
- Depends on: 4, 14, 33
- Tests: integration — a request to search with the term "Ramesh" produces **no log line containing "Ramesh"**; an exception thrown while rendering a visit logs the visit id but not the diagnosis text. CI — the sensitive-logging guard fails a deliberately-added violation.

**40. E2E suite (Playwright)**
- What: BR §3.10's 4–6 journeys, plus the timed ones already specified above: (1) login → search → consult → print; (2) register a patient with the duplicate hint; (3) walk-in with no appointment; (4) export CSV; (5) history + date filter + reprint; (6) interrupted consultation → resume draft. Run against Chromium and WebKit (A8).
- Files (new): `tests/PatientManagement.E2ETests/Journeys/*.cs`, `Fixtures/AppFixture.cs`, `playwright.config.ts` (or the .NET equivalent)
- Depends on: 25, 27, 30, 32, 33
- Tests: the step is the test. **The two that must never be allowed to go red:** the ≤150s consultation journey (step 25) and the print-fidelity/reprint journey (step 27) — BR §9's "never cut" list is search quality, printing, draft autosave, backups + one verified restore, and the append-only stance on finalized records.

**41. [Optional — not a BRD requirement] Adoption metric**
- What: one number in Settings — **% of the day's patients with a completed visit record** (BR §6, ~2 hours). Explicitly *not* "advanced analytics"; it is a single ratio with no charts, no cohorts, no export. Flagged optional precisely because the BRD excludes analytics; include only with stakeholder sign-off.
- Files (new): `src/PatientManagement.Web/Pages/Settings/Adoption.cshtml(.cs)`
- Depends on: 25, 32
- Tests: unit — the ratio counts finalized visits over day-list rows and excludes cancelled appointments.

**42. Go-live: runbooks, parallel run, support** — *gated on A10*
- What: not code. Reboot test (service comes back unattended). Printer checklist run on the real device (step 29). Restore drill executed by the named restorer (step 36). Recovery codes and the backup key printed and placed in the safe. **Two-week parallel run** with the printed Rx becoming the doctor's retained copy, replacing the carbon pad (BR §6). **Someone sits beside the doctor for his first two clinics** — BR §6: "minimal training required" is a design goal, not a launch plan. Blank pads kept as the paper fallback, with backdated entry (A5) so yesterday's paper notes key in at ~30s each.
- Files (new): `docs/runbooks/go-live.md`, `docs/runbooks/daily-operations.md`, `docs/runbooks/paper-fallback.md`
- Depends on: 29, 36, 37, 40, A10 decided
- Tests: manual checklist, each item initialled: reboot survived; restore drill timed under RTO; test print correct on real paper; backup footer green; a named support contact and channel exist. BR §5 calls "no on-site support" the most common cause of a working app being abandoned — it is an adoption risk wearing an ops costume.

---

### Sequencing rationale

**Steps 1–2 come before any production code** because BR §8's top six questions are schema- or topology-level, and BR §10 singles out prescription mutability as the one decision whose retrofit is a rewrite rather than a patch. The spike costs a day and retires the entire performance question plus the two failure modes (print misalignment, font tofu) that otherwise surface after go-live on a printed medical document.

**Steps 6–10 (data model) unblock literally everything else**, and the two constraints inside them are load-bearing in opposite directions: the conditional vitals CHECK is what makes draft autosave legal (step 21), and the partial unique index on finalized prescriptions is what makes amendment safe (step 28). Both are cheap now and migrations later.

**Steps 11–12 sit before patients** not for security theatre but because **printing depends on a clinic profile that no BRD requirement creates** (BR §4 gap #1). Building the print layer against a hardcoded letterhead and retrofitting settings would invalidate every stored snapshot.

**Milestone 5 is the critical path for the 2–3 minute target and is ordered by BR §2.1's time budget, not by data-model tidiness.** Prescription entry is ~45% of the budget and ~70% of the input events, so steps 22–24 (drug autocomplete with last-used regimen, the Enter-to-add-row grid, repeat-last-Rx) are scheduled as core work, not stretch. BR §10 is explicit: if only three features are built well, they are those plus diagnosis autocomplete (step 20). A plan that ships vitals polish before step 22 has optimized the wrong thing.

**Within Milestone 5, step 18 (single-payload open with preloaded history) precedes every UI step** because it establishes the no-extra-round-trip contract. Once the history rail, phrase suggestions and drug shortlist all ship in the open payload, later steps physically cannot add a network hop without visibly breaking that step's own test.

**Printing (27) follows finalize (25) rather than preceding it** because the print view renders from `rendered_snapshot`, and the snapshot cannot be designed before the finalize transaction exists. Amendment (28) follows printing because "revised, supersedes #…" is meaningless before there is a printed artefact to supersede.

**Appointments (31–32) come after the whole consultation loop** on BR §9's reasoning: a solo GP's real workflow is a walk-in queue, the daily-appointment list is the least-used headline feature, and step 17 already delivers the home screen the BRD's daily list needs — appointments only widen its data source. This is why step 17 is built visits-only and deliberately, rather than discovering the split later.

**Export (33–34) comes after history** because the CSV contract needs stable business keys and a settled visit shape, and because it is a month-3 need (BR §9). It is also first on BR §9's cut list if the schedule slips — print/PDF is what replaces paper, CSV is not.

**Milestone 10 is last in build order but not in importance.** Backups (35) and the restore test (36) are on BR §9's "never cut at any deadline" list, alongside search quality, printing, draft autosave and the append-only stance. They sit late only because they need a real schema and real data to protect; they are hard gates for go-live, not nice-to-haves. Step 36 doubles as the only staging environment, which is why no permanent staging appears anywhere in this plan.

**Safe to defer past Phase 1, in this order if time is short** (BR §9's cut order): prescription templates (26) → CSV export (33) → appointment scheduling (31–32, keeping step 17's today-list) → history date filters (30, keeping reverse-chronological) → patient profile editing beyond name/phone/age (15) → the adoption metric (41) → the CSV importer (16, if no digital list exists).

---

### Deferred / Phase 2+

Named so none of it is silently dropped, and none of it is smuggled into Phase 1:

- **Receptionist / multi-user access** (BRD out-of-scope; BR §7.1 flags that excluding it puts vitals on the doctor's own critical path and directly undermines the 2–3 minute criterion — a real contradiction in the BRD, raised here, not resolved unilaterally). Phase 1 keeps only the `[hook]`s from A2: a real users table, `created_by`/`updated_by`, role checks behind one function that returns "doctor", and Registration/Vitals as separate partials.
- **Duplicate merge** (BR §1.5 Phase 1.5). Phase 1 ships prevention (search-before-create) and the reserved `merged_into_patient_id` column only.
- **Follow-up reminders** (BRD out-of-scope; BR §7.4). Phase 1 stores and prints `follow_up_after_days` with **no notification, no due list**.
- **Fee capture / billing / invoicing** (BRD out-of-scope). Omitted entirely, including the schema hook — BR §9 itself names it the strongest scope-creep magnet on its list.
- **Lab report attachments** (BR §8 Q16). Architecturally intrusive to add later; Phase 1 decides nothing, which is a knowingly accepted cost.
- **Sick-leave / fitness certificates** (BR §5 — expect the request the moment printing works). Mitigated cheaply by keeping the print layer template-driven in step 27 rather than one hardcoded page.
- **Sharing the Rx via WhatsApp/email** — step 34's real PDF file is the hook; no sharing UI in Phase 1.
- **Simple reports** (patients/day, common diagnoses). Free-text diagnosis makes this permanently hard; the reserved `diagnosis_code` column keeps the door open at zero cost.
- **Second doctor / second location** — deliberately **not** hooked. BR §9 suggests a defaulted `clinic_id`, but the BRD says single-clinic; adding the column is premature multi-tenancy for a single doctor and is left out.
- **Offline functionality** (BRD out-of-scope). Answered at the system level instead, per BR §3.3: local hosting (A1), an optional 4G failover router, and the paper-fallback path in step 42 — roughly one day of work versus 4–8 weeks of sync and a permanent stream of conflict bugs.
- **Genuine PHI purge** (erase-on-request / retention enforcement). Soft delete means PHI is retained indefinitely; a real purge must reach audit rows, snapshots, exports and backups. Recorded as a **known gap** in `docs/decisions/0014-retention-gap.md`, with the yearly-retention duration flagged to the stakeholder as a legal question (BR §3.7), not answered here.
- **WebAuthn / Windows Hello login** (BR §3.5) — a good fit for "minimal training" and near-mandatory if the app is ever internet-exposed, but unnecessary on a LAN where the threat is someone physically in the room.
- **Column-level PHI encryption** — rejected for Phase 1 with reasoning recorded in ADR 0015, because it breaks search and export and creates key management with no admin.

**Estimated effort**, per BR §3.2's 5–7 weeks for this stack, spread as: Milestone 0 ~2 days; M1–M2 ~1 week; M3–M4 ~1 week; **M5 ~1.5–2 weeks (the largest block, correctly)**; M6 ~4 days; M7 ~2 days; M8 ~4 days; M9 ~4 days; M10 ~1 week including drills. Milestones 0–7 alone constitute a shippable "replace the pad" release (BR §9's MVP) if the schedule compresses.
