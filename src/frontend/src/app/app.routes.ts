import { Routes } from '@angular/router';
import { AppointmentFormComponent } from './features/appointments/appointment-form/appointment-form.component';
import { DailyScheduleComponent } from './features/appointments/daily-schedule/daily-schedule.component';
import { WalkInRegistrationComponent } from './features/appointments/walk-in-registration/walk-in-registration.component';
import { authGuard } from './features/auth/auth.guard';
import { LoginComponent } from './features/auth/login/login.component';
import { ConsultationWorkflowComponent } from './features/consultation/consultation-workflow/consultation-workflow.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { DoctorDetailsFormComponent } from './features/doctor-details/doctor-details-form/doctor-details-form.component';
import { ExportPageComponent } from './features/export/export-page/export-page.component';
import { VisitDetailComponent } from './features/patient-history/visit-detail/visit-detail.component';
import { VisitHistoryListComponent } from './features/patient-history/visit-history-list/visit-history-list.component';
import { PatientEditComponent } from './features/patients/patient-edit/patient-edit.component';
import { PatientProfileComponent } from './features/patients/patient-profile/patient-profile.component';
import { PatientRegistrationFormComponent } from './features/patients/patient-registration-form/patient-registration-form.component';
import { PatientsComponent } from './features/patients/patients/patients.component';
import { PrescriptionFormComponent } from './features/prescriptions/prescription-form/prescription-form.component';
import { PrintablePrescriptionComponent } from './features/prescriptions/printable-prescription/printable-prescription.component';

// `data: { hideHeader: true }` marks every add/edit (data-entry) screen so
// the global header (see shared/app-header) stays off the consultation
// entry path -- AppComponent reads this from the deepest activated route
// rather than matching on the URL string, so a route's header visibility is
// declared right next to its own definition.
export const routes: Routes = [
  { path: 'login', component: LoginComponent, data: { hideHeader: true } },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard],
  },
  {
    path: 'patients',
    component: PatientsComponent,
    canActivate: [authGuard],
  },
  {
    path: 'doctor-details',
    component: DoctorDetailsFormComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
  },
  {
    path: 'export',
    component: ExportPageComponent,
    canActivate: [authGuard],
  },
  {
    path: 'appointments/new',
    component: AppointmentFormComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
  },
  {
    path: 'appointments/walk-in',
    component: WalkInRegistrationComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
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
    data: { hideHeader: true },
  },
  {
    path: 'visits/:visitId/prescriptions/new',
    component: PrescriptionFormComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
  },
  {
    path: 'visits/:visitId',
    component: ConsultationWorkflowComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
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
    data: { hideHeader: true },
  },
  {
    path: 'patients/:id/edit',
    component: PatientEditComponent,
    canActivate: [authGuard],
    data: { hideHeader: true },
  },
  {
    path: 'patients/:id/history',
    component: VisitHistoryListComponent,
    canActivate: [authGuard],
  },
  {
    // Distinct from Module 5's /visits/:visitId (the create/edit surface) --
    // this is Module 7's read-only history detail view, nested under its
    // patient for a natural "back to history" breadcrumb.
    path: 'patients/:patientId/visits/:visitId',
    component: VisitDetailComponent,
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
