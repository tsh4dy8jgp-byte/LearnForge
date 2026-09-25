import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { User } from './models';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

// Turns HTTP failures, including .NET validation problems, into one readable message.
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (!(error instanceof HttpErrorResponse)) return new ApiError('The request could not be completed.', 0);
  const data = (error.error ?? {}) as { detail?: string; errors?: Record<string, string[]> };
  const fieldErrors = data.errors ? Object.values(data.errors).flat().join(' ') : '';
  return new ApiError(
    data.detail ||
      fieldErrors ||
      (error.status === 401 ? 'Sign in to continue.' : 'The request could not be completed.'),
    error.status,
  );
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  readonly user = signal<User | null>(null);
  readonly unavailable = signal(false);
  csrfToken = '';
  async initialize() {
    try {
      await this.refreshCsrf();
      this.user.set(await this.get<User>('/auth/me'));
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) this.unavailable.set(true);
    }
  }
  async refreshCsrf() {
    this.csrfToken = (await this.get<{ token: string }>('/auth/csrf')).token;
  }
  async refreshSession() {
    this.user.set(await this.get<User>('/auth/me'));
    await this.refreshCsrf();
    this.unavailable.set(false);
  }
  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }
  post<T>(path: string, body: unknown = {}): Promise<T> {
    return this.request<T>('POST', path, body);
  }
  put<T>(path: string, body: unknown = {}): Promise<T> {
    return this.request<T>('PUT', path, body);
  }
  delete<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('DELETE', path, body);
  }
  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    try {
      return (await firstValueFrom(this.http.request<T>(method, '/api' + path, { body }))) as T;
    } catch (e) {
      throw toApiError(e);
    }
  }
}

// Adds the antiforgery token to unsafe API requests, including those made by httpResource.
export const csrfInterceptor: HttpInterceptorFn = (request, next) =>
  request.method === 'GET' || request.method === 'HEAD' || !request.url.startsWith('/api')
    ? next(request)
    : next(request.clone({ setHeaders: { 'X-CSRF-TOKEN': inject(Api).csrfToken } }));

export const message = (e: unknown) =>
  e instanceof HttpErrorResponse || e instanceof ApiError
    ? toApiError(e).message
    : e instanceof Error
      ? e.message
      : 'Something went wrong. Please try again.';
