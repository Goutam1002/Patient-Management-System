import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Patient } from '../../patients/patient.service';
import { SearchService } from '../search.service';

/**
 * Quick patient search -- two independent fields (Name, Phone), matching the
 * backend's own AND-combined contains-semantics contract 1:1 rather than
 * inventing single-box OR semantics the API doesn't support. Meant to be
 * mounted once, globally, per Modules/08-search-navigation.md; currently
 * hosted on the Dashboard (see dashboard.component), the app's post-login
 * landing screen, rather than a persistent header -- this app has no header
 * shell yet and adding one is a bigger change than this module's checklist
 * calls for.
 */
@Component({
  selector: 'app-patient-search-bar',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './patient-search-bar.component.html',
})
export class PatientSearchBarComponent {
  private readonly searchService = inject(SearchService);

  readonly form = new FormGroup({
    name: new FormControl<string>(''),
    phone: new FormControl<string>(''),
  });

  readonly results = signal<Patient[] | null>(null);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  search(): void {
    const name = this.form.value.name?.trim() || null;
    const phone = this.form.value.phone?.trim() || null;
    if (!name && !phone) {
      this.results.set(null);
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.searchService.search(name, phone).subscribe({
      next: (results) => {
        this.results.set(results);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not search patients.');
      },
    });
  }

  clear(): void {
    this.form.reset({ name: '', phone: '' });
    this.results.set(null);
    this.errorMessage.set(null);
  }
}
