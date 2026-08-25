import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Appointment } from '../appointment.service';
import { DailyScheduleComponent } from './daily-schedule.component';

describe('DailyScheduleComponent', () => {
  let fixture: ComponentFixture<DailyScheduleComponent>;
  let httpMock: HttpTestingController;

  const scheduled: Appointment = {
    id: 1,
    patientId: 5,
    patientName: 'Alice',
    scheduledTime: '2026-03-02T09:00:00',
    durationMinutes: 45,
    status: 'Scheduled',
    visitId: null,
  };

  const walkIn: Appointment = {
    id: 2,
    patientId: 6,
    patientName: 'Bob',
    scheduledTime: '2026-03-02T11:30:00',
    durationMinutes: 10,
    status: 'Completed',
    visitId: 77,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DailyScheduleComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ date: '2026-03-02' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DailyScheduleComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function loadWith(appointments: Appointment[]): void {
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/appointments/daily?date=2026-03-02`)
      .flush(appointments);
    fixture.detectChanges();
  }

  it('shows scheduled and walk-in entries together in one time-ordered list', () => {
    loadWith([scheduled, walkIn]);

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Alice');
    expect(rows[1].textContent).toContain('Bob');
    // The walk-in entry is in the same table, marked by having a visit already.
    expect(rows[1].textContent).toContain('Visit recorded');
  });

  it('offers a status control for open appointments but not for completed ones', () => {
    loadWith([scheduled, walkIn]);

    const selects = (fixture.nativeElement as HTMLElement).querySelectorAll('select');
    // FLAGGED ASSUMPTION: Completed rows get a read-only badge, no control,
    // because Completed can only be reached by recording a visit.
    expect(selects.length).toBe(1);
    const options = Array.from(selects[0].querySelectorAll('option')).map((o) => o.textContent);
    expect(options).toEqual(['Scheduled', 'Cancelled', 'NoShow']);
  });

  it('sends a status change and replaces the row with the server response', () => {
    loadWith([scheduled]);

    fixture.componentInstance.changeStatus(scheduled, 'Cancelled');
    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/1/status`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ status: 'Cancelled' });
    req.flush({ ...scheduled, status: 'Cancelled' });

    expect(fixture.componentInstance.appointments()[0].status).toBe('Cancelled');
    expect(fixture.componentInstance.updatingId()).toBeNull();
  });

  it('links a scheduled, no-visit appointment straight to Start Consultation', () => {
    loadWith([scheduled, walkIn]);

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    const startLink = rows[0].querySelector<HTMLAnchorElement>('a.btn');
    expect(startLink?.textContent).toContain('Start Consultation');
    expect(startLink?.getAttribute('href')).toBe('/appointments/1/consultation');

    // The already-completed walk-in links to the recorded visit instead.
    const visitLink = rows[1].querySelector<HTMLAnchorElement>('a.badge');
    expect(visitLink?.getAttribute('href')).toBe('/visits/77');
  });

  it('shows a message when the day is empty', () => {
    loadWith([]);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No appointments for this date.',
    );
  });

  it('shows an error when loading fails', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/appointments/daily?date=2026-03-02`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe(
      'Could not load the schedule for this date.',
    );
  });

  it('shows an error when a status update fails', () => {
    loadWith([scheduled]);

    fixture.componentInstance.changeStatus(scheduled, 'Cancelled');
    httpMock
      .expectOne(`${environment.apiUrl}/appointments/1/status`)
      .flush({ message: 'error' }, { status: 400, statusText: 'Bad Request' });

    expect(fixture.componentInstance.errorMessage()).toBe(
      'Could not update the appointment status.',
    );
    expect(fixture.componentInstance.appointments()[0].status).toBe('Scheduled');
  });
});
