# Brainstorm Review — Patient Management Application

**Source:** `BRD/Doc_BRD.md`
**Method:** four independent brainstorming passes (data model, consultation UX, technical architecture, gaps & roadmap) run against the BRD, then consolidated and reconciled.
**Date:** 2026-08-17
**Status:** pre-build. The repo contains the BRD only — no code, no schema, no stack. Every decision below is still free to make.

---

## 0. Executive summary

The BRD describes a coherent, appropriately-scoped Phase 1. Three findings matter more than the rest:

1. **"Open Questions: None" does not hold.** There are roughly 15 unanswered decisions, and at least 6 are schema- or topology-level — the kind that cost a rewrite rather than a patch. They are listed and ranked in §8.

2. **The 2–3 minute consultation target is won or lost in prescription entry**, not in layout, framework, or database. Prescription entry is ~45% of the time budget and ~70% of the input events. Three features — drug autocomplete that recalls the doctor's own last-used regimen, "repeat last Rx", and personal-corpus autocomplete on diagnosis — take a naive 4-minute form to ~2 minutes. Layout choice is worth maybe 20 seconds on top of that.

3. **Two requirements in the BRD contradict other requirements in the BRD.** Excluding receptionist access puts vitals entry on the doctor's own critical path, directly undermining the 2–3 minute criterion. Excluding offline functionality means a cloud-hosted app stops the clinic when the internet drops. Both need an explicit decision, not a silent one.

**Headline recommendation:** answer the six blocking questions in §8, then build a narrower MVP than the BRD's Phase 1 — the consultation loop and printing first, appointments and export second.

---

## 1. Data model

### 1.1 Visit vs. Appointment — the highest-cost structural decision

**Recommendation: two entities. `Visit` is the root clinical record; `Appointment` is the booking lifecycle. Walk-ins auto-create an appointment row so the daily list stays a single indexed query.**

Why not one table: collapsing them forces every clinical column to be nullable, which is exactly the constraint you most want the database to enforce (mandatory vitals). A cancelled appointment would carry an empty clinical shell.

Why walk-ins auto-create an appointment: it buys back the single-query daily list that a pure two-table model sacrifices, at the cost of one trivial insert.

**Status linkage rule:** finalizing a visit sets `appointment.status = 'completed'`. `appointment.status` is booking truth; `visit.status` is clinical truth. Do not let one derive the other beyond that single transition.

> **Risk if wrong:** if consultation hangs off appointment as a required parent, every walk-in forces a fake backdated appointment, the daily list fills with phantom rows, and status transitions become nonsense. Rework = schema migration plus rewrite of the home screen, history, and export.

### 1.2 Vitals

**Recommendation: typed columns on `visit` — `temperature`, `temperature_unit`, `bp_systolic`, `bp_diastolic`, `pulse_bpm` — plus a `vitals_extra jsonb` escape hatch.**

Three non-obvious consequences:

- **Store systolic and diastolic as separate smallints**, never `"120/80"` as text. A string kills validation, trending, and CSV usability.
- **Store the temperature unit alongside the value.** °C vs °F ambiguity in a clinical record is a real harm vector, and a clinic that changes thermometers will otherwise silently corrupt its history.
- **Mandatory ≠ `NOT NULL`.** If vitals are `NOT NULL` at the table level you cannot autosave a half-finished consultation — which fights the speed target directly, because the doctor *will* be interrupted. Enforce with a conditional check at finalize instead:

```sql
CHECK (status <> 'completed' OR (temperature IS NOT NULL AND bp_systolic IS NOT NULL
       AND bp_diastolic IS NOT NULL AND pulse_bpm IS NOT NULL))
```

### 1.3 Complaints and diagnosis

**Recommendation: keep the stored shape free text, exactly as the BRD specifies. Buy structure back at the keyboard, not in the schema.**

- Free text `complaints_text` / `diagnosis_text` on the visit.
- A `phrase_suggestion` side table (kind, text, use_count, last_used_at) feeding typeahead from the doctor's own past entries.
- A reserved nullable `diagnosis_code` column the UI ignores in Phase 1 — costs nothing now, avoids a painful migration if coding ever arrives.

A structured symptom picker would give clean analytics but contradicts the BRD, needs a maintained taxonomy, and clicking six checkboxes is slower than typing six words. Explicitly don't.

### 1.4 Prescription — the only immutable entity

**Recommendation: `prescription` header + `prescription_item` lines + a frozen `rendered_snapshot`.**

The prescription is the one artifact that leaves the building on paper and can be presented back to the doctor months later. It needs to be first-class, finalizable, and versioned — everything else in the visit can be a plain column.

- **Snapshot must include the letterhead fields**, not just the clinical ones. Otherwise updating the clinic phone number silently rewrites every historical prescription.
- **Line items always store the literal printed text** (`drug_name`, `dosage_text`, `frequency_text`, `duration_text`) alongside any `medication_id` FK. If a dictionary entry is later renamed, a historical prescription must not change retroactively. The FK is for autocomplete and stats; the text is the record.
- **Editing a finalized prescription creates version *n+1*** with `supersedes_prescription_id`; the old row flips to `superseded`. Silent in-place editing of a document already in a patient's hands is the one thing to avoid.

### 1.5 Identity, duplicates, and age

**There is no reliable natural key.** Phones are shared across families, names repeat and transliterate inconsistently, DOB is often estimated, and some patients have no phone at all. Every candidate unique constraint fails.

**Recommendation:**
- Surrogate PK + a human-readable `patient_code` (`P-000142`) printed on the prescription and used as the join key in CSV exports.
- **Soft duplicate detection at registration** — fuzzy name match or exact normalized-phone match shows "3 similar patients, is this one of them?" with last-visit dates. **Warn, never block.**
- Reserve `merged_into_patient_id` as a nullable self-FK now, even if merge ships in Phase 1.5. Retrofitting it after 2,000 patients is painful.
- **Store `phone_digits`** (stripped of `+`, spaces, dashes) as a separate indexed column from `phone_raw`. Users type `+91 98765 43210` and search `9876`; without normalization, phone search silently fails.

**Age vs DOB:** store `dob` + a `dob_precision` enum (`exact` / `year_only` / `estimated_from_age`). If only age is given, synthesize the DOB and mark it estimated, keeping `age_entered_years` and `age_recorded_on` for provenance. One column to compute from everywhere, plus a flag telling the UI to render "≈45 yrs" rather than a false birthday.

> **And snapshot `patient_age_years_at_visit` onto the visit row.** A prescription reprinted in 2029 for a 2026 visit must print the age the patient was in 2026. Deriving age at render time is a silent correctness bug in a printed medical document.

### 1.6 Deletion

Tiered, by whether clinical history exists:

| Case | Rule |
|---|---|
| Patient with **zero** visits | Real hard delete allowed — it's a registration typo, not a medical record. Log it anyway. |
| Patient **with** visits | No delete. `archived_at` + reason, hidden from default search, restorable in one click. Label the button **"Archive"**. |
| Visits and prescriptions | Never deletable. `voided_at` + `void_reason` so a wrongly-issued prescription can be struck through but stays on the record. |

> **Privacy flag:** soft delete means PHI is retained indefinitely. If "delete this patient" ever needs to mean *actually erase* — a patient request or a retention policy — that requires a separate genuine purge path reaching audit rows, snapshots, stored files, and backups. This is a real gap, not a hypothetical one.

### 1.7 Amendment and audit

A two-tier rule that adds zero clicks to the consultation path:

- **Visit:** freely editable — plain `UPDATE`, no ceremony — while in progress or within a grace window after finalize (proposal: end of the clinic day). After that the UI still says "Edit", but the save stamps `amended_at` / `amendment_note` and a DB trigger records before/after into a generic `audit_log`.
- **Prescription:** immutable the instant it's finalized, because that's the copy in the patient's hand. Versioned per §1.4.

