import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Patient, PatientService } from '../../patients/patient.service';
import { ConsultationService, Visit } from '../../consultation/consultation.service';
import { Prescription, PrescriptionService } from '../prescription.service';

/**
 * Header/patient/vitals/diagnosis/meds/footer print layout for a Prescription.
 * A Prescription's own row only carries the doctor/clinic snapshot and line
 * items (see PrescriptionDto); patient demographics and the visit's
 * vitals/diagnosis are composed here from ConsultationService/PatientService,
 * the same way ConsultationWorkflowComponent composes an Appointment's
 * daily-list entry from a separate service rather than the backend growing a
 * combined DTO nothing else needs.
 */
@Component({
  selector: 'app-printable-prescription',
  standalone: true,
  imports: [],
  templateUrl: './printable-prescription.component.html',
  styleUrl: './printable-prescription.component.css',
})
export class PrintablePrescriptionComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly prescriptionService = inject(PrescriptionService);
  private readonly consultationService = inject(ConsultationService);
  private readonly patientService = inject(PatientService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly prescription = signal<Prescription | null>(null);
  readonly visit = signal<Visit | null>(null);
  readonly patient = signal<Patient | null>(null);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.prescriptionService.get(id).subscribe({
      next: (prescription) => {
        this.prescription.set(prescription);
        this.loadVisitAndPatient(prescription.visitId);
      },
      error: () => this.failToLoad(),
    });
  }

  private loadVisitAndPatient(visitId: number): void {
    this.consultationService.get(visitId).subscribe({
      next: (visit) => {
        this.visit.set(visit);
        this.patientService.get(visit.patientId).subscribe({
          next: (patient) => {
            this.patient.set(patient);
            this.loading.set(false);
          },
          error: () => this.failToLoad(),
        });
      },
      error: () => this.failToLoad(),
    });
  }

  private failToLoad(): void {
    this.loading.set(false);
    this.errorMessage.set('Could not load this prescription.');
  }

  print(): void {
    window.print();
  }

  goToSchedule(): void {
    this.router.navigate(['/appointments']);
  }
}
