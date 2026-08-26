import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../features/auth/auth.service';
import { AppHeaderComponent } from './app-header.component';

describe('AppHeaderComponent', () => {
  let fixture: ComponentFixture<AppHeaderComponent>;
  let httpMock: HttpTestingController;

  function create(): void {
    fixture = TestBed.createComponent(AppHeaderComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AppHeaderComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('renders the doctor-uploaded logo when one exists', () => {
    create();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: 'Test Clinic',
      doctorName: 'Dr. Test',
      qualifications: null,
      registrationNumber: null,
      logo: 'AAAA',
      signature: null,
    });
    fixture.detectChanges();

    const img = (fixture.nativeElement as HTMLElement).querySelector('img');
    expect(img).toBeTruthy();
    expect(img!.getAttribute('src')).toBe('data:image/*;base64,AAAA');
  });

  it('shows a placeholder icon when no logo has been uploaded', () => {
    create();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: 'Test Clinic',
      doctorName: 'Dr. Test',
      qualifications: null,
      registrationNumber: null,
      logo: null,
      signature: null,
    });
    fixture.detectChanges();

    const img = (fixture.nativeElement as HTMLElement).querySelector('img');
    expect(img).toBeFalsy();
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('🏥');
  });

  it('shows the Dashboard, Patients, and Appointments nav links', () => {
    create();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: '',
      doctorName: '',
      qualifications: null,
      registrationNumber: null,
      logo: null,
      signature: null,
    });

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[routerlink="/dashboard"]')).toBeTruthy();
    expect(el.querySelector('a[routerlink="/patients"]')).toBeTruthy();
    expect(el.querySelector('a[routerlink="/appointments"]')).toBeTruthy();
  });

  it('the profile icon links to /doctor-details', () => {
    create();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: '',
      doctorName: '',
      qualifications: null,
      registrationNumber: null,
      logo: null,
      signature: null,
    });

    const link = (fixture.nativeElement as HTMLElement).querySelector('a[routerlink="/doctor-details"]');
    expect(link).toBeTruthy();
  });

  it('logs out and navigates to /login when Log out is clicked', () => {
    create();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: '',
      doctorName: '',
      qualifications: null,
      registrationNumber: null,
      logo: null,
      signature: null,
    });

    const authService = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const logoutSpy = spyOn(authService, 'logout').and.callThrough();
    const navigateSpy = spyOn(router, 'navigate');

    const button = (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;
    button.click();

    expect(logoutSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });
});
