# Verification-Readiness Review — `BRD/Doc_BRD.md`

**Author:** `verification-brd`, per `.claude/agents/verification-brd.md`
**Date:** 2026-08-19
**Branch:** `brd/review-report` (this session's working directory is already the isolated worktree; nothing touches `main`)
**Reviews:** `BRD/Doc_BRD.md` @ blob `212cddc`
**Grounded in:** `docs/worktree-brd-review.md` @ commit `7c8e0ea` (its Requirement Analysis Matrix, Contradiction Report, Healthcare/Architecture Completeness Reports, and Testability Review), `docs/implementation-brd-review.md` @ commit `3f811bd` (build-readiness), and `docs/plan-brd-review.md` (the plan `verification-brd`'s own instructions call "the plan being implemented").
**Scope:** assessment only. No application code, no tests, no BRD edits, no edits to any agent configuration file.

**Refreshed 2026-08-20 (fourth pass)** against one more stakeholder decision: **"recent patients" is confirmed ordered by most-recent visit date**, not registration date. Row 14 moves from "not testable as worded" to "testable now" — the ranking ambiguity that made it untestable is gone. (Third-pass decisions — emergency contact, no medical/surgical history — and earlier passes — walk-in support, double booking, consultation-timing interpretation, no password reset, contains-search, doctor-entered slot duration — remain reflected as before.)

---

## Why this isn't a PASS/FAIL run

`verification-brd`'s actual job is to run test suites and check output against `docs/implementation-progress.md`'s most-recent `Done` step. My own first-instruction rule is explicit: *"If nothing is marked `Done`, say so and stop; there's nothing to verify."* `docs/implementation-progress.md` does not exist in this repo — the repository has no `angular.json`, no `*.csproj`/`*.sln`, no test project, and no application code. That check was rerun for this document and confirmed: there is nothing to run.

What follows instead is the question my own test-strategy section and hard gates put me in a position to answer today, before any code exists: **once something is built, can I actually prove it correct against the BRD — and what exactly would that test assert?** This mirrors the prior verification-readiness pass in this repo's history (`git show b31eaab`), regenerated against the current BRD (unchanged) and the current, substantially more complete fixed specs in `.claude/agents/implementation-brd.md` and hard gates in `.claude/agents/verification-brd.md`.

The comparison that matters:

| | Prior pass (`b31eaab`) | Initial current-BRD pass | After 1st refresh | After 2nd refresh | After 3rd refresh | After 4th refresh |
|---|---|---|---|---|---|---|
| Testable now | 14 of 35 (40%) | 16 of 33 (48%) | 19 of 33 (58%) | 20 of 33 (61%) | 20 of 33 (61%) | **21 of 33 (64%)** |
| Harness gap | 3 of 35 (9%) | 4 of 33 (12%) | 4 of 33 (12%) | 4 of 33 (12%) | 4 of 33 (12%) | **4 of 33 (12%)** |
| Not testable as worded | 18 of 35 (51%) | 13 of 33 (39%) | 10 of 33 (30%) | 9 of 33 (27%) | 9 of 33 (27%) | **8 of 33 (24%)** |
| Testable using the BRD's own text alone (no fixed-spec/agent-decision assist) | ~7 of 35 (20%) | ~9 of 33 (27%) | ~10 of 33 (30%) | ~10 of 33 (30%) | ~10 of 33 (30%) | **~10 of 33 (30%)** |

(Row count dropped from 35 to 33 because this pass reuses `docs/worktree-brd-review.md`'s 32-row Requirement Analysis Matrix plus one Exclusions row, rather than re-deriving a separate count — the two documents should stay comparable, not diverge on how they count the same BRD.)

**The gap between "testable now" and "testable using the BRD's own text" is still the most important number in this document, and it hasn't closed.** Verification-readiness improved because `implementation-brd.md`'s fixed specs got more complete, not because the BRD itself got easier to test. A test written today against the fixed Vitals spec, the fixed Export spec, or the fixed Prescription spec can go green against decisions the BRD never actually states — `verification-brd`'s own rule 4 (*"a green suite that doesn't test the right thing is a false pass"*) is exactly the defence this document exists to keep sharp.

---

## Freshness check

- `git hash-object BRD/Doc_BRD.md` → `212cddc705dc0e2f960ca07c14793896caf54571` — matches what `docs/worktree-brd-review.md` states it reviewed. **BRD is unchanged.**
- `docs/worktree-brd-review.md` was committed at `7c8e0ea`, after the BRD's last commit (`5d512ca`) — current, not stale.
- **The Vitals, Prescription, and Export fixed specs in `.claude/agents/implementation-brd.md` have all changed materially since the prior verification pass** and were re-read in full before writing this: vitals are now mandatory *at entry* (not left ambiguous between entry/finalize), stored in °C with systolic/diastolic as separate columns and weight at `decimal(6,3)`; `DoctorDetails` now exists with a named field set and a snapshot-at-creation rule for prescriptions; `PatientId` is now a defined 0-based sequential identity. None of this existed in this form at the prior pass.
- **The hard-gate checklist in `.claude/agents/verification-brd.md` was checked against the current Export/Auth fixed specs for staleness — it is current, not stale.** The prior pass's top-priority finding (a hard-gate line that omitted `Prescriptions`, demanded an exact-match `patients.csv` header where the spec requires a superset, and carried no row-shape gate) **no longer exists.** The current file correctly states the superset requirement (*"checked reflectively against the EF entity's mapped properties... it must always be a superset, not an exact match"*), includes `Prescriptions` in both files, and explicitly gates the one-row-per-visit shape. This is closed — see §Part 3.

---

## Verdict, up front

**21 of 33 requirements are verification-ready today. 4 are testable in principle but have no harness or dataset defined. 8 (24%) cannot be tested at all as worded.**

The prior pass's two highest-priority blockers are both resolved:

1. **The stale export hard-gate is fixed.** (See Freshness check above and Part 3.) It no longer produces a false-FAIL on correct work or a false-PASS on the most likely real defect (one-row-per-patient instead of one-row-per-visit).
2. **The plaintext-password-vs-encryption-NFR contradiction is resolved.** Authentication now uses reversible encryption, not plaintext — a correctly built auth module storing an encrypted (not plaintext, not hashed) password no longer contradicts the BRD's literal "data encryption" wording the way plaintext storage did. A softer wording tension remains (§Part 3, Authentication gates) but it no longer forces `verification-brd` to FAIL correctly-built work.

**Since that pass, four more stakeholder decisions were confirmed (2026-08-20) and closed three of that pass's own priorities:**

1. **The 2–3 minute consultation criterion now has a measurement method.** Confirmed as workflow completeness — the doctor can enter patient/appointment/visit/prescription details without unnecessary steps — not a literal stopwatch measurement. Row 27 moves from "not testable as worded" to "testable now": my cross-cutting test-strategy rule can now be satisfied by reporting added friction (an extra click, modal, or round-trip) on the consultation path, which is directly assertable, instead of an undefined elapsed-time claim I could never write a test against.
2. **Password recovery is closed, not "open."** Explicitly confirmed as not required for Phase 1 — an accepted risk, not a gap awaiting a procedure. This resolves what the prior pass called a reopened Critical finding (see Part 4).
3. **Search match semantics are now defined** — contains (substring) matching on both name and phone. Rows 2 and 13 move from "not testable as worded" to "testable now."
4. **Appointment slot duration is now defined** — doctor-entered per appointment.

**A second refresh (also 2026-08-20) closes the one item the first refresh left open:** walk-in support and the double-booking rule are now both defined. Appointments are preferred, but a walk-in is explicitly supported — the system creates its own linked appointment record at registration/consultation, so `Visit.AppointmentId` stays populated either way — and double booking (two appointments for the same date/time slot) is explicitly rejected, whether the conflicting appointment was scheduled or walk-in-generated. Row 3 moves from "not testable as worded" to "testable now," closing the last decision-gated row in this document at that point (see Part 1, Part 2).

**A third refresh (also 2026-08-20) closed a caveat, not a row:** `EmergencyContactName`/`EmergencyContactPhone` are confirmed `Patient` fields, and `MedicalSurgicalHistory` is a confirmed Phase 1 exclusion — row 1 no longer carries an asterisk about the entity's completeness, and Part 3's Patient/Vitals gates gained two more fully-specified rows.

**A fourth refresh (also 2026-08-20) closes the row that opened this document's remaining "no defined ranking" gap:** "recent patients" is confirmed ordered by most-recent visit date. Row 14 moves from "not testable as worded" to "testable now."

---

## Part 1 — Requirement-by-requirement verification map

Rows mirror `docs/worktree-brd-review.md`'s Requirement Analysis Matrix, in the same order, plus one Exclusions row at the end. Status is one of **`Testable now`**, **`Harness gap`** (measurable in principle; no dataset/tooling/measurement point defined), **`Not testable as worded`**.

| # | BRD requirement | Status | Concrete test, or what's missing |
|---|---|---|---|
| 1 | Add/edit/view patient + capture Name/Age-DOB/Gender/Contact | **Testable now** | Integration: POST→GET round-trip; PUT persists; GET 404s for unknown id; Age and DOB persist independently (both now fixed, closing the prior pass's Age-vs-DOB ambiguity); Allergies/CurrentMedications/ChronicConditions round-trip. **Resolved 2026-08-20:** `EmergencyContactName`/`EmergencyContactPhone` are now fixed-spec fields — add to the round-trip test. `MedicalSurgicalHistory` is explicitly excluded from Phase 1 (`docs/worktree-brd-review.md` HC-2, Resolved as an accepted exclusion) — its absence is no longer a test-coverage gap, it's the confirmed shape of the entity. |
| 2 | Search patients by name/phone | **Testable now** *(was "not testable as worded")* | **Resolved 2026-08-20.** Match semantics are now defined: contains (substring) matching on both name and phone, not prefix-only. Integration: `"kum"` matches `"Kumar"`; a parameterized test can now assert this directly instead of guessing at intent. |
| 3 | Schedule appointments | **Testable now** *(was "not testable as worded")* | **Fully resolved 2026-08-20** (`docs/worktree-brd-review.md` MR-1, now Resolved). All three sub-gaps closed: slot duration is doctor-entered per appointment (accepts and persists user input); walk-in patients are supported via an auto-created linked appointment record at registration/consultation, so `Visit.AppointmentId` stays populated for scheduled and walk-in visits alike; double booking is explicitly rejected — creating a second appointment for a date/time already occupied by an existing one (scheduled or walk-in) must fail. Integration: three tests now writable — slot-duration persistence, walk-in produces exactly one linked `Appointment`+`Visit`, and a same-slot second appointment is rejected. |
| 4 | View daily appointment list | **Testable now** | Integration: appointments dated D returned, D−1/D+1 excluded. **Caveat:** asserts a midnight clinic-day boundary by assumption — undefined anywhere, same caveat as row 12. |
| 5 | Update appointment status (4 values) | **Testable now** | Integration: each status persists and reads back — the enum is fully enumerated in the BRD. **If** the transition-guard recommendation in `docs/brainstorm-brd-review.md` §1.4 (status can't be manually set to `Completed` without a linked `Visit`) is adopted, that's a second, equally writable test; as of today it's a recommendation, not a locked rule, so only the basic persistence test is unconditionally writable. |
| 6 | Mandatory vitals capture (temp/BP/pulse) | **Testable now** | **The single largest improvement in this pass.** The prior pass's Critical finding here (mandatory at entry vs. at finalize, unstated units, unstated BP storage format) is fully closed: non-nullable columns, °C only, separate systolic/diastolic smallints, `Weight` at `decimal(6,3)`. Component test: `Validators.required` on all three blocks submission. Integration: a visit request missing any of the three is rejected server-side, not just client-side. |
| 7 | Complaints (free text) | **Testable now** | Integration: round-trip persistence, including Unicode/newlines. |
| 8 | Diagnosis notes | **Testable now** | Integration: round-trip persistence. |
| 9 | Add medicines (name/dosage/frequency/duration/instructions) | **Testable now** | Integration: all five named fields persist and reload — the BRD names them explicitly. |
| 10 | Generate printable prescription | **Testable now** | **Second-largest improvement in this pass.** The prior Critical finding (no header/footer field list to assert against) is closed: `DoctorDetails` (`ClinicName`, `DoctorName`, `Qualifications`, `RegistrationNumber`, `Logo`, `Signature`) is fixed, snapshotted onto the prescription at creation. Integration: print view/PDF contains all six fields as they existed *at creation time*, unchanged by a later `DoctorDetails` edit — directly testable by editing `DoctorDetails` after creation and asserting the existing prescription is unaffected. |
| 11 | View previous visits + vitals/complaints/dx/Rx | **Testable now** | Integration: a patient with N visits returns N visits with children eager-loaded. |
| 12 | Filter history by date | **Testable now** | Integration: range boundaries inclusive/exclusive, empty range returns empty. Same clinic-day-boundary caveat as row 4. |
| 13 | Quick patient search (Search & Navigation) | **Testable now** *(was "not testable as worded")* | Inherits row 2's resolution — this is `docs/worktree-brd-review.md`'s CR-4 duplicate requirement, so it inherits row 2's status rather than having its own. |
| 14 | View recent patients | **Testable now** *(was "not testable as worded")* | **Resolved 2026-08-20** (`docs/worktree-brd-review.md` MR-2, now Resolved). Ordered by most-recent visit date, not registration date. Integration: a patient registered earlier but visited more recently sorts ahead of one registered later but visited longer ago (or not visited at all) — a direct, parameterized test. |
| 15 | Navigation between profile and visits | **Testable now (shallow)** | Component/routing test proves a route exists and is reachable — but "easy" carries no click-budget, so a *regression* in navigation friction (an extra required click added later) has nothing to fail against. Proves existence, not quality. |
| 16 | Export data as CSV | **Testable now** | Fully specified; see Part 3, Export gates. The prior pass's stale-hard-gate issue is resolved — this row is unconditionally testable against the current gate list. |
| 17 | Export data as PDF | **Testable now** | Integration: single-patient only (no bulk entry point); extracted text contains demographics/visit history/prescriptions; an out-of-range visit is absent; confirmation and audit-log gates apply identically to CSV. |
| 18 | NFR — simple, minimal UI, fast data entry | **Not testable as worded** | No interaction budget exists (`docs/worktree-brd-review.md` TR-1 proposes one, not adopted). Needs a number (e.g. keystrokes/clicks) before it's an assertable Angular component test. |
| 19 | NFR — page load < 2s | **Harness gap** | The number exists — ahead of most NFRs. `dotnet test`/`ng test` don't measure page load, and no dataset volume is defined to load-test against. Needs a cold/warm definition, a seeded dataset size (TR-5 proposes 5,000 patients/25,000 visits, not adopted), and a perf harness outside my current test-strategy layers. |
| 20 | NFR — fast patient search and retrieval | **Not testable as worded** | Duplicates row 28 in vaguer language (`docs/worktree-brd-review.md` CR-2) — recommend deleting this NFR line rather than writing a second, looser test for the same requirement. |
| 21 | NFR — no data loss | **Not testable as worded** | An absolute with no experiment that demonstrates it. Needs an RPO/RTO-style restatement (`docs/worktree-brd-review.md` TR-3 proposes a quarterly restore-drill definition) before it's assertable as anything beyond "we hope so." |
| 22 | NFR — regular automated backups | **Harness gap** *(improved from "not verifiable by software at all")* | Under the local-deployment model, a backup used to mean SQL Server maintenance the human owns via SSMS — nothing in-process to test. `docs/brainstorm-brd-review.md` §3.5 now proposes a concrete, scriptable mechanism (Task Scheduler + `sqlcmd BACKUP DATABASE`), which — *if adopted* — is genuinely testable: a smoke test can run the script against a populated database and assert a valid, restorable `.bak` file results. **Not adopted yet** (`docs/worktree-brd-review.md` AC-1, Critical), so still not testable *today*, but the path to testable is now concrete rather than structurally out of reach. |
| 23 | NFR — secure login (single-user auth) | **Testable now** | Five gates, all specified — see Part 3, Authentication gates. **The prior pass's FAIL-forcing contradiction (plaintext storage vs. the BRD's encryption NFR) is resolved** — reversible encryption is not plaintext, and satisfies the BRD's literal "data encryption" wording, even though a wording tension with "secure" remains (see Part 3; tracked as `docs/worktree-brd-review.md` CR-3, not a verification blocker). |
| 24 | NFR — data encryption at rest and in transit | **Not testable as worded** | `docs/worktree-brd-review.md` AC-3 (High): scope of "at rest" beyond the login password column is undefined — whole-database TDE, host-level disk encryption, or just the password? "In transit" is not applicable beyond `localhost` under the local-deployment model, so that half is moot rather than untestable. The only assertion available today is a negative (*no column-level encryption exists on other tables*), which isn't what the requirement claims. |
| 25 | NFR — moderate patient volume (Scalability) | **Not testable as worded** | No figure exists anywhere (`docs/worktree-brd-review.md` TR-5 proposes 5,000 patients/25,000 visits, not adopted). Every volume-dependent performance test (rows 19, 28) is blocked on the same missing number. |
| 26 | NFR — modern browsers (Chrome, Edge, Safari) | **Harness gap** | Assertable in principle for Chrome/Edge (`ng test` runs headless Chrome by default). **Safari cannot be run on the doctor's Windows machine at all** — no Mac is confirmed to exist anywhere in the repo. Needs either dropping Safari from the supported list or naming the machine it will actually be verified on. |
| 27 | SC — consultation record within 2–3 minutes | **Testable now** *(was "not testable as worded")* | **Resolved 2026-08-20** (`docs/worktree-brd-review.md` CR-1, now Resolved). Confirmed as workflow completeness — patient/appointment/visit/prescription details enterable without unnecessary steps — not a stopwatch measurement. My cross-cutting rule is now satisfiable: report added friction (extra click/modal/round-trip) on the consultation path per step, which is directly assertable, rather than an undefined elapsed-time claim. |
| 28 | SC — search/history retrieval within 2–5 seconds | **Harness gap** | A real number, but no dataset size or percentile (needs e.g. *p95 < 2s at 5,000 patients/25,000 visits*, consistent with row 25's proposed figures once adopted). Generous enough to pass trivially at small seed sizes, so it measures little without a defined dataset. |
| 29 | SC — ≥80% reduction in paper usage | **Not testable as worded** | Has a real number but no baseline or measurement method (`docs/worktree-brd-review.md` TR-7). Not a software test regardless of how it's worded — needs a stated before/after measurement process, not a unit test. |
| 30 | SC — smooth generation and printing | **Not testable as worded** | "Smooth" has no pass/fail condition (`docs/worktree-brd-review.md` TR-8). Print *content* correctness is now testable (row 10); print *quality on physical hardware* is a manual gate, not an automated one, and should be labeled as such rather than implying a suite can cover it. |
| 31 | SC — successful CSV/PDF export | **Testable now** *(superseded by rows 16/17)* | The BRD sentence itself ("successful... export") is unverifiable as worded, but the fixed Export spec supersedes it entirely — rows 16/17 carry the real assertions. Left here as testable because, unlike the prior pass, the fixed spec's coverage is now complete enough that nothing is missing beneath it. |
| 32 | SC — high usability, minimal training | **Not testable as worded** | No onboarding-time target exists (`docs/worktree-brd-review.md` TR-9 proposes one, not adopted). |
| 33 | Exclusions (10 items: receptionist/multi-user, billing, insurance, lab/pharmacy, AI diagnosis, offline, mobile, analytics, multi-clinic, reminders) | **Testable now** | The cheapest, highest-value-per-effort row in this table, unchanged from the prior pass: negative integration tests — no billing/invoicing endpoint, no second user/role, no lab/pharmacy integration client, no reminder scheduler, no analytics endpoint. Deterministic, catches scope creep, and nothing else in this document catches that failure mode. |

### Counts

| Status | Count | Share |
|---|---|---|
| **Testable now** | **21** | 64% |
| **Harness gap** | **4** | 12% |
| **Not testable as worded** | **8** | 24% |
| Total | 33 | |

**Testable now:** rows 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 23, 27, 31, 33.
**Harness gap:** rows 19, 22, 26, 28.
**Not testable as worded:** rows 18, 20, 21, 24, 25, 29, 30, 32.

**Read the 21 carefully.** Rows 1 (partially), 2, 3, 6, 10, 13, 14, 16, 17, 23, and 27 are verifiable *only because a fixed spec or an explicit agent-level decision supplied a contract the BRD text itself does not contain* — eleven of twenty-one. On the BRD's own unaided terms, the genuinely verification-ready set is still closer to **10 of 33 (30%)**, essentially flat across every refresh pass — a much smaller improvement than the headline 48%→64% suggests, because the BRD's own text hasn't changed at all; every gain across all four refresh passes comes from specs and decisions sitting alongside it, not from the document itself.

---

## Part 2 — What every "not testable" row needs, condensed

**A number is missing (6 rows):** 18 (interaction budget), 20+28 (search percentile/threshold), 21 (RPO/RTO), 24 (encryption-at-rest scope), 25 (dataset volume), 32 (onboarding-time target). Each is one sentence and becomes an ordinary automated test once written.

**A decision is missing (1 row, down from 4):** 5 (whether the status-transition guard is adopted). Row 3 (appointment walk-in support + double-booking rule) is resolved and removed from this list as of the second 2026-08-20 refresh. Row 22 (whether the proposed backup mechanism is adopted) stays open too, but is tracked separately below since it's now a harness-gap, not a pure decision gap. Row 2/13 (search match semantics) was resolved and removed in the first refresh. This remaining decision is the *same* one `docs/implementation-brd-review.md` already names as a build-blocker — the build blocker and the verification blocker are the same item, so one decision session unblocks both.

**A measurement method is missing (1 row, down from 2):** 29 (paper-usage baseline). Row 27 (2–3 minute consultation) is resolved and removed from this list — its measurement method is now defined as workflow completeness/friction, not elapsed time.

**Not verifiable by software at all, but now correctly scoped as manual/operational gates rather than implied automated tests (2 rows):** 29 (paper reduction — a business observation), 30 (print quality on physical hardware — a manual gate). Row 22 (backups) has moved *out* of this category this pass, now that a scriptable mechanism has been proposed.

---

## Part 3 — Fixed-spec hard gates mapped to BRD requirements

### Export gates — verify rows 16, 17, 31

| Gate | Verifies | Fully specified? |
|---|---|---|
| No unbounded/full-database export path exists | Row 16/17 | **Yes.** Every export entry point requires a patient set or date range; neither → 400. |
| `patients.csv` — one row per **visit**, not per patient | Row 16 | **Yes**, and explicitly gated as such in the current `.claude/agents/verification-brd.md` — this is the exact assertion the prior pass's stale gate was missing. |
| `patients.csv` carries every current `Patient` field (checked reflectively) plus `VisitDate`/`Diagnosis`/`Prescriptions` | Row 16 | **Yes, and correctly specified as a superset check** — the prior pass's stale "exact match" wording is gone. The reflective design means this row's value scales automatically with entity completeness: it already picked up `EmergencyContactName`/`EmergencyContactPhone` without needing a test rewrite when they were added 2026-08-20, and `MedicalSurgicalHistory`'s confirmed absence (an accepted exclusion, not a pending addition) means the entity is no longer "thinner than it should be" — it's the settled shape. |
| `visits.csv` — one row per visit, exact column order `PatientId, Name, DOB, Phone, VisitDate, Diagnosis, Prescriptions` | Row 17 | **Yes.** Matches the current spec exactly, including `Prescriptions` — the prior pass's stale gate omitted it; the current one doesn't. |
| `Prescriptions` semicolon encoding | Rows 9/16/17 | **Mostly** — open item O1 below. |
| Export without confirmation is rejected | Rows 16/17 | **Yes.** |
| A completed export writes an audit entry | Rows 16/17 | **Yes as an assertion; open as an architecture question** — O2 below. |

**Open items:**
- **O1 — `Prescriptions` cell edge cases undefined.** What happens when a drug name itself contains `;` or `(`? What's in the cell for a visit with zero prescriptions — empty, or a literal marker? A unit test needs a stated expectation; not mine to invent against a locked spec.
- **O2 — the export audit log's store, immutability, and retention are unspecified.** Ties directly to `docs/worktree-brd-review.md` AC-5 (retention rationale). The gate can assert "an entry was written"; it cannot assert tamper-evidence or a retention period, because nothing states one.

### Authentication gates — verify row 23

| Gate | Fully specified? |
|---|---|
| Login succeeds on exact username + decrypted-password match | **Yes** |
| Wrong username / wrong password / empty / missing credentials all rejected | **Yes** |
| `Users` exposes exactly `Id`, `Username`, `Password` | **Yes** — best written against `DbContext.Model` metadata so a later migration that quietly adds a column fails it too, not just a POCO change. |
| Stored password is not equal to plaintext, and decrypts back to exactly the original value | **Yes** — this is the current spec's own anti-drift gate, correctly distinguishing "encrypted" from "hashed" (a hash would fail the round-trip half of this test, which is the point). |
| No registration/self-signup endpoint exists | **Yes** |

All five are fully specified and buildable today, and **the prior pass's FAIL-forcing collision is resolved**: reversible encryption is not plaintext, so a correctly-built auth module satisfies the BRD's literal "data encryption" wording. What remains is a softer, non-blocking wording tension — `docs/worktree-brd-review.md` CR-3: "secure login" reads as implying stronger practice (one-way hashing) than the accepted reversible-encryption tradeoff delivers. This does not force a FAIL of correct work the way plaintext storage did; it's a BRD-wording reconciliation, already carrying its own accepted-risk rationale inside `implementation-brd.md` itself.

### Patient/Vitals gates — verify rows 1, 6, 10

| Gate | Fully specified? |
|---|---|
| No delete/archive endpoint for `Patient` exists | **Yes** — route-enumeration negative test. |
| A visit cannot be saved with missing temperature/BP/pulse (server-side, not just client-side) | **Yes** — the prior pass's largest single gap, now closed. |
| A printed `Prescription`'s content cannot be modified via an update endpoint once created | **Yes** — route-enumeration + integration test (attempt a PATCH, assert rejection or absence of the route). |
| `Patient.EmergencyContactName`/`EmergencyContactPhone` persist and round-trip | **Yes** — resolved 2026-08-20, a plain integration round-trip test, same shape as the Allergies/CurrentMedications/ChronicConditions fields already covered under row 1. |
| No `MedicalSurgicalHistory` field or endpoint exists | **N/A by design, not a gap** — resolved 2026-08-20 as an explicit Phase 1 exclusion. Nothing to test *for*; if one is ever added unprompted, that itself would be the finding (same pattern as the no-password-reset gate under Authentication). |

All five are fully specified and buildable today with no open questions.

### Appointment gates — verify row 3

| Gate | Fully specified? |
|---|---|
| Appointment slot duration is accepted from and persists as user input, per appointment | **Yes** — resolved in the first 2026-08-20 refresh. Negative check: no fixed/hardcoded duration is silently applied when the field varies between appointments. |
| A walk-in registration creates exactly one `Appointment` and one linked `Visit` in a single flow, with `Visit.AppointmentId` populated | **Yes** — resolved in the second 2026-08-20 refresh. No separate manual pre-booking step should exist on this path; integration test asserts both rows are created together, not via two independently callable endpoints that could be invoked out of order. |
| Creating a second appointment for a date/time already occupied by an existing appointment (scheduled or walk-in-generated) is rejected | **Yes** — resolved in the second 2026-08-20 refresh. Integration test: seed one appointment at a given date/time, attempt to create a second at the identical date/time, assert rejection (not a warning, not silent success) regardless of which path created the first one. |

All three are fully specified and buildable today with no open questions — this closes what was, until this refresh, the last decision-gated row in Part 1 (row 3).

### Consultation-path cross-cutting gate — verifies row 27

| Gate | Fully specified? |
|---|---|
| The flow (patient/appointment/visit/prescription entry) adds no unnecessary round-trip/modal/step beyond what the BRD requires, evaluated as workflow completeness against the 2–3 minute target | **Yes, as of 2026-08-20** — resolved by `.claude/agents/implementation-brd.md`'s "Fixed interpretation" section and the matching update to `.claude/agents/verification-brd.md`. Deliberately **not** a timed/stopwatch assertion — there is no start point, stop point, or physical-vitals-taking inclusion to define, because none is required. What's assertable per consultation-path step: no field beyond what the BRD/fixed specs require, no added modal, no added round-trip — a concrete, checkable property today, unlike the prior pass's unenforceable latency claim. |

---

## Part 4 — Testable in isolation, not verifiable end-to-end

Cases where a unit or integration test can pass while the requirement remains unproven, because an architecture decision upstream is still missing. Local-deployment-model closures (hosting, in-transit encryption, data residency, third-party data flow) are excluded — they're resolved, not gaps.

| Finding | What's testable in isolation | What can't be verified, and what would fix it |
|---|---|---|
| Backup / restore ownership (`docs/worktree-brd-review.md` AC-1, Critical) | Nothing yet — but **this moved from structurally untestable to "testable once adopted"** this pass, since a concrete scriptable mechanism now exists as a proposal (row 22). | Row 21 ("no data loss") can't PASS until the mechanism is adopted and a restore drill exists to test against. |
| Password recovery (`docs/worktree-brd-review.md` AC-2, Resolved 2026-08-20) | Nothing needed — **resolved, not open.** Confirmed as an accepted Phase 1 exclusion: no password-reset/recovery flow is required. This closes what the prior pass called a reopened Critical finding (the plaintext→encryption spec change had made manual SSMS recovery non-trivial); the stakeholder has now accepted that risk explicitly rather than requiring a procedure. | A route-enumeration negative test (no reset/recovery endpoint exists) is the only assertion this row needs going forward — same shape as the "no registration endpoint" gate it already sits beside. |
| Clinical (non-export) audit logging | Nothing, but **this is resolved, not open** — `implementation-brd.md`'s "Fixed scope note" explicitly rules out a general clinical audit log for Phase 1. A prior "is this missing or intentional" ambiguity is now an explicit decision either way a test can respect. | — |
| Data retention | A trivial test that a record still exists after creation (never-delete is the fixed policy) — genuinely testable today, unlike the prior pass. | The *policy rationale* for "keep forever" isn't stated (`docs/worktree-brd-review.md` AC-5, Medium) — a documentation gap, not a verification gap. |
| Deployment/update model — upgrade-in-place | A fresh-apply migration smoke test (my own test strategy mandates one). | **Unchanged, still open.** Nothing verifies applying migration *n* on top of *n−1* with real data present and confirming the data survives — the case that actually matters on a permanently-local single machine that gets updated in place, not reinstalled. Needs a stated update procedure (auto-backup before schema change, rollback path) before this second, higher-value smoke test can be written. |
| Expected load / data volume | Correctness tests at any seed size. | Every performance criterion (rows 19, 28) is unverifiable without a dataset volume — `docs/worktree-brd-review.md` TR-5 proposes 5,000 patients/25,000 visits, not adopted. |
| Clinic-day boundary | Rows 4/12 pass against an assumed midnight boundary. | Unchanged, still open — an evening clinic running past midnight would split one session across two days, and every date-scoped test currently encodes an unstated assumption. |
| Authorization | Closed by the fixed Auth spec (single account, no roles) — not counted as a gap. | — |
| Third-party data flow / data residency | Closed by the local-deployment model — not counted as a gap. | — |

---

## Part 5 — State of the verification apparatus itself

- **`docs/implementation-progress.md` does not exist**, and no step is marked `Done`. My own default-scope rule applies correctly: nothing to verify. This is repo state, not a defect.
- **`docs/verification-report.md` does not exist** and shouldn't be created until there's a real verdict to record.
- **No test project exists in either stack** — `dotnet test`/`ng test` would both report zero tests collected today. My own rule 2 correctly classifies a zero-test green run as a failure to flag, not a pass — worth stating plainly, since that's the single most likely false PASS at the start of this project.
- **My own instructions call for reading "the plan being implemented (`docs/planning-review.md` or equivalent)."** `docs/planning-review.md` no longer exists in the working tree; `docs/plan-brd-review.md` is its replacement and **is correctly stack-matched** (Angular/.NET Web API/SQL Server/EF Core throughout) — unlike the prior pass, where the only plan present targeted Razor Pages + SQLite + Argon2id and would have produced systematic false FAILs against anything actually built to the fixed stack. **This entire failure mode is closed.**
- **Worktree handling:** this session is already inside the isolated worktree `worktree-brd` created for this branch — no separate verification-only worktree was needed for producing this assessment. Once real implementation work lands (likely on its own branch/worktree per `docs/implementation-brd-review.md`'s sequencing), a future verification run should follow my own instructions: enter the implementer's worktree directly if one exists, or create a temporary `git worktree add <temp> impl/<slug>` if implementation happened on a plain branch — and remove only the latter, never the former.

---

## Final verdict

**21 of 33 requirements (64%) are verification-ready today; 4 have a harness gap; 8 (24%) cannot be tested as worded.** On the BRD's own unaided terms — excluding the eleven rows testable only because a fixed spec or an explicit agent-level decision supplied a contract the document itself lacks — the figure is still closer to **10 of 33 (30%)**, essentially flat across every refresh pass, consistent with `docs/worktree-brd-review.md`'s own observation that the BRD text hasn't moved even though the specs and decisions around it keep improving.

**Both of the original pass's highest-priority blockers were already resolved** (stale export hard-gate; plaintext-vs-encryption FAIL-collision). **The first refresh closed three more** (consultation-timing measurement method, password recovery, search match semantics). **The second refresh closed the last decision-gated row at that point** (walk-in support and the double-booking rule). **The third refresh closed a caveat, not a row** (Patient row's Emergency Contact/Medical-Surgical History asterisk). **This fourth refresh closes another row**: "recent patients" is now ordered by most-recent visit date, resolving row 14.

**What's left is entirely numbers-and-wording work, not decisions.** Every remaining row in the "not testable as worded" bucket (Part 2) needs either a missing number (interaction budget, RPO/RTO, dataset volume, onboarding-time target) or a BRD-text edit for vague language ("smooth," "moderate volume") — none of it schema-shaped, and none of it blocking any plan step. Across all four refresh passes, every decision-gated finding in this document has now been closed; what remains is documentation work, not open questions.

**The encouraging part, again from the verification side:** every remaining item that would improve testability here is a number `docs/worktree-brd-review.md`'s Testability Review already proposes a value for — nothing in this document requires new investigation or a further decision session. Writing those numbers into the BRD, plus adopting the backup mechanism (row 22's remaining harness-gap), is what would move verification-readiness from 64% toward nearly complete.

**What I did not do:** no application code, no tests, no BRD edits, no edits to either agent configuration file.
