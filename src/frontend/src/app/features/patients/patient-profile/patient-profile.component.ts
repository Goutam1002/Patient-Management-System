import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Patient, PatientService } from '../patient.service';

@Component({
  selector: 'app-patient-profile',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './patient-profile.component.html',
})
export class PatientProfileComponent implements OnInit {
  private readonly patientService = inject(PatientService);
  private readonly route = inject(ActivatedRoute);

  readonly patientId = Number(this.route.snapshot.paramMap.get('id'));

  readonly patient = signal<Patient | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.patientService.get(this.patientId).subscribe({
      next: (patient) => {
        this.patient.set(patient);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load patient.');
      },
    });
  }
}
