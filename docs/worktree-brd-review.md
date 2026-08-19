# BRD Review — Doc_BRD.md

**Reviews:** `BRD/Doc_BRD.md` @ blob `212cddc` (198 lines, unmodified)
**Date:** 2026-08-19
**Branch/commit:** `brd/review-report`, based on `main` @ `5d512ca`
**Prior analysis reused:** `docs/brainstorm-review.md` (cited **BR §n**), `docs/planning-review.md` (cited **PR**). Where those documents already establish a finding, this review cites them and assigns severity rather than re-deriving the analysis.
**Scope of this document:** review only. No BRD text was modified. Every "Suggested BRD Text" below is a proposal requiring stakeholder acceptance, not an applied change.

---

## Executive Summary

The BRD is a competent early scoping document and a poor build specification. It describes a coherent product with a genuinely well-drawn exclusion list — nothing in the Scope section smuggles back in something the exclusions rule out, which is rarer than it sounds. Its weakness is not scope creep but unearned confidence: it states requirements as settled that are in fact contradictory, unmeasurable, or dependent on decisions the document never makes. Eight pairs of requirements cannot all hold simultaneously, and the sharpest of them — mandatory vitals, single-user access, and a 2–3 minute consultation — binds the doctor's own time three ways at once and cannot be resolved by implementation cleverness. As a clinical system that prescribes medication, it is missing an allergy field, a current-medication list, and weight, which is a patient-safety gap rather than a documentation gap. Architecturally it never states a hosting model, which is upstream of backup, disaster recovery, encryption, deployment, and support, and is the single decision most likely to force rework if deferred. Roughly a quarter of its requirements are build-ready as written; the rest need a decision, a number, or a resolution first. The single highest-leverage problem is the closing line "Open Questions: None", because it asserts a closure the document does not have and thereby suppresses every question below. **This document is not currently safe to build from without a decision session first.** It is, however, a good starting point for that session — the gaps are all closable on paper, and none require code to investigate.

---

## BRD Quality Score: 3.0 / 10

Average of five sub-scores:

- **Completeness (functional + domain coverage): 4** — the stated administrative workflow (register → schedule → consult → prescribe → print → history → export) is covered end to end with no missing step. Clinical completeness is much weaker: a prescribing system with no allergies, no current-medication list, and no weight is incomplete on domain grounds regardless of scope decisions, and printing depends on a Clinic Profile feature no requirement creates.
- **Consistency (freedom from contradiction): 3** — eight contradictions, one Critical and four High. They are not edge cases: they involve the primary success criterion, the security NFR, and the reliability NFR.
- **Testability (share of requirements with measurable acceptance criteria): 3** — four requirements carry real numbers (<2s page load, 2–3 min, 2–5s, 80%). Against those sit "fast", "smooth", "successful", "high usability", "easy", "basic", "quick", "regular", "moderate", and "no data loss" — ten unquantified terms, several of them load-bearing success criteria.
- **Architecture readiness (share of architectural decisions documented): 2** — of the twelve architectural areas a system like this must settle, two are gestured at (encryption, backups) and both without specifics. Hosting, disaster recovery, authorization, password recovery, audit logging, data retention, deployment/update model, and expected load are entirely absent.
- **Traceability (requirements linked to a clear source/rationale): 3** — the Problem Statement does real work and the Scope section plausibly follows from it. But no requirement carries an ID, so nothing downstream can reference anything precisely; no requirement carries a rationale; and "Open Questions: None" claims a decision history the document does not show.

---

## Development Readiness: 26%

Counting a requirement build-ready only if it is unambiguous **and** non-contradictory **and** testable **and** not blocked by an undocumented architecture decision:

**9 of 35 requirements are build-ready as written.**

Build-ready: add/edit/view patient details; view daily appointment list; update appointment status; complaints free text; diagnosis notes; view previous visits; filter history by date; page load < 2 seconds; browser compatibility (Chrome/Edge/Safari).

The other 26 are blocked by at least one of: an unresolved contradiction (7), an undefined term with no threshold (11), a missing upstream decision (6), or a dependency on a feature the BRD never specifies (2).

---

## Critical Findings

Nine Critical issues. Each blocks development or carries a patient-safety / data-integrity risk.

