import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { DoctorDetails } from '../doctor-details.service';
import { DoctorDetailsFormComponent } from './doctor-details-form.component';

describe('DoctorDetailsFormComponent', () => {
  let fixture: ComponentFixture<DoctorDetailsFormComponent>;
  let httpMock: HttpTestingController;

  const existing: DoctorDetails = {
    id: 1,
    clinicName: 'Sunrise Clinic',
    doctorName: 'Dr. Rao',
    qualifications: 'MBBS',
    registrationNumber: 'REG-1',
    logo: null,
    signature: null,
  };

  function createAndLoad(details: DoctorDetails = existing): void {
    fixture = TestBed.createComponent(DoctorDetailsFormComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush(details);
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DoctorDetailsFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the existing details into the form on init', () => {
    createAndLoad();

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      clinicName: 'Sunrise Clinic',
      doctorName: 'Dr. Rao',
      qualifications: 'MBBS',
      registrationNumber: 'REG-1',
    });
  });

  it('does not submit an incomplete form', () => {
    createAndLoad();
    fixture.componentInstance.form.patchValue({ clinicName: '' });

    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/doctor-details`);
    expect(fixture.componentInstance.form.touched).toBeTrue();
  });

  it('submits the form values and shows a success message', () => {
    createAndLoad();

    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/doctor-details`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      clinicName: 'Sunrise Clinic',
      doctorName: 'Dr. Rao',
      qualifications: 'MBBS',
      registrationNumber: 'REG-1',
      logo: null,
      signature: null,
    });
    req.flush(existing);

    expect(fixture.componentInstance.saving()).toBeFalse();
    expect(fixture.componentInstance.saveSucceeded()).toBeTrue();
  });

  it('shows an error message when saving fails', () => {
    createAndLoad();

    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/doctor-details`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not save clinic/doctor details.');
  });

  it('shows an error message when loading fails', () => {
    fixture = TestBed.createComponent(DoctorDetailsFormComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(`${environment.apiUrl}/doctor-details`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load clinic/doctor details.');
  });
});
