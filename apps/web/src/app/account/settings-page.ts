import { Session } from '../authentication/session';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { removeDrafts } from '../attempts/attempt-drafts';
@Component({
  imports: [FormsModule, RouterLink],
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  protected readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  protected readonly error = signal('');
  protected readonly success = signal('');
  protected readonly busy = signal(false);
  protected currentPassword = '';
  protected newPassword = '';
  protected deletePassword = '';
  protected async changePassword() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post('/auth/password', {
        currentPassword: this.currentPassword,
        newPassword: this.newPassword,
      });
      await this.session.refreshSession();
      this.currentPassword = '';
      this.newPassword = '';
      this.success.set('Password updated.');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  protected async deleteAccount() {
    if (!confirm('Permanently delete your account and all learning history?')) return;
    this.busy.set(true);
    try {
      await this.api.delete('/me/account', { password: this.deletePassword });
      removeDrafts(this.session.user()?.id);
      await this.session.clear();
      await this.router.navigateByUrl('/sign-in');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