A generic trigger-driven `audit_log(table, row_pk, action, before jsonb, after jsonb, changed_fields, actor, at)` covers every entity at once, is invisible to the UI, and costs ~50 lines.

### 1.8 Recommended schema sketch

Ten tables. Bigint identity PKs, plus a human-readable `code` on the three entities that appear in exports and conversation.

```sql
-- ── settings & auth ──────────────────────────────────────────
clinic_profile     -- single row, CHECK (id = 1) — NOT multi-doctor
  clinic_name, address_*, phone, email
  doctor_name, qualifications, registration_no, specialty
  logo_bytes, signature_bytes
  prescription_header_note, prescription_footer_note
  timezone, default_temperature_unit CHECK (IN ('C','F'))

app_user
  username, password_hash, password_algo, full_name
  last_login_at, password_changed_at, failed_attempts, locked_until

-- ── patient ──────────────────────────────────────────────────
patient
  id, patient_code UNIQUE                    -- 'P-000142'
  full_name, name_normalized (generated, indexed)
  gender, dob, dob_precision, age_entered_years, age_recorded_on
  phone_raw, phone_digits (indexed), alt_phone_digits, email, address
  allergies_text, chronic_conditions_text    -- [stretch] safety-relevant
  last_visit_at, visit_count                 -- denormalized for list/search
  archived_at, archive_reason, merged_into_patient_id
  created_at, updated_at, row_version
  CHECK (dob IS NOT NULL OR age_entered_years IS NOT NULL)

-- ── appointment (booking lifecycle) ──────────────────────────
appointment
  patient_id, scheduled_at, scheduled_date (indexed), duration_minutes
  source ENUM('booked','walk_in'), reason
  status ENUM('scheduled','arrived','completed','cancelled','no_show')
  status_changed_at, cancel_reason

-- ── visit (clinical encounter) ───────────────────────────────
visit
  id, visit_code UNIQUE                      -- 'V-2026-000871'
  patient_id, appointment_id UNIQUE
  visit_date (user-settable), started_at, finalized_at
  status ENUM('in_progress','completed','voided')
  temperature, temperature_unit
  bp_systolic, bp_diastolic, pulse_bpm       -- range CHECKs
  vitals_extra jsonb
  complaints_text, examination_text, diagnosis_text, diagnosis_code
  advice_text, follow_up_after_days          -- data only; reminders out of scope
  patient_age_years_at_visit                 -- snapshot for reprints
  amended_at, amendment_note, voided_at, void_reason
  CHECK (status <> 'completed' OR vitals all present)

-- ── prescription ─────────────────────────────────────────────
prescription
  visit_id, version, supersedes_prescription_id
  status ENUM('draft','finalized','superseded','voided')
  finalized_at, rendered_snapshot jsonb, revision_reason
  UNIQUE (visit_id, version)
  UNIQUE (visit_id) WHERE status = 'finalized'

prescription_item
  prescription_id, line_no, medication_id (nullable FK)
  drug_name, strength_text, form
  dosage_text, frequency_code, frequency_text
  duration_value, duration_unit, duration_text
  timing, instructions

-- ── input accelerators [stretch, but see §2.4] ───────────────
medication              -- self-building dictionary + remembered regimen
prescription_template + prescription_template_item
phrase_suggestion       -- complaint / diagnosis / advice corpus

-- ── record integrity ─────────────────────────────────────────
audit_log               -- trigger-populated, before/after jsonb
export_log              -- [stretch] PHI leaves the building; log it
```

### 1.9 Indexes the performance targets imply

Honest framing: **at 10k patients an unindexed scan already meets 2–5 seconds.** The target will not be missed on query planning — it will be missed on N+1 queries, unpaginated history loads, and cold starts. Index anyway, but spend the worry elsewhere.

| Purpose | Index |
|---|---|
| Name search (substring, typo-tolerant) | GIN trigram on `lower(unaccent(full_name))`, or SQLite FTS5 |
| Phone search (prefix) | btree on `phone_digits` |
| Recent patients | btree `(last_visit_at DESC)` partial on active rows |
| Patient history + date filter | btree `(patient_id, visit_date DESC, id DESC)` |
| Daily appointment list | btree `(scheduled_date, scheduled_at)` |

Plus the two denormalized columns — `patient.last_visit_at` and `patient.visit_count`. Without them, the patient list needs a correlated `MAX(visit_date)` per row, which is the classic reason a "fast" search screen takes six seconds.

Search is always `LIMIT 20` + debounce. Never return an unbounded patient list.

---

## 2. Consultation UX

### 2.1 The time budget, decomposed

Every UX decision is scored against this. 2–3 min = 120–180s; design to 150s with slack.

| Step | Target | Dominant cost | Fixable by UX? |
|---|---|---|---|
| List → consultation open | 2s | navigation clicks | yes — one click |
| Vitals (3 values) | 12s | 3 tab stops, unit fumbling | yes — big |
| Complaints (free text) | 25s | typing while talking | partly |
| Diagnosis (free text) | 15s | typing | yes — autocomplete |
| **Prescription (2–3 meds)** | **60–75s** | **4 sub-fields × 3 rows = 12 inputs** | **yes — biggest lever** |
| Review + save + print | 20s | print dialog | partly |
| Slack | ~15s | interruptions | draft/autosave |

**The finding that should drive the whole design:** a UX that shaves 3s off vitals and ignores prescription reuse has optimized the wrong thing.

### 2.2 Screen shape

**Recommendation: a dense single-screen "cockpit" — all four zones visible at once — launched as a full-screen overlay from the daily list, with a preloaded collapsible history rail.**

Options considered and rejected:

| Option | Verdict |
|---|---|
| Stepped wizard (Vitals → Complaints → Dx → Rx → Review) | **Reject.** 4 transitions ≈ 6–10s, and consultations are *nonlinear* — the patient mentions a symptom while you're writing the Rx and you have to walk back two steps. Also destroys review-before-print. |
| Tabbed panes | **Reject.** Hides mandatory vitals behind a tab, so validation points at something invisible. Tab-switching is mouse work in a keyboard-first flow. |
| Single long scrolling page | Acceptable fallback. At 3 meds the Rx block pushes vitals off-screen, so you can't verify before printing. |
| **Cockpit + history rail + overlay launch** | **Recommended.** Zero navigation cost, whole record visible before printing, tab order is a straight line, nonlinear editing free. Needs ≈1280px; degrades to the scrolling page below that. |
| Shorthand command line (`t98.6 bp120/80 p78 \| fever 3d \| URI \| pcm650 1-1-1 3d af`) | Reject as primary — violates "minimal training". **But steal its grammar for individual fields** (BP, frequency). |

