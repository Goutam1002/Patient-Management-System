# Implementation-Readiness Review — `BRD/Doc_BRD.md`

**Author:** `implementation-brd` (implementation engineer perspective), per `.claude/agents/implementation-brd.md`
**Date:** 2026-08-19
**Worktree / branch:** `brd/review-report` (this session's working directory is already inside the `worktree-brd`-created worktree — no new worktree entered, nothing touches `main`)
**Reviews:** `BRD/Doc_BRD.md` @ blob `212cddc` (197 lines, unmodified since commit `5d512ca`)
**Grounded in:** `docs/worktree-brd-review.md` @ commit `7c8e0ea` (BRD Quality 5.7/10, Development Readiness 59%, 4 Critical / 7 High / 8 Medium / 4 Low findings), `docs/plan-brd-review.md` (untracked, present in the working tree), and `docs/brainstorm-brd-review.md` (untracked, present in the working tree) — all read in full.
**Scope of this document:** assessment only. No application code was written, no entity or migration was created, no BRD text was modified, and none of the three review/plan documents above were touched.

---

## 1. The question this document answers

`docs/worktree-brd-review.md` asks *"is this a good specification?"* This document asks the narrower, mechanical question my own agent definition exists to answer:

> **Given the stack and feature specs already locked into `.claude/agents/implementation-brd.md`, which BRD feature areas can I open an editor and start building today, and which would force me to invent a decision nobody made?**

The answer is meaningfully better than the last time this question was asked in this repo. Three things changed since the prior implementation-readiness pass (visible in git history at `9a0d793`, which this document supersedes):

1. **My own agent definition (`implementation-brd.md`) has since had its fixed specs substantially expanded** — commits `86e1d47` and `379eaad` locked in Patient fields (Age *and* DOB both stored, Allergies/CurrentMedications/ChronicConditions, `PatientId` as a 0-based sequential identity), Appointment/Visit shape, Vitals precision rules, Prescription immutability, DoctorDetails snapshotting, and switched Authentication from plaintext to reversible encryption. **Most of what blocked the previous pass is now closed.**
2. **`docs/worktree-brd-review.md` was regenerated** against the current BRD and the current fixed specs — its finding count dropped from 9 Critical / 18 High / 17 Medium / 6 Low to 4 Critical / 7 High / 8 Medium / 4 Low, and Development Readiness rose from 26% to 59%.
3. **A usable plan now exists against the correct stack.** The previous pass's single biggest blocker — `docs/planning-review.md` was a fully-worked-out plan targeting Razor Pages + SQLite + Argon2id + cloud backup, none of which matches my fixed stack — no longer applies. That document is gone from the working tree; `docs/plan-brd-review.md` was written directly against Angular + .NET Web API + SQL Server + EF Core and requires no stack translation. **This alone removes what was previously a hard "I cannot execute *any* step today" blocker.** See §3.

What's left to assess is narrower and more honest: with the stack-translation problem gone and most data-model ambiguity closed, do the *remaining* Critical/High findings in the current BRD review still stop specific steps, or can implementation proceed with documented assumptions?

---

## 2. Freshness check — current, not stale

My blocker-awareness rule requires verifying the review is up to date with the BRD before relying on it.

| Check | Result |
|---|---|
| Blob the review claims to review | `212cddc` (stated in its header) |
| `git hash-object BRD/Doc_BRD.md` in this worktree | `212cddc705dc0e2f960ca07c14793896caf54571` — **match** |
| Last commit touching `BRD/Doc_BRD.md` | `5d512ca`, 2026-08-19 18:54 |
| Last commit touching `docs/worktree-brd-review.md` | `7c8e0ea`, 2026-08-19 22:46 — **later than the BRD**, so the review post-dates the document it reviews |
| Any newer BRD revision on another ref | None — single commit in `git log --all -- BRD/Doc_BRD.md` |
| Any review doc superseding `worktree-brd-review.md` | None found |

**Conclusion: the review is current.** Its Critical/High findings are live and unresolved (except where noted below as closed by my own locked specs), and the blocker check in §4 is valid.

Repo state also reconfirmed: `angular.json`, `*.sln`, and `*.csproj` — none exist anywhere in this worktree. **The repository is still documentation only; "scaffold both projects" remains genuinely step zero**, and is now Step 1 of `docs/plan-brd-review.md` rather than an unwritten assumption.

---

## 3. What's already closed by my own locked specs

Same exercise as the prior pass, rerun against the current, larger set of fixed decisions:

| Locked spec (`implementation-brd.md`) | Prior-review findings it closes | Current-review findings it closes |
|---|---|---|
| **Patient:** `Age` and `DateOfBirth` both stored independently; `Allergies`/`CurrentMedications`/`ChronicConditions` present; `Phone` optional and non-unique; `PatientId` a 0-based sequential identity; no delete/archive endpoint ever. | Closes the old MR-2 (Age/DOB ambiguity), HC-1 (no allergy field), HC-2 (no medication list), HC-3 (no chronic conditions), CON-6 (phone ambiguity), MR-4 (no deletion policy) — six of the old review's nine Critical/High findings, all on one entity. | Closes what would otherwise be re-flagged findings on the same fields; the current review's HC-4 (Medium) notes these decisions exist but aren't copied into the BRD text — a documentation gap, not a build blocker. |
| **Appointment/Visit:** status enum fully enumerated; `Visit.AppointmentId` non-nullable; vitals mandatory **at entry** (non-nullable columns, no draft path); `Temperature` in °C; BP as separate systolic/diastolic columns; `Weight` as `decimal(6,3)`. | Closes the old review's ambiguity between "mandatory at entry" vs. "mandatory at finalize" (old CON-1's implementation fork) by picking entry — the schema-forking half of that finding no longer exists. | Current review's CR-1 (High) is a *different*, narrower claim: the BRD doesn't state a time budget for vitals within the 2–3 minute target. This doesn't fork implementation the way the old ambiguity did — see §5.3. |
| **Prescription:** immutable once generated; corrections create a new record, never an in-place edit; `DoctorDetails` snapshotted onto the prescription at creation rather than joined live. | Closes the old review's prescription-versioning and DoctorDetails-snapshot findings outright. | No open findings against Prescription in the current review. |
| **Export:** two-file CSV split with fixed column shapes, semicolon-encoded prescriptions, confirmation-gated, audit-logged; single-patient PDF, date-range selectable. | Closes the old review's export-shape findings. | No open findings against Export in the current review. |
| **Authentication:** single account, `Users(Id, Username, Password)`, reversible symmetric encryption (not plaintext, not hashing), no registration flow. | Prior spec was plaintext; that's since changed to encrypted-but-reversible, which is a stronger position than what the old review assessed. | Current review's CR-3 (High) flags that "secure login" in the BRD's own wording implies stronger practice than reversible encryption — but the accepted-risk rationale already lives in `implementation-brd.md` itself (single-machine, no network exposure, single account). This is a documentation-reconciliation item, not an unresolved design question — see §5.8. |
| **Local deployment model:** never hosted, runs permanently on the doctor's machine; Kestrel on `localhost`; local SQL Server via SSMS; no staging/production/remote access. | Closed the old review's hosting-model finding outright and collapsed several downstream findings (offline contradiction, in-transit encryption, data residency). | Current review's AC-4 (Low) notes this decision exists but isn't copied into the BRD text — documentation gap only. |

