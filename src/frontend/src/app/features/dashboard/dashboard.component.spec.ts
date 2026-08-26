import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    // RecentPatientsListComponent fires a request from ngOnInit as soon as
    // the dashboard renders -- flush it so every test starts from a settled view.
    httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=5`).flush([]);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('welcomes the doctor to the clinic', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Welcome to the Clinic');
  });

  it('hosts the patient search bar', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Search Patients');
  });

  it('hosts the recent patients list', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Recent Patients');
  });

  it('links to the export page', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[routerlink="/export"]');
    expect(link).toBeTruthy();
    expect(link!.textContent).toContain('Export Data');
  });
});