**Home screen — the daily appointment list:**

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Dr. A. Sharma Clinic          [ / Search patient or phone…        ]   ⚙  ⏻  │
├──────────────────────────────────────────────────────────────────────────────┤
│  Mon 17 Aug 2026        ‹ ›  [Today]        18 scheduled · 7 done · 1 no-show │
├──────────────────────────────────────────────────────────────────────────────┤
│ 09:30 │ Aarav Gupta         6M  9900112233 │ new patient             │ ✓ Done │
├───────┼─────────────────────────────────────────────────────────────┬────────┤
│ 09:45 │ Meena Joshi        34F  9765412300 │ last 20 Jul · Migraine  │ ▸START │
│ 10:00 │ Priya Nair         28F  9898989898 │ ⟳ draft in progress     │ ▸RESUME│
│ 10:15 │ Iqbal Ahmed        55M  9700011122 │ last 14 Jan · T2DM      │ ▸START │
├───────┴─────────────────────────────────────────────────────────────┴────────┤
│  [+ Walk-in]  [+ Schedule]                              Status: ▾ per-row     │
└──────────────────────────────────────────────────────────────────────────────┘
```

Rules: **one action from list to consultation** (clicking the row body opens it — never row → profile → "New consultation"). **Status transitions are implicit** — `Scheduled → Completed` fires on Save & Print, so the doctor never has to remember to mark someone done; only Cancelled and No-show are manual. **Draft rows are visually distinct** so an interrupted consultation can't be silently lost. The "last visit · diagnosis" column earns its width — it's the recognition cue.

**Consultation cockpit:**

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ ← Meena Joshi · 34F · 9765412300 · P-001042           17 Aug 2026 10:02   ⟳ saved 10:03 │
├────────────────────────────────────────────────────────────┬─────────────────────────────┤
│ VITALS *                                                   │ HISTORY            [Alt+H ⟩]│
│  Temp [ 99.4 ]°F   BP [ 130/80 ]   Pulse [  88 ] /min      │ ┌─────────────────────────┐ │
│  (98.6)(99)(100)      (120/80)(130/80)    (72)(80)(88)     │ │ 20 Jul 26 · Migraine    │ │
│  ↳ chips = last-used + normals; typing always wins         │ │ 98.6 · 118/76 · 74      │ │
├────────────────────────────────────────────────────────────┤ │ Sumatriptan 50 · SOS ×5 │ │
│ COMPLAINTS                          [same as last visit ⤾] │ │ Domperidone 10 · 1-0-1  │ │
│ ┌────────────────────────────────────────────────────────┐ │ │       [⤾ copy Rx][open] │ │
│ │ Throbbing headache × 2 days, photophobia, no vomiting▌ │ │ ├─────────────────────────┤ │
│ └────────────────────────────────────────────────────────┘ │ │ 03 Apr 26 · Viral fever │ │
│ recent: (headache × __ days) (fever with chills) (cough…)  │ │ 101.2 · 120/80 · 96     │ │
├────────────────────────────────────────────────────────────┤ │                  [open] │ │
│ DIAGNOSIS                                                  │ └─────────────────────────┘ │
│ ┌────────────────────────────────────────────────────────┐ │  [ all 11 visits ⟩ ]        │
│ │ Migraine without aura▌                                 │ │                             │
│ └────────────────────────────────────────────────────────┘ │                             │
│ recent: (Migraine w/o aura) (Acute gastritis) (URI) (T2DM) │                             │
├────────────────────────────────────────────────────────────┤                             │
│ PRESCRIPTION          [repeat last Rx ⤾]  [templates ▾]    │                             │
│ ┌──┬──────────────────┬─────────┬─────────┬──────┬───────┐ │                             │
│ │ 1│ Sumatriptan      │ 50 mg   │ SOS     │ 5 d  │ —     │✕│                             │
│ │ 2│ Domperidone      │ 10 mg   │ 1-0-1   │ 5 d  │ B/food│✕│                             │
│ │ 3│ Naprox▌          │         │         │      │       │ │                             │
│ │  │ ▸ Naproxen 250mg — you last used: 250mg, 1-0-1, 5d  │ │                             │
│ └──┴──────────────────┴─────────┴─────────┴──────┴───────┘ │                             │
│ + add medicine (Enter)                                     │                             │
├────────────────────────────────────────────────────────────┴─────────────────────────────┤
│ Advice / notes [ Avoid bright light, review if no relief in 3 days              ]        │
│                                       [ Save draft ]  [ SAVE & PRINT  Ctrl+↵ ]           │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

The tab order is the product's spine: Temp → BP → Pulse → Complaints → Diagnosis → Drug1 → Dose1 → Freq1 → Dur1 → Instr1 → *(Enter)* → Drug2 → … → Advice → Ctrl+Enter. A single unbroken keyboard path from empty to printed, with no modal, no tab click, and no scroll in the common case.

### 2.3 Making mandatory vitals fast, not obstructive

Vitals sit first and topmost, but the mandate fires **at finalize, not at entry**.

1. **Single BP field with auto-format** — one input, one tab stop. `12080`, `120/80`, and `120 80` all parse; stored as two integers. Halves the tab stops for the field doctors dictate as one number. **Highest-value vitals decision.**
2. **Numeric inputs with `inputmode="decimal"`** — no spinners, no per-field unit dropdown. Unit is a fixed label set once in Settings. A unit dropdown on a mandatory field is a per-consultation tax for a once-per-clinic decision.
3. **Quick-chips, never prefilled defaults** — tappable clinical normals plus this patient's last recorded value. **Do not auto-prefill 98.6.** A prefilled vital nobody looked at is a fabricated medical record; the chip requires an explicit act, prefilling requires nothing.
4. **Inline validation, never a blocking modal** — Save with empty vitals turns the field red and moves focus into it. A confirm dialog costs two interactions and trains the doctor to click through warnings.
5. **Plausibility warnings, not errors** — temp `986` or BP `12/80` gets a soft "check this?" that doesn't block. Catches the decimal typo that would otherwise be printed.

> **Open decision, for the doctor, not for us:** what happens when a vital genuinely cannot be taken — broken cuff, screaming toddler, 30-second repeat-prescription visit. A hard mandate with no escape hatch gets satisfied by reflexively typing `120/80`, which is worse than a blank because it's a false clinical record. Proposal: an explicit **"Unable to record — reason"** option that prints as "not recorded". Note also that paediatric plausibility ranges differ sharply (pulse 130 is normal in an infant), so any warning needs age awareness or it cries wolf.

### 2.4 Prescription entry — where the budget is won

Ranked by seconds saved:

1. **★★★ Drug selection auto-fills the doctor's last-used regimen for that drug** — pick "Amoxicillin 500" and dosage/frequency/duration/instructions populate from the last time *this doctor* prescribed it, all editable. Turns a 5-field line into a 1-field line. **Saves ~30–40s on a 3-drug prescription — alone the difference between 3:00 and 2:00.** The dropdown must show the regimen it will apply, so it's never a surprise.
2. **★★★ "Repeat last Rx"** — copies the previous visit's medicine list into the grid, editable, per-row ✕. Refills and chronic meds become one click. Near-zero cost; data is already loaded. Never auto-apply — always require the click, and mark copied rows until touched.
3. **★★ Frequency as a coded token field** — accepts `1-0-1`, `1-1-1`, `SOS`, `STAT`, `BD`, `TDS`, `HS`, with chips for the top 5 and free text as fallback.
4. **★★ Common-drug shortlist** — the empty drug field shows the doctor's top 15 by usage before any typing. Zero-keystroke selection; also the tablet path.
5. **★★ Named prescription templates** ("URI 5-day", "Gastritis") — collapses a 3-drug prescription from ~50s to ~5s. `[stretch]`; risk is stale templates applied without reading, so mark applied rows as needing review.
6. **★ Row grid with Enter-to-add-row** — never a "+ Add medicine" modal. Modals for repeated entry are the classic 3× time multiplier.
7. **★ Duration = number + unit chip** (3/5/7 quick chips); **instructions as chips + free text** (After food / Before food / Bedtime / Empty stomach).

With 1+2+3: **20–35s for three medicines**, versus 90s+ for a naive form.

> **Known decay problem:** with no formulary, the self-built corpus accumulates `Paracetamol` / `Paracetmol` / `PCM` / `Calpol` as four entries with four remembered regimens. Autocomplete quality degrades over months. Needs either a merge/rename tool in settings, fuzzy matching on suggest, or a seeded starter list of ~200 common drugs.

### 2.5 History alongside, without slowing anything

- **One fetch, at consultation open.** The last 3 visits ship in the same payload as the patient record. Expanding a card must never hit the network — this is what protects the 2-second requirement.
- **Collapsed to one line per visit**: `date · diagnosis · vitals triple · med names`. Enough to answer "what did I do last time?" without opening anything.
- **Actions live in the history**, not just display: `⤾ copy Rx` and `⤾ copy complaints` per card. This is what makes history a *speed* feature rather than a reading feature.
- Full history with date filters is a separate route — it's for the occasional deep look, not the live consultation.

### 2.6 Printing

**Browser print (`window.print()` + `@media print`) for the daily prescription; server-side PDF for the export requirement and the archival copy.** Building the daily prescription as a server PDF adds 1–3s per patient plus a download-then-open step for zero benefit.

```
┌───────────────────────── A5 portrait ─────────────────────────┐
│  Dr. A. Sharma, MBBS, MD (Gen. Med.)          Reg. No. 12345  │  ← header (suppressible)
│  Sharma Clinic · 14 MG Road, Pune 411001 · 020-2555 1234      │
├───────────────────────────────────────────────────────────────┤
│  Meena Joshi          34 / F          P-001042                │
│  9765412300                           17 Aug 2026             │
├───────────────────────────────────────────────────────────────┤
│  Temp 99.4 °F    BP 130/80 mmHg    Pulse 88 /min              │
├───────────────────────────────────────────────────────────────┤
│  Complaints : Throbbing headache × 2 days, photophobia        │
│  Diagnosis  : Migraine without aura                           │
├───────────────────────────────────────────────────────────────┤
│  ℞                                                             │
│   1. Sumatriptan 50 mg        SOS            5 days           │
│   2. Domperidone 10 mg        1-0-1          5 days           │
│                               Before food                     │
├───────────────────────────────────────────────────────────────┤
│  Advice: Avoid bright light. Review if no relief in 3 days.   │
│                                        ______________________ │
│                                        Dr. A. Sharma          │
│  Rx #2026-1042-07 · printed 17 Aug 2026 10:04                 │  ← traceability
└───────────────────────────────────────────────────────────────┘
```

- **Default A5 portrait** (standard Rx pad size), A4 configurable. This must be a setting, not a guess — printers default to A4 and a mismatch produces a quarter-page prescription, which is exactly the "smooth printing" failure the BRD wants to avoid.
- **Pre-printed letterhead toggle** `[stretch, ~2 lines of CSS]` — suppresses the header/footer blocks and reserves margin instead. Many clinics already have stationery; without this, the doctor gets a double header and quietly goes back to the pad.
- `@page { size: A5 portrait; margin: 10mm }`, `break-inside: avoid` per medication row, `<thead>` repeat for multi-page, ≈25mm of real whitespace for a pen signature.
- **Reprint = byte-identical, no watermark** — a patient who lost their copy needs a usable prescription, and a "DUPLICATE" stamp can cause a pharmacist to refuse it. But log every print event. **Amendment is different from reprint**: it creates a new version with its own Rx number and a visible "Revised — supersedes #…07" line.
- **Test with a real printer, not print preview.** Driver-level "fit to page" scaling is the usual cause of a prescription shifted 4mm off letterhead.

### 2.7 Drafts, sessions, and input mode

- **The visit is created as `DRAFT` the instant the consultation opens**, not on first save — so an interruption at any point has something to attach to.
- **Debounced autosave every ~3s of idle** plus on field blur. A quiet `⟳ saved 10:03` in the header — not a toast, not a spinner.
- **Session timeout must not eat a draft.** If auth expires mid-consultation, show an inline re-login *over the intact form* and resume. On a shared clinic desktop a screen lock will happen mid-consultation regularly. Once drafts are durable, a short timeout is free; without that, the doctor will demand you disable it.
- **Keyboard-first, mouse-complete, chip-parallel** — every accelerator exists in both forms, so nobody has to learn shortcuts and nobody who knows them is slowed down. `/` search · `Enter` add row · `Ctrl+Enter` Save & Print · `Alt+H` history · `Esc` back.
- **Tablet:** design responsive to ~1024px landscape (history rail collapses); **don't build a phone layout** — free-text consultation entry on a phone keyboard cannot meet the target. The chip-heavy design is what makes tablet viable, since it converts typing into tapping.

### 2.8 Search

- **Global, always-present, `/` to focus**, ~200ms debounce, search-as-you-type. The 2–5s requirement is met by *not having a submit step*.
- **One field searches name and phone.** Match phone as a substring (doctors remember the last 4 digits); match name on any word, not just prefix.
- **Result rows carry age/gender/phone/last-visit** — this is the fix for the duplicate-name problem, and it turns shared family phone numbers from a bug into a feature: phone search returns the family, which is the fastest way to find "the little girl who came with her mother last Tuesday."
- **Empty query shows recent patients** — satisfies the BRD's "view recent patients" with no extra screen.
- **Profile and visit list are one page.** The BRD's "easy navigation between patient profile and visits" is best satisfied by *there being no navigation*.

> **The real search requirement is not 2–5 seconds** — that's trivially met at this data volume and therefore measures nothing. It's that **the doctor never fails to find a patient he knows exists.** One failed search is what kills trust, and trust does not recover cheaply.

---

## 3. Technical architecture

### 3.1 Sizing — which invalidates most performance worry

20–40 patients/day × ~250 days ≈ 5–10k visits/year. After 10 years: ~100k visits, ~15k patients, a few hundred MB of mostly text. **One concurrent user.** Peak write rate ~0.1/sec. This dataset fits in RAM on a phone.

**No stack under consideration will fail on performance.** So the selection criteria are build effort, operational burden in year 3, and who gets called when it breaks.

### 3.2 Stack options

| Option | Build (1 dev) | Hosting $/mo | Ops burden | Team fit | Main risk |
|---|---|---|---|---|---|
| **1. ASP.NET Core Razor Pages + htmx + SQLite** | **5–7 wk** | **~$1** (on-prem) | **Low** — one process, one file | Good | Team resistance to "no SPA" |
| 2. .NET API + Angular + Postgres | 9–13 wk | $28–40 | Med — 2 pipelines | **Best** | 2× effort; bundle weight vs <2s |
| 3. Blazor Server + Postgres | 6–8 wk | $28–40 | Med | Good | **Circuit drop = frozen consultation** |
| 4. Next.js + Postgres (PaaS) | 5–7 wk | $20–40 | Low | Poor | Maintenance island in a .NET shop |
| 5. Django/FastAPI + HTMX | 5–6 wk | $8–15 | Low | Poor | No Python bench |
| 6. Low-code (Budibase/Retool) | 2–3 wk to 70% | $0–50 | Med | N/A | Dies on the two things that matter |
| 7. Off-the-shelf EMR (OpenEMR/SaaS) | 0 build | $20–60 | Low | N/A | Fails usability + training criteria |

Notes on the rejections: **Blazor Server is disqualifying** — the UI is a live socket, so a flaky Wi-Fi drop mid-consultation freezes the screen and can lose an unsaved form. **Low-code** collapses on exactly the two things that matter here: a keyboard-fast consultation screen and a pixel-controlled printable prescription. **Off-the-shelf EMR** should be raised once with the stakeholder and then dropped — OpenEMR is a hospital-grade UI that fails "2–3 minute consultation" and "minimal training" outright.

**Recommendation: option 1, with option 2 as the fallback if the team hard-refuses.** They share the entire backend, so an API can be bolted on later if a receptionist or mobile phase arrives.

### 3.3 Hosting — and the internet-drop answer

> **"Web-based" in this BRD means "runs in a browser." It does not say "reachable from the internet."** That ambiguity is the single most consequential unanswered question in the document, because it decides the whole architecture.

**Recommendation: on-premise** — a ~$200 mini-PC in the clinic (or the doctor's own consulting-room PC, accepting the single point of failure), served over the LAN, with continuous encrypted replication offsite.

"Offline is out of scope" is a statement about the **application**, not the **system**. Three system-level answers, all of which you should take:

1. **Remove the internet from the critical path** — host locally. Cost: $0–200 one-time. This is the actual answer.
2. **Add a second path** — a 4G/5G failover router, ~$10–15/mo, 20 minutes to configure. Buys availability far cheaper than any code.
3. **Plan the paper fallback, because even on-prem the box can die** — keep blank pads, and make `visit_date` settable to a past date with an "entered late" flag so yesterday's paper notes key in at ~30 seconds each. This is the only mitigation covering *every* failure mode, and it's ~1 day of work.

Building offline sync would cost 4–8 weeks and a permanent stream of conflict bugs. The BRD is right to exclude it — these three are why it can afford to.

**Switch to cloud** (Azure App Service B1 + Azure PostgreSQL Flexible, ~$28–40/mo, plus a 4G failover router) **if and only if** the doctor routinely works from more than one location, or the stakeholder refuses hardware in the clinic. The application code is identical either way, which is precisely why this stack is the low-regret choice while the question is unresolved.

### 3.4 Database

| Option | Verdict |
|---|---|
| **SQLite (WAL + FTS5)** | **Pick for on-prem.** One file, zero admin, no service to patch. Backup = replicate the file. Sub-millisecond name search. WAL mode handles a few concurrent users, so a Phase-2 receptionist doesn't force a rewrite. Needs a *durable* disk — never ephemeral PaaS storage. |
| **PostgreSQL** | **Pick for cloud.** Same EF Core code, real backup tooling with PITR, `pg_trgm` for typo tolerance. Cost: a service to run, patch, and monitor. |
| SQL Server Express | Works; 10GB cap is ~20× headroom. Best-understood in a .NET shop, but 1–2GB idle RAM is heavy for a $200 box. |
| MongoDB / document store | **Wrong.** The domain is emphatically relational and every requirement is a relational query. |

Do not over-trust EF Core provider portability — full-text search, date functions, and collation all differ. **Confine provider-specific SQL to a single `PatientSearchRepository`** and the swap stays a one-day job.

### 3.5 Auth for exactly one user

Do not build a user-management system. One row, one password hash, no roles, no registration screen.

- **Argon2id** (19 MiB, t=2, p=1) via `Konscious.Security.Cryptography`. ASP.NET Core Identity is far more machinery than one user justifies.
- Cookie: `HttpOnly`, `Secure`, `SameSite=Strict`, no persistent "remember me" beyond one clinic day.
- **`Cache-Control: no-store` on every page containing PHI** — otherwise the back button after logout re-renders a patient record on a shared desktop.
- **`autocomplete="off"` on patient search**, so names don't accumulate in browser autofill on a shared machine.
- **Two-tier session:** idle 10 min → a **soft lock overlay** cleared by PIN or Windows Hello, not full password re-entry. Absolute lifetime 12 hours (one clinic day). Plus a prominent **Lock** button, since the real case is "stepping out", and idle timeouts always fire too late.
- **Second factor:** skip on LAN-only (the threat is someone physically in the room); near-mandatory if internet-exposed. **WebAuthn / Windows Hello** `[stretch]` is the best fit — login becomes a fingerprint touch, faster than a password, and directly serves "minimal training".

**Password reset with no admin and no email:** **printed one-time recovery codes** generated at setup, kept in the clinic safe alongside the backup encryption key. Zero infrastructure, ~2 hours. Plus a local `reset-password` CLI as the developer-assisted backstop. Email/SMS OTP is a recurring dependency and a phishing surface to serve one user roughly once every three years — skip it.

### 3.6 Encryption

**In transit**

| Shape | Mechanism |
|---|---|
| Cloud (App Service) | Managed certificate, TLS 1.2+, auto-renew — free |
| Cloud (VPS) | Caddy + automatic Let's Encrypt, HSTS, 80→443 redirect |
| App on the doctor's PC | **Loopback — nothing on the wire.** Browsers treat `localhost` as a secure context, so `Secure` cookies still work |
| Separate LAN box | `mkcert` local CA trusted once on the clinic PC, **or** a real Let's Encrypt cert via the **DNS-01 challenge** with an A record pointing at the private IP — this works, auto-renews, and needs no inbound internet |

That last trick is the one most people miss: you can hold a publicly-trusted certificate for a machine with no public IP.

**At rest**

- On-prem Windows: **BitLocker with TPM** on the data drive. This is the single most important on-prem control, because the realistic threat is the PC being stolen from the clinic.
- Cloud VPS: **many cheap providers don't encrypt volumes by default.** Verify; LUKS the data volume yourself if not.
- SQLCipher: honest assessment — on an unattended box the passphrase must live where the app can read it at boot, i.e. next to the data. BitLocker + a locked room beats that. *Unless* the doctor types a passphrase each morning, which is genuinely viable for a once-a-day single-user clinic.
- **Column-level PHI encryption: recommend against for Phase 1.** It breaks search and CSV export and creates key management with no admin to manage keys.

> **A genuine conflict the BRD doesn't notice:** "encryption at rest" and the 2–5 second search target collide. Column-level encryption on `full_name`/`phone` destroys the trigram and prefix indexes that search relies on. Interpret the requirement as disk/tablespace/managed-DB encryption — or accept slow search.

### 3.7 Backup and restore

**"No data loss" is not achievable literally and is not testable.** Replace it with two numbers the doctor signs off on:

- **RPO ≤ 5 minutes** — worst case you lose one consultation, which the doctor can re-key from the printed prescription.
- **RTO ≤ 4 hours** — running again within the same clinic day.

**Mechanism: SQLite + Litestream**, streaming the WAL continuously to S3/Backblaze B2. RPO in seconds, restore is a single command, ~$0.50/mo, ~2 hours to set up. This is an extraordinarily good fit and an independent argument for SQLite. (Cloud equivalent: managed Postgres PITR, which is a checkbox.)

- **3-2-1, concretely:** live DB on the box → nightly encrypted dump to a second local disk → continuous replication to cloud object storage.
- **Object Lock / versioning on the bucket.** The realistic ransomware failure mode is a compromised box deleting its own backups. Immutable retention is the only counter. Free, one setting.
- **Encrypt before upload** (age/restic/gpg) — not merely provider-side encryption with provider-held keys. Store the key printed, in the clinic safe, next to the auth recovery codes.
- **Soft delete everything.** Backups don't protect against the doctor deleting a patient and not noticing for a week.
- **Retention:** 7 daily / 4 weekly / 12 monthly, then yearly. *How long the yearly tier persists is a legal question, not a technical one* — outpatient record retention varies by jurisdiction. Ask.

**The two highest-value items in this section:**

1. **An automated monthly restore test** — pull the latest backup into a scratch container, apply migrations, assert row counts non-zero, most recent visit within 48h, a known seeded record readable. Report pass/fail somewhere a human reads. ~1 day. **An untested backup is not a backup.**
2. **A "last successful backup: 3 minutes ago" line in the UI footer.** It converts an invisible system into a visible one for a non-technical owner, and it's the only way the doctor will ever notice that replication silently stopped two months ago.

**Who restores?** This is the genuine gap and has no clever technical answer — there is no IT staff. Pick explicitly: a named developer/vendor with a written runbook and a support arrangement, or a restore that is literally one command on a card taped to the machine. Build the one-command script either way; it costs half a day and it also powers the test above.

### 3.8 Where the performance risk actually is

Ranked:

1. **Cold start — the #1 realistic breach.** Free/shared cloud tiers idle out after ~20 min; the first request costs 5–30s. **The doctor's first patient of the day hits it, every day,** and forms their entire opinion of the product. Fix with an always-on tier or a locally-running process. This is a plan/tier decision, not a code decision.
2. **Frontend bundle weight.** A default Angular app ships 300KB–1MB+ before first paint; on a clinic's 5 Mbps line that's 1–3s of blank screen. Server-rendered HTML at 30–60KB paints in ~200ms. **The strongest technical argument for Razor+htmx over the SPA.**
3. **Region latency** — host in-country, which is also the data-residency answer.
4. **PDF generation** — headless-Chromium rendering is 300ms–2s and CPU-spiky; on a 1-vCPU box it can stall other requests.
5. **N+1 in visit history** — trivial to avoid with `.Include()`, extremely easy to hit with lazy loading.

Make it measurable: emit a `Server-Timing` header and log it, and add a CI smoke test asserting search p95 < 500ms against 20k patients / 100k visits seeded. That turns the BRD's fuzzy number into a gate.

### 3.9 PDF, CSV, and where PHI leaks

**PDF:** browser print for the daily prescription (§2.6) + **QuestPDF** server-side for exports and the archival copy. This avoids headless Chromium entirely — 300MB of dependency whose classic failure is **missing fonts**, which bites specifically on non-Latin patient names. Avoid iText (AGPL), wkhtmltopdf (unmaintained), and `html2canvas` (produces a *raster* PDF — fuzzy, unsearchable, unacceptable for a medical document).

**CSV:**
- **UTF-8 with a BOM (`﻿`).** Without it, Excel on Windows mangles every non-ASCII name. One line of code; the most common CSV bug there is.
- **CSV injection is a real vulnerability here.** Complaints and diagnosis are free text; any cell starting with `= + - @` executes as a formula when opened in Excel. Prefix with `'`. One function.
- **Shape — the BRD doesn't decide this.** Medications nest inside visits and CSV is flat. Recommend `patients.csv` + `visits.csv` (vitals flattened to columns, medicines `;`-joined) + optionally `medications.csv` keyed by visit. **This requires stable exportable business keys** — `patient_code`, `visit_code` — so the files can be rejoined in Excel. Exporting bare surrogate PKs makes the export useless to a human.
- Excel also mangles phone numbers (leading zeros stripped, `+91` eaten) and dates. Quote and prefix.