**Net effect: the current BRD review has 4 Critical findings, and my own locked specs — which didn't exist in this form for the prior pass — have already closed the majority of what would otherwise be Critical/High blockers on the Patient, Appointment/Visit, Prescription, Export, and Authentication feature areas.** What's left is smaller and more precisely scoped than before.

---

## 4. Plan status — RESOLVED (previously the single largest blocker)

The prior pass's §4 was titled *"there is no executable plan, and the only plan present targets a different stack"* and was, on its own, enough to block starting any step. That is no longer true.

`docs/plan-brd-review.md` is present, ordered (19 steps across 8 phases), file-targeted against exactly my fixed stack (`frontend/` Angular workspace, `backend/PatientManagement.Api/` .NET Web API project, EF Core migrations), and its own Scope & Assumptions section already flags the same two open decisions the current BRD review flags as Critical (walk-in handling, backup mechanism) rather than silently building around them. Verified line by line against my fixed stack:

| Layer | `docs/plan-brd-review.md` | My fixed stack | Status |
|---|---|---|---|
| Frontend | Angular, standalone components, reactive forms | Angular (latest stable), standalone components, reactive forms | **Match** |
| Backend | .NET Web API, thin controllers/services/DTOs | .NET Web API — thin controllers, services, DTOs separate from EF entities | **Match** |
| Database | SQL Server Express, SSMS-inspected | SQL Server, local instance, SSMS | **Match** |
| ORM | EF Core, code-first | EF Core, code-first | **Match** |
| Auth | Users(Id, Username, Password), AES reversible encryption, decrypt-and-compare login | Same | **Match** |
| Backup | Task Scheduler + `sqlcmd BACKUP DATABASE`, flagged in the plan as an unconfirmed assumption | No hosting; local backup mechanism required by NFR but unassigned | **Match, and correctly flagged as open rather than silently decided** |
| Deployment | Local batch/shortcut script, Kestrel + Angular prod build | Local-only, no hosting, no CI/CD, no reverse proxy | **Match** |