1. **CON-1 — Mandatory vitals + single user + 2–3 minute consultation cannot all hold.** The BRD excludes receptionist access, mandates temperature/BP/pulse for every consultation, and targets a 2–3 minute record. All three bind the same resource: the doctor's own hands and time. Resolving this changes either the access model, the mandate, or the target.
2. **HC-1 — No allergy field in a system whose primary output is a prescription.** The BRD nowhere records drug allergies or prior adverse reactions, and the exclusion list does not exclude them. This is the clearest patient-safety gap in the document.
3. **HC-2 — No current / long-term medication list.** Without it the system cannot show what the patient is already taking at the moment of prescribing, making duplicate therapy and interaction risk invisible.
4. **AR-1 — Hosting model is absent entirely.** "Web-based" fixes the client, not the deployment. Cloud vs. on-premise determines backup, outage behaviour, encryption, updates, cost, and support, and is not mentioned anywhere.
5. **MR-1 — The printable prescription depends on a Clinic Profile feature no requirement creates.** Header and footer content (clinic name, doctor's qualifications, medical registration number, logo, signature) has no requirement anywhere, yet two separate requirements depend on it. The registration number is legally expected on a prescription in most jurisdictions.
6. **MR-2 — "Age / DOB" is written as if the two are interchangeable.** They are not. Getting this wrong is silent data corruption in a clinical record, not a visible bug, and it propagates to every history display and every reprint.
7. **MR-3 — Whether a printed prescription can be amended is unspecified.** The BRD has no immutability, versioning, or amendment rule for a document that has left the building in a patient's hands. Medico-legally significant, and retrofitting immutability is a rewrite rather than a patch (BR §10).
8. **CON-4 / AR-2 — "No data loss" is asserted with no RPO, no retention, no destination, no named restorer, and no restore test.** As written it is a sentence, not a requirement, and it sits alongside an offline exclusion that makes availability during an outage undefined.
9. **META-1 — "Open Questions: None (all major product decisions defined for Phase 1)" is false.** `docs/brainstorm-review.md` §8 already lists 18 open questions, six of them schema- or topology-level. This line is Critical not because it is wrong but because it is load-bearing: it tells every reader to stop asking.

---

## Requirement Analysis Matrix

Classification is `Existing` throughout — this reviews an unmodified baseline, not a proposed change.

| Requirement | Category | Classification | Scope tag | Consistency | Testable | Notes |
|---|---|---|---|---|---|---|
| Single user (GP); no receptionist access | Scope/Users | Existing | `[in scope]` | Conflicts with: F-Vitals, SC-Consultation-Time | Yes | CON-1 Critical. Clear as written; contradicts two other requirements |
| Web-based (browser) access | Scope | Existing | `[in scope]` | OK | No | AR-1 Critical. Hosting model undocumented; "web-based" ≠ internet-reachable |
| Add, edit, view patient details | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready.** Deletion/archival policy absent (MR-4) |
| Capture Name, Age/DOB, Gender, Contact | Functional | Existing | `[in scope]` | Internally ambiguous | No | MR-2 Critical. Age and DOB conflated; no field marked mandatory |
| Search patients by name or phone | Functional | Existing | `[in scope]` | Conflicts with: patient population (no-phone, shared-phone) | No | CON-6 High. Match semantics undefined |
| Schedule appointments | Functional | Existing | `[in scope]` | OK | No | MR-5 High. No slot length, double-booking rule, or duration |
| View daily appointment list | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready.** Clinic-day/timezone boundary undefined (AR-9, Medium) |
| Update appointment status (4 values) | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready.** Lapse behaviour for stale rows undefined |
| Mandatory vitals — temp, BP, pulse | Functional | Existing | `[in scope]` | Conflicts with: Single-user, SC-Consultation-Time | No | CON-1 Critical. No units, no format, no escape hatch (HC-9) |
| Complaints (free text) | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready.** No length bound (TS-10, Low) |
| Diagnosis notes | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready.** Free text forecloses future analytics — consistent with exclusions, but a one-way door |
| Add medicines (name/dosage/frequency/duration/instructions) | Functional | Existing | `[in scope]` | OK | No | MR-6 High. Free text vs coded undecided; no formulary |
| Generate printable prescription | Functional | Existing | `[in scope]` | Depends on absent Clinic Profile | No | MR-1 Critical. Paper size, letterhead, registration number all unspecified |
| View previous visits + vitals/complaints/dx/Rx | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready** |
| Filter history by date | Functional | Existing | `[in scope]` | OK | Yes | **Build-ready** |
| Quick patient search | Functional | Existing | `[in scope]` | Duplicates "Search patients by name or phone" | No | "Quick" unquantified; near-duplicate requirement |
| View recent patients | Functional | Existing | `[in scope]` | OK | No | "Recent" undefined — how many, what window |
| Easy navigation profile↔visits | Functional | Existing | `[in scope]` | OK | No | TS-7 Medium. "Easy" is not a testable condition |
| Export patient/visit data as CSV | Functional | Existing | `[in scope]` | Conflicts with: NFR-Encryption | No | CON-2 High. Entity, columns, scope all undefined (MR-7) |
| Export patient/visit data as PDF | Functional | Existing | `[in scope]` | Conflicts with: NFR-Encryption | No | CON-2 High. Same contract gap |
| NFR Usability — simple, minimal UI | NFR | Existing | `[in scope]` | Tension with: mandatory structured fields | No | TS-1 Medium |
| NFR Performance — page load < 2s | NFR | Existing | `[in scope]` | OK | Yes | **Build-ready.** Cold vs warm unstated but workable |
| NFR Performance — fast search and retrieval | NFR | Existing | `[in scope]` | Duplicates SC-Search-Time | No | TS-2 Medium. "Fast" unquantified |
| NFR Reliability — no data loss | NFR | Existing | `[in scope]` | Conflicts with: offline exclusion, single-machine scope | No | AR-2 Critical. Untestable absolute |
| NFR Reliability — regular automated backups | NFR | Existing | `[in scope]` | OK | No | AR-2 High. "Regular" undefined; no destination/retention/restore test |
| NFR Security — secure single-user login | NFR | Existing | `[in scope]` | OK | No | AR-4 High. No password recovery path; launch-blocking lockout risk |
| NFR Security — encryption at rest and in transit | NFR | Existing | `[in scope]` | Conflicts with: Export (CSV/PDF), SC-Search-Time | No | CON-2, CON-3 High. "At rest" scope (disk vs column) undefined |
| NFR Scalability — single clinic, moderate volume | NFR | Existing | `[in scope]` | OK | No | TS-5 Medium. "Moderate" unquantified — no load target exists |
| NFR Compatibility — Chrome, Edge, Safari | NFR | Existing | `[in scope]` | OK | Yes | **Build-ready.** Safari daily-driver unconfirmed (Low) |
| SC — consultation record in 2–3 minutes | Success Criterion | Existing | `[in scope]` | Conflicts with: Mandatory vitals, Single-user | No | CON-1 Critical. Number given, measurement method absent |
| SC — search/history retrieval in 2–5 seconds | Success Criterion | Existing | `[in scope]` | Conflicts with: NFR-Encryption | No | CON-3 High. Generous to the point of measuring little (BR §7.10) |
| SC — ≥80% reduction in paper usage | Success Criterion | Existing | `[in scope]` | Conflicts with: printing a prescription per patient | No | CON-5 High. No baseline defined |
| SC — smooth generation and printing | Success Criterion | Existing | `[in scope]` | OK | No | TS-3 Medium. "Smooth" has no pass/fail |
| SC — successful CSV/PDF export | Success Criterion | Existing | `[in scope]` | OK | No | TS-4 Medium. "Successful" undefined absent an export contract |
| SC — high usability, minimal training | Success Criterion | Existing | `[in scope]` | Tension with: mandatory structured fields | No | TS-6 Medium |
| Exclusions (10 items) | Exclusion | Existing | `[in scope]` as an exclusion list | Two exclusions are load-bearing (receptionist, offline) | n/a | Internally coherent; nothing in Scope re-includes an excluded item |

**Downstream impact (folded in per spec):** `docs/brainstorm-review.md` and `docs/planning-review.md` both declare `BRD/Doc_BRD.md` as source and consume it in full; `.claude/agents/brainstorm.md:12` and `.claude/agents/plan-brd.md:12` instruct agents to treat it as the source of truth for scope, so the BRD is a live tooling input, not just documentation. `docs/planning-review.md` carries ten assumptions (A1–A10) standing in for decisions the BRD never makes, with three hard gates (A3 blocks its Step 19, A9 its Step 35, A10 its Step 42). A `Grep` for application or schema code found none — the repository is documentation only. **Consequence: every contradiction below is still free to fix.** Any future BRD edit touching vitals, backup, or support invalidates the corresponding PR assumption.

---

## Contradiction Report

### CON-1 — Mandatory vitals × single-user access × 2–3 minute consultation
**Sides:** "Mandatory vitals capture (temperature, BP, pulse)" and "Record for every consultation" (Scope; Consultation Workflow) × "General Physician (Single User)" / "Receptionist access not included in Phase 1" (Users; Out of Scope) × "Doctor can complete a consultation record within 2–3 minutes" (Success Criteria).
**Why they conflict:** all three bind one resource — the doctor's own time and hands. In clinics that hit a 2–3 minute consultation record, vitals are taken by a nurse or assistant *before* the doctor sees the patient. Excluding a second user moves three physical measurements (thermometer, cuff, pulse) plus their data entry onto the critical path the BRD simultaneously wants compressed. This is not an implementation-efficiency problem; no UI can make taking a blood pressure faster.
**Severity:** Critical
**Business Impact:** The doctor falls behind on a busy day, reverts to paper "just for the rush", and does not come back (BR §6). Alternatively the mandate is satisfied reflexively — typing `120/80` without measuring — which produces a false clinical record, worse than a blank one.
**Technical Impact:** The team builds a hard `NOT NULL` mandate against a 150-second budget it cannot meet, then discovers post-launch that either the mandate or the target must be abandoned. If vitals are enforced at column level, drafts cannot autosave (BR §1.2), which forces a schema change plus a rewrite of the consultation save path.
**Recommendation:** Keep the mandate, drop the pretence that it is free. Resolve in the BRD by (a) enforcing vitals at *finalize* rather than at entry, (b) adding an explicit "unable to record — reason" escape hatch that prints as "not recorded", and (c) restating the time target as covering *data entry*, excluding physical measurement time. If the stakeholder instead wants the 2–3 minute target to include measurement, then receptionist access must move out of the exclusion list — that is the honest trade.
**Suggested BRD Text:**
> **Vitals Capture (Mandatory at finalization)**
> - Temperature, Blood Pressure, and Pulse must be recorded before a consultation can be marked Completed.
> - A consultation may be saved as a draft with vitals incomplete.
> - Where a vital genuinely cannot be measured, the doctor may record "Unable to record" with a reason; this prints on the prescription as "not recorded". A vital must never be left to be guessed.
>
> **Success Criteria (revised)**
> - Doctor can complete a consultation *record* within 2–3 minutes, measured from opening the consultation screen to printing the prescription, and excluding time spent physically measuring vitals.

---

### CON-2 — Encryption at rest × CSV/PDF export
**Sides:** "Data encryption (at rest and in transit)" (NFR Security) × "Export patient or visit data as CSV / PDF" (Scope; Functional Requirements).
**Why they conflict:** they bind the same constraint — the confidentiality boundary around PHI. Export is a deliberate, user-invoked path for patient data to leave the encrypted store and land unencrypted in a `Downloads` folder on a shared clinic desktop, where it persists indefinitely. The encryption requirement is silent about the exported artifact, so as written the system satisfies both requirements while leaking a complete health extract.
**Severity:** High
**Business Impact:** A full unencrypted patient extract sitting on a shared desktop is the most likely real-world privacy breach this product will cause, and the one the doctor is least likely to notice. In jurisdictions treating health data as sensitive (e.g. India's DPDP Act 2023), it is also the hardest to defend.
**Technical Impact:** Without a stated boundary, the team implements export as an unrestricted "download everything" button, and retrofitting scoping, logging, and confirmation later means changing a feature users already rely on.
**Recommendation:** State that the encryption requirement covers data at rest *within the system*, and constrain export explicitly: no unscoped full-database export, every export logged, an explicit confirmation step. Friction is correct here, unlike on the consultation path.
**Suggested BRD Text:**
> **Data Export (revised)**
> - Export is scoped to a selected patient or an explicit date range. There is no "export entire database" action.
> - Every export is recorded in an export log (timestamp, type, filter, row count).
> - Exporting requires an explicit confirmation acknowledging that the exported file is not encrypted by the application.
> - The encryption-at-rest requirement applies to data held by the application; exported files are outside that boundary by design.

---

### CON-3 — Encryption at rest × 2–5 second search
**Sides:** "Data encryption (at rest)" (NFR Security) × "Patient search and history retrieval within 2–5 seconds" (Success Criteria), together with "Fast patient search and retrieval" (NFR Performance).
**Why they conflict:** they bind index-ability. If "encryption at rest" is read as column-level encryption on `name` and `phone` — a legitimate reading of the phrase — then those columns cannot be meaningfully indexed for prefix or substring search, and every search degrades to a full decrypt-and-scan. The requirement is ambiguous in a way that makes one reading incompatible with the performance target (BR §3.6).
**Severity:** High
**Business Impact:** Either search is slow enough to lose the doctor's trust, or the security requirement is quietly interpreted down without anyone recording that a decision was made.
**Technical Impact:** Discovered late, this forces either removing column encryption after the schema ships, or bolting on a searchable-ciphertext scheme — both expensive relative to deciding now.
**Recommendation:** Disambiguate to disk/volume/managed-database encryption, which satisfies the realistic threat (a stolen clinic PC) without touching indexes.
**Suggested BRD Text:**
> **Security (revised)**
> - Data encryption in transit (TLS 1.2 or higher for any non-loopback access).
> - Data encryption at rest at the **disk or database-volume level** (e.g. BitLocker with TPM on-premise, or managed-database encryption if hosted). Column-level encryption of searchable fields is explicitly out of scope, as it is incompatible with the stated search performance target.

---

### CON-4 — Offline excluded × "No data loss" × web-based access
**Sides:** "Offline functionality" (Out of Scope) × "No data loss" (NFR Reliability) × "Web-based access (browser-based system)" (Scope).
**Why they conflict:** they bind availability. If "web-based" is realised as internet-hosted, then an internet outage during clinic hours stops the clinic entirely, and any consultation in progress is at risk — which is precisely what "no data loss" promises against. The BRD excludes the mitigation (offline mode) without stating the alternative, and never fixes the hosting model that decides whether the risk exists at all.
**Severity:** High
**Business Impact:** A dead clinic at 11am on a Tuesday with a waiting room full of patients. This is the failure mode most likely to end adoption permanently.
**Technical Impact:** If the team assumes cloud hosting and the risk is only recognised later, the options are building offline sync (4–8 weeks plus a permanent stream of conflict bugs, per BR §3.3) or relocating the deployment after the fact.
**Recommendation:** Excluding offline *application* functionality is correct and should stand. Resolve it at the system level instead — decide hosting (AR-1), and record the outage strategy in the BRD as a reliability requirement rather than leaving it implied.
**Suggested BRD Text:**
> **Reliability (addition)**
> - Offline functionality is out of scope for the application. System-level availability during an internet outage is addressed by the deployment model (see Hosting), not by application-level sync.
> - The system must remain usable during an internet outage of up to one clinic day, or the deployment model must document the accepted downtime.
> - Consultations entered on paper during an outage must be enterable afterwards with a user-settable visit date.

---

### CON-5 — 80% paper reduction × printing a prescription for every patient
**Sides:** "At least 80% reduction in paper usage" (Success Criteria) × "Printable prescriptions" / "Generate printable prescription" (Scope; Functional Requirements).
**Why they conflict:** they bind paper. The system's primary output is a printed page per consultation. If the clinic currently writes one carbon-copy prescription per patient, printing one page per patient is not obviously a reduction at all, and the criterion as stated could be failed by a fully successful product.
**Severity:** High
**Business Impact:** A headline success criterion that cannot be honestly reported, which erodes confidence in the other criteria.
**Technical Impact:** None directly — but an unfalsifiable acceptance criterion cannot gate a release, so it silently drops out of the acceptance process.
**Recommendation:** Reframe the reduction as applying to registers and case files, which is where the genuine saving is, and state the measurement.
**Suggested BRD Text:**
> - No paper case files or patient registers are maintained after go-live; patient history exists only in the system. (Prescriptions continue to be printed, one per consultation, and are excluded from this measure.)

---

### CON-6 — Search by name or phone × the stated patient population
**Sides:** "Search patients by name or phone number" (Functional Requirements) × "Contact details" as an unqualified capture field (Functional Requirements), in a general-practice setting.
**Why they conflict:** they bind identity. Phone is offered as a primary search key, but the BRD never makes it mandatory or unique — correctly, since elderly patients, labourers, and children frequently have no phone, and families routinely share one number. The two stated search keys therefore do not reliably address the population the system serves, and the BRD offers no third key (such as a patient ID).
**Severity:** High
**Business Impact:** One failed search for a patient the doctor knows exists is, per BR §2.8, the failure that ends trust and does not recover cheaply.
**Technical Impact:** If phone is implemented as unique or required, registration blocks for legitimate patients and duplicate-handling has to be redesigned after data exists.
**Recommendation:** State explicitly that phone is optional and non-unique, add a system-generated human-readable patient identifier as a reliable third key, and define match semantics.
**Suggested BRD Text:**
> **Patient Management (revised)**
> - Each patient is assigned a system-generated Patient ID (e.g. `P-000142`), displayed in search results and printed on the prescription.
> - Phone number is optional and is not unique — multiple patients may share one number (e.g. family members).
> - Search matches on any part of the name, on any trailing portion of the phone number, and on exact Patient ID. Results display name, age, gender, phone, and last visit date so that same-name patients can be told apart.

---

### CON-7 — "Minimal training" × mandatory structured data entry
**Sides:** "High usability with minimal training required" (Success Criteria) and "Simple, minimal UI" (NFR Usability) × "Mandatory vitals capture" and the structured medication fields (Functional Requirements).
**Why they conflict:** structure *is* the training cost. Every mandatory field and every structured sub-field is a rule the doctor must learn and comply with, in exchange for data quality. The BRD asks for maximum structure and minimum training without acknowledging the trade.
**Severity:** Medium
**Business Impact:** Expectation mismatch at handover — the stakeholder was promised "minimal training" and receives a form with enforced fields.
**Technical Impact:** Manageable, but it constrains design: the UI must teach itself (inline validation, defaults, chips) rather than relying on a training session, and that is a design requirement nobody has written down.
**Recommendation:** Keep both, and make the resolution explicit as a design constraint plus a measurable training target.
**Suggested BRD Text:**
> - A new user must be able to complete a full consultation record unaided after a single 15-minute walkthrough. Field-level guidance, validation messages, and defaults must be sufficient to use the system without written documentation.

---

### CON-8 — Follow-up reminders excluded × the workflow the BRD describes
**Sides:** "Follow-up alerts/reminders" (Out of Scope) × the consultation and prescription workflow (Functional Requirements).
**Why they conflict:** general physicians write "review after 5 days" on a large share of prescriptions. Under the BRD the instruction is printed but recorded nowhere as data, so the clinical intent leaves the system entirely. The exclusion is defensible for *notifications*; applied to *capture*, it discards information the workflow generates.
**Severity:** Medium
**Business Impact:** The clinic cannot answer "who was due back and didn't come", which is the first question asked once the system has three months of data.
**Technical Impact:** Adding the field later is cheap; back-filling the year of follow-up intent lost in the meantime is impossible.
**Recommendation:** Keep reminders excluded. Capture follow-up interval as structured data, printed on the prescription, with no notification of any kind.
**Suggested BRD Text:**
> **Consultation Workflow (addition)**
> - Record an optional follow-up interval (e.g. "review after 5 days"), stored as structured data and printed on the prescription.
> - No alerts, notifications, or reminders are generated from this field in Phase 1 — it is captured for the record only.

---

## Missing Requirement Report

General functional/NFR gaps that are neither healthcare-domain nor architecture.

### MR-1 — Clinic Profile / print settings feature is required but never specified
**Severity:** Critical
**Business Impact:** The prescription is the product's primary physical output and the thing that replaces the pad. Without clinic name, doctor's qualifications, and medical registration number, the printed output is not usable as a prescription — in most jurisdictions the registration number is legally expected. The doctor quietly goes back to the pad.
**Technical Impact:** Two requirements ("Printable prescriptions", "Clinic/doctor header") depend on a data source no requirement creates, so it gets hardcoded during the build and becomes a change request within a week. Header fields must also be snapshotted per prescription, or changing the clinic phone number silently rewrites historical prescriptions (BR §1.4).
**Recommendation:** Add a Clinic Profile requirement covering identity, branding, and print configuration, and require that printed prescriptions store the header/footer values used at the time of printing.
**Suggested BRD Text:**
> ### Clinic Profile & Print Settings
> - Maintain a single clinic profile: clinic name, address, phone, email; doctor name, qualifications, specialty, and medical registration number; optional logo and signature image; prescription header and footer notes.
> - Configure print output: paper size (A5 default, A4 selectable) and a "pre-printed letterhead" option that suppresses the header block and reserves top margin instead.
> - Each printed prescription retains the profile values in force at the time of printing, so that reprints reproduce the original exactly.

### MR-2 — "Age / DOB" conflated
**Severity:** Critical
**Business Impact:** Age recorded as a number silently becomes wrong the moment it is stored; a prescription reprinted in 2029 for a 2026 visit must show the age the patient was in 2026. Paediatric dosing decisions reference age directly.
**Technical Impact:** Silent data corruption rather than a visible failure. Correcting it after real data exists requires a migration that cannot recover the missing precision (BR §1.5, §8 Q5).
**Recommendation:** Store date of birth as the single source of truth, with an explicit precision marker where only an approximate age was given, and snapshot the patient's age onto each visit.
**Suggested BRD Text:**
> - Capture Date of Birth. Where the patient knows only an approximate age, record the age given and flag the derived date of birth as estimated; the interface must display estimated ages distinctly (e.g. "≈45 yrs") rather than showing a false birthday.
> - Each visit records the patient's age at the date of that visit, so that historical records and reprints show the age at the time of the consultation.

### MR-3 — No amendment or immutability rule for a printed prescription
**Severity:** Critical
**Business Impact:** A prescription that has been handed to a patient can currently be edited in place with no trace. If a doctor changes a dose after the patient has left, the record and the paper in the patient's hand silently disagree — a medico-legal exposure in a dispute.
**Technical Impact:** Retrofitting immutability is a rewrite of the prescription storage and reprint path, not a patch — BR §10 names it "the one thing to get right before the first migration".
**Recommendation:** Make a finalized prescription immutable; edits create a new version with a reason, and the superseded version is retained.
**Suggested BRD Text:**
> - Once printed, a prescription is final and cannot be altered. A correction creates a new version, recording the reason and referencing the version it supersedes; the original is retained and remains visible in the patient's history.
> - Reprinting an unaltered prescription reproduces it exactly, with no additional markings.

### MR-4 — No data deletion, archival, or correction policy
**Severity:** High
**Business Impact:** Registration typos and test records accumulate with no way to remove them, while genuine clinical records need protection from accidental deletion. The two cases require opposite handling and the BRD addresses neither.
**Technical Impact:** Deletion behaviour gets decided ad hoc during implementation — typically a hard `DELETE` — after which a mistaken deletion is unrecoverable outside a backup restore.
**Recommendation:** Tier by whether clinical history exists (BR §1.6).
**Suggested BRD Text:**
> - A patient with no recorded visits may be deleted outright (e.g. a registration error).
> - A patient with recorded visits may only be archived, with a reason; archived patients are hidden from default search and can be restored.
> - Visits and prescriptions are never deleted. An erroneous record may be voided with a reason and remains visible as voided in the patient's history.

### MR-5 — Appointment semantics undefined
**Severity:** High
**Business Impact:** Whether the clinic runs on timed slots or a walk-in token queue changes the daily list entirely. Most small general practices are walk-in dominated, and the BRD's appointment-first framing may not match how the clinic actually operates.
**Technical Impact:** Slot length, double-booking, and walk-in handling determine the scheduling data model. If a consultation is built to require an appointment as its parent, every walk-in forces a fake backdated appointment (BR §1.1).
**Recommendation:** State that a consultation can exist without a prior appointment, and define slot length, double-booking, and the handling of past-dated unattended appointments.
**Suggested BRD Text:**
> - A consultation may be recorded for a walk-in patient with no prior appointment; an appointment is not a precondition for a consultation.
> - Default appointment slot length is configurable. Double-booking is permitted with a warning.
> - Appointments still marked Scheduled at the end of their day are automatically marked No-show.

### MR-6 — Medication entry: free text or coded, undecided
**Severity:** High
**Business Impact:** Determines whether prescribing takes 20 seconds or 90 seconds, which is the largest single component of the 2–3 minute target (BR §2.1, §2.4).
**Technical Impact:** Decides the prescription schema and whether any future interaction checking or reporting is possible. Free text with no drug list also degrades over time as spelling variants accumulate (BR §2.4).
**Recommendation:** Free text entry backed by an autocomplete list that builds from the doctor's own prescribing history, recalling the last-used regimen per drug.
**Suggested BRD Text:**
> - Medicine name, dosage, frequency, duration, and instructions are entered as text, assisted by autocomplete drawn from the doctor's own prescribing history.
> - Selecting a previously prescribed medicine pre-fills the dosage, frequency, duration, and instructions last used for it, all of which remain editable.
> - The previous consultation's medication list can be copied into the current prescription in one action.

### MR-7 — Export contract undefined
**Severity:** High
**Business Impact:** "Export patient or visit data" currently serves three unrelated jobs — a referral letter, a personal backup, and a vendor-lock-in escape hatch — and satisfying one does not satisfy the others (BR §4 gap 4).
**Technical Impact:** Medications nest inside visits while CSV is flat, so the file shape is a real design decision. Without stable exportable identifiers the files cannot be rejoined and the export is useless to a human (BR §3.9).
**Recommendation:** Name the job, then define entity, columns, scope, and file shape.
**Suggested BRD Text:**
> - CSV export produces separate patient and visit files, each keyed by the human-readable Patient ID and Visit ID so they can be related to one another.
> - PDF export produces a readable patient summary suitable for referral, covering the selected date range.
> - Both are scoped to a selected patient or date range (see Data Export constraints under Security).

---

## Healthcare Completeness Report

Clinical fields and safeguards expected in a system that records consultations and prescribes medication. None of the items below is named in the BRD's exclusion list, so none is treated here as intentional scope-narrowing.

### HC-1 — No allergy / adverse drug reaction record
**Severity:** Critical
**Business Impact:** The system's primary output is a prescription, and it holds no information that could warn against prescribing something the patient is known to react to. A penicillin-allergic patient handed an amoxicillin prescription is a foreseeable harm the system is silent about. Paper case files usually carry this on the front cover; replacing them without it is a net safety regression.
**Technical Impact:** Adding an allergy field later is easy; back-filling allergy data for existing patients is not, because it can only be recollected patient by patient. Any future interaction checking depends on this field existing from the start.
**Recommendation:** Add allergies as a patient-level field, surfaced persistently on the consultation screen rather than buried in the profile.
**Suggested BRD Text:**
> - Record known drug allergies and previous adverse reactions against the patient record, including "None known" and "Not asked" as distinct states.
> - Recorded allergies are displayed prominently on the consultation screen whenever that patient is open, and are printed on the prescription.

### HC-2 — No current / long-term medication list
**Severity:** Critical
**Business Impact:** Without knowing what the patient is already taking — often prescribed elsewhere — the doctor cannot see duplicate therapy or an interacting combination at the moment of prescribing. This is especially acute for the chronic patients (hypertension, diabetes) who make up much of a GP's repeat workload.
**Technical Impact:** Distinct from per-visit prescriptions: this is patient-level state that persists across visits, so it cannot be derived reliably from prescription history alone (which excludes anything prescribed by another doctor).
**Recommendation:** Add a maintained current-medication list at patient level, reviewable at each consultation.
**Suggested BRD Text:**
> - Maintain a list of the patient's current long-term medications, including those prescribed elsewhere, with start date and prescriber where known.
> - The list is displayed during consultation and can be updated from the prescription being written.

### HC-3 — No chronic conditions / past medical history
**Severity:** High
**Business Impact:** The doctor cannot see at a glance that a patient is diabetic or hypertensive, which changes both diagnosis and prescribing. Reconstructing it means reading every previous visit's free-text diagnosis.
**Technical Impact:** Free-text diagnosis per visit is not a substitute for persistent problem-level state; deriving it later requires parsing unstructured text.
**Recommendation:** Add a patient-level problem list, separate from per-visit diagnosis.
**Suggested BRD Text:**
> - Record ongoing conditions and significant past medical or surgical history against the patient record, displayed during every consultation and distinct from the per-visit diagnosis.

### HC-4 — Weight not captured
**Severity:** High
**Business Impact:** Weight is not optional for paediatric dosing — most drugs for children are dosed per kilogram. A general physician sees children routinely. Prescribing for a child without a recorded weight is a dosing-error risk.
**Technical Impact:** Trivial to add now as a vitals field; adding it later means historical visits have no weight and any trend view starts empty.
**Recommendation:** Add weight to the vitals set. Recommend making it mandatory for paediatric patients specifically, optional otherwise, rather than mandatory for all.
**Suggested BRD Text:**
> - Record weight at each consultation. Weight is mandatory for patients under 12 years, where it is required for dose calculation, and optional otherwise.

### HC-5 — SpO2 not captured
**Severity:** Medium
**Business Impact:** Oxygen saturation is now a routine part of a general physician's observation set, particularly for any respiratory presentation, and pulse oximeters are inexpensive and ubiquitous post-2020.
**Technical Impact:** Low — one additional optional vitals field alongside the existing three.
**Recommendation:** Add as an optional vital.
**Suggested BRD Text:**
> - Record oxygen saturation (SpO2, %) as an optional vital sign at each consultation.

### HC-6 — Height and BMI not captured
**Severity:** Medium
**Business Impact:** BMI is a standard screening input for the metabolic and cardiovascular conditions that dominate general practice, and cannot be computed without height.
**Technical Impact:** Height is slow-changing patient-level data, not a per-visit vital; BMI should be derived rather than stored, to avoid it going stale.
**Recommendation:** Capture height at patient level; derive BMI for display when both are present.
**Suggested BRD Text:**
> - Record patient height, updated as needed rather than at every visit. Display derived BMI wherever weight and height are both available.

### HC-7 — No emergency contact / next of kin
**Severity:** Medium
**Business Impact:** No means of contacting a relative if a patient becomes unwell at the clinic, and no recorded guardian for a minor. Also relevant given that many patients share a household phone.
**Technical Impact:** Small addition; interacts with CON-6, since a guardian relationship is what makes shared-phone search results interpretable.
**Recommendation:** Add optional emergency contact with relationship.
**Suggested BRD Text:**
> - Record an optional emergency contact for each patient: name, relationship, and phone number. For patients under 18, record the accompanying guardian.

### HC-8 — No examination findings field
**Severity:** Medium
**Business Impact:** The BRD's consultation flow goes complaints → diagnosis with nothing in between, but a physician examines the patient before diagnosing. Findings currently have to be crammed into the complaints or diagnosis text, making history harder to read back.
**Technical Impact:** One additional free-text field; cheap now, and separating it out of existing free text later is impractical.
**Recommendation:** Add an examination findings field between complaints and diagnosis.
**Suggested BRD Text:**
> #### Examination
> - Record examination findings (free text), between complaints and diagnosis.

### HC-9 — Vitals lack units, ranges, and an escape hatch
**Severity:** High
**Business Impact:** Temperature without a recorded unit is genuinely ambiguous between °C and °F in a clinical record, and a clinic changing thermometers would silently corrupt its own history. A decimal typo (`986`) printed on a prescription is a visible error in a medical document.
**Technical Impact:** Storing blood pressure as free text such as `"120/80"` destroys validation, trending, and CSV usability; systolic and diastolic must be separate numeric values (BR §1.2). The escape-hatch decision changes the finalize-time validation rule.
**Recommendation:** Specify units, store BP as two numbers, add non-blocking plausibility warnings with age awareness, and provide the "unable to record" path from CON-1.
**Suggested BRD Text:**
> - Temperature is recorded with an explicit unit (°C or °F), configured per clinic and stored with each reading.
> - Blood pressure is recorded as separate systolic and diastolic values.
> - Values outside plausible clinical ranges produce a non-blocking warning asking for confirmation. Plausible ranges vary by patient age.

### HC-10 — No immunization record
**Severity:** Low
**Business Impact:** Relevant if the practice sees children, where immunization status is routinely tracked; largely irrelevant for a purely adult general practice.
**Technical Impact:** A separate repeating record per patient — non-trivial to model, and not worth building on speculation.
**Recommendation:** Do not add to Phase 1. Confirm with the doctor whether paediatric immunization tracking is expected; if it is, it is a Phase 2 feature in its own right, not a field.
**Suggested BRD Text:**
> - Immunization tracking is out of scope for Phase 1. *(To be added to the Out of Scope list if the stakeholder confirms it is not required.)*

---

## Architecture Completeness Report

Decisions the BRD implies but never documents. Each carries an explicit rework risk.

### AR-1 — Hosting model
**Severity:** Critical — decide before development starts.
**What's missing:** Whether the application runs on a machine in the clinic or on cloud infrastructure. "Web-based access (browser-based system)" specifies the client, not the deployment.
**Business Impact:** Determines whether an internet outage stops the clinic, what the monthly cost is, who patches the machine, and whether patient data leaves the premises. All are stakeholder-visible decisions currently being made by default.
**Technical Impact:** Upstream of backup, disaster recovery, encryption approach, TLS strategy, deployment, and support model — the six areas below all depend on it.
**Rework Risk:** Choosing hosting after the data model and operational tooling are built risks a storage-engine mismatch requiring full data migration, plus rebuilt backup and TLS paths. Deferring it is the single most expensive deferral in the document (BR §8 Q1).
**Recommendation:** Decide now, and record it in the BRD even if provisionally. On-premise is recommended for a single-clinic single-user system (BR §3.3), because it removes the internet from the critical path and resolves CON-4 without building offline sync.
**Suggested BRD Text:**
> ### Hosting & Deployment
> - The application is deployed **on-premise**, on a dedicated machine within the clinic, accessed over the local network. *(If cloud hosting is selected instead, an internet-outage mitigation must be documented — see Reliability.)*
> - Data is stored within the clinic and replicated off-site in encrypted form for backup purposes only.

### AR-2 — Backup, disaster recovery, and restore ownership
**Severity:** Critical — decide before development starts.
**What's missing:** "No data loss" and "Regular automated backups" specify no frequency, destination, retention period, encryption of backups, restore procedure, restore owner, or verification that restores work.
**Business Impact:** "No data loss" is unachievable as an absolute and untestable as written. In practice, backup failure is silent — the realistic outcome is discovering at the moment of need that replication stopped months earlier. There is no IT staff, so "who restores this?" has no default answer.
**Technical Impact:** Without a target recovery point, the team cannot choose a backup mechanism, because continuous replication and a nightly dump differ by orders of magnitude in both cost and complexity.
**Rework Risk:** Moderate for the mechanism, severe for the omission — an untested backup is not a backup, and the failure surfaces only during a real incident when nothing can be recovered.
**Recommendation:** Replace the absolute with two numbers the stakeholder signs off on, and name a restore owner before go-live.
**Suggested BRD Text:**
> **Reliability (revised)**
> - Recovery Point Objective: at most 5 minutes of data may be lost in a failure.
> - Recovery Time Objective: the system is restored and usable within 4 hours, within the same clinic day.
> - Backups are encrypted before leaving the clinic and retained 7 daily, 4 weekly, 12 monthly.
> - A restore is tested at least quarterly and the result recorded. A named individual is responsible for performing a restore.
> - The interface displays the time of the last successful backup.

### AR-3 — Data retention policy
**Severity:** High — decide before development starts.
**What's missing:** How long patient records are kept, and whether a patient can require erasure.
**Business Impact:** Medical record retention periods are set by law and vary by jurisdiction; this is a legal question, not a technical one. If erasure is ever required, retaining PHI indefinitely becomes a compliance exposure.
**Technical Impact:** Soft deletion (recommended in MR-4) retains PHI indefinitely by design. A genuine purge must reach audit rows, prescription snapshots, exports, and backups — architecturally intrusive to add later (BR §1.6).
**Rework Risk:** High if erasure is required late, because purge paths must reach immutable and backed-up data that was deliberately designed not to change.
**Recommendation:** Ask the stakeholder for the applicable retention period; do not assert one. Record the erasure position explicitly even if it is "not supported in Phase 1".
**Suggested BRD Text:**
> - Patient records are retained for *[period to be confirmed against local medical-record retention requirements]*.
> - Permanent erasure of a patient record is not supported in Phase 1; records may be archived but not purged. *(Flagged for stakeholder confirmation against applicable data-protection obligations.)*

### AR-4 — Password recovery
**Severity:** High — decide before development starts.
**What's missing:** How the single user regains access after a forgotten password. With one user there is no administrator, and possibly no verified email address.
**Business Impact:** Total lockout of the only user, with the entire patient history inaccessible and a waiting room full of patients. Launch-blocking when it happens (BR §8 Q15).
**Technical Impact:** Determines whether email infrastructure is needed at all — a significant dependency for one user recovering access roughly once every few years.
**Rework Risk:** Low technically, severe operationally. The cost of discovering this gap is paid entirely at the worst possible moment.
**Recommendation:** Printed one-time recovery codes generated at setup and stored in the clinic safe, plus a documented local reset procedure. No email dependency.
**Suggested BRD Text:**
> - At first-time setup the system generates single-use recovery codes for printing and secure physical storage. Any unused code restores access without email or a second user.
> - A documented local password-reset procedure exists as a fallback.

### AR-5 — Audit logging
**Severity:** High — decide before development starts.
**What's missing:** Whether changes to clinical records are recorded — who changed what, when, and what the previous value was.
**Business Impact:** In a medico-legal dispute the ability to show that a record was not altered after the fact is the point of the record. Without an audit trail the system's clinical records are weaker evidence than the paper they replace.
**Technical Impact:** Retrofitting audit logging cannot reconstruct history for changes already made — the gap is permanently unbackfillable (PR A2).
**Rework Risk:** Moderate to implement later, but the data lost in the interim is unrecoverable.
**Recommendation:** Record before/after values for changes to patient, visit, and prescription records from the first release. It is invisible to the interface and adds no time to the consultation path (BR §1.7).
**Suggested BRD Text:**
> - All changes to patient, visit, and prescription records are recorded with the previous value, the new value, the user, and the timestamp. The audit record is retained for the life of the patient record and is not editable.

### AR-6 — Authorization model
**Severity:** Medium — decide before development starts.
**What's missing:** The BRD specifies authentication (single-user login) but never authorization, because with one user there are no roles.
**Business Impact:** The exclusion of receptionist access is a Phase 1 decision, not a permanent one, and in practice the doctor will hand the machine to staff regardless of what the BRD says — at which point "single user authentication" becomes a shared password with no accountability.
**Technical Impact:** If user identity is hardcoded rather than modelled as a real record, adding a second user later means rewriting every write path to attribute changes.
**Rework Risk:** Low if user identity is modelled properly now; high if a single credential is embedded in configuration.
**Recommendation:** Keep single-user behaviour, but model the user as a real record and attribute every write to it.
**Suggested BRD Text:**
> - The system supports exactly one user account in Phase 1. User identity is stored as a record rather than fixed configuration, and every created or modified record is attributed to the acting user, so that additional users can be introduced later without loss of accountability.

### AR-7 — Deployment and update model
**Severity:** Medium — decide before development starts.
**What's missing:** How the software is installed, how updates are applied, and what happens to the database schema during an update.
**Business Impact:** With no IT staff on site, an update that fails leaves the clinic unable to work with nobody able to intervene.
**Technical Impact:** Applying schema migrations automatically on startup is only safe with exactly one running instance — an assumption that holds here but must be recorded, since it silently breaks if a second instance is ever added.
**Rework Risk:** Low, provided a pre-update backup is automatic. Without one, a failed migration has no rollback.
**Recommendation:** Define an update procedure that takes an automatic backup first and can be reversed.
**Suggested BRD Text:**
> - Updates are applied by a documented procedure that automatically backs up the database before any schema change and can be rolled back to the pre-update state.
> - The system is designed to run as a single instance.

### AR-8 — Expected load and data volume
**Severity:** Medium
**What's missing:** "Moderate patient volume" gives no figure, so no performance target has a stated test condition.
**Business Impact:** The performance criteria cannot be verified before go-live, so the first real evidence arrives from the doctor's own daily use.
**Technical Impact:** Without a volume target, performance testing has no dataset size, and the 2–5 second search criterion is untestable in any meaningful sense.
**Rework Risk:** Low — at realistic volumes for a single clinic (roughly 15k patients and 100k visits after ten years, per BR §3.1) no plausible technology choice fails. The risk is not slowness but unverifiability.
**Recommendation:** State expected volumes so performance can be tested against a realistic dataset.
**Suggested BRD Text:**
> - The system is sized for up to 60 consultations per day, approximately 15,000 patients and 100,000 visits over ten years, with one concurrent user. Performance criteria are verified against a dataset of this size.

### AR-9 — Time zone and clinic-day boundary
**Severity:** Medium
**What's missing:** What "today" means for the daily appointment list, and whether a consultation can be recorded against an earlier date.
**Business Impact:** An evening clinic running past midnight splits one clinic session across two days in every list and report. Paper notes taken during an outage must be enterable afterwards against the correct date.
**Technical Impact:** Affects every daily list, every date filter, and every export — cheap to define now, expensive once export contracts are relied upon.
**Rework Risk:** Moderate — the fix touches every date-scoped query in the system.
**Recommendation:** Define an explicit clinic-day boundary and permit backdated entry with a marker.
**Suggested BRD Text:**
> - The clinic day is defined by a configurable boundary hour rather than midnight, and all daily lists and date filters use it.
> - A consultation may be recorded against an earlier date (e.g. entering notes taken on paper), and is marked as entered later than the visit date.

### AR-10 — Third-party integrations and external data flow
**Severity:** Low
**What's missing:** The BRD excludes lab and pharmacy integration but is silent on whether *any* external service is involved — analytics, error reporting, email, SMS, or cloud storage.
**Business Impact:** Each third party is another place patient data can land, and none has been considered.
**Technical Impact:** Error-reporting tools capture local variables and full URLs by default, which can include patient names and diagnoses (BR §3.10).
**Rework Risk:** Low.
**Recommendation:** State a default position of no third-party data sharing, with backup storage as the single named exception.
**Suggested BRD Text:**
> - The application transmits no patient data to third-party services. No analytics or usage-tracking service is included. Encrypted off-site backup storage is the sole exception, and backups are encrypted before leaving the clinic.
> - Diagnostic logs must not contain patient names, contact details, or clinical text.

### AR-11 — Data residency
**Severity:** Low (Medium if cloud hosting is selected)
**What's missing:** Where patient data physically resides — moot on-premise, significant if hosted.
**Business Impact:** Health data is classified as sensitive personal data under several regimes, including India's DPDP Act 2023, with implications for cross-border storage.
**Technical Impact:** Region selection is a deployment-time decision but is effectively permanent once data exists.
**Rework Risk:** High if wrong and cloud-hosted — relocating a live database across regions requires downtime and re-verification.
**Recommendation:** If AR-1 resolves to cloud, require in-country hosting. Flag the legal question; do not assert a conclusion.
**Suggested BRD Text:**
> - If the system is hosted rather than on-premise, all patient data is stored within the clinic's own country. *(Applicable health-data protection obligations to be confirmed with the stakeholder.)*

---

## Open Questions

The BRD states: *"Open Questions: None (all major product decisions defined for Phase 1)."*

**This does not hold.** `docs/brainstorm-review.md` §8 already lists 18 open questions ranked by rework cost, six of them schema- or topology-level, and `docs/planning-review.md` cannot proceed without ten explicit assumptions (A1–A10) substituting for decisions the BRD never makes, three of which are hard gates blocking specific build steps. Two documents in this repository independently found the BRD's decision set incomplete. The line is not merely inaccurate — it is the highest-leverage defect in the document, because a reader who believes it will not ask anything below.

The following are the stakeholder's call and are deliberately **not** answered here:

1. **Hosting: a machine in the clinic, or cloud?** (AR-1) — upstream of every non-functional requirement.
2. **Is the 2–3 minute target measured including or excluding physically taking vitals?** (CON-1) — decides whether the receptionist exclusion survives.
3. **What happens when a vital genuinely cannot be measured?** (HC-9) — decides the data quality of the most-enforced field in the product.
4. **Can a printed prescription be corrected, or only superseded?** (MR-3) — architectural fork; expensive to reverse.
5. **How long must patient records be retained, and can a patient require erasure?** (AR-3) — a legal question, not a technical one.
6. **Does the practice see children?** — determines whether weight is mandatory (HC-4) and whether immunization tracking is needed (HC-10).
7. **Is the clinic appointment-driven or walk-in driven?** (MR-5) — decides whether the daily appointment list is the right home screen at all.
8. **Who restores the system, and who is called when it breaks during clinic hours?** (AR-2) — not a code question, and per BR §5 the most common cause of a working system being abandoned.
9. **What is the current paper baseline the 80% reduction is measured against?** (CON-5).
10. **Is there an existing digital patient list to import?** — determines whether the system is useful in week one or only after months of accumulated data.

**Suggested BRD Text (replacing the current Open Questions section):**
> ## Open Questions
> The following decisions are outstanding and are required before development begins. Each is owned by the Clinic Owner unless noted.
>
> 1. Hosting model — on-premise or cloud.
> 2. Scope of the 2–3 minute consultation target — does it include physically taking vitals?
> 3. Handling of vitals that cannot be measured.
> 4. Whether a printed prescription may be corrected in place or only superseded.
> 5. Patient record retention period and erasure obligations (legal input required).
> 6. Whether the practice treats children (determines mandatory weight and immunization needs).
> 7. Appointment-driven or walk-in-driven clinic operation.
> 8. Named owner for backup restoration and in-hours support.
> 9. Current paper usage baseline for the 80% reduction target.
> 10. Availability of an existing patient list for import.

---

## Risk Register

Delivery risks rolled up from all four analyses, prioritised. Distinct from Critical Findings, which describe defects in the document rather than outcomes in the project.

| Risk | Likelihood | Impact | Severity | Owner / Mitigation |
|---|---|---|---|---|
| Doctor abandons the system in week 3 because consultations take longer than paper | High | High | **Critical** | Product Owner — resolve CON-1 before build; prioritise the prescription-entry accelerators in BR §2.4; measure the share of the day's patients with a completed record |
| Adverse drug event the system could have warned about but held no allergy data | Medium | Severe | **Critical** | Clinic Owner — accept HC-1 and HC-2 into Phase 1 scope; both are patient-level fields, not features |
| Hosting decided late or by default, forcing migration of storage, backup, and TLS | Medium | High | **Critical** | Solution Architect — close AR-1 in the decision session before any schema work |
| Backup silently fails and is discovered only at the moment of need | Medium | Severe | **Critical** | Named restore owner (AR-2) — quarterly restore drill; last-successful-backup indicator in the interface |
| Age/DOB conflation silently corrupts clinical records and reprints | High | High | **Critical** | Development Team — resolve MR-2 before the first migration |
| Prescription edited in place after being handed to the patient | Medium | High | **High** | Development Team — implement MR-3 before the prescription schema ships |
| Internet outage stops the clinic mid-session | Medium | High | **High** | Solution Architect — resolved as a side effect of AR-1 if on-premise; otherwise requires a documented failover |
| Unencrypted full-database export left on a shared desktop | Medium | High | **High** | Development Team — implement CON-2 export scoping, logging, and confirmation |
| Total lockout of the only user after a forgotten password | Low | Severe | **High** | Development Team — printed recovery codes at setup (AR-4), before go-live |
| Search fails to find a patient the doctor knows exists | Medium | High | **High** | Development Team — resolve CON-6; add Patient ID as a reliable third key |
| Acceptance stalls because success criteria cannot be objectively judged | High | Medium | **High** | Product Owner — adopt the Testability Review rewrites before sign-off |
| Printed prescription is unusable because clinic identity and registration number were never specified | High | Medium | **High** | Product Owner — accept MR-1 into scope; verify on the real printer and paper before go-live |
| No named support contact when the system fails during clinic hours | High | Medium | **High** | Clinic Owner — name a person and a channel before go-live |
| Retention obligations unmet because the period was never established | Low | High | **Medium** | Clinic Owner — legal input on AR-3 |
| Audit trail absent, weakening the record in a dispute | Low | High | **Medium** | Development Team — AR-5 from the first release; the gap cannot be backfilled |

---

## Testability Review

Requirements that cannot currently be passed or failed. Severity follows how central each is to a stated success criterion.

### TS-1 — "Simple, minimal UI optimized for fast data entry" (NFR Usability)
**Severity:** Medium | **Business Impact:** Usability disputes at handover have no reference point, so the argument is settled by whoever is more insistent. | **Technical Impact:** No acceptance gate, so usability regressions ship unnoticed. | **Recommendation:** Convert to a measurable interaction budget on the primary workflow.
**Suggested BRD Text:** *A complete consultation record can be entered using the keyboard alone, without using the mouse and without leaving the consultation screen. Entering a consultation requires no more than 25 keystrokes beyond the clinical content itself.*

### TS-2 — "Fast patient search and retrieval" (NFR Performance)
**Severity:** Medium | **Business Impact:** Duplicates the 2–5 second success criterion in vaguer language, so the two can be judged differently. | **Technical Impact:** No threshold, no dataset size, no percentile — nothing to test against. | **Recommendation:** Delete as a separate NFR and state one measurable search target.
**Suggested BRD Text:** *Search results begin appearing within 500ms of the last keystroke, measured at the 95th percentile against the dataset volumes stated under Scalability.*

### TS-3 — "Smooth generation and printing of prescriptions" (Success Criterion)
**Severity:** Medium | **Business Impact:** "Smooth" cannot be reported against, and printing is the product's primary physical output — the one thing that must work on day one. | **Technical Impact:** Print correctness depends on paper size, margins, and driver scaling, none of which is captured by "smooth". | **Recommendation:** Convert to a physical, verifiable outcome on the clinic's actual printer.
**Suggested BRD Text:** *A prescription prints correctly on the clinic's own printer and paper without manual adjustment of print settings, on the configured paper size, with all content within the printable area and at least 25mm of clear space for a signature. Verified on the clinic's hardware before go-live.*

### TS-4 — "Successful export of data in CSV/PDF format" (Success Criterion)
**Severity:** Medium | **Business Impact:** "Successful" is undefined while the export contract itself is undefined (MR-7), so this criterion currently means only "a file was produced". | **Technical Impact:** No verification that the file is correct, complete, or usable. | **Recommendation:** Define success as the file opening correctly with data intact.
**Suggested BRD Text:** *An exported CSV opens in Microsoft Excel with all columns correctly separated, non-English characters intact, phone numbers unaltered, and dates in an unambiguous format. An exported PDF is text-searchable and renders all patient names correctly.*

### TS-5 — "Designed for a single clinic with moderate patient volume" (NFR Scalability)
**Severity:** Medium | **Business Impact:** No agreed capacity, so no basis for judging whether the system is adequate. | **Technical Impact:** No dataset size against which any performance target can be tested. | **Recommendation:** Replace with the figures in AR-8. **Suggested BRD Text:** as AR-8.

### TS-6 — "High usability with minimal training required" (Success Criterion)
**Severity:** High — it is a stated success criterion. | **Business Impact:** The stakeholder's expectation of "minimal" and the team's may differ by an order of magnitude, discovered only at handover. | **Technical Impact:** Cannot gate a release. | **Recommendation:** Convert to a time-boxed onboarding target with an unaided-completion condition. **Suggested BRD Text:** as CON-7.

### TS-7 — "Easy navigation between patient profile and visits" (Functional)
**Severity:** Medium | **Business Impact:** "Easy" cannot be verified, and navigation cost is a real component of the consultation time budget. | **Technical Impact:** No acceptance condition on the most frequently repeated interaction in the product. | **Recommendation:** State a click budget.
**Suggested BRD Text:** *A patient's profile and full visit history are reachable from any search result in a single action, and a consultation can be opened from the daily list in a single action.*

### TS-8 — "No data loss" (NFR Reliability)
**Severity:** High | **Business Impact:** An absolute that cannot be honestly promised at this budget, and therefore either misleads or gets ignored. | **Technical Impact:** Untestable — there is no experiment that demonstrates zero loss under all conditions. | **Recommendation:** Replace with RPO/RTO. **Suggested BRD Text:** as AR-2.

### TS-9 — "Regular automated backups" (NFR Reliability)
**Severity:** High | **Business Impact:** "Regular" is satisfied by a monthly backup, which would lose a month of records. | **Technical Impact:** No frequency, destination, retention, or verification. | **Recommendation:** Replace with the specifics in AR-2. **Suggested BRD Text:** as AR-2.

### TS-10 — "Basic search functionality" / "Quick patient search" / "View recent patients"
**Severity:** Medium | **Business Impact:** Three overlapping requirements describing one feature in three vaguenesses ("basic", "quick", "recent"), which invites building three things or none. | **Technical Impact:** Match semantics, result ordering, result count, and the definition of "recent" are all unspecified. | **Recommendation:** Consolidate into one specified search requirement.
**Suggested BRD Text:** *A single search field matches on any part of the patient name, any trailing portion of the phone number, or the Patient ID. Results are ordered by most recent visit, limited to 20, and show name, age, gender, phone, and last visit date. With the field empty, the 10 most recently seen patients are shown.*

**Also noted (Low):** no requirement carries an identifier, making precise reference from downstream documents impossible; free-text fields have no length bound, and an unbounded complaint destroys the printed layout (BR §5); Safari is listed as a supported browser but no Mac has been confirmed to exist in the clinic (BR §8 Q17).

---

## Developer Readiness Assessment

**Could a developer start building today from this document? No — not without guessing, and the guesses would be expensive.**

They could make a genuine start on a narrow slice: patient registration and editing, the daily appointment list and its status transitions, complaints and diagnosis capture, visit history with a date filter. That is roughly a quarter of the document and it is real, useful work.

They could not, without a decision, do any of the following — listed in the order the decisions are needed:

1. **Choose a deployment target or a database** (AR-1). This is first because everything operational depends on it, and it is the decision most expensive to reverse.
2. **Design the patient table** — because "Age / DOB" (MR-2), whether phone is required or unique (CON-6), and whether allergies and chronic conditions exist as fields (HC-1, HC-3) are all unresolved, and all are columns.
3. **Design the prescription tables** — because whether a printed prescription is immutable (MR-3) determines whether this is one table or a versioned structure, and retrofitting is a rewrite.
4. **Build the consultation screen** — because the vitals mandate conflicts with the time target (CON-1) and there is no defined behaviour when a vital cannot be taken (HC-9).
5. **Build the prescription print output** — because the clinic identity it prints does not exist as a requirement (MR-1), and paper size and letterhead handling are unspecified.
6. **Build export** — because the export contract is undefined (MR-7) and its interaction with the encryption requirement is unresolved (CON-2).
7. **Write acceptance tests for any success criterion except page load time** — because the rest are unmeasurable as written.

**The blocking set is small and closable on paper.** None of these needs code, a prototype, or research to resolve — they need roughly a session with the doctor and the stakeholder. That is the encouraging part of this assessment: the BRD is not far from buildable, it is just currently claiming to be there already.

---

## Final Verdict

**Do not build from this document yet.** It is a sound scoping document and a genuinely good basis for a decision session — the product is coherent, the exclusion list is disciplined, and the problem statement does real work. But it asserts a completeness it does not have, and three of its defects (the vitals/single-user/timing contradiction, the absent allergy and current-medication fields, and the undocumented hosting model) will each cost materially more to resolve after code exists than before. Building now means the team resolves them silently, by assumption, in whatever order they happen to hit them — which is how a project acquires a schema it cannot change.

**The single change that would move this verdict most is deleting "Open Questions: None" and replacing it with the real list.** Not because the line is the biggest defect in itself, but because it is the one holding all the others in place: every gap in this review is closable in an afternoon with the doctor in the room, and none of them will be raised while the document says there is nothing left to ask. Close the ten questions above, fold the resulting decisions back into the BRD, and this becomes a document worth building from — realistically within a week, without writing a line of code in the meantime.
