import { Session } from './session';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { SiteSettingsStore } from '../site/site-settings-store';
@Component({
  imports: [FormsModule, RouterLink],
  templateUrl: './auth-page.html',
})
export class AuthPage {
  protected readonly session = inject(Session);
  protected readonly site = inject(SiteSettingsStore);
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  protected readonly register = signal(false);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected email = '';
  protected password = '';
  protected displayName = '';
  protected async submit() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.session.refreshCsrf();
      await this.api.post('/auth/' + (this.register() ? 'register' : 'login'), {
        email: this.email,
        password: this.password,
        ...(this.register() ? { displayName: this.displayName } : {}),
      });
      await this.session.refreshSession();
      await this.router.navigateByUrl('/dashboard');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
