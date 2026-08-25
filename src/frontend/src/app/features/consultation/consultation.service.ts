import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Vitals mirror the backend's non-nullable Visit columns, so all are required. */
export interface StartConsultationRequest {
  temperature: number;
  bpSystolic: number;
  bpDiastolic: number;
  pulse: number;
  weight: number;
  complaints: string | null;
  diagnosis: string | null;
}

/**
 * Post-creation edit surface -- complaints/diagnosis only. Vitals are
 * mandatory-at-entry and never editable retroactively, so there is no
 * property for them here at all (mirrors the backend's UpdateVisitRequest).
 */
export interface UpdateVisitRequest {
  complaints: string | null;
  diagnosis: string | null;
}

/** The full clinical read shape for a Visit. */
export interface Visit {
  id: number;
  patientId: number;
  appointmentId: number;
  /** Sequential per patient (1, 2, 3, ...), not a global counter. */
  visitNumber: number;
  temperature: number;
  bpSystolic: number;
  bpDiastolic: number;
  pulse: number;
  weight: number;
  complaints: string | null;
  diagnosis: string | null;
}

@Injectable({ providedIn: 'root' })
export class ConsultationService {
  private readonly http = inject(HttpClient);

  /** Creates the Visit for a scheduled appointment and completes it, in one call. */
  startConsultation(appointmentId: number, request: StartConsultationRequest): Observable<Visit> {
    return this.http.post<Visit>(
      `${environment.apiUrl}/appointments/${appointmentId}/start-consultation`,
      request,
    );
  }

  get(visitId: number): Observable<Visit> {
    return this.http.get<Visit>(`${environment.apiUrl}/visits/${visitId}`);
  }

  update(visitId: number, request: UpdateVisitRequest): Observable<Visit> {
    return this.http.put<Visit>(`${environment.apiUrl}/visits/${visitId}`, request);
  }
}
