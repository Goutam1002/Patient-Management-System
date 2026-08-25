import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Prescription } from '../prescription.service';
import { PrescriptionFormComponent } from './prescription-form.component';

const created: Prescription = {
  id: 7,
  visitId: 9,
  createdAt: '2026-04-01T09:15:00',
  clinicName: 'Sunrise Clinic',
  doctorName: 'Dr. Rao',
  qualifications: null,
  registrationNumber: null,
  logo: null,
  signature: null,
  items: [{ id: 1, drugName: 'Paracetamol', dosage: '500mg', frequency: null, duration: null, instructions: null }],
};

describe('PrescriptionFormComponent', () => {
  let fixture: ComponentFixture<PrescriptionFormComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrescriptionFormComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ visitId: '9' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(PrescriptionFormComponent);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('starts with exactly one medicine line item, one screen -- no separate "add a medicine" round-trip needed to submit', () => {
    expect(fixture.componentInstance.items.length).toBe(1);
  });

  it('blocks submit when a drug name is missing -- no request is issued', () => {
    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/visits/9/prescriptions`);
    expect(fixture.componentInstance.items.invalid).toBeTrue();
  });

  it('addItem/removeItem add and remove line-item rows, and the last row cannot be removed', () => {
    fixture.componentInstance.addItem();
    expect(fixture.componentInstance.items.length).toBe(2);

    fixture.componentInstance.removeItem(1);
    expect(fixture.componentInstance.items.length).toBe(1);

    fixture.componentInstance.removeItem(0);
    expect(fixture.componentInstance.items.length).toBe(1); // last row stays
  });

  it('submits every line item against the visit-nested route and navigates to the printable view', () => {
    const navigateSpy = spyOn(router, 'navigate');
    fixture.componentInstance.itemGroup(0).setValue({
      drugName: 'Paracetamol',
      dosage: '500mg',
      frequency: 'twice daily',
      duration: '5 days',
      instructions: 'After food',
    });

    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/9/prescriptions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      items: [
        {
          drugName: 'Paracetamol',
          dosage: '500mg',
          frequency: 'twice daily',
          duration: '5 days',
          instructions: 'After food',
        },
      ],
    });
    req.flush(created);

    expect(navigateSpy).toHaveBeenCalledWith(['/prescriptions', 7]);
  });

  it('shows an error message when saving fails', () => {
    fixture.componentInstance.itemGroup(0).controls.drugName.setValue('Paracetamol');
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/visits/9/prescriptions`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not save the prescription.');
  });
});
