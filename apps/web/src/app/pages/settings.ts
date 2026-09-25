import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api, message } from '../api';
@Component({
  imports: [FormsModule],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">YOUR ACCOUNT, YOUR CHOICES</p>
        <h1>Account & privacy<span class="accent">.</span></h1>
        <p class="lead">Manage your sign-in and keep control of your learning records.</p>
      </div>
    </div>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    @if (success()) {
      <p class="alert success" role="status">{{ success() }}</p>
    }
    <div class="practice-layout">
      <form class="panel" (ngSubmit)="changePassword()">
        <h2>Change password</h2>
        <p>{{ api.user()?.email }}</p>
        <label
          >Current password<input
            name="current"
            type="password"
            autocomplete="current-password"
            [(ngModel)]="currentPassword"
            required /></label
        ><label
          >New password<input
            name="new"
            type="password"
            autocomplete="new-password"
            [(ngModel)]="newPassword"
            required
            minlength="12"
            maxlength="128" /></label
        ><button class="button" [disabled]="busy()">Update password</button>
        <p class="muted small">Other sessions are invalidated within one minute.</p>
      </form>
      <div>
        <section class="panel">
          <h2>Take your progress with you</h2>
          <p>Download your attempts, released results and objective analysis as JSON.</p>
          <a href="/api/me/export" class="button secondary">Export my history ↓</a>
        </section>
        <section class="panel danger-zone">
          <h2>Delete your account</h2>
          <p>Your account, attempts and lesson completion records will be permanently removed.</p>
          <label
            >Confirm with your password<input
              type="password"
              autocomplete="current-password"
              [(ngModel)]="deletePassword" /></label
          ><button
            class="button danger"
            [disabled]="busy() || !deletePassword"
            (click)="deleteAccount()"
          >
            Delete account
          </button>
        </section>
      </div>
    </div>`,
})
export class SettingsPage {
  readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly error = signal('');
  readonly success = signal('');
  readonly busy = signal(false);
  currentPassword = '';
  newPassword = '';
  deletePassword = '';
  async changePassword() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post('/auth/password', {
        currentPassword: this.currentPassword,
        newPassword: this.newPassword,
      });
      await this.api.refreshSession();
      this.currentPassword = '';
      this.newPassword = '';
      this.success.set('Password updated.');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async deleteAccount() {
    if (!confirm('Permanently delete your account and all learning history?')) return;
    this.busy.set(true);
    try {
      await this.api.delete('/me/account', { password: this.deletePassword });
      const prefix = 'learnforge.draft.' + this.api.user()?.id + '.';
      for (const key of Object.keys(localStorage))
        if (key.startsWith(prefix)) localStorage.removeItem(key);
      this.api.user.set(null);
      await this.api.refreshCsrf();
      await this.router.navigateByUrl('/sign-in');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
