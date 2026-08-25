import { Component } from '@angular/core';
import { PatientSearchBarComponent } from '../search/patient-search-bar/patient-search-bar.component';
import { RecentPatientsListComponent } from '../search/recent-patients-list/recent-patients-list.component';

/**
 * Module 8 (Search & Navigation) gives the placeholder Dashboard from Step 10
 * its first real content -- per that step's own note ("replace this
 * component's content rather than adding a second route"), not a new route.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [PatientSearchBarComponent, RecentPatientsListComponent],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent {}
