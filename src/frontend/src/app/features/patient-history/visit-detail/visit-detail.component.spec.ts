import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { VisitDetail } from '../patient-history.service';
import { VisitDetailComponent } from './visit-detail.component';

describe('VisitDetailComponent', () => {
  let fixture: ComponentFixture<VisitDetailComponent>;
  let httpMock: HttpTestingController;

  const detail: VisitDetail = {
    id: 3,
    patientId: 5,
    appointmentId: 9,
    visitNumber: 2,
    visitDate: '2026-04-01T09:00:00',
    temperature: 37.2,
    bpSystolic: 120,
    bpDiastolic: 80,
    pulse: 72,
    weight: 52.85,
    complaints: 'Cough',
    diagnosis: 'URI',
    prescriptions: [
      {
        id: 11,
        visitId: 3,
        createdAt: '2026-04-01T09:15:00',
        clinicName: 'Sunrise Clinic',
        doctorName: 'Dr. Rao',
        qualifications: null,
        registrationNumber: null,
        logo: null,
        signature: null,
        items: [{ id: 1, drugName: 'Paracetamol', dosage: '500mg', frequency: null, duration: null, instructions: null }],
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VisitDetailComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ patientId: '5', visitId: '3' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(VisitDetailComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads and displays vitals, complaints, diagnosis, and prescriptions', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/visits/3`).flush(detail);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading()).toBeFalse();
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('37.2');
    expect(text).toContain('120/80');
    expect(text).toContain('Cough');
    expect(text).toContain('URI');
    expect(text).toContain('1 item(s)');
  });

  it('shows an empty-state message when the visit has no prescriptions', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/visits/3`).flush({ ...detail, prescriptions: [] });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('No prescriptions recorded');
  });

  it('shows an error message when loading fails', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/visits/3`)
      .flush({ message: 'error' }, { status: 404, statusText: 'Not Found' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load this visit.');
  });
});
