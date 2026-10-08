import { HttpClient } from '@angular/common/http';
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
  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    try {
      return (await firstValueFrom(this.http.request<T>(method, '/api' + path, { body }))) as T;
    } catch (e) {
      throw toApiError(e);
    }
  }
}
