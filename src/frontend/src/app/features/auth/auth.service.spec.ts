import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('starts logged out when nothing is in session storage', () => {
    const service = TestBed.inject(AuthService);

    expect(service.isLoggedIn()).toBeFalse();
    expect(service.sessionToken).toBeNull();
  });

  it('logging in stores the session and flips isLoggedIn', () => {
    const service = TestBed.inject(AuthService);
    service.login('doctor', 'ChangeMe123!').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'doctor', password: 'ChangeMe123!' });
    req.flush({ username: 'doctor', sessionToken: 'abc123' });

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.username()).toBe('doctor');
    expect(service.sessionToken).toBe('abc123');
    expect(sessionStorage.getItem('pm_session')).toContain('abc123');
  });

  it('a failed login does not set a session', () => {
    const service = TestBed.inject(AuthService);
    service.login('doctor', 'wrong').subscribe({ error: () => {} });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(service.isLoggedIn()).toBeFalse();
  });

  it('logout clears the session', () => {
    const service = TestBed.inject(AuthService);
    service.login('doctor', 'ChangeMe123!').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/auth/login`).flush({ username: 'doctor', sessionToken: 'abc123' });

    service.logout();

    expect(service.isLoggedIn()).toBeFalse();
    expect(sessionStorage.getItem('pm_session')).toBeNull();
  });

  it('restores an existing session from session storage on construction', () => {
    sessionStorage.setItem('pm_session', JSON.stringify({ username: 'doctor', sessionToken: 'stored-token' }));

    const restored = TestBed.inject(AuthService);

    expect(restored.isLoggedIn()).toBeTrue();
    expect(restored.sessionToken).toBe('stored-token');
  });
});
