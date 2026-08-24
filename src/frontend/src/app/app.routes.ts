import { Routes } from '@angular/router';
import { authGuard } from './features/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { DoctorDetailsFormComponent } from './features/doctor-details/doctor-details-form/doctor-details-form.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard],
  },
  {
    path: 'doctor-details',
    component: DoctorDetailsFormComponent,
    canActivate: [authGuard],
  },
  // Root always points at the dashboard; authGuard is the single source of
  // truth for bouncing an unauthenticated visitor to /login?returnUrl=... .
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
];
