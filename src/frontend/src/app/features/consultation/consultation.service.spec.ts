import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ConsultationService, Visit } from './consultation.service';

describe('ConsultationService', () => {
  let service: ConsultationService;
  let httpMock: HttpTestingController;

  const visit: Visit = {
    id: 9,
    patientId: 5,
    appointmentId: 3,
    visitNumber: 1,
    temperature: 37,
    bpSystolic: 120,
    bpDiastolic: 80,
    pulse: 72,
    weight: 52.85,
    complaints: 'Cough',
    diagnosis: 'URI',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ConsultationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts a consultation against the appointment-nested route', () => {
    service
      .startConsultation(3, {
        temperature: 37,
        bpSystolic: 120,
        bpDiastolic: 80,
        pulse: 72,
        weight: 52.85,
        complaints: 'Cough',
        diagnosis: 'URI',
      })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/3/start-consultation`);
    expect(req.request.method).toBe('POST');
    req.flush(visit);
  });

  it('fetches a visit by id', () => {
    service.get(9).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/9`);
    expect(req.request.method).toBe('GET');
    req.flush(visit);
  });

  it('updates only complaints/diagnosis on a visit', () => {
    service.update(9, { complaints: 'Updated', diagnosis: 'Bronchitis' }).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/9`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ complaints: 'Updated', diagnosis: 'Bronchitis' });
    req.flush(visit);
  });
});