**No translation work is required before executing this plan.** This is the single biggest change in overall implementation readiness since the prior pass — it converts "I cannot start *any* step today without inventing a plan" into "I can start most steps today; a handful are gated on specific, named decisions."

---

## 5. Feature-area assessment

| # | Feature area | Verdict | Blocking findings (severity) |
|---|---|---|---|
| 0 | Scaffolding (Angular + .NET Web API + local SQL Server) | **Ready — GO** | none |
| 1 | Patient records | **Blocked** — narrowly, on 2 findings (was 6) | HC-1 (C), HC-2 (H) |
| 2 | Appointment / Visit | **Blocked** — on the one decision my own spec already names as open | MR-1 (C) |
| 3 | Consultation workflow (vitals/complaints/diagnosis/medication) | **Ready, with a documented note** | CR-1 (H) — doesn't fork implementation, see §5.3 |
| 4 | Prescriptions / printing | **Ready** | none |
| 5 | Search & navigation | **Ready**, except "recent patients" ranking | MR-2 (H) — scoped to one sub-feature only |
| 6 | Visit history | **Ready** | none |
| 7 | CSV / PDF export | **Ready** | none |
| 8 | Authentication (login) | **Ready to build now; not release-ready** | AC-2 (C) — gates general release, not the login step itself; CR-3 (H) — wording reconciliation only, see §5.8 |
| 9 | Backup mechanism | **Blocked on adoption, not on ambiguity** | AC-1 (C) — a specific mechanism is already proposed (`docs/plan-brd-review.md` Step 18); it needs a decision, not more design |

**Feature-area count: 6 of 9 ready to start now (0, 3, 4, 5 with a carve-out, 6, 7), 1 ready-to-build-but-not-release-ready (8), 2 genuinely blocked (1, 2), 1 blocked-on-adoption-not-design (9).** Compare to the prior pass's 3 of 8 ready — the improvement is real and driven almost entirely by §3's locked specs, not by anything in this document's own judgment.

### 5.1 Patient records — BLOCKED (narrowly)

**BRD says:** add, edit, view patient details; capture Name, Age/DOB, Gender, Contact; search by name or phone.

