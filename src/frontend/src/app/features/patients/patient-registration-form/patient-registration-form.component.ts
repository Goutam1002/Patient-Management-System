import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PatientService, toPatientRequest } from '../patient.service';

@Component({
  selector: 'app-patient-registration-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './patient-registration-form.component.html',
})
export class PatientRegistrationFormComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly patientService = inject(PatientService);
  private readonly router = inject(Router);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', Validators.required],
    age: [''],
    dateOfBirth: [''],
    gender: ['', Validators.required],
    phone: [''],
    allergies: [''],
    currentMedications: [''],
    chronicConditions: [''],
    emergencyContactName: [''],
    emergencyContactPhone: [''],
  });

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.patientService.create(toPatientRequest(this.form.getRawValue())).subscribe({
      next: (patient) => {
        this.saving.set(false);
        this.router.navigate(['/patients', patient.patientId]);
      },
      error: () => {
        this.saving.set(false);
        this.errorMessage.set('Could not register patient.');
      },
    });
  }
}
