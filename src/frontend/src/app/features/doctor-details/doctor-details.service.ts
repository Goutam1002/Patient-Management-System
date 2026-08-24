import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DoctorDetails {
  id: number;
  clinicName: string;
  doctorName: string;
  qualifications: string | null;
  registrationNumber: string | null;
  // Base64-encoded image bytes -- matches the backend DoctorDetailsDto shape.
  logo: string | null;
  signature: string | null;
}

export interface UpdateDoctorDetailsRequest {
  clinicName: string;
  doctorName: string;
  qualifications: string | null;
  registrationNumber: string | null;
  // Null means "leave the existing image unchanged" (see the backend's
  // UpdateDoctorDetailsRequest) -- only sent when the doctor picks a new file.
  logo: string | null;
  signature: string | null;
}

@Injectable({ providedIn: 'root' })
export class DoctorDetailsService {
  private readonly http = inject(HttpClient);

  get(): Observable<DoctorDetails> {
    return this.http.get<DoctorDetails>(`${environment.apiUrl}/doctor-details`);
  }

  update(request: UpdateDoctorDetailsRequest): Observable<DoctorDetails> {
    return this.http.put<DoctorDetails>(`${environment.apiUrl}/doctor-details`, request);
  }
}
