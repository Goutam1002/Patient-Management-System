import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import {
  Appointment,
  AppointmentService,
  MANUALLY_SETTABLE_STATUSES,
  WalkInVisit,
  toDateParam,
} from './appointment.service';

describe('AppointmentService', () => {
  let service: AppointmentService;
  let httpMock: HttpTestingController;

  const sample: Appointment = {
    id: 1,
    patientId: 5,
    patientName: 'Alice',
    scheduledTime: '2026-03-02T09:00:00',
    durationMinutes: 45,
    status: 'Scheduled',
    visitId: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppointmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates an appointment with the duration the caller supplied', () => {
    const request = { patientId: 5, scheduledTime: '2026-03-02T09:00:00', durationMinutes: 45 };
    service.create(request).subscribe((result) => expect(result).toEqual(sample));

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments`);
    expect(req.request.method).toBe('POST');
    // The duration goes over the wire exactly as entered -- the client adds no default.
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('fetches the merged daily list for a date', () => {
    service.getDaily('2026-03-02').subscribe((result) => expect(result).toEqual([sample]));

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/daily?date=2026-03-02`);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);
  });

  it('updates an appointment status', () => {
    service.updateStatus(1, 'Cancelled').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/1/status`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ status: 'Cancelled' });
    req.flush({ ...sample, status: 'Cancelled' });
  });

  it('registers a walk-in through the single combined endpoint', () => {
    const walkIn: WalkInVisit = { visitId: 9, appointmentId: 3, patientId: 5, visitNumber: 1 };
    service
      .createWalkIn({
        patientId: 5,
        durationMinutes: 12,
        temperature: 37,
        bpSystolic: 120,
        bpDiastolic: 80,
        pulse: 72,
        weight: 52.85,
        complaints: 'Cough',
        diagnosis: null,
      })
      .subscribe((result) => expect(result).toEqual(walkIn));

    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/walk-in`);
    expect(req.request.method).toBe('POST');
    req.flush(walkIn);
  });

  it('does not offer Completed as a manually settable status', () => {
    // FLAGGED ASSUMPTION: Completed is reachable only via visit creation.
    expect(MANUALLY_SETTABLE_STATUSES).toEqual(['Scheduled', 'Cancelled', 'NoShow']);
  });

  it('formats a date parameter in local time, not UTC', () => {
    expect(toDateParam(new Date(2026, 2, 2, 23, 30))).toBe('2026-03-02');
  });
});