**Where PHI actually leaks, in order:**

1. **Logs.** Default request logging captures query strings, so `/search?q=Ramesh+Kumar` lands in web logs, the cloud provider's log store, and any APM. Rules: search terms in POST bodies not query strings; log `patient_id` integers, never names or diagnoses; redact at the logger, not at every call site; 30-day retention. **EF Core's `EnableSensitiveDataLogging` writes parameter values — patient names and diagnoses — into your logs. Never enable it outside local.**
2. **Exports.** A full-database CSV in the `Downloads` folder is a complete unencrypted health extract that stays there forever, and it directly contradicts the encryption-at-rest requirement. No default "export everything" button; log every export (timestamp, type, filter, row count); a one-line confirmation dialog — **friction is good here**, unlike on the consultation path.
3. **Backups** — see §3.7.
4. **Every third party** is a new place PHI can land. **Ship zero analytics**: there is nothing to learn from one user's clickstream that you can't learn by asking them, and it's a free privacy win.
5. **Browser-side** — autofill accumulation and back-button caching on the shared desktop.
6. **Data residency** — if cloud-hosted, host in the clinic's country. India's DPDP Act 2023 classifies health data as sensitive; in-country hosting sidesteps the argument. Flag as a question; don't assert a legal conclusion.

### 3.10 Testing and environments

