import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PatientService, toPatientFormValue, toPatientRequest } from '../patient.service';

@Component({
  selector: 'app-patient-edit',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './patient-edit.component.html',
})
export class PatientEditComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly patientService = inject(PatientService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly patientId = Number(this.route.snapshot.paramMap.get('id'));

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

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.patientService.get(this.patientId).subscribe({
      next: (patient) => {
        this.form.setValue(toPatientFormValue(patient));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load patient.');
      },
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.patientService.update(this.patientId, toPatientRequest(this.form.getRawValue())).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/patients', this.patientId]);
      },
      error: () => {
        this.saving.set(false);
        this.errorMessage.set('Could not save patient.');
      },
    });
  }
}
