import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AppointmentService, WalkInVisit } from '../appointment.service';

@Component({
  selector: 'app-walk-in-registration',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './walk-in-registration.component.html',
})
export class WalkInRegistrationComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly appointmentService = inject(AppointmentService);
  private readonly router = inject(Router);

  // One flow: submitting this creates the walk-in's Appointment and its linked
  // Visit together server-side -- there is no separate pre-booking step.
  readonly form = this.formBuilder.nonNullable.group({
    patientId: ['', Validators.required],
    durationMinutes: ['', [Validators.required, Validators.min(1)]],
    temperature: ['', Validators.required],
    bpSystolic: ['', Validators.required],
    bpDiastolic: ['', Validators.required],
    pulse: ['', Validators.required],
    weight: ['', Validators.required],
    complaints: [''],
    diagnosis: [''],
  });

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly result = signal<WalkInVisit | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    this.appointmentService
      .createWalkIn({
        patientId: Number(raw.patientId),
        durationMinutes: Number(raw.durationMinutes),
        temperature: Number(raw.temperature),
        bpSystolic: Number(raw.bpSystolic),
        bpDiastolic: Number(raw.bpDiastolic),
        pulse: Number(raw.pulse),
        weight: Number(raw.weight),
        complaints: raw.complaints || null,
        diagnosis: raw.diagnosis || null,
      })
      .subscribe({
        next: (walkIn) => {
          this.saving.set(false);
          this.result.set(walkIn);
        },
        error: (error: { status?: number }) => {
          this.saving.set(false);
          this.errorMessage.set(
            error.status === 409
              ? 'That moment is already occupied by another appointment. Try again in a moment.'
              : 'Could not register the walk-in visit.',
          );
        },
      });
  }

  goToSchedule(): void {
    this.router.navigate(['/appointments']);
  }
}