**Two environments, not four:** `local` (seeded fake data) and `production`. A permanent staging environment for a one-doctor app is waste — *unless* you need somewhere to rehearse restores, which you do. So make staging **ephemeral and identical to the restore test**: spun up monthly from the latest real backup, asserted against, destroyed. **Staging *is* the restore drill** — one piece of infrastructure, two jobs.

**Never put real patient data on a dev machine.** Seed with Bogus.

| Layer | Count | Where the value is |
|---|---|---|
| Unit | 60–100 | BP string parsing, age-from-DOB across leap years, phone normalization, dosage formatting, CSV escaping + injection guard, date-range filters |
| Integration | ~20 | Against a real DB; especially the search repository, the one place provider-specific SQL lives |
| E2E (Playwright) | 4–6 | Login → find → consult → print; register patient; export CSV; history + date filter |
| Performance smoke | 1 | search p95 < 500ms at 20k/100k rows |
| **Monthly restore test** | 1 | The most important "test" in the project |

**Migrations:** EF Core, applied on startup — *only* safe because there is exactly one instance; note that assumption in the code. Always take an automatic pre-migration backup; that is your rollback.

**Error monitoring is not optional**, because the doctor will not file a ticket — they will go back to paper and you'll find out in three weeks. Serilog to a rotating file plus an uptime ping is the minimum. Sentry's free tier works but **must be configured to scrub**, since it captures local variables and full URLs by default.

