import { Routes } from '@angular/router';
import { authGuard } from './features/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { DoctorDetailsFormComponent } from './features/doctor-details/doctor-details-form/doctor-details-form.component';
import { PatientEditComponent } from './features/patients/patient-edit/patient-edit.component';
import { PatientProfileComponent } from './features/patients/patient-profile/patient-profile.component';
import { PatientRegistrationFormComponent } from './features/patients/patient-registration-form/patient-registration-form.component';

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
  {
    path: 'patients/new',
    component: PatientRegistrationFormComponent,
    canActivate: [authGuard],
  },
  {
    path: 'patients/:id/edit',
    component: PatientEditComponent,
    canActivate: [authGuard],
  },
  {
    path: 'patients/:id',
    component: PatientProfileComponent,
    canActivate: [authGuard],
  },
  // Root always points at the dashboard; authGuard is the single source of
  // truth for bouncing an unauthenticated visitor to /login?returnUrl=... .
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
];
