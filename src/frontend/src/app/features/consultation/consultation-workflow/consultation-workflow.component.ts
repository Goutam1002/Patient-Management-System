import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ComplaintsComponent } from '../complaints/complaints.component';
import { ConsultationService, Visit } from '../consultation.service';
import { DiagnosisComponent } from '../diagnosis/diagnosis.component';
import { VitalsFormComponent } from '../vitals-form/vitals-form.component';

/**
 * The single combined consultation screen -- vitals, complaints, and
 * diagnosis entry all in one place, kept to one screen per the 2-3 minute
 * workflow-completeness interpretation (implementation-brd.md's "Fixed
 * interpretation" section). Prescription entry (Module 6) is out of scope
 * for this step, but nothing here blocks bolting it onto the same screen
 * later -- this component already owns the one place a doctor lands after
 * "start consultation."
 *
 * Two modes, one component (see docs/implementation-progress.md Step 13 for
 * the post-creation edit boundary this reflects):
 * - create (/appointments/:appointmentId/consultation): a scheduled
 *   appointment with no visit yet. Vitals + complaints + diagnosis are all
 *   editable; submitting calls start-consultation.
 * - edit (/visits/:visitId): an existing visit. Vitals are loaded and
 *   disabled (visible for context, never editable retroactively);
 *   complaints/diagnosis stay editable and submitting calls update.
 */
@Component({
  selector: 'app-consultation-workflow',
  standalone: true,
  imports: [ReactiveFormsModule, VitalsFormComponent, ComplaintsComponent, DiagnosisComponent],
  templateUrl: './consultation-workflow.component.html',
})
export class ConsultationWorkflowComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly consultationService = inject(ConsultationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly mode = signal<'create' | 'edit'>('create');
  private appointmentId: number | null = null;
  private visitId: number | null = null;

  readonly vitalsGroup = this.formBuilder.nonNullable.group({
    temperature: ['', Validators.required],
    bpSystolic: ['', Validators.required],
    bpDiastolic: ['', Validators.required],
    pulse: ['', Validators.required],
    weight: ['', Validators.required],
  });
  readonly complaintsControl = this.formBuilder.control<string | null>(null);
  readonly diagnosisControl = this.formBuilder.control<string | null>(null);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly visit = signal<Visit | null>(null);
  readonly savedJustNow = signal(false);

  /** Only meaningful in create mode -- gates the form away in favour of a success panel. */
  readonly createdVisit = computed(() => (this.mode() === 'create' ? this.visit() : null));

  ngOnInit(): void {
    const appointmentIdParam = this.route.snapshot.paramMap.get('appointmentId');
    const visitIdParam = this.route.snapshot.paramMap.get('visitId');

    if (appointmentIdParam) {
      this.mode.set('create');
      this.appointmentId = Number(appointmentIdParam);
      return;
    }

    if (visitIdParam) {
      this.mode.set('edit');
      this.visitId = Number(visitIdParam);
      this.loadExistingVisit(this.visitId);
    }
  }

  private loadExistingVisit(visitId: number): void {
    this.loading.set(true);
    this.consultationService.get(visitId).subscribe({
      next: (visit) => {
        this.loading.set(false);
        this.vitalsGroup.setValue({
          temperature: String(visit.temperature),
          bpSystolic: String(visit.bpSystolic),
          bpDiastolic: String(visit.bpDiastolic),
          pulse: String(visit.pulse),
          weight: String(visit.weight),
        });
        // Vitals are mandatory-at-entry and never editable retroactively --
        // disabling (not hiding) keeps them visible for clinical context.
        this.vitalsGroup.disable();
        this.complaintsControl.setValue(visit.complaints);
        this.diagnosisControl.setValue(visit.diagnosis);
        this.visit.set(visit);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load this visit.');
      },
    });
  }

  submit(): void {
    if (this.mode() === 'create') {
      this.submitCreate();
    } else {
      this.submitEdit();
    }
  }

  private submitCreate(): void {
    if (this.vitalsGroup.invalid) {
      this.vitalsGroup.markAllAsTouched();
      return;
    }

    const vitals = this.vitalsGroup.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    this.consultationService
      .startConsultation(this.appointmentId!, {
        temperature: Number(vitals.temperature),
        bpSystolic: Number(vitals.bpSystolic),
        bpDiastolic: Number(vitals.bpDiastolic),
        pulse: Number(vitals.pulse),
        weight: Number(vitals.weight),
        complaints: this.complaintsControl.value || null,
        diagnosis: this.diagnosisControl.value || null,
      })
      .subscribe({
        next: (visit) => {
          this.saving.set(false);
          this.visit.set(visit);
        },
        error: (error: { status?: number }) => {
          this.saving.set(false);
          this.errorMessage.set(
            error.status === 409
              ? 'This appointment already has a visit recorded.'
              : 'Could not start the consultation.',
          );
        },
      });
  }

  private submitEdit(): void {
    if (this.visitId === null) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.savedJustNow.set(false);

    this.consultationService
      .update(this.visitId, {
        complaints: this.complaintsControl.value || null,
        diagnosis: this.diagnosisControl.value || null,
      })
      .subscribe({
        next: (visit) => {
          this.saving.set(false);
          this.visit.set(visit);
          this.savedJustNow.set(true);
        },
        error: () => {
          this.saving.set(false);
          this.errorMessage.set('Could not save the changes.');
        },
      });
  }

  goToSchedule(): void {
    this.router.navigate(['/appointments']);
  }
}
