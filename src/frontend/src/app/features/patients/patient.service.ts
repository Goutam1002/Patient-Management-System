import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Patient {
  patientId: number;
  name: string;
  age: number | null;
  // ISO date (YYYY-MM-DD), independent of age -- neither is derived from the other.
  dateOfBirth: string | null;
  gender: string;
  phone: string | null;
  allergies: string | null;
  currentMedications: string | null;
  chronicConditions: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
}

export interface PatientRequest {
  name: string;
  age: number | null;
  dateOfBirth: string | null;
  gender: string;
  phone: string | null;
  allergies: string | null;
  currentMedications: string | null;
  chronicConditions: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
}

/** Shape of the registration/edit forms' raw string values before conversion to a PatientRequest. */
export interface PatientFormValue {
  name: string;
  age: string;
  dateOfBirth: string;
  gender: string;
  phone: string;
  allergies: string;
  currentMedications: string;
  chronicConditions: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
}

export function toPatientRequest(raw: PatientFormValue): PatientRequest {
  return {
    name: raw.name,
    age: raw.age ? Number(raw.age) : null,
    dateOfBirth: raw.dateOfBirth || null,
    gender: raw.gender,
    phone: raw.phone || null,
    allergies: raw.allergies || null,
    currentMedications: raw.currentMedications || null,
    chronicConditions: raw.chronicConditions || null,
    emergencyContactName: raw.emergencyContactName || null,
    emergencyContactPhone: raw.emergencyContactPhone || null,
  };
}

export function toPatientFormValue(patient: Patient): PatientFormValue {
  return {
    name: patient.name,
    age: patient.age !== null ? String(patient.age) : '',
    dateOfBirth: patient.dateOfBirth ?? '',
    gender: patient.gender,
    phone: patient.phone ?? '',
    allergies: patient.allergies ?? '',
    currentMedications: patient.currentMedications ?? '',
    chronicConditions: patient.chronicConditions ?? '',
    emergencyContactName: patient.emergencyContactName ?? '',
    emergencyContactPhone: patient.emergencyContactPhone ?? '',
  };
}

@Injectable({ providedIn: 'root' })
export class PatientService {
  private readonly http = inject(HttpClient);

  create(request: PatientRequest): Observable<Patient> {
    return this.http.post<Patient>(`${environment.apiUrl}/patients`, request);
  }

  get(patientId: number): Observable<Patient> {
    return this.http.get<Patient>(`${environment.apiUrl}/patients/${patientId}`);
  }

  update(patientId: number, request: PatientRequest): Observable<Patient> {
    return this.http.put<Patient>(`${environment.apiUrl}/patients/${patientId}`, request);
  }

  /**
   * Contains-semantics search on name and/or phone. Owned by this module's
   * API; the search UI itself belongs to the Search & Navigation module
   * (Modules/08-search-navigation.md) -- this method exists for that module
   * to consume, not used by any component here.
   */
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
}
