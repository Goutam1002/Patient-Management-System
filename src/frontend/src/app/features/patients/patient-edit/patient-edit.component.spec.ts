import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../patient.service';
import { PatientEditComponent } from './patient-edit.component';

describe('PatientEditComponent', () => {
  let fixture: ComponentFixture<PatientEditComponent>;
  let httpMock: HttpTestingController;

  const existing: Patient = {
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

  function createAndLoad(patient: Patient = existing): void {
    fixture = TestBed.createComponent(PatientEditComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(patient);
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientEditComponent],
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

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the existing patient into the form on init', () => {
    createAndLoad();

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      name: 'Alice',
      age: '34',
      dateOfBirth: '1990-05-14',
      gender: 'Female',
      phone: '9876543210',
      allergies: '',
      currentMedications: '',
      chronicConditions: '',
      emergencyContactName: '',
      emergencyContactPhone: '',
    });
  });

  it('submits the mapped request and navigates back to the profile', () => {
    createAndLoad();
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture.componentInstance.form.patchValue({ name: 'Alice Renamed' });
    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.name).toBe('Alice Renamed');
    req.flush({ ...existing, name: 'Alice Renamed' });

    expect(router.navigate).toHaveBeenCalledWith(['/patients', 5]);
  });

  it('shows an error message when loading fails', () => {
    fixture = TestBed.createComponent(PatientEditComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(`${environment.apiUrl}/patients/5`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load patient.');
  });

  it('shows an error message when saving fails', () => {
    createAndLoad();

    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/patients/5`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not save patient.');
  });
});
