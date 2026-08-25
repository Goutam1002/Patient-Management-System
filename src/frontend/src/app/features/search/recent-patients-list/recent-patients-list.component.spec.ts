import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { RecentPatient } from '../search.service';
import { RecentPatientsListComponent } from './recent-patients-list.component';

describe('RecentPatientsListComponent', () => {
  let fixture: ComponentFixture<RecentPatientsListComponent>;
  let httpMock: HttpTestingController;

  const recent: RecentPatient[] = [
    { patientId: 5, name: 'Alexandra Smith', phone: '9876543210', lastVisitDate: '2026-04-01T09:00:00' },
    { patientId: 6, name: 'Bob Jones', phone: null, lastVisitDate: null },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecentPatientsListComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(RecentPatientsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads and lists recent patients, showing "no visits yet" for one with none', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=5`).flush(recent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Alexandra Smith');
    expect(text).toContain('Bob Jones');
    expect(text).toContain('no visits yet');
  });

  it('shows an empty-state message when there are no patients', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=5`).flush([]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('No patients yet');
  });

  it('shows an error message when loading fails', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`${environment.apiUrl}/patients/recent?count=5`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toBe('Could not load recent patients.');
  });
});
