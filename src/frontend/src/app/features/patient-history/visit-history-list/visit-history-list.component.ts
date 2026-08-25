import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Patient, PatientService } from '../../patients/patient.service';
import { DateRange, DateRangeFilterComponent } from '../date-range-filter/date-range-filter.component';
import { PatientHistoryService, VisitSummary } from '../patient-history.service';

@Component({
  selector: 'app-visit-history-list',
  standalone: true,
  imports: [DatePipe, RouterLink, DateRangeFilterComponent],
  templateUrl: './visit-history-list.component.html',
})
export class VisitHistoryListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly patientHistoryService = inject(PatientHistoryService);
  private readonly patientService = inject(PatientService);

  readonly patientId = Number(this.route.snapshot.paramMap.get('id'));

  readonly patient = signal<Patient | null>(null);
  readonly visits = signal<VisitSummary[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.patientService.get(this.patientId).subscribe({ next: (patient) => this.patient.set(patient) });
    this.load(null, null);
  }

  onRangeChange(range: DateRange): void {
    this.load(range.from, range.to);
  }

  private load(from: string | null, to: string | null): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.patientHistoryService.getVisits(this.patientId, from, to).subscribe({
      next: (visits) => {
        this.visits.set(visits);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load visit history.');
      },
    });
  }
}
