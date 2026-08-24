import { Routes } from '@angular/router';
import { authGuard } from './features/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { DoctorDetailsFormComponent } from './features/doctor-details/doctor-details-form/doctor-details-form.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: 'doctor-details',
    component: DoctorDetailsFormComponent,
    canActivate: [authGuard],
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
];
