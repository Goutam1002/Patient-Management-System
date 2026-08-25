import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { Prescription, PrescriptionService } from './prescription.service';

describe('PrescriptionService', () => {
  let service: PrescriptionService;
  let httpMock: HttpTestingController;

  const prescription: Prescription = {
    id: 7,
    visitId: 9,
    createdAt: '2026-04-01T09:15:00',
    clinicName: 'Sunrise Clinic',
    doctorName: 'Dr. Rao',
    qualifications: 'MBBS',
    registrationNumber: 'REG-42',
    logo: null,
    signature: null,
    items: [{ id: 1, drugName: 'Paracetamol', dosage: '500mg', frequency: null, duration: null, instructions: null }],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PrescriptionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates a prescription against the visit-nested route', () => {
    service
      .create(9, { items: [{ drugName: 'Paracetamol', dosage: '500mg', frequency: null, duration: null, instructions: null }] })
      .subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/9/prescriptions`);
    expect(req.request.method).toBe('POST');
    req.flush(prescription);
  });

  it('fetches a prescription by id', () => {
    service.get(7).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/7`);
    expect(req.request.method).toBe('GET');
    req.flush(prescription);
  });

  it('requests drug suggestions with the term as the prefix query parameter', () => {
    service.drugSuggestions('oxic').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/drug-suggestions?prefix=oxic`);
    expect(req.request.method).toBe('GET');
    req.flush(['Amoxicillin']);
  });
});
