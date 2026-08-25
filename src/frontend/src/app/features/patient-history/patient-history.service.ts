import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Prescription } from '../prescriptions/prescription.service';

/** One row in a patient's visit history -- see backend VisitSummaryDto. */
export interface VisitSummary {
  id: number;
  patientId: number;
  /** Sequential per patient (1, 2, 3, ...), not a global counter. */
  visitNumber: number;
  /** Computed server-side from the linked Appointment.ScheduledTime -- Visit has no own date column. */
  visitDate: string;
  diagnosis: string | null;
}

/**
 * The full clinical read shape for one visit -- vitals, complaints,
 * diagnosis, and every prescription recorded at that visit. Prescriptions
 * reuse prescription.service's own Prescription type since the backend
 * embeds the exact same PrescriptionDto shape here.
 */
export interface VisitDetail {
  id: number;
  patientId: number;
  appointmentId: number;
  visitNumber: number;
  visitDate: string;
  temperature: number;
  bpSystolic: number;
  bpDiastolic: number;
  pulse: number;
  weight: number;
  complaints: string | null;
  diagnosis: string | null;
  prescriptions: Prescription[];
}

@Injectable({ providedIn: 'root' })
export class PatientHistoryService {
  private readonly http = inject(HttpClient);

  /** Newest-first; from/to are an optional inclusive date range (yyyy-MM-dd). */
  getVisits(patientId: number, from: string | null, to: string | null): Observable<VisitSummary[]> {
    const params: Record<string, string> = {};
    if (from) {
      params['from'] = from;
    }
    if (to) {
      params['to'] = to;
    }
    return this.http.get<VisitSummary[]>(`${environment.apiUrl}/patients/${patientId}/visits`, { params });
  }

  /**
   * Shares GET /api/visits/{id} with the Consultation Workflow module's own
   * edit-mode read (ConsultationService.get) -- that endpoint was extended
   * server-side to return this richer shape rather than this module adding a
   * second, competing route for the same visit id.
   */
  getVisitDetail(visitId: number): Observable<VisitDetail> {
    return this.http.get<VisitDetail>(`${environment.apiUrl}/visits/${visitId}`);
  }
}
