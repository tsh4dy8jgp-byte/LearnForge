import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CsrfToken } from './csrf-token';

// Unsafe same-origin API requests must include the current antiforgery token.
export const csrfInterceptor: HttpInterceptorFn = (request, next) =>
  request.method === 'GET' || request.method === 'HEAD' || !request.url.startsWith('/api')
    ? next(request)
    : next(request.clone({ setHeaders: { 'X-CSRF-TOKEN': inject(CsrfToken).value } }));