---

## 4. Requirement gaps — asked for, not buildable as written

| # | Gap | Decision it forces |
|---|---|---|
| 1 | **"Printable prescription with basic header/footer"** — undefined: clinic details, doctor's degrees, **medical registration number** (legally expected in most jurisdictions), logo, signature (blank space vs. image), A4 vs A5, and **whether the doctor already has pre-printed letterhead**. Also: *there is no Clinic Settings feature anywhere in the BRD*, yet printing depends on one. | Build a Clinic Profile screen + a print template with a letterhead toggle and configurable top margin. |
| 2 | **Browser print vs. server PDF** | Pick before writing the print path; retrofitting is a rewrite of the whole output layer. |
| 3 | **"Basic search"** — prefix vs substring, last-4-of-phone, typo/transliteration tolerance (Sanjay/Sanjai, Mohd/Mohammad), result ordering, whether visits/diagnoses are searchable | See §2.8. The stated 2–5s criterion measures nothing; **findability** is the real requirement. |
| 4 | **"Export patient or visit data"** — which entity, which columns, what scope, who consumes it. This feature is silently doing three unrelated jobs: referral letter, personal backup, and vendor-lock-in escape hatch | Name the job, then design. See §3.9. |
| 5 | **What a "visit" is with no appointment** | Highest-cost undefined item. See §1.1. |
| 6 | **Vitals units, formats, and escape hatch** — plus **SpO2 and weight are absent**, and weight is not optional for paediatric dosing | See §2.3. Expect a request in week 1. |
| 7 | **Medication entry with no formulary** — is dosage/frequency free text or coded? | See §2.4. |
| 8 | **"Age / DOB"** written as if interchangeable | See §1.5. Getting this wrong is silent data corruption, not a visible bug. |
| 9 | **Patient identity keys** — is phone required? unique? is there a printed patient ID? | See §1.5. |
| 10 | **Amendment / immutability of a printed prescription** — *not mentioned anywhere in the BRD* | See §1.4/§1.7. Medico-legally significant and an architectural fork. |
| 11 | **"No data loss" + "regular automated backups"** — no RPO, retention, destination, named restorer, or restore test. As written this is a sentence, not a requirement | See §3.7. |
| 12 | **Auth specifics** — reset with no second user and possibly no email; timeout on an unattended desktop vs. speed | See §3.5. |
| 13 | **Hosting model** — *not in the BRD at all*, and upstream of backup, outage, encryption, updates, cost, and support | See §3.3. **Question #1.** |
| 14 | **No allergies, chronic conditions, or long-term medications field** | A prescribing system with no allergy field is a patient-safety gap. A persistent banner on the consultation screen is a few hours' work. |
| 15 | **Appointment semantics** — slot length, double-booking, time vs. token order, and what happens to yesterday's still-"Scheduled" rows (they need auto-lapse or the list rots) | Decide before building the daily list. |

---

## 5. Edge cases

