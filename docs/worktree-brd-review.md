# Worktree BRD Review — Patient Management Application

**Reviews:** `BRD/Doc_BRD.md` (198 lines, unmodified baseline)
**Date:** 2026-08-19
**Branch:** `brd/review-report` (worktree, branched from `main` @ `5d512ca`)
**Method:** read-only review of the BRD as it currently stands. No BRD text was changed. Prior analysis in `docs/brainstorm-review.md` (referenced below as **BR §n**) and `docs/planning-review.md` is cross-referenced rather than restated — where a finding here matches one already recorded there, this document points at it instead of re-deriving it.

This is a **baseline snapshot**, not a change review. Every requirement is classified `Existing`; nothing is proposed, added, or resolved here.

---

## 1. Requirement classification report

One row per distinct requirement / functional area currently in the BRD.

| Requirement | Classification | Scope tag | Consistency result |
|---|---|---|---|
| Product Goal — web app for a GP covering scheduling, records, complaints, diagnosis, medication | Existing | `[in scope]` | OK |
| Users — General Physician, single user; receptionist explicitly not in Phase 1 | Existing | `[in scope]` | **Conflicts with:** "Mandatory vitals capture" + "consultation within 2–3 minutes". Excluding a second user puts temp/BP/pulse on the doctor's own critical path for every patient (BR §7.1) |
| Scope — web-based (browser) access | Existing | `[in scope]` | **Ambiguous, not contradictory:** "web-based" fixes the client (browser) but never states the hosting model. Cloud vs. on-premise is absent from the whole document (BR §8 Q1) |
| Scope / FR — patient registration and profile management (name, age/DOB, gender, contact) | Existing | `[in scope]` | **Conflicts internally:** "Age / DOB" is written as if the two are interchangeable; they are not (BR §1.5, §8 Q5) |
| Scope / FR — appointment scheduling, daily list, status (Scheduled/Completed/Cancelled/No-show) | Existing | `[in scope]` | OK as written. Under-specified: no slot length, double-booking rule, or lapse behaviour for yesterday's still-`Scheduled` rows (BR §4 gap 15) |
| Scope / FR — mandatory vitals capture (temperature, BP, pulse) | Existing | `[in scope]` | **Conflicts with:** the single-user exclusion and the 2–3 minute criterion (above). Also silent on units, format, and what happens when a vital genuinely cannot be taken (BR §2.3, §8 Q11/Q12) |
| Scope / FR — recording patient complaints (free text) | Existing | `[in scope]` | OK |
| Scope / FR — diagnosis documentation (free text notes) | Existing | `[in scope]` | OK. Note: free-text diagnosis permanently forecloses the excluded "advanced analytics" — consistent with the exclusion list, worth knowing it is a one-way door |
| Scope / FR — medication entry (name, dosage, frequency, duration, instructions) | Existing | `[in scope]` | OK as written. Undecided whether fields are free text or coded (BR §8 Q7) |
| Scope / FR — printable prescription (header, patient details, vitals, diagnosis, medications, footer) | Existing | `[in scope]` | **Conflicts with:** nothing in the BRD — but it *depends on a feature the BRD never creates*. Header/footer content (clinic name, qualifications, registration no., logo, signature) has no Clinic Profile/Settings requirement anywhere (BR §4 gap 1, §7.9) |
| Scope / FR — patient visit history (previous visits; vitals, complaints, diagnosis, prescriptions; filter by date) | Existing | `[in scope]` | OK |
| Scope / FR — basic search; quick patient search, recent patients, profile↔visit navigation; search by name or phone | Existing | `[in scope]` | **Conflicts with:** the stated patient population. Name-or-phone are the only keys, but patients without a phone and families sharing one phone are both normal in this setting (BR §7.7). "Basic" is undefined (BR §4 gap 3) |
| Scope / FR — data export, patient or visit data as CSV / PDF | Existing | `[in scope]` | **Conflicts with:** NFR "data encryption at rest" — export is a designed path for PHI to leave the encrypted store onto a shared desktop, unencrypted (BR §7.6, §3.9). Entity, columns, and scope of the export are also undefined (BR §4 gap 4) |
| Out of Scope — receptionist/multi-user, billing, insurance, lab/pharmacy integration, AI diagnosis, offline, mobile app, advanced analytics, multi-doctor/multi-clinic, follow-up alerts | Existing | `[in scope]` (as an exclusion list) | Mostly coherent. **Two exclusions are load-bearing and conflict with other BRD statements:** receptionist (see above) and offline (see NFR Reliability). A third — follow-up alerts — excludes notification while the workflow it excludes it from ("review after 5 days") is printed on prescriptions and captured nowhere (BR §7.4) |
| Success Criteria — consultation record in 2–3 minutes | Existing | `[in scope]` | **Conflicts with:** mandatory vitals + single user (above). This is the sharpest contradiction in the document (BR §7.1) |
| Success Criteria — patient search/history retrieval in 2–5 seconds | Existing | `[in scope]` | **Conflicts with:** NFR "encryption at rest", if read as column-level encryption on name/phone — that destroys the indexes search depends on (BR §3.6). Also measures little at this data volume (BR §7.10) |
| Success Criteria — ≥80% reduction in paper usage | Existing | `[in scope]` | **Conflicts with:** printing a prescription for every patient. Printing is paper; the reduction is in registers and case files (BR §7.5). No measurement baseline is defined |
| Success Criteria — smooth prescription generation/printing; successful CSV/PDF export; high usability with minimal training | Existing | `[in scope]` | OK, but none of the three is stated testably ("smooth", "successful", "high") |
| NFR — Usability: minimal UI optimized for fast entry | Existing | `[in scope]` | **Tension with:** structured mandatory fields, which are themselves the training cost (BR §7.8). Not a contradiction, but the two constrain each other |
| NFR — Performance: page load < 2s, fast search | Existing | `[in scope]` | OK |
| NFR — Reliability: "No data loss", regular automated backups | Existing | `[in scope]` | **Conflicts with:** the single-clinic, minimal-cost scope — an absolute with no RPO, retention, destination, named restorer, or restore test is not testable as written (BR §7.2, §3.7) |
| NFR — Security: secure single-user login; encryption at rest and in transit | Existing | `[in scope]` | **Conflicts with:** CSV/PDF export and the 2–5s search target (both above). Password recovery with one user and possibly no email is unaddressed (BR §8 Q15) |
| NFR — Scalability: single clinic, moderate patient volume | Existing | `[in scope]` | OK — consistent with the multi-doctor/multi-clinic exclusion |
| NFR — Compatibility: Chrome, Edge, Safari | Existing | `[in scope]` | OK. Safari is listed but no Mac has been confirmed to exist (BR §8 Q17) — a testing-scope question, not a contradiction |
| Open Questions — "None (all major product decisions defined for Phase 1)" | Existing | `[in scope]` | **Conflicts with:** the document's own content and with `docs/brainstorm-review.md` §8, which lists 18 open questions, 6 of them schema- or topology-level. See §3 below |

