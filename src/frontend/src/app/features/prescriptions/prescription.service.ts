import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PrescriptionItem {
  id: number;
  drugName: string;
  dosage: string | null;
  frequency: string | null;
  duration: string | null;
  instructions: string | null;
}

/** The full read/print shape for a Prescription -- see backend PrescriptionDto. */
export interface Prescription {
  id: number;
  visitId: number;
  createdAt: string;
  clinicName: string;
  doctorName: string;
  qualifications: string | null;
  registrationNumber: string | null;
  logo: string | null;
  signature: string | null;
  items: PrescriptionItem[];
}

export interface CreatePrescriptionItemRequest {
  drugName: string;
  dosage: string | null;
  frequency: string | null;
  duration: string | null;
  instructions: string | null;
}

/** VisitId is not a property here -- it's part of the create() URL, mirroring the backend's route/body split. */
export interface CreatePrescriptionRequest {
  items: CreatePrescriptionItemRequest[];
}

@Injectable({ providedIn: 'root' })
export class PrescriptionService {
  private readonly http = inject(HttpClient);

  create(visitId: number, request: CreatePrescriptionRequest): Observable<Prescription> {
    return this.http.post<Prescription>(`${environment.apiUrl}/visits/${visitId}/prescriptions`, request);
  }

  get(prescriptionId: number): Observable<Prescription> {
    return this.http.get<Prescription>(`${environment.apiUrl}/prescriptions/${prescriptionId}`);
  }

  /**
   * Contains-semantics, case-insensitive suggestions drawn from the doctor's
   * own prior prescribing history -- an autocomplete UX assist only, never a
   * validation constraint on what a drug name may be. Query parameter is
   * named "prefix" to match the backend's fixed route shape even though the
   * match itself isn't prefix-only.
   */
  drugSuggestions(term: string): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiUrl}/prescriptions/drug-suggestions`, {
      params: { prefix: term },
    });
  }
}
