import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Serialized by name from the backend's AppointmentStatus enum. */
export type AppointmentStatus = 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow';

/**
 * Statuses the doctor may set by hand from the daily schedule.
 *
 * FLAGGED ASSUMPTION (see docs/implementation-progress.md Step 12): 'Completed'
 * is deliberately absent -- the API rejects setting it directly with a 400,
 * because completion happens only as a side effect of recording a visit.
 * If that rule is overturned, add 'Completed' back here and drop the guard in
 * the backend's AppointmentService.
 */
export const MANUALLY_SETTABLE_STATUSES: readonly AppointmentStatus[] = [
  'Scheduled',
  'Cancelled',
  'NoShow',
];

export interface Appointment {
  id: number;
  patientId: number;
  patientName: string;
  /** Local ISO date-time, e.g. 2026-03-02T09:00:00. */
  scheduledTime: string;
  /** Entered by the doctor per appointment -- never a system default. */
  durationMinutes: number;
  status: AppointmentStatus;
  /** Null until the appointment has produced a visit. */
  visitId: number | null;
  /** True once at least one Prescription exists for this appointment's visit. */
  hasPrescription: boolean;
}

export interface CreateAppointmentRequest {
  patientId: number;
  scheduledTime: string;
  durationMinutes: number;
}

/** Vitals mirror the backend's non-nullable Visit columns, so all are required. */
export interface WalkInVisitRequest {
  patientId: number;
  durationMinutes: number;
  temperature: number;
  bpSystolic: number;
  bpDiastolic: number;
  pulse: number;
  weight: number;
  complaints: string | null;
  diagnosis: string | null;
}

export interface WalkInVisit {
  visitId: number;
  appointmentId: number;
  patientId: number;
  visitNumber: number;
}

/** Formats a Date as the YYYY-MM-DD the daily endpoint expects, in local time. */
export function toDateParam(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

@Injectable({ providedIn: 'root' })
export class AppointmentService {
  private readonly http = inject(HttpClient);

  create(request: CreateAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(`${environment.apiUrl}/appointments`, request);
  }

  /**
   * The merged daily list: scheduled and walk-in entries come back together,
   * ordered by time, because both are plain appointment rows on the server.
   */
  getDaily(date: string): Observable<Appointment[]> {
    return this.http.get<Appointment[]>(`${environment.apiUrl}/appointments/daily`, {
      params: { date },
    });
  }

  updateStatus(appointmentId: number, status: AppointmentStatus): Observable<Appointment> {
    return this.http.put<Appointment>(
      `${environment.apiUrl}/appointments/${appointmentId}/status`,
      { status },
    );
  }

  /** Creates the Appointment and its linked Visit in one server-side flow. */
  createWalkIn(request: WalkInVisitRequest): Observable<WalkInVisit> {
    return this.http.post<WalkInVisit>(`${environment.apiUrl}/appointments/walk-in`, request);
  }
}
