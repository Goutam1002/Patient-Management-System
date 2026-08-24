import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

const SESSION_TOKEN_HEADER = 'X-Session-Token';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).sessionToken;

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { [SESSION_TOKEN_HEADER]: token } }));
};
