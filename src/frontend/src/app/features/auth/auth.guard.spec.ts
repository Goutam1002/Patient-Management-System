import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => sessionStorage.clear());

  it('allows navigation when logged in', () => {
    sessionStorage.setItem('pm_session', JSON.stringify({ username: 'doctor', sessionToken: 'abc123' }));

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, { url: '/patients' } as any),
    );

    expect(result).toBeTrue();
  });

  it('redirects to /login with a returnUrl when logged out', () => {
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, { url: '/patients' } as any),
    ) as UrlTree;

    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result)).toBe('/login?returnUrl=%2Fpatients');
  });
});
