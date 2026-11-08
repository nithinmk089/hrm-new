import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const accessToken = inject(AuthService).accessToken();

  if (!accessToken || request.url.includes('/auth/login') || request.url.includes('/auth/refresh')) {
    return next(request);
  }

  return next(request.clone({
    setHeaders: { Authorization: `Bearer ${accessToken}` },
    withCredentials: true
  }));
};
