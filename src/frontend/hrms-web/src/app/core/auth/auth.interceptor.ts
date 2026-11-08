import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../config/api.config';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const accessToken = inject(AuthService).accessToken();
  const apiBaseUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  const isApiRequest = request.url === apiBaseUrl || request.url.startsWith(`${apiBaseUrl}/`);

  // Never attach HRMS credentials to unrelated origins or non-API requests.
  if (!isApiRequest) {
    return next(request);
  }

  const isAnonymousAuthRequest =
    request.url === `${apiBaseUrl}/auth/login` ||
    request.url === `${apiBaseUrl}/auth/refresh` ||
    request.url === `${apiBaseUrl}/auth/logout`;

  if (isAnonymousAuthRequest || !accessToken) {
    return next(request.clone({ withCredentials: true }));
  }

  return next(request.clone({
    setHeaders: { Authorization: `Bearer ${accessToken}` },
    withCredentials: true
  }));
};
