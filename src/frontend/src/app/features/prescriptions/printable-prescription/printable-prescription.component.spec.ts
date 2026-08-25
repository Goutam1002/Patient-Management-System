import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../../patients/patient.service';
import { Visit } from '../../consultation/consultation.service';
import { Prescription } from '../prescription.service';
import { PrintablePrescriptionComponent } from './printable-prescription.component';

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
  items: [{ id: 1, drugName: 'Paracetamol', dosage: '500mg', frequency: 'twice daily', duration: '5 days', instructions: 'After food' }],
};

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

const patient: Patient = {
  patientId: 5,
  name: 'Alice',
  age: 34,
  dateOfBirth: null,
  gender: 'Female',
  phone: '9876543210',
  allergies: null,
  currentMedications: null,
  chronicConditions: null,
  emergencyContactName: null,
  emergencyContactPhone: null,
};

describe('PrintablePrescriptionComponent', () => {
  let fixture: ComponentFixture<PrintablePrescriptionComponent>;
  let httpMock: HttpTestingController;

  function createAndLoad(): void {
    fixture = TestBed.createComponent(PrintablePrescriptionComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/prescriptions/7`).flush(prescription);
    httpMock.expectOne(`${environment.apiUrl}/visits/9`).flush(visit);
    httpMock.expectOne(`${environment.apiUrl}/patients/5`).flush(patient);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrintablePrescriptionComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('composes the prescription, visit, and patient into the required header/patient/vitals/diagnosis/meds/footer sections', () => {
    createAndLoad();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sunrise Clinic'); // header
    expect(text).toContain('Alice'); // patient
    expect(text).toContain('Temp 37'); // vitals
    expect(text).toContain('URI'); // diagnosis
    expect(text).toContain('Paracetamol'); // medications
    expect(text).toContain('Signature'); // footer
  });

  it('shows an error message when any of the three loads fails', () => {
    fixture = TestBed.createComponent(PrintablePrescriptionComponent);
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/prescriptions/7`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load this prescription.');
  });

  it('print() delegates to window.print()', () => {
    createAndLoad();
    const printSpy = spyOn(window, 'print');

    fixture.componentInstance.print();

    expect(printSpy).toHaveBeenCalled();
  });
});