### Clinical workflow
- **Walk-in, no appointment** — Visit is root, Appointment optional. Never require an appointment to consult.
- **Second visit the same day** — morning consult, sent for an X-ray, returns at 6pm. A `unique(patient, date)` constraint is a natural-looking mistake that breaks this permanently. Allow N/day; prompt "continue this morning's visit" or "start a new one" — both are legitimate and the doctor should choose.
- **Consultation interrupted** — emergency call, tab closed, power cut. Autosaved drafts + a "Resume in-progress consultation" strip. Without this, one interruption loses the record and the doctor writes on paper "just in case" from then on.
- **Amending after printing** — patient calls at 8pm, the antibiotic causes a rash. v2 with reason; original preserved.
- **Reprint weeks later** — must render *identically*. If the clinic address or signature image changed since, a naive template reprints today's header on an old prescription. Hence the stored snapshot.
- **Patient with no phone** — very common for elderly patients, labourers, and children. Phone cannot be required or unique. Also silently blocks any future SMS follow-up for that patient.
- **Minors sharing a guardian's phone** — phone maps to N patients; search must return a chooser. Add a guardian/relation field `[stretch, ~2h]`.
- **Patient name changes** — keep previous names searchable; printed prescriptions retain the old name; never mutate history rows.
- **Repeat prescription** — the monthly chronic hypertensive. Turns a 3-minute record into 30 seconds. Not in the BRD, and arguably the highest-leverage feature for the success criterion.
- **Batch/after-hours entry** — some doctors won't type in front of a patient. If records get entered after clinic hours, "today's list" and backdating semantics both change.
- **Deceased / inactive patient** — needs a status, or recent-patients and any future reminder misbehaves.
- **Sick-leave / fitness certificate** — GPs are asked constantly; expect the request the moment printing works.
- **Multi-page prescription** — 8 medicines overflow A5. Page breaks, "Page 1 of 2", header repeat.

### Data
- **Duplicate patients** — inevitable within weeks ("Ramesh K" / "Ramesh Kumar" / "R. Kumar"). Three mechanisms; only the first belongs in MVP: **prevent** (search-before-create inside the Add Patient form), **detect** (a duplicates report), **merge** (Phase 1.5). The MVP constraint this imposes is simply: never use phone or name as a key, and never hard-delete.
- **Wrong patient selected mid-consultation** — for a *draft*, offer "move to another patient". For a *finalized* one, require void-with-reason + re-entry, never silent reassignment, and make the void visible in both patients' histories.
- **Date boundaries for "today"** — server vs browser timezone, evening clinics past midnight, a visit saved at 00:10 belonging to the previous clinic day, backdated entry, a clinic PC with a wrong clock. Store `timestamptz` **plus a derived `clinic_date`** used by all daily lists, and define the clinic day boundary explicitly (e.g. 04:00–04:00) rather than letting midnight decide.
- **Two tabs on the same visit** — last-write-wins silently overwrites. Cheap fix: a version column and a conflict warning.
- **Unbounded free text** — a 5,000-character complaint destroys the print layout.
- **PDF font embedding** for non-Latin scripts — silent tofu boxes on the printed prescription. Test with real names on day one.
- **Decimal/locale parsing** on temperature (`38,5` vs `38.5`).

### Operational

| Risk | Why it's real here | Cheap mitigation |
|---|---|---|
| Single machine, single user | Desktop dies → the clinic stops and records are unreachable. No second user to work around it. | Any-browser access; a known-working spare device; no dependence on one machine's local state. |
| Untested backups | Silent failure is the norm. | "Last backup succeeded 04:00 today" in the UI; quarterly restore drill. |
| Forgotten password | Single user, likely no verified email, no admin → total lockout. | Printed recovery codes + a documented reset path. Decide *before* launch, not during. |
| Internet outage in clinic hours | Offline is out of scope, so a cloud-hosted app means a dead clinic at 11am Tuesday. **Sharpest scope/reliability conflict in the BRD.** | Local hosting, 4G failover, paper fallback (§3.3). |
| Browser printer setup | Wrong paper size, injected headers, margins, no silent printing from a browser, Chrome updates shifting CSS. | One-time print setup checklist + a permanent "Test print" page. Verify on the real printer and paper before go-live. |
| **No on-site support** | Who fixes it at 10:05am Monday with eight patients waiting? | Name the person and the channel before launch. This is an adoption risk wearing an ops costume. |

---

## 6. Adoption — the risk nobody budgets for

**Paper is currently faster.** A prescription pad is 15 seconds and never crashes. The 2–3 minute target is measured against a workflow the doctor has done 50,000 times. The competition is muscle memory, and the bottleneck is likely typing speed, not software.

**What makes this fail in week 3**, when the novelty is gone:

1. **The 40-patient day.** The doctor falls behind, reverts to paper "just for the rush", and never comes back. → The app must survive being *skipped*: allow fast retro-entry, never punish a gap.
2. **Half-entered records.** If 30% of visits are incomplete, history becomes untrustworthy — and an untrustworthy history means the paper file stays, which makes the app pure overhead. → **Completeness beats richness.** Fewer required fields, more finished records.
3. **One failed search.** He knows the patient exists, types the name, gets nothing.
4. **The vitals mandate feeling like a tax** on a 20-second repeat-prescription visit.
5. **A printer jam on day two** with no one to call.
6. **Nobody sat with him for the first two clinics.** "Minimal training required" is a design goal, not a launch plan.

**The empty-database problem:** history, search, and recent-patients — three headline features — do nothing until ~3 months of visits accrue.

- **Floor (build regardless):** Add Patient must be genuinely <10 seconds — name, phone, age, gender, everything else optional.
- **Highest-ROI item not in the BRD:** a one-off **CSV importer** (~1 day). Most clinics have *something* — an Excel sheet, an old billing export, a phone contact list. It transforms week one from "everyone is new" to "he found his patients."
- **Zero-dev alternative:** an assistant types the last 200 patients' names and phones from the register over a weekend.
- **Transition plan:** parallel-run for two weeks; the printed Rx becomes the doctor's retained copy, replacing the carbon pad.

**Measure adoption, because the BRD's criteria aren't measurable.** One number — **% of the day's patients with a completed visit record** — tells you within two weeks whether this succeeded. Nothing else does. ~2 hours to build.

---

## 7. Internal contradictions in the BRD

1. **Single user + mandatory vitals.** In real clinics a nurse or assistant records vitals. Excluding receptionist access puts BP/temperature/pulse on the doctor's own critical path for every patient — the exact path that must fit in 2–3 minutes. **The exclusion of a second user directly undermines the primary success criterion.** Sharpest contradiction in the document.
2. **"No data loss" vs. single-clinic, minimal-cost, single-machine scope.** Reliability of that grade needs infrastructure the scope implies won't be funded. State a real RPO or drop the absolute.
3. **"Offline out of scope" vs. reliability.** Both statements are in the BRD; only one can hold. See §3.3.
4. **"Follow-up reminders out of scope" vs. the workflow itself.** Doctors write "review after 5 days" on most prescriptions. The instruction gets *printed* while nothing records or surfaces it. → Capture `follow_up_after_days` as structured data in Phase 1 with no notifications; the door stays open for ~1 hour of work.
5. **"80% reduction in paper usage" vs. printing a prescription for every patient.** Printing is paper. The reduction is in registers and case files, not prescriptions. → Reframe as "no paper case files maintained."
6. **CSV/PDF export vs. "encryption at rest."** Export is the designed leak path out of the encrypted store onto a shared desktop, unencrypted.
7. **"Search by name or phone" vs. patients with no phone and families sharing one.** The stated search keys don't cover the stated patient population.
8. **"Minimal training" vs. structured mandatory fields.** Structure *is* the training cost. Manageable — but it means the UI must teach itself.
9. **Printing requires a clinic profile that no requirement creates.** A missing feature implied by an existing one.
10. **The performance criteria are the wrong ones.** 2–5s search is generous to the point of meaninglessness at this volume, while what actually matters — keystroke-to-suggestion latency and a keyboard-only consultation path — is unstated.

