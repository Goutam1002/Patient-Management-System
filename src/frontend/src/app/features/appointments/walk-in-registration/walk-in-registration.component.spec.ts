import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { WalkInRegistrationComponent } from './walk-in-registration.component';

describe('WalkInRegistrationComponent', () => {
  let fixture: ComponentFixture<WalkInRegistrationComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WalkInRegistrationComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(WalkInRegistrationComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function fillValidForm(): void {
    fixture.componentInstance.form.setValue({
      patientId: '5',
      durationMinutes: '12',
      temperature: '37',
      bpSystolic: '120',
      bpDiastolic: '80',
      pulse: '72',
      weight: '52.85',
      complaints: 'Cough',
      diagnosis: '',
    });
  }

  it('blocks submit when vitals are missing', () => {
    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/appointments/walk-in`);
    expect(fixture.componentInstance.form.invalid).toBeTrue();
    expect(fixture.componentInstance.saving()).toBeFalse();
  });

  it('registers appointment and visit in a single request, with no pre-booking call', () => {
    fillValidForm();
    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/walk-in`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      patientId: 5,
      durationMinutes: 12,
      temperature: 37,
      bpSystolic: 120,
      bpDiastolic: 80,
      pulse: 72,
      weight: 52.85,
      complaints: 'Cough',
      diagnosis: null,
    });
    req.flush({ visitId: 9, appointmentId: 3, patientId: 5, visitNumber: 1 });
    fixture.detectChanges();

    // No separate POST /api/appointments was made -- httpMock.verify() in
    // afterEach would fail if anything else had been issued.
    expect(fixture.componentInstance.result()?.visitNumber).toBe(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Registered visit #1');
  });

  it('explains a slot conflict when the API returns 409', () => {
    fillValidForm();
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/appointments/walk-in`)
      .flush({ detail: 'conflict' }, { status: 409, statusText: 'Conflict' });

    expect(fixture.componentInstance.errorMessage()).toContain('already occupied');
    expect(fixture.componentInstance.result()).toBeNull();
  });
});
