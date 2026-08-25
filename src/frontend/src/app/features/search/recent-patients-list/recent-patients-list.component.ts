import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RecentPatient, SearchService } from '../search.service';

@Component({
  selector: 'app-recent-patients-list',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './recent-patients-list.component.html',
})
export class RecentPatientsListComponent implements OnInit {
  private readonly searchService = inject(SearchService);

  readonly patients = signal<RecentPatient[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.searchService.getRecent().subscribe({
      next: (patients) => {
        this.patients.set(patients);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load recent patients.');
      },
    });
  }
}
