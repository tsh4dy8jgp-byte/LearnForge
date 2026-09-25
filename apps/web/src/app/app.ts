import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api, message } from './api';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly error = signal('');
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
