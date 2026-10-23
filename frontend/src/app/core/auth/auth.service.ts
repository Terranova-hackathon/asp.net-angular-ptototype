import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, firstValueFrom, of, tap } from 'rxjs';
import { Credentials, RegisterRequest, User } from './auth.models';

/**
 * Holds "who is logged in" as a signal. The JWT itself lives in an HttpOnly cookie
 * that the browser sends automatically, so this app never touches the token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user = signal<User | null>(null);
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);

  /** Runs once at startup so guards know the login state before the first navigation. */
  async restoreSession(): Promise<void> {
    const user = await firstValueFrom(
      this.http.get<User>('/api/auth/me').pipe(catchError(() => of(null))),
    );
    this._user.set(user);
  }

  login(credentials: Credentials): Observable<User> {
    return this.http
      .post<User>('/api/auth/login', credentials)
      .pipe(tap((user) => this._user.set(user)));
  }

  register(request: RegisterRequest): Observable<User> {
    return this.http
      .post<User>('/api/auth/register', request)
      .pipe(tap((user) => this._user.set(user)));
  }

  logout(): void {
    this.http.post('/api/auth/logout', {}).subscribe({ complete: () => this.clearSession() });
  }

  /** Called when the server says our cookie is no longer valid. */
  clearSession(): void {
    this._user.set(null);
    this.router.navigate(['/login']);
  }
}