**Scope-tag summary:** no requirement currently in the BRD reads as `[out of scope]` — nothing in the Scope section re-includes anything the exclusion list rules out, and the exclusion list does not contradict itself. The document's problems are **contradictions between in-scope requirements** and **omissions**, not scope leakage. Nothing here is a stop-and-ask on scope grounds; the stop-and-ask items are the open questions in §3.

---

## 2. Structured impact report

Framed as a baseline snapshot — what depends on this document today, not what changed.

```
### Impact report
- Changed: nothing. This is a read-only review of BRD/Doc_BRD.md at commit 5d512ca;
  the BRD is unmodified on this branch.
- Downstream references found: 4 files
  - docs/brainstorm-review.md:3 — declares "Source: BRD/Doc_BRD.md"; consumes the full
    BRD and already records 15 requirement gaps (§4), 10 internal contradictions (§7),
    and 18 ranked open questions (§8). This review corroborates it; no conflict.
  - docs/planning-review.md:3,10,12 — declares "Source: BRD/Doc_BRD.md,
    docs/brainstorm-review.md"; a full Phase 1 work breakdown traced to BRD scope.
    Carries 10 explicit assumptions (A1–A10) standing in for BRD decisions that do
    not exist, with three hard gates (A3 blocks Step 19, A9 blocks Step 35,
    A10 blocks Step 42). Any future BRD edit to vitals, backup, or support
    provisions invalidates the corresponding assumption.
  - .claude/agents/brainstorm.md:12 and .claude/agents/plan-brd.md:12 — both instruct
    the agent to read BRD/Doc_BRD.md as "the source of truth for scope" before
    responding. The BRD is a live input to tooling, not just documentation.
  - .claude/agents/worktree-brd.md:8-16 — governs edits to the BRD itself.
- Cross-doc references (docs/brainstorm-review.md, etc.): both prior docs treat the
  BRD as authoritative and unedited. This review does not contradict either; where a
  finding overlaps, it cites BR §n rather than restating. No follow-up edit needed to
  either file.
- App/plan-code references (via Grep): none. The repository contains no application
  code, schema, or stack — only README.md, BRD/Doc_BRD.md, two review docs, and three
  agent configs. Nothing executable depends on these requirements yet, which is why
  the contradictions below are still free to fix.
- Action needed as a result: none taken. This review adds one new file
  (docs/worktree-brd-review.md) and edits nothing. The contradictions in §1 and the
  open questions in §3 are flagged for the stakeholder, deliberately unresolved.
```

