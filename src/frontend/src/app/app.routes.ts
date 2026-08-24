import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  // Every other module's feature routes are expected to add canActivate:
  // [authGuard] once they exist (Modules/README.md) -- there's nothing to
  // guard yet, Authentication is the first module with any Angular UI.
  { path: '', pathMatch: 'full', redirectTo: 'login' },
];