Everything that made this area a six-finding stop in the prior pass is now closed (see §3's Patient row). What's left, per the current `docs/worktree-brd-review.md`:

- **HC-1 (Critical) — Emergency contact is entirely absent.** This is a Patient-level column decision, and unlike the finding-severity-vs-schema-cost distinction I draw for CR-1 below, this one *is* a genuine schema question I'd otherwise be inventing by default: nullable text field(s), or a structured name/phone pair? Small in isolation, but it's exactly the kind of "the column is the decision" case my process exists to stop on, per my own instructions ("A Critical or High unresolved finding on the requirement you're about to build is a stop-and-ask, not a note").
- **HC-2 (High) — Medical/surgical history has no field**, distinct from the already-fixed `ChronicConditions`/`CurrentMedications`. Same category of decision as HC-1: a real column, not yet named.

Both are cheap to resolve (two nullable fields, no relationship to existing data, no migration risk to anything already built) — but resolving them *after* `Step 4` of `docs/plan-brd-review.md` ships means either a second migration touching a table every other feature area already depends on, or a silent decision made without stakeholder input. Neither is worth the schema I'd otherwise write speculatively. **Stop-and-ask on these two specifically; everything else about Patient (CRUD, search fields, DTOs, Angular form skeleton) is unblocked and can be scaffolded in parallel while this is confirmed.**

### 5.2 Appointment / Visit — BLOCKED (on my own agent definition's own named gap)

**BRD says:** schedule appointments; view the daily list; update status across Scheduled/Completed/Cancelled/No-show.

The status enum, the daily-list query, and `Visit.AppointmentId`'s non-nullability are all already decided in my fixed spec — none of that is in question. The one thing that stops `Step 5` of `docs/plan-brd-review.md`:

- **MR-1 (Critical) — walk-in support.** My own agent definition already states this explicitly: *"If a walk-in with no prior appointment needs support, stop and flag it as an open question rather than silently making the field nullable."* `docs/brainstorm-brd-review.md` §1.1 and `docs/plan-brd-review.md`'s Scope & Assumptions both independently converge on the same fix — auto-create a same-moment `Appointment` row for a walk-in, which requires **zero schema change** to the already-fixed non-nullable `AppointmentId` — but a proposed fix across two documents is still not a stakeholder decision. I am not going to silently adopt it just because it appears twice; that's the exact failure mode my process exists to prevent.

**This is the cheapest blocker in this document to actually clear** — the fix is a single service-layer method (`CreateWalkInVisit`), not a schema change, and it's already fully designed in `docs/plan-brd-review.md` Step 5. It only needs a "yes, build it that way" from whoever owns this decision.

### 5.3 Consultation workflow — READY, with a documented note (not a stop)

**BRD says:** mandatory vitals (temperature, BP, pulse) for every consultation; complaints as free text; diagnosis notes; medication entry.

The prior pass's hard stop here (CON-1: does "mandatory" mean non-nullable at entry, or nullable-with-a-finalize-check allowing drafts?) is closed — my fixed spec picks non-nullable at entry, no draft path, full stop. That was the *forking* decision: it produces genuinely different entities, different migrations, different Angular validator wiring depending on the answer, which is exactly why my process stopped on it before.

The current review's finding in this area, **CR-1 (High) — the BRD never states a time budget for vitals within the 2–3 minute consultation target**, does not have that property. I checked this deliberately rather than pattern-matching "High severity, tied to a step I'm about to build → stop," because that pattern-match is what the prior pass got right on CON-1 and I want the same rigor applied here, not skipped because the outcome is more convenient:

- There is exactly one sensible implementation of "fast mandatory vitals entry" — a fixed-tab-order set of plain numeric inputs, `Validators.required`, no unit toggle (already resolved: °C only) — regardless of whether the BRD ever states "vitals ≤30 seconds" as an explicit sub-budget.
- Whether or not that budget line gets added to the BRD, the Angular component I'd write and the tests I'd write against it (`docs/plan-brd-review.md` Step 8: "mandatory vitals block submission... no added round-trip/modal") are identical either way.
- Nothing about this finding changes an entity, a migration, or an API contract — it changes a sentence in the BRD, which is a stakeholder/documentation action independent of implementation.

**Recommendation for this document specifically:** proceed with Step 7/8, and record in `docs/implementation-progress.md` that the consultation UI was built to the fastest defensible implementation of "mandatory vitals" without a stated sub-budget in the BRD, per CR-1 — so the gap is visible, not silently absorbed.

### 5.4 Prescriptions / printing — READY

**BRD says:** generate printable prescription with header/patient/vitals/diagnosis/medications/footer.

Immutability, the new-record-on-correction rule, and the `DoctorDetails` snapshot-at-creation rule are all fixed and unambiguous. No Critical/High finding in the current review targets this area. `docs/plan-brd-review.md` Step 10 (browser-native `window.print()`, no server PDF round-trip) is directly buildable.

### 5.5 Search & navigation — READY, except one scoped sub-feature

**BRD says:** search by name/phone; quick search; recent patients; navigation between profile and visits.

Phone's ambiguity (nullable, non-unique — previously the old CON-6 finding) is closed. General name/phone search (`docs/plan-brd-review.md` Step 11) is buildable today. The one open item:

- **MR-2 (High) — "recent patients" has no defined ranking** (most-recently-registered vs. most-recently-visited). This is scoped tightly to one query/one list component — it does not block patient search generally, visit history, or navigation. `docs/brainstorm-brd-review.md` §4.1 already recommends most-recently-visited, consistent with the rest of the search-ranking design — I'd build the rest of Step 11 now and treat "recent patients" ranking as a one-line confirmation needed before that specific sub-component ships, not before the step starts.

### 5.6 Visit history — READY

No blocking findings target this area in the current review. `docs/plan-brd-review.md` Step 12 is directly buildable once Step 5 (Appointment/Visit) unblocks.

### 5.7 CSV / PDF export — READY

Fully settled by the fixed Export spec; the current review has zero open findings against it. `docs/plan-brd-review.md` Steps 13–15 are directly buildable once the entities they read from (Patient, Visit, Prescription) exist.

### 5.8 Authentication — READY to build now; not release-ready

**BRD says:** secure login, single-user authentication, data encryption.

The `Users` table shape, the encryption mechanism, and the login comparison logic are all fixed and require no further decision — `docs/plan-brd-review.md` Step 17 is buildable today with no open schema question.

Two findings from the current review touch this area, and I'm treating them differently on purpose:

- **CR-3 (High) — "secure login" reads as stronger than reversible encryption implies.** This is a wording-reconciliation problem, not a design gap: my own agent definition already carries the accepted-risk rationale word-for-word (*"safe only because the app has no hosting, no network exposure beyond localhost, and exactly one local account... if any of those three conditions ever changes, this spec needs to be revisited"*). The current review's suggested fix is to copy that same rationale into the BRD. Nothing about Step 17's implementation changes either way — **proceed**, and note the pending BRD-text reconciliation in `docs/implementation-progress.md` when this step lands, exactly as my own auth-spec section already instructs.
- **AC-2 (Critical) — no password-recovery path exists.** Unlike HC-1/MR-1 above, this doesn't touch the `Users` table shape (`Id, Username, Password` stays exactly as fixed) or the login comparison logic — the current review's own suggested resolution is a documented **manual support procedure** (direct DB access by whoever supports the doctor), not a schema addition or a new endpoint. That means Step 17 itself is not blocked; what's blocked is calling authentication *done* for general release without that procedure existing somewhere. **Build Step 17 now; treat AC-2 as a release gate, consistent with `docs/worktree-brd-review.md`'s own Developer Readiness Assessment, which places it under "blocks general release, not initial development."**

### 5.9 Backup mechanism — blocked on adoption, not on ambiguity

**BRD says:** regular automated backups; no data loss.

- **AC-1 (Critical) — no backup mechanism is actually adopted anywhere.** This is different in kind from HC-1/MR-1: there's no remaining design question. `docs/brainstorm-brd-review.md` §3.5 and `docs/plan-brd-review.md` Step 18 both already specify the exact mechanism (Windows Task Scheduler + `sqlcmd BACKUP DATABASE`, chosen specifically for compatibility with the SQL Server Express edition the plan recommends). What's missing is a decision-owner saying "yes, build that," not more analysis. I'd sequence this per the plan (Phase 8, near the end) rather than blocking earlier feature work on it, but it cannot be silently skipped at general-release time — it's the one finding in this document where "proceed and revisit later" is explicitly not an option, because the cost of being wrong (unrecoverable data loss on the doctor's single machine) is unbounded, unlike every other deferred item here.

---

## 6. Verdict

- **Scaffolding: GO, today.** No blocker of any kind.
- **Consultation, Prescription/printing, Visit history, Export, Search (minus "recent patients"), Authentication (build, not release): GO, today.** Six of nine feature areas, versus three of eight in the prior pass — the difference is almost entirely `implementation-brd.md`'s own locked specs doing the work, not a change in how strictly this document applies its own stop-and-ask rule (§5.3 shows the rule applied exactly as strictly as before; it just doesn't trigger where it used to, because the underlying ambiguity is gone).
- **Patient records and Appointment/Visit: STOP.** Two Critical/High findings each, both narrowly scoped (Emergency Contact + Medical/Surgical History for Patient; walk-in support for Appointment/Visit), both cheap to resolve once a decision-owner responds, neither requiring new design work — the proposed answers already exist in `docs/worktree-brd-review.md` and `docs/brainstorm-brd-review.md`.
- **Backup mechanism: adopt the already-designed plan before general release.** Not a today-blocker for feature work, but the one finding in this document that cannot be silently deferred past release.
- **Password recovery: document the manual procedure before general release.** Same category as backup — a release gate, not a today-blocker.

**Net: unlike the prior pass, there is no reason to wait before starting implementation.** Six feature areas and the scaffolding step can begin immediately; Patient and Appointment/Visit need two small, already-answered-in-draft decisions confirmed before their entities are committed; and two release gates (backup, password recovery) need to be tracked so they don't get lost between now and general release rather than resolved before writing a single line of code.