---

## 3. Open questions and gaps

**The BRD's "Open Questions: None (all major product decisions defined for Phase 1)" does not hold.** This is the single most consequential line in the document, because it asserts closure that the rest of the text contradicts — and it is the line most likely to stop anyone from asking. `docs/brainstorm-review.md` §8 already ranks 18 open questions by rework cost, six of which are schema- or topology-level. That list is not reproduced here; it stands.

What this review adds, reading the BRD text alone:

**Contradictions the document does not acknowledge.** Five pairs of statements in §1 above cannot both hold as written: single-user vs. mandatory vitals vs. the 2–3 minute target; offline-excluded vs. "no data loss"; encryption-at-rest vs. CSV/PDF export; encryption-at-rest vs. 2–5 second search; 80% paper reduction vs. printing every prescription. Each needs an explicit decision recorded in the BRD, not a silent one. None is resolved here.

**A feature the BRD requires but never specifies.** "Printable prescriptions (with basic header, footer, and content)" appears in Scope and again in Functional Requirements, but no requirement anywhere creates the Clinic Profile / Settings screen that supplies the header and footer content. This is a missing requirement implied by an existing one — the clearest structural gap in the document.

**Absent decisions that are upstream of everything non-functional.** The hosting model (cloud vs. a machine in the clinic) does not appear in the BRD at all, yet it determines backup design, outage behaviour, encryption, updates, cost, and support. Likewise, whether a printed prescription can be edited or only amended is unmentioned, and is medico-legally significant.

**Clinical-safety omissions.** There is no allergies field, no chronic-conditions field, and no long-term-medication field anywhere in the BRD, in a system whose primary output is a prescription. Weight and SpO2 are also absent from the mandatory vitals set; weight is not optional for paediatric dosing.

**Requirements stated in untestable language.** "No data loss", "smooth" printing, "successful" export, "high usability", "basic search", "moderate patient volume" — six requirements that cannot be passed or failed as phrased. The 80% paper-reduction criterion additionally has no defined measurement baseline.

**Recommendation on the line itself:** replace "Open Questions: None" with a real open-questions section, or an explicit pointer to `docs/brainstorm-review.md` §8. Leaving it as "None" makes the BRD assert a closure that neither of the two review documents in this repository supports. **This review does not make that change** — it is a BRD edit, and the decision of what to put there belongs to the stakeholder.
