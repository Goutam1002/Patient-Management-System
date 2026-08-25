import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { Patient } from '../patients/patient.service';
import { RecentPatient, SearchService } from './search.service';

describe('SearchService', () => {
  let service: SearchService;
  let httpMock: HttpTestingController;

  const patient: Patient = {
    patientId: 5,
    name: 'Alexandra Smith',
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

  const recent: RecentPatient = {
    patientId: 5,
    name: 'Alexandra Smith',
    phone: '9876543210',
    lastVisitDate: '2026-04-01T09:00:00',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SearchService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends the name query parameter to the shared /patients/search endpoint', () => {
    service.search('andra', null).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/search?name=andra`);
    expect(req.request.method).toBe('GET');
    req.flush([patient]);
  });

  it('sends both name and phone when both are supplied', () => {
    service.search('Alexandra', '987').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/search?name=Alexandra&phone=987`);
    expect(req.request.method).toBe('GET');
    req.flush([patient]);
  });

  it('fetches recent patients with a default count', () => {
    service.getRecent().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=5`);
    expect(req.request.method).toBe('GET');
    req.flush([recent]);
  });

  it('fetches recent patients with an explicit count', () => {
    service.getRecent(10).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=10`);
    expect(req.request.method).toBe('GET');
    req.flush([recent]);
  });
});
