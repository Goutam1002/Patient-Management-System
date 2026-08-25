import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AppointmentFormComponent } from './appointment-form.component';

describe('AppointmentFormComponent', () => {
  let fixture: ComponentFixture<AppointmentFormComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentFormComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(AppointmentFormComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function fillValidForm(durationMinutes: string): void {
    fixture.componentInstance.form.setValue({
      patientId: '5',
      date: '2026-03-02',
      time: '09:00',
      durationMinutes,
    });
  }

  it('starts with an empty duration so nothing is silently defaulted', () => {
    expect(fixture.componentInstance.form.controls.durationMinutes.value).toBe('');
    expect(fixture.componentInstance.form.invalid).toBeTrue();
  });

  it('blocks submit and sends nothing when the duration is missing', () => {
    fixture.componentInstance.form.setValue({
      patientId: '5',
      date: '2026-03-02',
      time: '09:00',
      durationMinutes: '',
    });

    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/appointments`);
    expect(fixture.componentInstance.saving()).toBeFalse();
  });

  it('posts the doctor-entered duration and navigates to that day schedule', () => {
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate');
    fillValidForm('45');

    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments`);
    expect(req.request.body).toEqual({
      patientId: 5,
      scheduledTime: '2026-03-02T09:00:00',
      durationMinutes: 45,
    });
    req.flush({
      id: 1,
      patientId: 5,
      patientName: 'Alice',
      scheduledTime: '2026-03-02T09:00:00',
      durationMinutes: 45,
      status: 'Scheduled',
      visitId: null,
    });

    expect(navigate).toHaveBeenCalledWith(['/appointments'], {
      queryParams: { date: '2026-03-02' },
    });
  });

  it('explains a double-booking rejection when the API returns 409', () => {
    fillValidForm('30');
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/appointments`)
      .flush({ detail: 'conflict' }, { status: 409, statusText: 'Conflict' });

    expect(fixture.componentInstance.errorMessage()).toContain('already booked');
    expect(fixture.componentInstance.saving()).toBeFalse();
  });

  it('reports an unknown patient when the API returns 404', () => {
    fillValidForm('30');
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/appointments`)
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(fixture.componentInstance.errorMessage()).toBe('No patient exists with that ID.');
  });
});