---

## 8. Blocking questions, ranked by rework cost

| # | Question | If the assumption is wrong, you rebuild |
|---|---|---|
| **1** | **Cloud/VPS, or a machine in the clinic?** | Backup design, outage strategy, encryption, updates, support model, cost — everything non-functional. |
| **2** | **Is Visit the root record, with Appointment optional?** | Schema + home screen + history + export + every walk-in flow. |
| **3** | **Will anyone but the doctor ever touch the machine (registration, vitals)?** | Auth layer, every table's audit columns, screen decomposition — plus an unbackfillable data gap. |
| **4** | **Can a printed prescription be edited, or only amended (v2 + reason)?** | Prescription storage, reprint fidelity, medico-legal defensibility. Retrofitting immutability is a rewrite. |
| **5** | **DOB, age-at-registration, or both?** | Every patient row, every history display, every reprint — silent corruption, not a visible bug. |
| **6** | **Print stack: server PDF or browser print? Pre-printed letterhead? A4 or A5?** | The entire output layer, plus later PDF-sharing and reprint features. |
| 7 | Medicines: pure free text, or autocomplete + coded frequency? | Prescription schema, the 2–3 min target, any future reporting or safety checks. |
| 8 | Is phone required? Unique? Is there a printed patient ID? | Identity model, search UX, duplicate/merge strategy. |
| 9 | Backup destination, frequency, retention, restorer — and has a restore been tested? | Nothing structural; but it's the difference between "no data loss" being true and being a sentence. |
| 10 | Does a digital list of existing patients exist to import? | Week-1 value and the whole adoption curve. |
| 11 | What happens when a mandatory vital genuinely cannot be measured? | Data quality of the most-enforced field in the product. |
| 12 | °C or °F — fixed per clinic or per reading? | Schema, UI, and every historical reading if changed later. |
| 13 | Clinic day boundary, timezone, and is backdated entry allowed? | Daily lists, filters, exports, every "today" query. |
| 14 | What language/script are patient names and the prescription in? | Font embedding, PDF pipeline, search collation. Fails visibly and late. |
| 15 | Password recovery path and session timeout policy? | A launch-blocking lockout, discovered at the worst moment. |
| 16 | File attachments (photographed lab reports) — coming or not? | Storage, backup volume, PDF export. Architecturally intrusive to add later. |
| 17 | Which browser is the actual daily driver — is there really a Mac/Safari? | Print testing scope. The BRD lists Safari; nobody has confirmed a Mac exists. |
| 18 | Who supports this at 10am on a Monday? | Not code — but the most common cause of a working app being abandoned. |

---

## 9. Phasing

The BRD's Phase 1 is roughly 1.5× what a first release should be. **The cut is appointments and export — not the consultation loop.**

### MVP — "replace the pad" (~4 weeks)
Patient quick-add → search → **Visit** (vitals, complaints, diagnosis, medications) → **print** → chronological history.
Plus: draft autosave, clinic profile / print settings, backups running with visible status, search-before-create duplicate prevention.

**Deliberately excluded** (all are BRD Phase 1 scope): appointment scheduling, CSV export, history date filters, merge, amendment versioning UI.

*Why:* a solo GP's real workflow is a walk-in queue. The daily-appointment list is the least-used headline feature and export is a month-3 need. Every day spent on them is a day not spent making the consultation loop faster than paper — which is the only thing determining whether this product survives.

> **Note the tension:** §2.2 designs the appointment list as the home screen. If appointments are cut from MVP, that screen becomes "today's visits" — same layout, same one-click-to-consultation rule, no scheduling. The UX survives the cut intact; build it that way deliberately rather than discovering it later.

### Phase 1.5 — "complete the BRD" (~3–4 weeks)
Appointments + daily list + statuses; CSV/PDF export with the contract finally defined; **repeat-prescription / copy-last-Rx**; drug autocomplete from own history; duplicate detection + merge; prescription amendment with versioning; history date filters; **a tested restore drill**; the adoption metric.

### Phase 2 — "what they'll ask for" (scoped after 4–6 weeks of real use)
Receptionist role + permissions; follow-up list and reminders; fee capture; attachments; certificates; simple reports; mobile-friendly viewing.
*Order this by what the doctor actually asks for in week 4, not by this list.*

### Cut order if time is short
1. CSV export (print/PDF stays — that's the one replacing paper)
2. Appointment scheduling (keep a today's-visits list; walk-ins dominate)
3. History date filters (reverse-chronological is enough at low volume)
4. Patient profile editing beyond name/phone/age
5. Recent-patients panel (search covers it, if search is good)

**Never cut, at any deadline:** search quality, printing, draft autosave, backups + one verified restore, and the append-only stance on finalized records. Those cost 10× to add later or destroy trust when missing.

### Doors to keep open cheaply (~2–3 days total, now)

All are `[out of scope]` for Phase 1 — these are architecture hooks, not features.

| Demanded by | Item | Hook |
|---|---|---|
| Week 1–3 | **Receptionist access** — the doctor *will* hand the machine to staff whatever the BRD says, and "single user auth" instantly becomes a shared password | `created_by`/`updated_by` on every write from day one; a real users table (not a hardcoded credential); role checks behind one function that currently returns "doctor"; **build Registration and Vitals as their own screens** so they can be permissioned later without splitting the consultation UI |
| Week 1 | **Fee capture** — money is why clinics buy software | One nullable `amount` + `payment_mode` on the visit, and a day-total on the daily list. ~90% of what a solo clinic wants. **Be disciplined:** invoicing, tax, receipts, and refunds are a product of their own and the strongest scope-creep magnet on this list. One number, no invoice. |
| Week 2–4 | **Follow-up reminders** | `follow_up_after_days` on the visit, printed on the Rx, plus a plain "Due for follow-up" list |
| Week 2 | **Sharing the Rx via WhatsApp/email** | Generate a real PDF file rather than relying solely on `window.print()` |
| Month 1–2 | **Lab report attachments** | Decide blob storage location and a `files` table shape now; build UI later |
| Month 1 | **Tablet/mobile viewing** | Responsive layout, no hover-dependent interactions. Do not build a native app. |
| Month 1 | **Sick-leave / fitness certificates** | Make the print layer template-driven, not one hardcoded page |
| Month 2–3 | **Simple reports** (patients/day, common diagnoses) | Free-text diagnosis makes this permanently impossible; optional diagnosis *tags* alongside the free text keep it alive |
| Month 3+ | **Second doctor / second location** | A `clinic_id`/`doctor_id` column on core tables, defaulted. Never hardcode "the doctor." |

---

## 10. Recommendation

**Do not write code yet.** Spend 45 minutes with the doctor — at his desk, with his prescription pad and his printer in the room — closing questions 1–8 in §8. They are all schema or topology decisions, all free today, and all expensive within a month.

**In parallel, a one-day spike:** a Razor Pages skeleton + SQLite seeded with 20,000 patients and 100,000 visits, one search page, measured on hardware comparable to the clinic's. That retires the entire performance question in a day and gives the stakeholder something to click.

**Then build the MVP in §9**, ship it with someone sitting beside the doctor for his first two clinics, and let his week-4 complaints — not this document — order Phase 2.

**If you build only three features well, build these:** drug autocomplete that recalls the doctor's last-used regimen, "repeat last Rx", and personal-corpus autocomplete on diagnosis. They are what turn a 4-minute form into a 2-minute one.

**The one thing to get right before the first migration:** decide whether a finalized prescription is mutable. Everything else in this document is a patch. That one is a rewrite.
