import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Patient } from '../../patients/patient.service';
import { PatientSearchBarComponent } from './patient-search-bar.component';

describe('PatientSearchBarComponent', () => {
  let fixture: ComponentFixture<PatientSearchBarComponent>;
  let httpMock: HttpTestingController;

  const results: Patient[] = [
    {
      patientId: 5,
      name: 'Alexandra Smith',
      age: 34,
      dateOfBirth: '1990-05-14',
      gender: 'Female',
      phone: '9876543210',
      allergies: null,
      currentMedications: null,
      chronicConditions: null,
      emergencyContactName: null,
      emergencyContactPhone: null,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientSearchBarComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(PatientSearchBarComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('does not call the API when both fields are empty', () => {
    fixture.componentInstance.search();
    httpMock.expectNone(`${environment.apiUrl}/patients/search`);
    expect(fixture.componentInstance.results()).toBeNull();
  });

  it('searches by name and renders the matching patients', () => {
    fixture.componentInstance.form.setValue({ name: 'andra', phone: '' });
    fixture.componentInstance.search();

    const req = httpMock.expectOne(`${environment.apiUrl}/patients/search?name=andra`);
    req.flush(results);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Alexandra Smith');
  });

  it('shows a no-results message for an empty result set', () => {
    fixture.componentInstance.form.setValue({ name: 'zzz', phone: '' });
    fixture.componentInstance.search();

    httpMock.expectOne(`${environment.apiUrl}/patients/search?name=zzz`).flush([]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('No matching patients');
  });

  it('clear resets the form and the results', () => {
    fixture.componentInstance.form.setValue({ name: 'andra', phone: '' });
    fixture.componentInstance.search();
    httpMock.expectOne(`${environment.apiUrl}/patients/search?name=andra`).flush(results);
    fixture.detectChanges();

    fixture.componentInstance.clear();
    fixture.detectChanges();

    expect(fixture.componentInstance.form.value.name).toBe('');
    expect(fixture.componentInstance.results()).toBeNull();
  });

  it('shows an error message when the search fails', () => {
    fixture.componentInstance.form.setValue({ name: 'andra', phone: '' });
    fixture.componentInstance.search();

    httpMock
      .expectOne(`${environment.apiUrl}/patients/search?name=andra`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toBe('Could not search patients.');
  });
});
