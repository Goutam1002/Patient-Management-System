import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../../patients/patient.service';
import { VisitSummary } from '../patient-history.service';
import { VisitHistoryListComponent } from './visit-history-list.component';

describe('VisitHistoryListComponent', () => {
  let fixture: ComponentFixture<VisitHistoryListComponent>;
  let httpMock: HttpTestingController;

  const patient: Patient = {
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

  const visits: VisitSummary[] = [
    { id: 3, patientId: 5, visitNumber: 2, visitDate: '2026-03-10T09:00:00', diagnosis: 'Bronchitis' },
    { id: 1, patientId: 5, visitNumber: 1, visitDate: '2026-01-05T09:00:00', diagnosis: 'URI' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VisitHistoryListComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '5' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(VisitHistoryListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function flushInitialLoad(): void {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(patient);
    httpMock.expectOne(`${environment.apiUrl}/patients/5/visits`).flush(visits);
    fixture.detectChanges();
  }

  it('loads and lists the visits newest-first as returned by the API', () => {
    flushInitialLoad();

    expect(fixture.componentInstance.visits().length).toBe(2);
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Bronchitis');
    expect(text).toContain('URI');
    expect(text).toContain('Alice');
  });

  it('re-fetches with the from/to date range when the filter emits', () => {
    flushInitialLoad();

    fixture.componentInstance.onRangeChange({ from: '2026-01-01', to: '2026-01-31' });

    const req = httpMock.expectOne(
      `${environment.apiUrl}/patients/5/visits?from=2026-01-01&to=2026-01-31`,
    );
    req.flush([visits[1]]);
    fixture.detectChanges();

    expect(fixture.componentInstance.visits().length).toBe(1);
  });

  it('shows an empty-state message when there are no visits in range', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(patient);
    httpMock.expectOne(`${environment.apiUrl}/patients/5/visits`).flush([]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('No visits recorded');
  });

  it('shows an error message when loading fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(patient);
    httpMock
      .expectOne(`${environment.apiUrl}/patients/5/visits`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not load visit history.');
  });
});
