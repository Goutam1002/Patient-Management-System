import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { PatientHistoryService, VisitDetail, VisitSummary } from './patient-history.service';

describe('PatientHistoryService', () => {
  let service: PatientHistoryService;
  let httpMock: HttpTestingController;

  const summary: VisitSummary = {
    id: 3,
    patientId: 5,
    visitNumber: 2,
    visitDate: '2026-04-01T09:00:00',
    diagnosis: 'URI',
  };

  const detail: VisitDetail = {
    id: 3,
    patientId: 5,
    appointmentId: 9,
    visitNumber: 2,
    visitDate: '2026-04-01T09:00:00',
    temperature: 37.0,
    bpSystolic: 120,
    bpDiastolic: 80,
    pulse: 72,
    weight: 52.85,
    complaints: 'Cough',
    diagnosis: 'URI',
    prescriptions: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PatientHistoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('fetches visits for a patient with no date range', () => {
    service.getVisits(5, null, null).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/5/visits`);
    expect(req.request.method).toBe('GET');
    req.flush([summary]);
  });

  it('sends from/to as query parameters when a date range is supplied', () => {
    service.getVisits(5, '2026-01-01', '2026-01-31').subscribe();

    const req = httpMock.expectOne(
      `${environment.apiUrl}/patients/5/visits?from=2026-01-01&to=2026-01-31`,
    );
    expect(req.request.method).toBe('GET');
    req.flush([summary]);
  });

  it('fetches the full visit detail against the shared visits/{id} route', () => {
    service.getVisitDetail(3).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/3`);
    expect(req.request.method).toBe('GET');
    req.flush(detail);
  });
});
