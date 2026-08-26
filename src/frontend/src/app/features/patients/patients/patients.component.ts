import { Component } from '@angular/core';
import { PatientSearchBarComponent } from '../../search/patient-search-bar/patient-search-bar.component';
import { RecentPatientsListComponent } from '../../search/recent-patients-list/recent-patients-list.component';

/**
 * The `/patients` page -- hosts Module 8 (Search & Navigation)'s search bar
 * and recent-patients list, split out of DashboardComponent (where Step 16
 * had mounted them, absent a header/nav shell) now that the global header
 * gives them a dedicated nav entry.
 */
@Component({
  selector: 'app-patients',
  standalone: true,
  imports: [PatientSearchBarComponent, RecentPatientsListComponent],
  templateUrl: './patients.component.html',
})
export class PatientsComponent {}
