import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('does not add a session-token header when logged out', () => {
    http.get('/api/whatever').subscribe();

    const req = httpMock.expectOne('/api/whatever');
    expect(req.request.headers.has('X-Session-Token')).toBeFalse();
    req.flush({});
  });

  it('adds the session-token header once logged in', () => {
    sessionStorage.setItem('pm_session', JSON.stringify({ username: 'doctor', sessionToken: 'abc123' }));
    // AuthService reads session storage at construction, so it must be
    // injected only after the storage is seeded.
    TestBed.inject(AuthService);

    http.get('/api/whatever').subscribe();

    const req = httpMock.expectOne('/api/whatever');
    expect(req.request.headers.get('X-Session-Token')).toBe('abc123');
    req.flush({});
  });
});
