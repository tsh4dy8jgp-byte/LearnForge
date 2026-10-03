import { Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api, message } from './api';
import { Site } from './site-settings';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  readonly api = inject(Api);
  readonly site = inject(Site);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  readonly error = signal('');
  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd) this.document.getElementById('main')?.focus();
    });
  }
  async logout() {
    try {
      await this.api.post('/auth/logout');
      this.api.user.set(null);
      await this.api.refreshCsrf();
      await this.router.navigateByUrl('/sign-in');
    } catch (e) {
      this.error.set(message(e));
    }
  }
}
