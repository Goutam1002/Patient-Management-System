import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { DoctorDetails, DoctorDetailsService } from './doctor-details.service';

describe('DoctorDetailsService', () => {
  let service: DoctorDetailsService;
  let httpMock: HttpTestingController;

  const sample: DoctorDetails = {
    id: 1,
    clinicName: 'Sunrise Clinic',
    doctorName: 'Dr. Rao',
    qualifications: 'MBBS, MD',
    registrationNumber: 'REG-123',
    logo: null,
    signature: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DoctorDetailsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('fetches the doctor details', () => {
    service.get().subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/doctor-details`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('sends an update request with the full payload', () => {
    const request = {
      clinicName: 'Sunrise Clinic',
      doctorName: 'Dr. Rao',
      qualifications: null,
      registrationNumber: null,
      logo: 'AAA=',
      signature: null,
    };
    service.update(request).subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/doctor-details`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });
});
