# BRD Review — Doc_BRD.md

**Reviewed:** `BRD/Doc_BRD.md` (unchanged since commit `5d512ca`) on branch `brd/review-report`, 2026-08-19.
**Reviewer role:** Senior Business Analyst + Solution Architect + Domain Reviewer, per `.claude/agents/worktree-brd.md`.
**Context this review is grounded against:** the fixed tech stack and fixed feature specs recorded in `.claude/agents/implementation-brd.md` (Angular / .NET Web API / SQL Server via SSMS / EF Core, local-only deployment; Patient, Appointment/Visit, Consultation Vitals, Prescription, DoctorDetails, Export, and Authentication specs), plus the open items already raised in `docs/brainstorm-brd-review.md` and the sequencing in `docs/plan-brd-review.md`.

**A load-bearing distinction this review makes throughout:** a decision can be *resolved for the build* (it's pinned down in `implementation-brd.md`) while still being *undocumented in the BRD itself* (`BRD/Doc_BRD.md`'s text hasn't changed). Those get a lower severity than a decision that's genuinely unresolved anywhere — but they're still findings, because the BRD is supposed to be the source of truth, and right now a reader of the BRD alone would not know about several decisions that already govern the build.

**Refreshed 2026-08-20 (third pass)** against two more stakeholder decisions: **`EmergencyContactName`/`EmergencyContactPhone` are now confirmed additions to `Patient`**, closing HC-1 (previously the last Critical finding besides the backup mechanism); and **no separate `MedicalSurgicalHistory` field is needed for Phase 1** — the per-visit prescription record is considered sufficient history — closing HC-2 as an explicit accepted-scope decision rather than a gap. Both are recorded in `.claude/agents/implementation-brd.md` and `.claude/agents/verification-brd.md` and marked **Resolved** below. (Prior refresh passes closed CR-1, AC-2, MR-1, and the double-booking rule — see the history in each finding's own section below.)

---

## Executive Summary

This is a meaningfully stronger position than the BRD's text alone would suggest, because a large share of what would otherwise be Critical architecture and data-model gaps have already been closed by `implementation-brd.md` — tech stack, deployment model, `Patient` identity scheme, phone-uniqueness, prescription immutability, and the full CSV/PDF export shape are all pinned down. None of that is reflected back into `BRD/Doc_BRD.md` itself, which still reads exactly as it did at baseline, including its optimistic "Open Questions: None."

Four things matter most:

1. **Exactly one Critical finding remains in this entire document, and it's the same one for three refresh passes running: there is no backup mechanism actually adopted** (only a brainstorm recommendation, not a decision). Every other Critical finding raised across the initial pass and all three refreshes — password recovery, walk-in support, and, as of this refresh, emergency contact — is now a closed, documented decision.
2. **The walk-in question and double booking are resolved** (closed in the prior refresh): appointments are preferred, walk-ins fit into availability between scheduled patients, and a walk-in creates its own linked appointment record. `Visit.AppointmentId` stays non-nullable either way.
3. **The healthcare-domain field set is now materially complete for what Phase 1 actually needs.** `Patient` carries Allergies/CurrentMedications/ChronicConditions/EmergencyContactName/EmergencyContactPhone. Medical/surgical history was considered and explicitly declined — the per-visit prescription record is judged sufficient history for now, a deliberate scope call rather than an omission. What remains (Height/SpO2/BMI, HC-3) is Medium severity and explicitly framed as a stakeholder's call to make, not a gap to close unilaterally.
4. **Measurability is still the weakest dimension by far, and the one area none of these decisions have touched.** Of the BRD's 9 NFRs, only one (`page load < 2s`) is independently testable as written. This hasn't improved since baseline — vague language is a documentation problem, not a data-model or scope decision, so none of the six decisions confirmed across three refresh passes move this number. (The 2–3 minute consultation Success Criterion is the one exception — its interpretation is now settled; see CR-1, Resolved.)

**Is this document currently safe to build from?** Yes, essentially without qualification on the feature side. Every feature slice in `docs/plan-brd-review.md`, including Patient and Appointment/Visit, is now build-ready against `implementation-brd.md`'s fixed specs, with zero unresolved BRD-level decisions blocking any of them. The backup mechanism (AC-1) is the one remaining item that must be adopted before general release — an operational gate, not a feature blocker, and the only thing standing between this document and a clean "ship it" verdict.

---

## BRD Quality Score: 6.8 / 10

- **Completeness (functional + domain coverage): 8.0** *(up from 7.0)* — Emergency contact is now a resolved field addition, and medical/surgical history is a deliberate, documented scope exclusion rather than a silent gap. What remains (Height/SpO2/BMI, HC-3; "recent patients" ranking, MR-2) is Medium/High, not Critical.
- **Consistency (freedom from contradiction): 8.0** — Unchanged this pass; HC-1/HC-2 were domain-completeness findings, not contradictions.
- **Testability (share of requirements with measurable acceptance criteria): 4.5** — Unchanged. 1 of 9 NFRs and 3 of 6 Success Criteria are independently testable as written; resolving healthcare-domain gaps doesn't touch this axis.
- **Architecture readiness (share of architectural decisions documented): 7.0** — Unchanged this pass. Backup/restore (AC-1) remains the only genuinely open architecture item.
- **Traceability (requirements linked to a clear source/rationale): 6.5** *(up from 6.0)* — Two more decisions (emergency contact, no-medical-surgical-history) now have documented rationale in `implementation-brd.md`/`verification-brd.md`. Six decisions total across three refresh passes now live there instead of in `BRD/Doc_BRD.md` — the traceability gap is narrowing in substance even though the BRD text itself is still unchanged.

---

## Development Readiness: 63%

(requirements that are unambiguous AND non-contradictory AND testable AND not blocked by an undocumented architecture decision) ÷ (total requirements) × 100 — **20 of 32 requirements are build-ready as written** (17 Functional + 9 NFR + 6 Success Criteria = 32; see the matrix below for the per-requirement call). Unchanged this pass — HC-1/HC-2 affect Healthcare Completeness, not the requirement-level Testable/Consistency calls the Patient row already carried.

This is a real improvement over the prior baseline (26%), driven almost entirely by `implementation-brd.md`'s fixed specs resolving what used to be open data-model and architecture questions. It has not improved on the NFR/measurability axis at all — that requires editing the BRD's language, which no amount of implementation-spec or scope-decision work fixes.

---

## Critical Findings

1. **No backup mechanism is actually adopted anywhere** — the BRD requires "regular automated backups," `implementation-brd.md` doesn't assign one, and `docs/brainstorm-brd-review.md`'s proposal is a recommendation, not a decision. See Architecture Completeness Report, AC-1. **This is now the only Critical finding in the entire document.**

*Resolved across the three refresh passes:* no password-recovery path for the single doctor account (accepted Phase 1 exclusion — see Architecture Completeness Report, AC-2, now Resolved); walk-in patients being structurally unsupported (walk-ins are now explicitly supported, double booking explicitly rejected — see Missing Requirement Report, MR-1, now Resolved); emergency contact being entirely absent from the Patient data model (`EmergencyContactName`/`EmergencyContactPhone` are now confirmed fields — see Healthcare Completeness Report, HC-1, now Resolved).

---

## Requirement Analysis Matrix

| Requirement | Category | Classification | Scope tag | Consistency | Testable | Notes |
|---|---|---|---|---|---|---|
| Add/edit/view patient details + capture Name/Age-DOB/Gender/Contact | Functional | Existing | [in scope] | OK | Yes | Field set extended by `implementation-brd.md` (Allergies, CurrentMedications, ChronicConditions, EmergencyContactName, EmergencyContactPhone — see HC-1, Resolved) — not reflected in BRD text (see AC-6). Medical/Surgical History deliberately excluded — see HC-2, Resolved. |
| Search patients by name/phone | Functional | Existing | [in scope] | Conflicts with: Search & Navigation "Quick patient search" | Yes | Near-duplicate requirement across two BRD sections — see Contradiction Report, CR-4. |
| Schedule appointments | Functional | Existing | [in scope] | OK — see MR-1 (Resolved) | Yes | Walk-ins supported (appointment auto-created at registration/consultation) and double booking rejected — see `.claude/agents/implementation-brd.md`. |
| View daily appointment list | Functional | Existing | [in scope] | OK | Yes | — |
| Update appointment status (Scheduled/Completed/Cancelled/No-show) | Functional | Existing | [in scope] | OK | Yes | Legal-transition rule (status can't be manually set to Completed) is a `docs/plan-brd-review.md` recommendation, not yet in the BRD. |
| Mandatory vitals capture (temp/BP/pulse) | Functional | Existing | [in scope] | OK — see CR-1 (Resolved) | Yes | Previously flagged against the 2–3 min consultation target; resolved by the workflow-completeness interpretation. |
| Record patient complaints (free text) | Functional | Existing | [in scope] | OK | Yes | — |
| Record diagnosis notes | Functional | Existing | [in scope] | OK | Yes | — |
| Add medicines (name/dosage/frequency/duration/instructions) | Functional | Existing | [in scope] | OK | Yes | — |
| Generate printable prescription (header/patient/vitals/diagnosis/meds/footer) | Functional | Existing | [in scope] | OK | Yes | Immutability + DoctorDetails snapshot rule fixed in `implementation-brd.md`, not in BRD text (AC-6). |
| View previous visits / access vitals, complaints, diagnosis, prescriptions | Functional | Existing | [in scope] | OK | Yes | — |
| Filter visit history by date | Functional | Existing | [in scope] | OK | Yes | — |
| Quick patient search | Functional | Existing | [in scope] | Conflicts with: Patient Management "Search patients by name/phone" | Yes | Duplicate, see CR-4. |
| View recent patients | Functional | Existing | [in scope] | OK | No | "Recent" has no defined ranking (last visited? last registered?) — see Missing Requirement Report, MR-2. |
| Navigation between patient profile and visits | Functional | Existing | [in scope] | OK | Yes | — |
| Export data as CSV | Functional | Existing | [in scope] | OK | Yes | Full two-file shape fixed in `implementation-brd.md`. |
| Export data as PDF | Functional | Existing | [in scope] | OK | Yes | Single-patient-summary shape fixed in `implementation-brd.md`. |
| Usability — simple, minimal UI, fast data entry | NFR | Existing | [in scope] | OK | No | Vague — see Testability Review, TR-1. |
| Performance — page load < 2s | NFR | Existing | [in scope] | OK | Yes | Only fully measurable NFR as written. |
| Performance — fast patient search and retrieval | NFR | Existing | [in scope] | Conflicts with: Success Criteria "search within 2–5 seconds" (unclear if same standard) | No | See CR-4b and TR-2. |
| Reliability — no data loss | NFR | Existing | [in scope] | OK | No | See TR-3. |
| Reliability — regular automated backups | NFR | Existing | [in scope] | OK | No | Blocked — see AC-1 (Critical). |
| Security — secure login (single-user auth) | NFR | Existing | [in scope] | Tension with: accepted reversible-encryption auth spec | No | See CR-3 and TR-4. |
| Security — data encryption at rest and in transit | NFR | Existing | [in scope] | OK | No | Scope of "at rest" beyond the password field is undefined — see AC-3. |
| Scalability — single clinic, moderate patient volume | NFR | Existing | [in scope] | OK | No | See TR-5. |
| Compatibility — modern browsers (Chrome, Edge, Safari) | NFR | Existing | [in scope] | OK | No | No version floor stated — see TR-6. |
| Success — consultation record within 2–3 minutes | Success Criterion | Existing | [in scope] | OK — see CR-1 (Resolved) | Yes | Confirmed interpretation: workflow completeness (patient/appointment/visit/prescription entry, no unnecessary steps), not a literal stopwatch measurement — see `.claude/agents/implementation-brd.md` "Fixed interpretation" and CR-1. |
| Success — search/history retrieval within 2–5 seconds | Success Criterion | Existing | [in scope] | Conflicts with: NFR "fast... retrieval" (see above) | Yes | — |
| Success — 80% reduction in paper usage | Success Criterion | Existing | [in scope] | OK | No | Has a number but no stated baseline/measurement method — see TR-7. |
| Success — smooth generation and printing of prescriptions | Success Criterion | Existing | [in scope] | OK | No | See TR-8. |
| Success — successful export of data in CSV/PDF | Success Criterion | Existing | [in scope] | OK | Yes | Testable now that the export spec is fixed. |
| Success — high usability, minimal training required | Success Criterion | Existing | [in scope] | OK | No | See TR-9. |

---

## Contradiction Report

### CR-1: Mandatory vitals + no-receptionist scope vs. the 2–3 minute consultation target — **RESOLVED**
- **Severity:** High *(closed 2026-08-20)*
- **Resolution:** Confirmed by the stakeholder and now recorded in `.claude/agents/implementation-brd.md` ("Fixed interpretation: the 2–3 minute consultation criterion") and `.claude/agents/verification-brd.md`: the target is interpreted as **workflow completeness**, not a literal wall-clock measurement — the doctor can enter patient details, appointment details, visit details, prescription details, and any other required fields without unnecessary steps. There is no stopwatch acceptance test; friction (an added click, modal, or round-trip) is the thing to catch, not elapsed seconds.
- **Business Impact (as originally raised):** The doctor is the only person in the room (receptionist access is explicitly excluded) and must personally capture everything within a target the BRD treated as achievable without acknowledging that vitals capture alone consumes part of it. The resolution above removes the ambiguity about what's actually being measured, which was the real risk — not the vitals themselves.
- **Technical Impact (as originally raised):** Nothing in the schema or workflow enforced a time budget, so there was no way to test "did vitals entry protect the 2–3 minute target." That's now resolved by redefining the target as step-count/friction, which *is* directly testable (see the updated Consultation-path gate in `.claude/agents/verification-brd.md`).
- **Residual documentation gap (Medium, not Critical/High):** This decision, like others in this report, lives in the agent config files, not in `BRD/Doc_BRD.md` itself. A reader of the BRD alone still can't tell "2–3 minutes" means workflow completeness rather than a timed test.
- **Suggested BRD Text:** Replace `"Doctor can complete a consultation record within 2–3 minutes"` under Success Criteria with: *"Doctor can complete a consultation record — patient details, appointment details, visit details, and prescription details — within 2–3 minutes of active workflow, meaning no unnecessary steps, clicks, or round-trips are added beyond what the required fields demand. This is evaluated as workflow completeness, not a literal stopwatch measurement."*

### CR-2: NFR "fast patient search and retrieval" vs. Success Criterion "search within 2–5 seconds"
- **Severity:** Low
- **Business Impact:** Two different phrasings of the same requirement invite two different implicit standards — a developer could satisfy the vague NFR while missing the concrete Success Criterion, or vice versa, and neither the doctor nor a reviewer would have a single number to hold the build to.
- **Technical Impact:** Redundant requirement with no single source of truth; a future edit to one (e.g., tightening to 2–3 seconds) could silently diverge from the other.
- **Recommendation:** Delete the NFR line and let the Success Criterion be the single measurable statement of this requirement — non-functional "performance" sections should point at the criterion, not restate it more vaguely.
- **Suggested BRD Text:** Replace `"Fast patient search and retrieval"` under Performance with: *"Patient search and retrieval performance is governed by the Success Criteria section (2–5 seconds); see Success Criteria."*

### CR-3: "Secure login" NFR vs. the accepted reversible-encryption authentication design
- **Severity:** High
- **Business Impact:** The BRD promises "secure login" and "data encryption," which a stakeholder would reasonably read as industry-standard practice (one-way password hashing). The actual accepted design (`implementation-brd.md`) stores passwords with **reversible** symmetric encryption specifically so they can be decrypted back out — a deliberate, justified tradeoff for a single-machine, no-network app, but one the BRD's own language doesn't disclose or authorize.
- **Technical Impact:** A future reviewer or auditor reading only the BRD would flag reversible password storage as a security defect, not realizing it was an accepted, scoped tradeoff — this is exactly the kind of undocumented deviation that causes rework or a bad audit finding later.
- **Recommendation:** Pull the accepted-risk rationale that already exists in `implementation-brd.md` into the BRD itself, so "secure login" has a stated, scoped definition instead of an implied one.
- **Suggested BRD Text:** Replace `"Secure login (single user authentication)"` under Security with: *"Secure login: single-user authentication with the password stored using reversible symmetric encryption (not one-way hashing), an accepted tradeoff valid only because the application has no network exposure beyond `localhost` and supports exactly one account. This tradeoff must be revisited if hosting, networking, or multi-user access is ever introduced."*

### CR-4: Duplicate patient-search requirement across two BRD sections
- **Severity:** Low
- **Business Impact:** None directly, but two independently-worded requirements for the same capability ("Search patients by name or phone number" under Patient Management, "Quick patient search" under Search & Navigation) risk being implemented as two different search experiences if built by different people at different times.
- **Technical Impact:** Duplicate FRs inflate the requirement count without adding coverage and create two places a future BRD edit has to stay in sync.
- **Recommendation:** Keep one canonical search requirement and have the other section reference it.
- **Suggested BRD Text:** Under Search & Navigation, replace `"Quick patient search"` with: *"Quick patient search — see Patient Management § Search patients by name or phone number; this section adds the surrounding navigation (recent patients, profile↔visits) around that same search capability."*

---

## Missing Requirement Report

### MR-1: Walk-in patients — the BRD implies a workflow it never describes — **RESOLVED**
- **Severity:** Critical *(closed 2026-08-20)*
- **Resolution:** Confirmed by the stakeholder and now recorded in `.claude/agents/implementation-brd.md` and `.claude/agents/verification-brd.md`: appointments are preferred, but walk-in patients are explicitly allowed. The doctor sees appointment-booked patients by their scheduled time and fits walk-ins into availability between them. For a walk-in, the system creates an `Appointment` record at the time of registration/consultation (in the same flow, no separate manual booking step), so `Visit.AppointmentId` remains non-nullable and every visit stays linked to an appointment either way. **Double booking is also now explicitly rejected**: only one appointment may exist for a given date/time slot, scheduled or walk-in-generated alike.
- **Business Impact (as originally raised):** A general physician's clinic that only accepts scheduled patients is atypical — walk-ins are a normal part of daily operation. The resolution matches how the clinic actually operates rather than forcing a fake backdated slot workaround.
- **Technical Impact (as originally raised):** `implementation-brd.md` previously fixed `Visit.AppointmentId` as non-nullable while deferring the walk-in question — the schema shape survives unchanged; only the previously-deferred workflow decision (how a walk-in acquires its appointment record) is now settled, via a single service-layer path rather than two divergent ones.
- **Residual documentation gap (Low, not Critical):** Like the other resolved findings in this report, this decision lives in the agent config files, not in `BRD/Doc_BRD.md` itself.
- **Suggested BRD Text:** Add under Appointment Management: *"Appointments are preferred; walk-in patients are also supported. The doctor sees appointment-booked patients according to their scheduled time and fits walk-in patients into availability between appointments. For a walk-in patient, the system creates an appointment record at the time of registration/consultation, so every visit remains linked to an appointment. Only one appointment may be scheduled for a specific date/time slot; the system rejects creating another appointment for a date/time already occupied."*

### MR-2: "View recent patients" has no defined ranking
- **Severity:** High
- **Business Impact:** "Recent" could mean most-recently-registered or most-recently-seen — these produce materially different lists for a clinic with a mix of new and returning patients, and the doctor's expectation is almost certainly "who have I seen lately," not "who did I add to the system lately."
- **Technical Impact:** Untestable as written — there's no acceptance criterion to check a build against.
- **Recommendation:** Define "recent" explicitly as most-recently-visited, consistent with the search-ranking approach already recommended in `docs/brainstorm-brd-review.md` §4.1.
- **Suggested BRD Text:** Replace `"View recent patients"` under Search & Navigation with: *"View recent patients — a list of patients ranked by most-recent visit date (not registration date), showing the N most recently seen."*

---

## Healthcare Completeness Report

### HC-1: Emergency contact is entirely absent — **RESOLVED**
- **Severity:** Critical *(closed 2026-08-20)*
- **Resolution:** Confirmed by the stakeholder and now recorded in `.claude/agents/implementation-brd.md` and `.claude/agents/verification-brd.md`: `Patient` carries `EmergencyContactName` and `EmergencyContactPhone`, alongside the already-fixed Allergies/CurrentMedications/ChronicConditions.
- **Business Impact (as originally raised):** A clinical setting that records vitals and administers treatment has a real, if infrequent, chance of an adverse event during a visit. With no emergency contact on file, the clinic had no fast path to reach family in that scenario — now resolved.
- **Technical Impact (as originally raised):** Not present in the BRD, and not added by the fixed Patient spec at the time HC-1 was first raised. Closed in the same pass that also resolved HC-2.
- **Residual documentation gap (Low, not Critical):** Like the other resolved findings in this report, this decision lives in the agent config files, not in `BRD/Doc_BRD.md` itself.
- **Suggested BRD Text:** Add under Patient Management § Capture: *"Emergency Contact Name and Emergency Contact Phone."*

### HC-2: Medical/surgical history is not distinguished from chronic conditions — **RESOLVED (explicit exclusion)**
- **Severity:** High *(closed 2026-08-20)*
- **Resolution:** Confirmed by the stakeholder and now recorded in `.claude/agents/implementation-brd.md` and `.claude/agents/verification-brd.md`: no separate `MedicalSurgicalHistory` field in Phase 1. This is a deliberate scope decision, not an oversight — the per-visit prescription record is judged sufficient history for now.
- **Business Impact (as originally raised, now an accepted tradeoff):** `ChronicConditions`/`CurrentMedications` cover ongoing state but not past surgeries or resolved-but-relevant events. The stakeholder has weighed a dedicated field against the per-visit prescription history already being maintained and judged the latter sufficient for Phase 1.
- **Technical Impact:** None — no schema change follows from this decision either way; it closes the open question rather than adding anything.
- **Residual documentation gap (Low, not High):** Like the other resolved findings in this report, this decision lives in the agent config files, not in `BRD/Doc_BRD.md` itself.
- **Recommendation:** If this ever needs revisiting (e.g., the per-visit record proves insufficient for a specific clinical need), the field can be added later without disturbing existing data — it's purely additive.
- **Suggested BRD Text:** Add under Patient Management § Capture, as a note rather than a new field: *"Medical/surgical history is not captured as a dedicated field in Phase 1; the per-visit prescription and diagnosis record is considered sufficient history."*

### HC-3: Height, weight-derived BMI, and SpO2 are inconsistently covered
- **Severity:** Medium
- **Business Impact:** `implementation-brd.md`'s Consultation Vitals spec already fixes Weight (kg, 3 decimal places) and the BRD's own vitals list covers temperature/BP/pulse — but Height (needed to make Weight clinically useful via BMI) and SpO2 (a standard vital in many GP consultations) are absent from every document.
- **Technical Impact:** BMI can't be computed or displayed without Height even though Weight is already being captured — the data currently collected is less useful than it could be for a marginal schema addition.
- **Recommendation:** Add Height as an optional vitals field (mirroring Weight's precision handling) and consider SpO2 as a stretch addition — flag both to the stakeholder rather than assuming either is wanted, since the BRD's vitals list was explicit and short (temp/BP/pulse) and this is genuinely additive scope.
- **Suggested BRD Text:** Add under Consultation Workflow § Vitals Capture: *"Height (optional, cm) — recorded to enable BMI calculation alongside Weight. SpO2 (optional, %) — recorded when a pulse oximeter is available."*

### HC-4: Allergies, current medications, and chronic conditions are decided but not written into the BRD
- **Severity:** Medium
- **Business Impact:** These fields exist for the build (via `implementation-brd.md`) and materially improve prescribing safety — the actual clinical risk from the original gap has already been addressed. The remaining risk is documentation drift: a future stakeholder reading only the BRD wouldn't know these fields exist.
- **Technical Impact:** None for the build itself; this is purely a source-of-truth consistency gap.
- **Recommendation:** Copy the already-accepted decision back into the BRD so the document matches what's actually being built.
- **Suggested BRD Text:** Add under Patient Management § Capture: *"Allergies (free text), Current Medications (free text), Chronic Conditions (free text)."*

---

## Architecture Completeness Report

### AC-1: No backup mechanism is actually adopted
- **Severity:** Critical
- **Business Impact:** "Regular automated backups" and "no data loss" are stated NFRs for a system holding irreplaceable clinical records, and there is currently nothing behind either — a hardware failure on the doctor's single local machine today would be unrecoverable.
- **Technical Impact:** `docs/brainstorm-brd-review.md` §3.5 proposes a mechanism (Windows Task Scheduler + `sqlcmd BACKUP DATABASE`, chosen for compatibility with the recommended SQL Server Express edition) but it is a recommendation, not a decision recorded in `implementation-brd.md` or the BRD. Nothing currently blocks someone from shipping without it, since no document treats it as a hard requirement with an owner.
- **Rework Risk:** Low technical rework (the mechanism itself is a small script + a scheduled task, not an architectural commitment that constrains other decisions) — but the risk of shipping without it is data loss, which is unrecoverable by definition. Decide before general release, even if it's decided after most feature work.
- **Recommendation:** Formally adopt a backup mechanism (the brainstorm recommendation is reasonable) and record it as an NFR implementation, not just a plan-doc suggestion.
- **Suggested BRD Text:** Replace `"Regular automated backups"` under Reliability with: *"Regular automated backups: a nightly scheduled task performs a full SQL Server backup to a local (or external) storage location, with a documented restore procedure. Backup success/failure is logged and checked periodically."*

### AC-2: No password-recovery path for the single doctor account — **RESOLVED (accepted risk)**
- **Severity:** Critical *(closed 2026-08-20)*
- **Resolution:** Confirmed by the stakeholder and now recorded in `.claude/agents/implementation-brd.md` ("No password-reset or recovery flow in Phase 1") and `.claude/agents/verification-brd.md`: password-reset/recovery functionality is explicitly not required for Phase 1. This is an accepted-risk decision, not an oversight — its absence is not a finding, and adding one unprompted would itself now be a deviation from the fixed spec.
- **Business Impact (as originally raised, now an accepted tradeoff):** If the doctor forgets the password, there is no way back in — every patient record becomes inaccessible through the application until whoever supports the doctor intervenes directly (e.g., resetting the row via SSMS/EF Core). The stakeholder has weighed this against building recovery infrastructure for a single-user, single-machine app and accepted the risk for now.
- **Technical Impact:** `implementation-brd.md`'s Authentication spec already ruled out registration/self-signup and any advanced auth mechanism; this decision closes the one remaining gap in that spec rather than leaving it silently unaddressed.
- **Residual documentation gap (Low, not Critical):** Like CR-1, this decision lives in the agent config files, not in `BRD/Doc_BRD.md` itself.
- **Recommendation:** If this ever needs revisiting (e.g., if the doctor is actually locked out in practice), the lowest-cost fallback remains a manual procedure — direct DB/EF Core access by whoever supports the doctor — rather than building self-service recovery infrastructure this single-machine app doesn't otherwise need.
- **Suggested BRD Text:** Add under Security: *"Password recovery: not implemented in Phase 1 — an accepted scope decision for a single-user, local-only application. If the doctor's password is lost, recovery requires direct technical support (e.g., a database-level reset); there is no self-service or automated recovery flow."*

### AC-3: Scope of "data encryption at rest" beyond the password field is undefined
- **Severity:** High
- **Business Impact:** The BRD's encryption NFR reads as covering all patient data, but the only encryption currently specified anywhere is the doctor's own login password. Whether the broader clinical data (vitals, diagnoses, prescriptions) is encrypted at rest in SQL Server is an open question with real privacy implications for a system holding health records.
- **Technical Impact:** SQL Server Transparent Data Encryption (TDE) or equivalent isn't mentioned in `implementation-brd.md`'s fixed stack notes — if this is intended to be covered, it needs to be a documented setup step (TDE is licensing-tier-dependent on some SQL Server editions); if it's intentionally out of scope for a local-only, single-machine deployment, that's a defensible position but needs to be a stated decision, not a silent gap.
- **Rework Risk:** Low to enable now (a setup-time configuration, not a schema change); higher if assumed-but-unverified and only discovered during a later security review.
- **Recommendation:** State explicitly whether "at rest" covers the full database (recommend: yes, via TDE if the licensed SQL Server edition supports it, or full-disk encryption on the doctor's machine as the practical equivalent for Express) or is scoped to credentials only.
- **Suggested BRD Text:** Replace `"Data encryption (at rest and in transit)"` under Security with: *"Data encryption at rest: covers the full patient database via SQL Server Transparent Data Encryption where the licensed edition supports it, or full-disk encryption on the host machine as the practical equivalent; the login password additionally uses application-level reversible encryption (see Secure login). In transit: not applicable beyond the local machine — the application has no network exposure beyond `localhost`."*

### AC-4: Deployment/hosting model decided but not reflected in the BRD text
- **Severity:** Low
- **Business Impact:** None for the build — this is resolved. Purely a documentation-consistency note: a stakeholder reading the BRD wouldn't know the app is local-only with no hosting.
- **Technical Impact:** None.
- **Recommendation:** Copy the decision into the BRD for traceability.
- **Suggested BRD Text:** Add under Non-Functional Requirements, a new subsection: *"**Deployment:** the application runs entirely locally on the doctor's own machine, permanently — no hosting, no staging/production environment, no remote access."*

### AC-5: Data retention policy has a technical answer but no stated rationale
- **Severity:** Medium
- **Business Impact:** `implementation-brd.md` fixes "patients are never deleted" as a technical rule, which is a defensible clinical-records posture, but the BRD never states a retention *policy* this rule is satisfying — there's no reference to how long records should legally or operationally be kept, just a technical "never" that happens to be a superset of any real requirement.
- **Technical Impact:** None currently — the technical behavior (never delete) trivially satisfies any retention period. The gap is purely that the BRD can't currently answer "why do we keep this forever" if asked.
- **Recommendation:** State the rationale explicitly, even if it's simply "no defined retention limit for Phase 1; records are kept indefinitely by default."
- **Suggested BRD Text:** Add under Non-Functional Requirements § Reliability: *"Data retention: patient and visit records are retained indefinitely; there is no automated deletion or archival in Phase 1."*

### AC-6: Fixed Patient/Prescription/Export decisions from `implementation-brd.md` aren't cited in the BRD
- **Severity:** Medium
- **Business Impact:** None for the build (already covered by AC-6 being folded from the matrix notes above and HC-4) — grouped here as a single architecture-level observation: the BRD is no longer the actual source of truth for several load-bearing decisions (PatientId is a 0-based sequential integer, Phone is optional and intentionally non-unique, prescriptions are immutable with versioning-by-new-record, the CSV export produces two deliberately overlapping files). A developer who reads only the BRD would not discover any of this.
- **Technical Impact:** Traceability risk — future BRD edits made without awareness of these decisions could silently propose something that contradicts already-built behavior.
- **Recommendation:** Either fold the key decisions into the BRD's Functional Requirements/NFR sections (preferred, keeps one source of truth) or add an explicit "See `.claude/agents/implementation-brd.md` for locked implementation decisions" pointer so readers know to look there.
- **Suggested BRD Text:** Add near the top of the document, after Product Goal: *"**Implementation note:** several data-model and behavioral details (patient identifier scheme, prescription immutability, export file formats, authentication mechanism) are locked in `.claude/agents/implementation-brd.md` and are binding even though not fully restated here."*

---

## Open Questions

The BRD states "Open Questions: None." That does not hold. Real open questions, none of them answered anywhere in the repo yet:

- Backup mechanism (AC-1) — a resolution is proposed but not adopted.
- Scope of "at rest" encryption beyond the login password (AC-3).
- "Recent patients" ranking definition (MR-2).
- Whether Height/SpO2 (HC-3) are wanted — this one is a genuine stakeholder call, not something to resolve unilaterally.
- Measurement method/baseline for the "80% paper reduction" success criterion (TR-7).

*Resolved and removed from this list across both refresh passes:* password recovery (accepted Phase 1 exclusion — see AC-2), the 2–3 minute consultation criterion's meaning (workflow completeness — see CR-1), and walk-in handling plus double booking (both now explicitly specified — see MR-1) are no longer open questions.

---

## Risk Register

| Risk | Likelihood | Impact | Severity | Owner/Mitigation |
|---|---|---|---|---|
| Local machine failure with no backup in place | Medium (any single machine eventually fails or is lost/stolen) | Total loss of all patient records | Critical | Adopt AC-1's backup mechanism before general release. |
| Doctor forgets password, no recovery path | Low-Medium (rare but plausible over years of use) | Total lockout from the application until a support contact intervenes directly | **Accepted** *(was Critical)* | Stakeholder decision confirmed 2026-08-20: no password-reset functionality for Phase 1. Risk is understood and intentionally carried, with manual DB-level reset as the documented fallback — see AC-2. |
| "Secure login" read as stronger than the accepted reversible-encryption design | Medium (likely to surface in any later security review) | Reputational/audit-finding risk, not a live technical vulnerability given the no-network deployment | High | Document the accepted tradeoff per CR-3 before any external review happens. |

---

## Testability Review

### TR-1: "Simple, minimal UI optimized for fast data entry" (Usability NFR)
- **Severity:** Medium
- **Business Impact:** Without a measurable definition, "simple" and "fast" can't be checked at acceptance time — the doctor's subjective reaction becomes the only test, which is fine for a demo but not a repeatable acceptance criterion.
- **Technical Impact:** No pass/fail condition exists for QA or `verification-brd` to check against.
- **Recommendation:** Convert into a measurable proxy — the consultation-time Success Criterion already does this for the consultation path; extend the same idea to general navigation.
- **Suggested BRD Text:** Replace with: *"Usability: any primary workflow (patient search, appointment creation, consultation entry) is completable in no more than 3 clicks/screens beyond the entry point, measured during usability testing with the doctor."*

### TR-2: "Fast patient search and retrieval" (Performance NFR)
- Covered under Contradiction Report CR-2 (duplicate of the measurable Success Criterion) — recommend deletion rather than a separate rewrite.

### TR-3: "No data loss" (Reliability NFR)
- **Severity:** High
- **Business Impact:** As stated, this can never be definitively verified — "no data loss" is a property, not a test. It needs a proxy that can actually be checked.
- **Technical Impact:** No acceptance test can target this line directly.
- **Recommendation:** Convert to a testable backup/recovery guarantee, tying directly into AC-1.
- **Suggested BRD Text:** Replace with: *"Reliability: any data successfully saved through the application is recoverable from the most recent automated backup (see Backups), verified by a quarterly restore drill."*

### TR-4: "Secure login" (Security NFR)
- Covered under Contradiction Report CR-3 — the suggested text there resolves both the contradiction and the measurability gap in one edit.

### TR-5: "Moderate patient volume" (Scalability NFR)
- **Severity:** Medium
- **Business Impact:** Without a number, there's no way to know whether the chosen search/indexing/backup approach (§3.3 and §4.1 of `docs/brainstorm-brd-review.md`, both sized against an assumed "clinic-scale" volume) is actually sufficient.
- **Technical Impact:** SQL Server Express's size cap and the prefix-search approach both depend on an assumed ceiling that's currently just "moderate."
- **Recommendation:** Pin an actual number.
- **Suggested BRD Text:** Replace `"Designed for a single clinic with moderate patient volume"` with: *"Designed for a single clinic with up to 5,000 patients and 25,000 visits over the system's operating life, comfortably within SQL Server Express's size limits."*

### TR-6: "Modern web browsers (Chrome, Edge, Safari)" (Compatibility NFR)
- **Severity:** Low
- **Business Impact:** "Modern" without a version floor means a build could be tested against whatever browser version happens to be installed during development and nothing else.
- **Technical Impact:** No fixed target for cross-browser testing.
- **Recommendation:** Pin minimum versions (or "current stable + one prior major version," a common practical standard).
- **Suggested BRD Text:** Replace with: *"Compatible with the current stable release and one prior major version of Chrome, Edge, and Safari."*

### TR-7: "At least 80% reduction in paper usage" (Success Criterion)
- **Severity:** Medium
- **Business Impact:** This has a real number, which is good, but no stated baseline or measurement method — 80% reduction from what measured quantity, over what period, measured how?
- **Technical Impact:** Unmeasurable as written despite looking measurable at a glance — the number creates false confidence.
- **Recommendation:** Define the baseline and method explicitly.
- **Suggested BRD Text:** Replace with: *"At least 80% reduction in paper usage, measured by comparing sheets of paper used per consultation in the month before go-live against the month three months after go-live."*

### TR-8: "Smooth generation and printing of prescriptions" (Success Criterion)
- **Severity:** Low
- **Business Impact:** "Smooth" has no operational meaning; the consultation-time budget (CR-1's suggested text) and the printing implementation itself (`docs/plan-brd-review.md` Step 10) already give this a real target if restated in those terms.
- **Technical Impact:** Not independently testable.
- **Recommendation:** Fold into a concrete timing/reliability statement.
- **Suggested BRD Text:** Replace with: *"Prescription generation and printing completes in under 5 seconds from clicking 'Print' to the print dialog appearing, with no manual reformatting required."*

### TR-9: "High usability with minimal training required" (Success Criterion)
- **Severity:** Medium
- **Business Impact:** This is the BRD's closest statement to an onboarding requirement, and as written it can't be checked before or after launch.
- **Technical Impact:** No test exists for "minimal training."
- **Recommendation:** Convert to a concrete onboarding-time target.
- **Suggested BRD Text:** Replace with: *"A doctor with no prior exposure to the system can complete a full consultation record (registration through printed prescription) unassisted within 15 minutes of first use."*

---

## Developer Readiness Assessment

A developer **can** start building today against every feature slice in `docs/plan-brd-review.md`, including Patient and Appointment/Visit — those are genuinely well-specified once `implementation-brd.md` is read alongside the BRD (though it shouldn't have to be a second document; see AC-6). Every Critical finding that ever blocked a feature slice — password recovery, walk-in support, and, as of this refresh, emergency contact — is now a closed decision. One thing blocks calling this "done" even after every feature ships: the backup mechanism (AC-1) — Critical, currently zero owner, not a UI polish item that can be deferred past general release, and now the *only* Critical finding left in this document.

In priority order, what specifically blocks a clean build:
1. Backup mechanism adoption (AC-1) — blocks general release, not initial development, and is the sole remaining Critical finding in this document.
2. Everything else in this report is real but non-blocking — the remaining clinical field question (HC-3: Height/SpO2/BMI, a genuine stakeholder call), measurability rewrites (TR-1 through TR-9), and documentation-consistency fixes (AC-4/AC-5/AC-6) can land incrementally without stopping feature work.

## Final Verdict

**Ship the full build now — patient, appointment/visit, consultation, prescription, and export are all unblocked; do not ship general release without a backup mechanism.** With password recovery (AC-2), walk-in support (MR-1), and emergency contact (HC-1) all now resolved as explicit decisions, the backup mechanism (AC-1) is the single Critical finding with no owner left in this document — the one thing that would move this verdict from "ship the build" to "ship, full stop" is adopting a concrete mechanism (`docs/brainstorm-brd-review.md` §3.5 already proposes one) before general release, even if it's decided after most feature work is built.
