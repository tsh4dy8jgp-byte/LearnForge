import { Injectable, signal } from '@angular/core';
import { User } from './models';
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}
@Injectable({ providedIn: 'root' })
export class Api {
  readonly user = signal<User | null>(null);
  readonly unavailable = signal(false);
  private csrf = '';
  async initialize() {
    try {
      await this.refreshCsrf();
      this.user.set(await this.get<User>('/auth/me'));
    } catch (e) {
      if (!(e instanceof ApiError && e.status === 401)) this.unavailable.set(true);
    }
  }
  async refreshCsrf() {
    this.csrf = (await this.get<{ token: string }>('/auth/csrf')).token;
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
    const response = await fetch('/api' + path, {
      method,
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        ...(method !== 'GET' ? { 'X-CSRF-TOKEN': this.csrf } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      const data = await response.json().catch(() => ({}));
      throw new ApiError(
        data.detail ||
          (response.status === 401
            ? 'Sign in to continue.'
            : 'The request could not be completed.'),
        response.status,
      );
    }
    if (response.status === 204 || response.headers.get('content-length') === '0')
      return undefined as T;
    const text = await response.text();
    return text ? (JSON.parse(text) as T) : (undefined as T);
  }
}
export const message = (e: unknown) =>
  e instanceof Error ? e.message : 'Something went wrong. Please try again.';
