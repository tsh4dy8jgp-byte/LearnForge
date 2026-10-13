import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { toApiError } from './api-error';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
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
  async file(method: 'GET' | 'POST', path: string, body?: unknown): Promise<Blob> {
    try { return await firstValueFrom(this.http.request(method, '/api' + path, { body, responseType: 'blob' })); }
    catch (error) {
      if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
        try {
          const details = JSON.parse(await error.error.text());
          throw toApiError(new HttpErrorResponse({ error: details, status: error.status }));
        } catch (parsed) { if (parsed instanceof Error && parsed.name !== 'SyntaxError') throw parsed; }
      }
      throw toApiError(error);
    }
  }
  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    try {
      return (await firstValueFrom(this.http.request<T>(method, '/api' + path, { body }))) as T;
    } catch (e) {
      throw toApiError(e);
    }
  }
}
