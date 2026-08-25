import { Routes } from '@angular/router';
import { AppointmentFormComponent } from './features/appointments/appointment-form/appointment-form.component';
import { DailyScheduleComponent } from './features/appointments/daily-schedule/daily-schedule.component';
import { WalkInRegistrationComponent } from './features/appointments/walk-in-registration/walk-in-registration.component';
import { authGuard } from './features/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { ConsultationWorkflowComponent } from './features/consultation/consultation-workflow/consultation-workflow.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { DoctorDetailsFormComponent } from './features/doctor-details/doctor-details-form/doctor-details-form.component';
import { PatientEditComponent } from './features/patients/patient-edit/patient-edit.component';
import { PatientProfileComponent } from './features/patients/patient-profile/patient-profile.component';
import { PatientRegistrationFormComponent } from './features/patients/patient-registration-form/patient-registration-form.component';
import { PrescriptionFormComponent } from './features/prescriptions/prescription-form/prescription-form.component';
import { PrintablePrescriptionComponent } from './features/prescriptions/printable-prescription/printable-prescription.component';

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
    path: 'appointments/new',
    component: AppointmentFormComponent,
    canActivate: [authGuard],
  },
  {
    path: 'appointments/walk-in',
    component: WalkInRegistrationComponent,
    canActivate: [authGuard],
  },
  {
    path: 'appointments',
    component: DailyScheduleComponent,
    canActivate: [authGuard],
  },
  {
    path: 'appointments/:appointmentId/consultation',
    component: ConsultationWorkflowComponent,
    canActivate: [authGuard],
  },
  {
    path: 'visits/:visitId/prescriptions/new',
    component: PrescriptionFormComponent,
    canActivate: [authGuard],
  },
  {
    path: 'visits/:visitId',
    component: ConsultationWorkflowComponent,
    canActivate: [authGuard],
  },
  {
    path: 'prescriptions/:id',
    component: PrintablePrescriptionComponent,
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
