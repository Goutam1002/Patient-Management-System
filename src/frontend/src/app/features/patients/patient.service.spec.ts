import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { Patient, PatientRequest, PatientService } from './patient.service';

describe('PatientService', () => {
  let service: PatientService;
  let httpMock: HttpTestingController;

  const sample: Patient = {
    patientId: 5,
    name: 'Alice',
    age: 34,
    dateOfBirth: '1990-05-14',
    gender: 'Female',
    phone: '9876543210',
    allergies: null,
    currentMedications: null,
    chronicConditions: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
  };

  const request: PatientRequest = {
    name: 'Alice',
    age: 34,
    dateOfBirth: '1990-05-14',
    gender: 'Female',
    phone: '9876543210',
    allergies: null,
    currentMedications: null,
    chronicConditions: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PatientService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates a patient', () => {
    service.create(request).subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/patients`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('fetches a patient by id', () => {
    service.get(5).subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/5`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('updates a patient', () => {
    service.update(5, request).subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('searches by name and phone, omitting empty terms', () => {
    service.search('Alice', null).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/search?name=Alice`);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);
  });
});
