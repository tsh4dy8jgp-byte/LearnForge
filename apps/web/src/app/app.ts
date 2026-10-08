import { Session } from './authentication/session';
import { Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiClient } from './http/api-client';
import { message } from './http/api-error';
import { SiteSettingsStore } from './site/site-settings-store';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  protected readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  protected readonly site = inject(SiteSettingsStore);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  protected readonly error = signal('');
  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd) this.document.getElementById('main')?.focus();
    });
  }
  protected async logout() {
    try {
      await this.api.post('/auth/logout');
      await this.session.clear();
      await this.router.navigateByUrl('/sign-in');
    } catch (e) {
      this.error.set(message(e));
    }
  }
}
