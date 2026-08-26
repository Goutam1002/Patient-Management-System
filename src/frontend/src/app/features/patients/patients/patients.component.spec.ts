import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { PatientsComponent } from './patients.component';

describe('PatientsComponent', () => {
  let fixture: ComponentFixture<PatientsComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientsComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(PatientsComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    // RecentPatientsListComponent fires a request from ngOnInit as soon as
    // the page renders -- flush it so every test starts from a settled view.
    httpMock.expectOne(`${environment.apiUrl}/patients/recent?count=5`).flush([]);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('has a Patients heading', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Patients');
  });

  it('hosts the patient search bar', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Search Patients');
  });

  it('hosts the recent patients list', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Recent Patients');
  });

  it('has an Add Patient button linking to /patients/new', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[routerlink="/patients/new"]');
    expect(link).toBeTruthy();
    expect(link!.textContent).toContain('Add Patient');
  });
});
