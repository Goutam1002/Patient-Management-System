# Implementation-Readiness Review — `BRD/Doc_BRD.md`

**Author:** `implementation-brd` (implementation engineer perspective)
**Date:** 2026-08-19
**Worktree / branch:** `.claude/worktrees/brd-review-report` on `brd/review-report` (the existing `worktree-brd` worktree, entered directly; `EnterWorktree` refused the switch because the session's working directory was the repository root, so the work was done in place on this branch rather than on a new worktree — nothing was committed to `main`)
**Reviews:** `BRD/Doc_BRD.md` @ blob `212cddc` (197 lines, unmodified)
**Grounded in:** `docs/worktree-brd-review.md` (this branch, @ `59e2dac` — BRD quality 3.0/10, development readiness 26%, 9 Critical findings), with `docs/planning-review.md` and `docs/brainstorm-review.md` read in full.
**Scope of this document:** assessment only. No application code was written, no entity or migration was created, no BRD text was modified, and `docs/worktree-brd-review.md` was not touched.

---

## 1. The question this document answers

`docs/worktree-brd-review.md` asks *"is this a good specification?"* and answers it thoroughly. This document asks a narrower and more mechanical question:

> **Given the stack and feature specs already locked into my agent definition, which BRD feature areas can I open an editor and start building today, and which would force me to invent a decision nobody made?**

The distinction matters commercially, not just semantically. A meaningful share of what the BRD review correctly flags as open is **already closed on the implementation side** by locked specs, and must not be re-counted as a project blocker in a decision session. Conversely, some things the BRD review treats as documentation gaps are, in a code-first EF Core build, *hard schema forks* that cannot be deferred past the first migration.

Three locked specs do the closing work:

| Locked spec | BRD findings it closes |
|---|---|
| **Local deployment model** — never deployed; runs permanently on the doctor's own machine. Angular served locally, .NET Web API on Kestrel/`localhost` with no reverse proxy, local SQL Server inspected via SSMS. No staging, no production, no external domain, no remote users. | **AR-1 (Critical — hosting model)** closed outright. Consequentially collapses **CON-4** (offline × availability × web-based), the "in transit" half of **CON-3** (loopback only), **AR-11** (data residency, moot), and **AR-10** (third-party data flow — nothing leaves the machine). |
| **Export spec** — scope is selected patients or a date range only, never full-database; two files (`patients.csv`, `visits.csv`) with fixed shapes and column orders; semicolon-encoded prescriptions; single-patient PDF with selectable date range; every export audit-logged; confirmation required before execution. | **CON-2 (High)** and **MR-7 (High)** closed, in materially more detail than the review proposed. |
| **Authentication spec** — exactly one doctor account; `Users(Id, Username, Password)`; password stored **plaintext, exactly as entered**; login is a direct lookup and equality check; no registration flow; no hashing framework, Identity, OAuth, MFA, or SSO. | **AR-4 (High — password recovery)** and **AR-6 (Medium — authorization model)** closed as *build* questions. |

Everything below keeps "blocked by the BRD" and "already settled for me" strictly separate, so the two are never double-counted.

---

## 2. Freshness check on the prior review — current, not stale

My own blocker-awareness rule requires verifying the review is up to date with the BRD before relying on any of its findings.

| Check | Result |
|---|---|
| Blob the review claims to review | `212cddc` (stated in its header, line 3) |
| `git hash-object BRD/Doc_BRD.md` in this worktree | `212cddc705dc0e2f960ca07c14793896caf54571` — **match** |
| SHA-256 of the BRD as it stands | `3c3e9b2ff4828bc75e004a087d849d27737f3d03982fe4643465a884ecdb5a94` |
| Last commit touching `BRD/Doc_BRD.md` | `5d512ca`, 2026-08-19 18:54 |
| Last commit touching `docs/worktree-brd-review.md` | `59e2dac`, 2026-08-19 19:22 — **later than the BRD**, so the review post-dates the document it reviews |
| Any newer BRD revision on another ref | None — `git log --all -- BRD/Doc_BRD.md` shows a single commit |
| Any review doc superseding `worktree-brd-review.md` | None found on any ref |

**Conclusion: the review is current. Its Critical/High findings are live and unresolved, and the blocker check in §4 below is valid.** This is stated explicitly because my instructions require me to say when a blocker check *did not* happen — here it did.

Repo state also verified before assessing: `Glob` for `angular.json`, `*.sln`, and `*.csproj` returns nothing anywhere in this worktree. **The repository is documentation only — no scaffolding exists, so "scaffold both projects" is genuinely step zero.**

---

## 3. Verdict in one line each

- **Scaffolding (Angular + .NET Web API + local SQL Server): GO.** Not blocked by the BRD at all.
- **Feature work: BLOCKED**, on thirteen decisions, six of them Critical, concentrated in exactly the files a code-first build touches first.
- **Feature-area count: 3 of 8 implementation-ready, 5 of 8 blocked.**

| # | Feature area | Verdict | Blocking findings (severity) |
|---|---|---|---|
| 1 | Patient records | **Blocked** | MR-2 (C), HC-1 (C), HC-2 (C), CON-6 (H), HC-3 (H), MR-4 (H) |
| 2 | Appointments | **Blocked** — largest partially-ready area | MR-5 (H) |
| 3 | Consultation workflow | **Blocked** — hardest area | CON-1 (C), HC-9 (H), HC-4 (H) |
| 4 | Prescriptions / printing | **Blocked** | MR-3 (C), MR-1 (C), MR-6 (H) |
| 5 | Search & navigation | **Blocked** | CON-6 (H) |
| 6 | Visit history | **Ready as specified** — sequenced behind 1, 3, 4 | none of its own |
| 7 | CSV / PDF export | **Ready** — settled by the fixed Export spec | none from the BRD |
| 8 | Authentication | **Ready** — settled by the fixed Authentication spec | none from the BRD; one BRD-text reconciliation owed |

Note honestly what that count means: **two of the three ready areas are ready because my locked specs overrode the BRD's silence, not because the BRD is adequate there.** Measured on the BRD's own terms, only Visit History is genuinely build-ready — which is consistent with, and independently corroborates, the prior review's 26% development-readiness figure.

The structural reason the split falls where it does: **in a code-first EF Core project the entity *is* the schema, and my own rules require the migration to ship in the same commit as the entity change that caused it.** There is no "start with patients and figure out the columns out later" path. The columns *are* the decision. Scaffolding touches no column, so it is free; the first entity class hits six Critical findings simultaneously.

---

## 4. Blocker zero — there is no executable plan, and the only plan present targets a different stack

Before any BRD question there is a process one. My execution loop requires a concrete, ordered set of steps before I write code. The repository contains exactly one such artifact, `docs/planning-review.md`: a genuinely good, well-sequenced 42-step Phase 1 plan with per-step files and tests.

**It is built against a different stack than the one locked into my agent definition.** Verified line by line:

| Layer | `docs/planning-review.md` | My fixed stack | Status |
|---|---|---|---|
| Frontend | ASP.NET Core Razor Pages + htmx 1.9 vendored locally, server-rendered `.cshtml` (line 32, and every file path in steps 4–42) | Angular (latest stable), standalone components, reactive forms | **Conflict** |
| Backend | Razor Pages app `PatientManagement.Web` with page models (step 3 file list) | .NET Web API — thin controllers, services, DTOs separate from EF entities | **Conflict** |
| Database | SQLite, WAL + FTS5 virtual tables and triggers (steps 7, 8) | SQL Server, local instance, inspected via SSMS | **Conflict** |
| ORM | EF Core | EF Core | Match |
| Auth | Step 11: Argon2id (19 MiB, t=2, p=1) via `Konscious.Security.Cryptography`, cookie `HttpOnly; Secure; SameSite=Strict`, soft PIN lock, 10 printed one-time recovery codes, failed-attempt lockout, `tools/reset-password` CLI | Plaintext `Users(Id, Username, Password)`, direct equality match, no recovery mechanism, no hashing of any kind | **Direct conflict — the plan names, by package, mechanisms my spec forbids by name** |
| Backup | Step 35 / assumption A9: Backblaze B2 or S3 with Object Lock, Litestream WAL streaming, encrypted off-site replication | No hosting, no cloud, single local machine | **Out of my scope** |
| CI/CD | Steps 3, 36, 38, 39: `.github/workflows/ci.yml` or `azure-pipelines.yml`, restore-test and PHI-guard workflows, perf gate in CI | No CI/CD pipelines (explicit non-goal) | **Out of my scope** |
| TLS | Step 37: certificates, HSTS | Loopback only, no reverse proxy | **Out of my scope** |

I am flagging this rather than silently substituting either side, exactly as my instructions require. **This is not a criticism of that plan** — it was written 2026-08-18, states plainly that no stack was committed at the time, and reasons its choice properly. But the practical consequences for execution are concrete:

- **Every file path in it is unusable.** `src/PatientManagement.Web/Pages/**/*.cshtml` has no equivalent in an Angular + Web API split.
- **Step 8 is unbuildable as written.** SQLite FTS5 virtual tables and triggers do not exist in SQL Server; the equivalent is a full-text catalog or an indexed normalized-name column — a different design producing different migration content and different tests.
- **Step 11 must be discarded and rewritten**, not adapted. It specifies Argon2id, recovery codes, and lockout; my spec forbids all three.
- **Steps 3, 35–39, and 42 are partly or wholly outside my scope** under the no-hosting rule.

Steps **1, 2, 6, 7, 9, 10** and the overall sequencing logic remain valid and reusable, because they encode decisions and domain logic rather than framework code.

**Consequence: even with every BRD question answered, I could not start the execution loop today** — I would be inventing step boundaries as I went, which my process forbids. This is resolved by one of: `plan-brd` re-issuing the plan against the fixed stack; the user supplying an explicit ordered step list; or an instruction to treat "scaffold Angular + .NET Web API" as a standalone step zero.

---

## 5. Feature-area assessment

### 5.1 Patient records — BLOCKED

**BRD says:** add, edit, view patient details; capture Name, Age / DOB, Gender, Contact details; search by name or phone.

The prior review rates "add, edit, view patient details" build-ready, and as a set of CRUD operations it is. **The blocker sits one layer down: I cannot write `Patient.cs` and `0001_InitialCreate` without knowing which columns exist.** Six findings land on that single file:

| Finding | Sev | Why it stops the first migration |
|---|---|---|
| **MR-2** — "Age / DOB" written as interchangeable | Critical | A column-type decision, not a UI one. `Age int` and `DateOfBirth date` are different schemas. The review is right that the wrong choice is silent corruption rather than a visible bug — and it propagates to every history display, every reprint, and the `DOB` column my Export spec mandates. Carries two sub-decisions: an estimated-DOB precision marker, and whether age-at-visit is snapshotted onto each visit row. |
| **HC-1** — no allergy field in a system whose primary output is a prescription | Critical | A patient-level column. Cheap to add later mechanically (one entity change, one migration) — but the *data* cannot be backfilled, only re-collected patient by patient. Deciding after go-live means a permanently thin allergy history. |
| **HC-2** — no current / long-term medication list | Critical | A child table (`PatientMedication`), not a column — a schema-*shape* decision, and one the BRD gives no hook for. Cannot be derived from prescription history, since it must include drugs prescribed elsewhere. |
| **CON-6** — phone offered as a search key in a population that often lacks or shares one | High | Decides `Phone` nullability and whether a unique index exists. Wrong, it blocks legitimate registrations. Also decides whether a human-readable patient code exists alongside the surrogate key — which the `PatientId` column in my Export spec arguably presumes (see §5.7). |
| **HC-3** — no chronic conditions / problem list | High | Same shape question as HC-2: child table or nullable text. Free-text per-visit diagnosis is not a substitute. |
| **MR-4** — no deletion, archival, or correction policy | High | Decides whether a DELETE endpoint exists at all, and whether `ArchivedAt` / `VoidedAt` are present from migration 1. |

**This is a stop-and-ask, not a note.** Three Critical and three High unresolved findings converge on one entity. Writing `Patient.cs` now means silently choosing all six by default — precisely the "encode the wrong behavior in code instead of catching it in review" failure my instructions exist to prevent.

**Genuinely ready inside this area, but uncommittable:** the API surface (`GET/POST/PUT api/patients`), DTO-vs-entity separation, validation plumbing, and the Angular reactive-form skeleton. All are typed against the entity, so none can ship first.

### 5.2 Appointments — BLOCKED (largest partially-ready area)

**BRD says:** schedule appointments; view the daily list; update status across Scheduled / Completed / Cancelled / No-show.

The prior review marks the daily list and the status transitions build-ready and I agree — the status enum is fully enumerated in the BRD, which is rarer than it sounds, and the daily list is one query.

**Blocked on one High finding, MR-5.** The stopping question is not slot length or double-booking — those are configurable defaults I could set and record. It is: **can a consultation exist without an appointment?** That single answer decides whether `Visit.AppointmentId` is `int` or `int?`, and whether `Visit` or `Appointment` is the clinical root record. `docs/planning-review.md` assumes "Visit is root; walk-ins auto-create an appointment row" — a defensible answer, but it is an assumption carried over from brainstorming, not a stakeholder decision, and the BRD's appointment-first framing points the other way. Changing it after data exists is a data migration plus a rewrite of the consultation save path.

**Proceed-with-note (Medium, not a stop):** AR-9, the clinic-day boundary. I would implement the daily list against local midnight and record that assumption in `docs/implementation-progress.md`.

If MR-5 alone were answered, appointments would be the first area I could actually ship — but it still cannot precede patients, and patients are blocked.

### 5.3 Consultation workflow — BLOCKED (hardest area; the one my own instructions single out)

**BRD says:** mandatory vitals (temperature, BP, pulse) for every consultation; complaints as free text; diagnosis notes.

**CON-1 (Critical) is a hard stop.** Mandatory vitals × single user with no receptionist × a 2–3 minute consultation record all bind one resource: the doctor's own hands and time. My agent definition names this exact contradiction as the canonical example of something that must not be built through. The review is right that no UI can make taking a blood pressure faster.

What makes it an *implementation* blocker rather than only a product debate is that the resolution changes code I would otherwise have to rewrite:

- **Vitals mandatory at entry:** `Temperature`, `Systolic`, `Diastolic`, `Pulse` are non-nullable; there is no draft or autosave path, because a draft cannot satisfy a `NOT NULL` column. The Angular test my strategy requires ("mandatory vitals block submission") reduces to `Validators.required`.
- **Vitals mandatory at finalize** (the review's recommendation): the columns are nullable, enforcement moves to a service-layer finalize rule plus a conditional constraint, drafts autosave, and the reactive form carries two submit paths with different validator sets. Different entity, different migration, different component, different tests.

Retrofitting the second from the first is a schema change plus a rewrite of the save path.

**Also blocking:**

- **HC-9 (High)** — temperature has no recorded unit (°C/°F ambiguity is real clinical corruption, and a clinic swapping thermometers would silently rewrite the meaning of its own history), and BP has no storage format. Storing `"120/80"` as a string destroys validation, trending, *and* the `visits.csv` export. Two `smallint` columns is the correct call, but it is a schema decision I am not authorized to make alone. The "unable to record — reason" escape hatch changes both the finalize rule and the column set.
- **HC-4 (High)** — weight absent. Per-kilogram paediatric dosing is not optional if the practice sees children, and whether it joins the vitals set is a column decision on the same entity.

**Cross-cutting note, required by my consultation-path rule:** I am obliged to report added latency and click cost against the 2–3 minute target on every step touching this path. **I currently cannot**, because the target has no defined measurement method — the BRD never says whether the 2–3 minutes includes physically taking the vitals. That makes the hardest constraint in the BRD unenforceable inside my own test strategy, not merely unverifiable at acceptance. It needs a stated start and stop point for the stopwatch before I can gate anything on it.

**Ready in isolation:** complaints and diagnosis free-text capture, both rated build-ready by the prior review. They live on the `Visit` entity, which the vitals decisions block, so they cannot ship first.

### 5.4 Prescriptions / printing — BLOCKED (three independent blockers)

**BRD says:** add medicines with name, dosage, frequency, duration, instructions; generate a printable prescription with clinic/doctor header, patient details, vitals, diagnosis, medications, and footer.

| Finding | Sev | Implementation consequence |
|---|---|---|
| **MR-3** — no immutability or amendment rule for a printed prescription | Critical | The single biggest schema fork in the product: one mutable `Prescription` table, or a versioned chain with `SupersedesPrescriptionId` plus a frozen rendered snapshot? Both the prior review and `docs/brainstorm-review.md` call the retrofit a rewrite rather than a patch. I will not pick this by default. |
| **MR-1** — the printable prescription consumes a Clinic Profile that no requirement creates | Critical | Two BRD requirements depend on a data source zero requirements produce. The honest outcomes are (a) hardcode clinic name, qualifications, and medical registration number and take the change request within a week, or (b) invent a `ClinicProfile` entity the BRD never authorized. Both are wrong moves. A second decision hides inside it: header values must be snapshotted per printed prescription, or changing the clinic phone number silently rewrites every historical prescription. |
| **MR-6** — medication entry free-text vs coded, undecided | High | Decides the `PrescriptionItem` schema *and* is the largest single component of the 2–3 minute budget. Autocomplete over the doctor's own prescribing history needs a corpus table from migration 1; plain free text needs nothing. |

**Also open but not a stop:** TS-3 (Medium) — "smooth generation and printing" has no pass/fail condition, and paper size and letterhead handling are unspecified. I would default to a print stylesheet driven by the browser's print dialog and record the assumption; the real answer requires the clinic's actual printer and paper.

**Note for the Export spec's benefit:** the `Prescriptions` cell format my spec locks — `DrugName (Dosage, Frequency); ...` — consumes `DrugName`, `Dosage`, and `Frequency` as discrete fields. That is compatible with either MR-6 outcome, but it does confirm those three must be separate columns rather than one free-text blob.

### 5.5 Search & navigation — BLOCKED

**BRD says:** search by name or phone; quick patient search; view recent patients; easy navigation between profile and visits.

**Blocked on CON-6 (High)** — the same decision as §5.1, arriving from the other end. The BRD offers exactly two search keys and the prior review establishes that neither reliably addresses the population served: elderly patients and children frequently have no phone, families share one number, and there is no third key because no patient-identifier requirement exists. Implementing search against a nullable, non-unique `Phone` with no human-readable code produces exactly the failure the review's risk register rates Critical — a search that misses a patient the doctor knows exists.

**Proceed-with-note (Medium):** TS-10 — "basic", "quick", and "recent" are three vaguenesses describing one feature, with match semantics, ordering, result count, and the definition of "recent" all unspecified. I would implement name-substring plus phone-suffix matching, ordered by last visit, limited to 20, and record it as an assumption. Note, not a stop.

**Do not count CON-3 as an open blocker against me.** CON-3 (encryption at rest × the 2–5 second search target) turns entirely on whether "encryption at rest" means column-level encryption of `Name`/`Phone`, which would destroy index-ability. Under the locked local deployment model, transit is loopback-only (nothing to terminate) and at-rest is an OS/volume concern — BitLocker on the doctor's machine — outside application code. Nothing in my fixed stack directs column-level encryption. **CON-3 reduces from High to a confirmation item.** It still deserves a recorded line in the BRD so the outcome is a decision rather than a default, but it does not block me.

### 5.6 Visit history — READY AS SPECIFIED (sequenced, not blocked)

**BRD says:** view previous visits with vitals, complaints, diagnosis, and prescriptions; filter by date.

Both requirements are rated build-ready by the prior review and I concur. **This is the one feature area where the BRD is genuinely a sufficient specification** — no contradiction, no missing clinical field, no undocumented architecture decision attached to it.

It is **dependency-sequenced rather than blocked**: it reads the `Visit` and `Prescription` entities, so it can only follow §5.1, §5.3, and §5.4. Its single open item — AR-9's clinic-day boundary, which decides what "filter by date" means at the edges — is Medium and proceeds under a recorded assumption.

### 5.7 CSV / PDF export — READY (settled by the fixed Export spec; do not double-count)

**This is the clearest case of a BRD gap already closed on the implementation side, and it should not appear on the stakeholder's blocker list at all.**

The prior review raises two findings here: **CON-2 (High)** — encryption at rest versus an export that lands an unencrypted extract in a Downloads folder — and **MR-7 (High)** — the export contract being entirely undefined. Both are correct about the BRD. Both are already answered, in more detail than the review proposed:

| The review asked for | The fixed Export spec already mandates |
|---|---|
| "No unscoped full-database export" (CON-2) | Scope is selected patients or a date range only. **No code path may produce an unbounded/all-patients CSV**, enforced by a required negative test. |
| "Every export logged" (CON-2) | Audit trail — who exported, what scope, which format, when. The endpoint is not done until logging exists *and is tested*. |
| "Explicit confirmation step" (CON-2) | UI confirmation on the Angular side; the API must not treat a request as implicitly confirmed. Enforced by a test that an unconfirmed export is rejected. |
| "Separate patient and visit files, keyed so they can be rejoined" (MR-7) | `patients.csv` and `visits.csv`, both carrying `PatientId`, deliberately overlapping rather than deduplicated. |
| "The file shape is a real design decision — medications nest inside visits while CSV is flat" (MR-7) | Resolved explicitly: `Prescriptions` is one cell per visit, semicolon-separated as `DrugName (Dosage, Frequency); DrugName (Dosage, Frequency); ...` |
| "PDF as a readable patient summary over a selected date range" (MR-7) | Single-patient summary only — demographics, visit history, prescriptions — with a selectable date range. |

**Net: CON-2 and MR-7 require no BRD decision before I can build export.** They do require a BRD *amendment* eventually, so the document and the build agree and a later verification pass does not fail a correct implementation — but that is a documentation task, not a blocker.

**The row shape is fully specified and is a hard gate.** `patients.csv` is one row **per visit**, not per patient; a patient with three visits in range produces three rows, with demographics repeating. That is confirmed shape, not an oversight to normalize away, and the required test proves it.

**One genuine implementation coupling worth naming, because it is easy to miss.** The spec requires `patients.csv` to carry *every field defined on the `Patient` entity at the time of export*, explicitly including fields the entity gains later from the healthcare-completeness gaps in `docs/worktree-brd-review.md` — allergies, emergency contact, and so on — and forbids hardcoding a shorter column list. **That makes the `patients.csv` header a function of the still-unresolved Patient schema decisions in §5.1 (D1, D2).** The spec anticipates this and resolves it correctly: the column set must be derived from the entity (reflection or an equivalent generated mapping), not written out by hand. So export is buildable before those decisions land — the writer must simply never contain a literal column list. This is a design constraint on the CSV writer, not a blocker, but building it the naive way would quietly violate the spec the first time `Patient` gains a field.

**Residual, not from the BRD:** `PatientId` semantics tie back to CON-6. Is the exported `PatientId` the surrogate EF key, or a human-readable patient code? The prior review argues an export keyed on a bare surrogate PK is not usable by a human. Same decision as §5.1 and §5.5, arriving from a third direction.

**A useful signal for the stakeholder session:** the locked export column list says `DOB`, not `Age`. That is an independent argument that MR-2 should resolve toward date-of-birth-as-source-of-truth — the export contract is already written that way, so choosing `Age int` would put the schema and the locked export spec in permanent disagreement.

### 5.8 Authentication — READY (settled by the fixed Authentication spec; do not double-count)

**BRD says:** "Secure login (single user authentication)" and "Data encryption (at rest and in transit)".

The BRD's phrasing is unbuildable on its own — "secure" is undefined — and the prior review adds **AR-4 (High: no password recovery path, launch-blocking lockout risk)** and **AR-6 (Medium: authorization model undefined)**. The fixed spec settles the mechanism completely: one doctor account; `Users` with exactly `Id`, `Username`, `Password`; password stored exactly as entered, plaintext, no transformation; login as a direct lookup and equality check; no registration flow; and an explicit prohibition on BCrypt/PBKDF2/Identity hashers, ASP.NET Identity, OAuth, external IdPs, MFA, and SSO.

**AR-4 dissolves rather than being answered.** With one plaintext row in a local SQL Server database the doctor can open in SSMS, password recovery is not a feature to build — no email infrastructure, no recovery-code generator, no reset endpoint. It becomes a one-line operational note that sits outside code. **AR-6 is closed** by the spec's "no roles/permissions", with the `Users` row itself supplying the "who" that the Export audit log requires.

**Two conflicts I must flag rather than resolve myself:**

1. **The fixed Authentication spec contradicts the BRD's own security NFR.** `Doc_BRD.md` requires "Data encryption (at rest and in transit)"; plaintext password storage is a direct, deliberate violation of that line as written. I am **not** proposing to change the auth spec — it is locked, and its accepted-risk rationale is explicit and sound. But it holds *only* because (a) there is no hosting, (b) no network exposure beyond `localhost`, and (c) exactly one local account. All three hold under the fixed local deployment model today. The BRD text still says otherwise, which means any verification pass against the BRD will correctly fail a correct build. **The BRD needs a recorded exception, or this contradiction becomes permanent.** If any of the three conditions ever changes — a second user, receptionist access, LAN or internet exposure — the spec must be revisited before shipping. That dependency will be recorded explicitly in `docs/implementation-progress.md` when the auth step is implemented, per my instructions, rather than silently built.
2. **`docs/planning-review.md` step 11 specifies precisely the mechanisms my spec forbids by name** — Argon2id via `Konscious.Security.Cryptography`, cookie auth with `HttpOnly; Secure; SameSite=Strict`, a soft PIN lock, printed one-time recovery codes, a failed-attempt lockout, and a `tools/reset-password` CLI. If that plan is ever accepted as-is, step 11 must be rewritten to the fixed spec, or the fixed spec must be changed by the user. I will not silently substitute either direction.

**Fully specified and testable today,** per my test strategy: a positive exact-match login; negatives for wrong username, wrong password, and empty/missing credentials; a schema test asserting `Users` exposes exactly `Id`, `Username`, `Password` with no `PasswordHash`, `Salt`, or `MfaSecret`; a round-trip test proving the stored password equals the seeded value byte for byte (proving no hashing is silently applied); and a test proving no registration or self-signup endpoint exists.

---

## 6. Decisions still needed before feature work can start

### 6.1 Before step zero — scaffolding

**None of these are BRD questions.** The BRD blocks feature work, not project setup.

| # | Decision | Owner | Why it is needed at step zero |
|---|---|---|---|
| **S1** | **Which ordered plan am I executing?** `docs/planning-review.md` is stack-incompatible (§4). Options: `plan-brd` re-issues it against Angular + Web API + SQL Server; the user supplies an explicit step list; or I am told to treat scaffolding as a standalone step zero. | User / `plan-brd` | My process forbids inferring scope from a feature name. Without this there are no step boundaries and no definition of done. **This is the real gate.** |
| **S2** | **Local SQL Server target** — LocalDB, SQL Server Express, or a full local instance; instance name; Windows auth or SQL auth. | User (owns the machine) | The `appsettings.json` connection string is written at scaffolding time, and the migration smoke test needs a real local instance to apply against. One sentence unblocks it. |
| **S3** | **Test database strategy** — EF Core InMemory, SQLite in-memory, or a disposable local SQL Server test database for integration tests. | Me, unless directed | My strategy permits in-memory or a test instance, but InMemory does not honour SQL Server constraints — which matters directly for the finalize/vitals rule in §5.3. I lean toward a disposable local SQL Server database, flagged here as a choice being made rather than found. |
| **S4** | **Repo layout** — `src/api` + `src/web` vs `backend/` + `frontend/`, and whether the Angular workspace sits inside the .NET solution directory. | Me, unless directed | Purely conventional; recorded so it is a decision rather than an accident. |

### 6.2 Before step one of feature work — BRD decisions, one session

Ordered by the sequence a code-first EF Core build hits them. **All are closable on paper; none needs code, a prototype, or research.**

| # | Decision | Blocks | Findings |
|---|---|---|---|
| **D1** | **DOB or Age as the source of truth?** Including approximate-age precision and whether age-at-visit is snapshotted per visit. | `Patient` entity, first migration, every history display and reprint, the `DOB` column in both CSVs | MR-2 (Critical) |
| **D2** | **Are allergies, current medications, and chronic conditions in Phase 1?** Each is a patient-level field or a child table. | `Patient` schema shape; also the `patients.csv` column set (§5.7) | HC-1, HC-2 (Critical), HC-3 (High) |
| **D3** | **Is phone optional and non-unique, and does a human-readable Patient ID exist?** | `Patient` entity, all search, the exported `PatientId` column | CON-6 (High) |
| **D4** | **Deletion policy** — hard delete for zero-visit patients, archive otherwise, never delete visits? | Whether a DELETE endpoint exists; `ArchivedAt`/`VoidedAt` on migration 1 | MR-4 (High) |
| **D5** | **Can a consultation exist without an appointment?** | `Visit.AppointmentId` nullability; which entity is the clinical root | MR-5 (High) |
| **D6** | **Vitals mandatory at entry or at finalize — and what happens when a vital genuinely cannot be measured?** Plus: **is the 2–3 minute target measured including or excluding physically taking vitals?** | Column nullability, draft/autosave, the finalize rule, the reactive form's validator structure, and my ability to test against the time target at all | CON-1 (Critical), HC-9 (High) |
| **D7** | **Vitals storage format** — temperature unit (°C/°F, stored per reading), BP as two numeric columns, and whether weight joins the set. | Vitals columns, validation, `visits.csv` usability | HC-9, HC-4 (High) |
| **D8** | **Is a printed prescription immutable?** Versioned chain with a frozen snapshot, or editable in place? | The prescription schema fork; the retrofit is a rewrite | MR-3 (Critical) |
| **D9** | **Does a Clinic Profile entity exist** — clinic name, doctor qualifications, medical registration number, logo/signature — and are its values snapshotted per printed prescription? | Printing cannot be built at all without it | MR-1 (Critical) |
| **D10** | **Medication entry: free text, or free text with autocomplete over the doctor's own history?** | `PrescriptionItem` schema; the largest component of the consultation time budget | MR-6 (High) |
| **D11** | **Is clinical audit logging (who changed what, before and after) in Phase 1?** Distinct from the export audit log, which my spec already mandates. | Cheap now, permanently unbackfillable later; touches the patient, visit, and prescription entities | AR-5 (High) |
| **D12** | **Record the accepted plaintext-password exception in the BRD**, naming the three conditions it depends on. | Not code — but without it the build permanently contradicts the BRD's security NFR and will fail any honest verification pass | §5.8 of this review |

**Confirm-only — build proceeds under a recorded assumption:** clinic-day boundary (AR-9); search match semantics, ordering, result limit, and the definition of "recent" (TS-10); examination-findings field (HC-8); SpO2 and height/BMI (HC-5, HC-6); emergency contact (HC-7); follow-up interval stored and printed with no reminder (CON-8); paper size and letterhead handling (TS-3); and confirmation that at-rest encryption means disk-level rather than column-level (CON-3, reduced by the local deployment model).

**Out of my scope entirely under the no-hosting rule — flagged, not built:** off-site or cloud backup replication (`docs/planning-review.md` A9 names Backblaze B2 with Object Lock), CI/CD pipelines (its steps 3, 36, 38, 39), TLS certificates and HSTS (step 37), and any staging environment. Backup on a permanently-local single machine is a SQL Server maintenance task the human owns via SSMS, not application code I write. **AR-2's RPO/RTO and named-restore-owner question remains a real operational gap** — it is simply not a coding gap for me, and it should not fall off the stakeholder's list just because it fell off mine.

### 6.3 Change from the assessment on `impl/brd-readiness-review`

That earlier assessment raised a thirteenth decision — an ambiguity in the Export spec's column list, which stated a single six-column order while also mandating two files, leaving it unclear whether each file carried all columns or the list was split across them. **That ambiguity is now resolved in the spec itself** and is therefore closed, not carried forward: `patients.csv` carries every `Patient` entity field plus `VisitDate`, `Diagnosis`, and `Prescriptions`, one row per visit; `visits.csv` carries `PatientId, Name, DOB, Phone, VisitDate, Diagnosis, Prescriptions` in that fixed order, with the semicolon-encoded prescription format defined for both. The CSV writer's header row can now be produced without guessing. This assessment carries twelve decisions rather than thirteen for that reason.

---

## 7. What I could build the day the decisions land

Recorded so the blocked verdict is not misread as "nothing is ready":

1. **Step zero — scaffolding.** Angular workspace with standalone components; .NET Web API solution with thin controllers, services, and DTOs separate from EF entities; unit and integration test projects; `localhost` connection string; `localhost`-only CORS; and an EF Core migration harness with a smoke test that applies migrations to a fresh local database. Blocked only by S1/S2, not by the BRD.
2. **Authentication, end to end**, exactly to the fixed spec, with all six required tests. Fully specified today.
3. **Export, end to end**, exactly to the fixed spec, with every required test — the negative unbounded-export proof, the one-row-per-visit proof for `patients.csv`, the fixed-column-order and semicolon-encoding proof for `visits.csv`, the unconfirmed-export rejection, and the audit-log assertion — once the entities it reads exist and the `PatientId` half of D3 is answered.
4. **Visit history**, once the entities beneath it exist.

That is 3 of 8 areas, matching the prior review's finding that roughly a quarter of the BRD is build-ready — with the difference that two of my three come from locked specs rather than from the document.

---

## 8. Final verdict

**Scaffolding: GO.** The BRD's single most expensive open decision — AR-1, the hosting model, which the prior review calls the most expensive deferral in the document — is already closed by the locked local deployment model. Nothing else that blocks feature work touches project setup. Given one answer on the plan (S1) and one on the local SQL Server instance (S2), I can create both projects, wire the migration harness, and prove the test loop runs green.

**Feature work: BLOCKED, on twelve decisions, six of them Critical.** Five of eight feature areas are blocked, and the blockers concentrate exactly where a code-first EF Core build hits first: the `Patient` entity (six findings on one file), the vitals columns on `Visit`, and the prescription immutability fork. These cannot be deferred past the first migration, because in this stack the entity *is* the schema.

**The encouraging part, restated from the build side:** none of the twelve needs code, a prototype, or research. They need one session with the doctor and the stakeholder, plus an amendment to the BRD's "Open Questions: None" line — which the prior review correctly identifies as the highest-leverage defect in the document, because it is the line suppressing every question below it. Answer D1–D11, record D12, and hand me a stack-correct ordered step list, and all five blocked areas become buildable in plan order with no rework of anything scaffolded in the meantime.

**What I deliberately did not do:** no application code, no entity classes, no migration, no BRD edit, no modification to `docs/worktree-brd-review.md`, and no silent substitution of a stack or an auth mechanism. The two conflicts between the locked specs and the existing plan — the whole stack, and Argon2id versus plaintext auth — are surfaced here for a decision rather than resolved by me.
