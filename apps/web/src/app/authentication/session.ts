import { inject, Injectable, signal } from '@angular/core';
import { ApiClient } from '../http/api-client';
import { ApiError } from '../http/api-error';
import { CsrfToken } from '../http/csrf-token';
import { User } from '../api-contracts';

@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(ApiClient);
  private readonly csrf = inject(CsrfToken);
  readonly user = signal<User | null>(null);
  readonly unavailable = signal(false);

  async initialize() {
    try {
      await this.refreshCsrf();
      this.user.set(await this.api.get<User>('/auth/me'));
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) this.unavailable.set(true);
    }
  }
  async refreshCsrf() {
    this.csrf.value = (await this.api.get<{ token: string }>('/auth/csrf')).token;
  }
  async refreshSession() {
    this.user.set(await this.api.get<User>('/auth/me'));
    await this.refreshCsrf();
    this.unavailable.set(false);
  }
}
