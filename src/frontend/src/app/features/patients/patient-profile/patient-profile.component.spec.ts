import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../patient.service';
import { PatientProfileComponent } from './patient-profile.component';

describe('PatientProfileComponent', () => {
  let fixture: ComponentFixture<PatientProfileComponent>;
  let httpMock: HttpTestingController;

  const existing: Patient = {
    patientId: 5,
    name: 'Alice',
    age: 34,
    dateOfBirth: '1990-05-14',
    gender: 'Female',
    phone: '9876543210',
    allergies: 'Penicillin',
    currentMedications: null,
    chronicConditions: null,
    emergencyContactName: 'Bob',
    emergencyContactPhone: '9876500000',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientProfileComponent],
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

    fixture = TestBed.createComponent(PatientProfileComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads and displays the patient', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(existing);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading()).toBeFalse();
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Alice');
    expect(text).toContain('Penicillin');
    expect(text).toContain('Bob');
  });

  it('links to the export page pre-filled for a single-patient PDF', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(existing);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a[routerlink="/export"]');
    expect(link).toBeTruthy();
    expect(link!.textContent).toContain('Export PDF');
  });

  it('shows an error message when loading fails', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/patients/5`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load patient.');
  });
});
