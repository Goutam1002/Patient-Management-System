import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PatientHistoryService, VisitDetail } from '../patient-history.service';

@Component({
  selector: 'app-visit-detail',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './visit-detail.component.html',
})
export class VisitDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly patientHistoryService = inject(PatientHistoryService);

  readonly patientId = Number(this.route.snapshot.paramMap.get('patientId'));
  readonly visitId = Number(this.route.snapshot.paramMap.get('visitId'));

  readonly visit = signal<VisitDetail | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.patientHistoryService.getVisitDetail(this.visitId).subscribe({
      next: (visit) => {
        this.visit.set(visit);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load this visit.');
      },
    });
  }
}
