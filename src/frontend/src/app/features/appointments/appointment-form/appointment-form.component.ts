import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AppointmentService, toDateParam } from '../appointment.service';

@Component({
  selector: 'app-appointment-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './appointment-form.component.html',
})
export class AppointmentFormComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly appointmentService = inject(AppointmentService);
  private readonly router = inject(Router);

  readonly form = this.formBuilder.nonNullable.group({
    patientId: ['', Validators.required],
    date: [toDateParam(new Date()), Validators.required],
    time: ['', Validators.required],
    // Required with no initial value on purpose: the doctor enters the slot
    // length for this appointment; there is no default to fall back on.
    durationMinutes: ['', [Validators.required, Validators.min(1)]],
  });

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);

    this.appointmentService
      .create({
        patientId: Number(raw.patientId),
        scheduledTime: `${raw.date}T${raw.time}:00`,
        durationMinutes: Number(raw.durationMinutes),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/appointments'], { queryParams: { date: raw.date } });
        },
        error: (error: { status?: number }) => {
          this.saving.set(false);
          this.errorMessage.set(
            error.status === 409
              ? 'That time slot is already booked. Double booking is not allowed — pick another time.'
              : error.status === 404
                ? 'No patient exists with that ID.'
                : 'Could not schedule the appointment.',
          );
        },
      });
  }
}
