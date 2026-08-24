import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../patient.service';
import { PatientRegistrationFormComponent } from './patient-registration-form.component';

describe('PatientRegistrationFormComponent', () => {
  let fixture: ComponentFixture<PatientRegistrationFormComponent>;
  let httpMock: HttpTestingController;

  const created: Patient = {
    patientId: 5,
    name: 'Alice',
    age: 34,
    dateOfBirth: null,
    gender: 'Female',
    phone: null,
    allergies: null,
    currentMedications: null,
    chronicConditions: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientRegistrationFormComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(PatientRegistrationFormComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('does not submit when required fields are missing', () => {
    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/patients`);
    expect(fixture.componentInstance.form.touched).toBeTrue();
  });

  it('submits the mapped request and navigates to the new patient profile', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture.componentInstance.form.setValue({
      name: 'Alice',
      age: '34',
      dateOfBirth: '',
      gender: 'Female',
      phone: '',
      allergies: '',
      currentMedications: '',
      chronicConditions: '',
      emergencyContactName: '',
      emergencyContactPhone: '',
    });
    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Alice',
      age: 34,
      dateOfBirth: null,
      gender: 'Female',
      phone: null,
      allergies: null,
      currentMedications: null,
      chronicConditions: null,
      emergencyContactName: null,
      emergencyContactPhone: null,
    });
    req.flush(created);

    expect(router.navigate).toHaveBeenCalledWith(['/patients', 5]);
  });

  it('shows an error message when registration fails', () => {
    fixture.componentInstance.form.setValue({
      name: 'Alice',
      age: '',
      dateOfBirth: '',
      gender: 'Female',
      phone: '',
      allergies: '',
      currentMedications: '',
      chronicConditions: '',
      emergencyContactName: '',
      emergencyContactPhone: '',
    });
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/patients`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not register patient.');
  });
});
