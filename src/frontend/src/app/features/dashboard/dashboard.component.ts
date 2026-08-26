import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Post-login landing page (Step 10). Module 8 (Step 16) had mounted patient
 * search/recent-patients here in the absence of a header/nav shell; the new
 * global header (ad hoc step, see docs/implementation-progress.md) gives
 * Patients its own route and nav entry, so this reverts to lean placeholder
 * content -- still a valid landing page, just no longer their host.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent {}
