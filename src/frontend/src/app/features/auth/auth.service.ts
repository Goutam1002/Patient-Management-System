import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResult {
  username: string;
  sessionToken: string;
}

const STORAGE_KEY = 'pm_session';

/**
 * Session state for the single-doctor login gate. The session token is an
 * opaque, server-issued, in-memory value (see the backend's
 * ISessionTokenStore) -- there's nothing to decode client-side, it's just
 * carried on every request via the auth interceptor and forgotten on logout.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly session = signal<LoginResult | null>(readStoredSession());

  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly username = computed(() => this.session()?.username ?? null);

  get sessionToken(): string | null {
    return this.session()?.sessionToken ?? null;
  }

  login(username: string, password: string): Observable<LoginResult> {
    return this.http
      .post<LoginResult>(`${environment.apiUrl}/auth/login`, { username, password })
      .pipe(tap((result) => this.setSession(result)));
  }

  /**
   * Client-side only -- there's no server-side logout endpoint (not in
   * implementation-brd.md's fixed Authentication API surface). This forgets
   * the token locally so the guard blocks further access; the token itself
   * stays valid server-side until the API process restarts.
   */
  logout(): void {
    this.session.set(null);
    sessionStorage.removeItem(STORAGE_KEY);
  }

  private setSession(result: LoginResult): void {
    this.session.set(result);
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(result));
  }
}

function readStoredSession(): LoginResult | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as LoginResult;
  } catch {
    return null;
  }
}
