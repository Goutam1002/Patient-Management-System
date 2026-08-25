import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Patient } from '../patients/patient.service';

/** A row in the "recent patients" list -- see backend RecentPatientDto. */
export interface RecentPatient {
  patientId: number;
  name: string;
  phone: string | null;
  /** Null for a patient with no visits yet -- such patients sort last, not excluded. */
  lastVisitDate: string | null;
}

/**
 * Consumes Patient Management's own /api/patients/search endpoint (CR-4:
 * one canonical search endpoint, not duplicated here) plus this module's own
 * /api/patients/recent.
 */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpClient);

  search(name: string | null, phone: string | null): Observable<Patient[]> {
    const params: Record<string, string> = {};
    if (name) {
      params['name'] = name;
    }
    if (phone) {
      params['phone'] = phone;
    }
    return this.http.get<Patient[]>(`${environment.apiUrl}/patients/search`, { params });
  }

  getRecent(count = 5): Observable<RecentPatient[]> {
    return this.http.get<RecentPatient[]>(`${environment.apiUrl}/patients/recent`, {
      params: { count: String(count) },
    });
  }
}
