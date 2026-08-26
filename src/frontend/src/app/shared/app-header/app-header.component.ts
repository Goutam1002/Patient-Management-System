import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../features/auth/auth.service';
import { DoctorDetailsService } from '../../features/doctor-details/doctor-details.service';

/**
 * Global nav shell, mounted once in AppComponent and shown/hidden per route
 * via each route's `data.hideHeader` (see app.routes.ts) -- ad hoc user
 * request, not tied to a Modules/*.md checklist. Hidden on every add/edit
 * (data-entry) screen so it never competes with the consultation workflow's
 * 2-3 minute friction budget.
 */
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-header.component.html',
})
export class AppHeaderComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly doctorDetailsService = inject(DoctorDetailsService);
  private readonly router = inject(Router);

  // Base64 image data URL, same encoding doctor-details-form.component.ts
  // already uses -- null shows a placeholder icon rather than a broken <img>.
  readonly logoDataUrl = signal<string | null>(null);

  ngOnInit(): void {
    this.doctorDetailsService.get().subscribe({
      next: (details) => this.logoDataUrl.set(details.logo ? `data:image/*;base64,${details.logo}` : null),
      error: () => this.logoDataUrl.set(null),
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
