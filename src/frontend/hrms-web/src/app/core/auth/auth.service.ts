import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuthSession, CurrentUserResponse, LoginRequest, LoginResponse } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly sessionState = signal<AuthSession | null>(null);
  private restoreRequest?: Observable<boolean>;

  readonly session = this.sessionState.asReadonly();
  readonly isAuthenticated = computed(() => !!this.sessionState()?.accessToken);
  readonly accessToken = computed(() => this.sessionState()?.accessToken ?? null);

  login(request: LoginRequest): Observable<AuthSession> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/auth/login`, request, {
      withCredentials: true
    }).pipe(
      map(response => this.toSession(response)),
      tap(session => this.sessionState.set(session)),
      switchMap(session => this.loadCurrentUser().pipe(
        map(currentUser => {
          const updatedSession = {
            ...session,
            userId: currentUser.userId,
            tenantId: currentUser.tenantId,
            tenantCode: currentUser.tenantCode,
            email: currentUser.email,
            membershipId: currentUser.membershipId
          };
          this.sessionState.set(updatedSession);
          return updatedSession;
        })
      ))
    );
  }

  refreshSession(): Observable<boolean> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/auth/refresh`, {}, {
      withCredentials: true
    }).pipe(
      map(response => this.toSession(response)),
      switchMap(session => {
        this.sessionState.set(session);
        return this.loadCurrentUser().pipe(
          map(currentUser => {
            this.sessionState.update(current => current ? {
              ...current,
              userId: currentUser.userId,
              tenantId: currentUser.tenantId,
              tenantCode: currentUser.tenantCode,
              email: currentUser.email,
              membershipId: currentUser.membershipId
            } : null);
            return true;
          })
        );
      }),
      catchError(() => {
        this.sessionState.set(null);
        return of(false);
      })
    );
  }

  restoreSession(): Observable<boolean> {
    if (this.isAuthenticated()) {
      return of(true);
    }

    if (!this.restoreRequest) {
      this.restoreRequest = this.refreshSession().pipe(
        tap(() => this.restoreRequest = undefined),
        catchError(() => {
          this.restoreRequest = undefined;
          return of(false);
        })
      );
    }

    return this.restoreRequest;
  }

  loadCurrentUser(): Observable<CurrentUserResponse> {
    return this.http.get<CurrentUserResponse>(`${this.apiBaseUrl}/auth/me`, {
      withCredentials: true
    });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/auth/logout`, {}, {
      withCredentials: true
    }).pipe(
      catchError(() => of(void 0)),
      tap(() => this.sessionState.set(null))
    );
  }

  clearSession(): void {
    this.sessionState.set(null);
  }

  private toSession(response: LoginResponse): AuthSession {
    return {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      userId: response.userId,
      displayName: response.displayName,
      tenantId: response.tenantId,
      tenantCode: response.tenantCode,
      tenantName: response.tenantName
    };
  }
}
