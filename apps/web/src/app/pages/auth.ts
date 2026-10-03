import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Site } from '../site-settings';
@Component({
  imports: [FormsModule, RouterLink],
  template: ` <div class="auth-layout">
    <section>
      <p class="eyebrow">WELCOME TO {{ site.settings().name }}</p>
      <h1>Make room for<br /><em>what comes next.</em></h1>
      <p class="lead">
        A home for your learning. Explore a subject, practise with purpose and see how far you have
        come.
      </p>
      <div class="feature-list">
        <p><span>01</span> Connected lessons and objectives</p>
        <p><span>02</span> Practice that builds understanding</p>
        <p><span>03</span> Your progress, clearly explained</p>
      </div>
      <a routerLink="/courses" class="text-link">Explore the learning library →</a>
    </section>
    <form class="panel auth-card" (ngSubmit)="submit()">
      <p class="eyebrow">YOUR LEARNING STARTS HERE</p>
      <h2>{{ register() ? 'Create your account' : 'Welcome back' }}</h2>
      @if (error()) {
        <p class="alert error" role="alert">{{ error() }}</p>
      }
      @if (register()) {
        <label
          >Display name<input
            name="displayName"
            [(ngModel)]="displayName"
            required
            maxlength="80"
            autocomplete="name"
        /></label>
      }
      <label
        >Email<input
          name="email"
          type="email"
          [(ngModel)]="email"
          required
          maxlength="254"
          autocomplete="email"
      /></label>
      <label
        >Password<input
          name="password"
          type="password"
          [(ngModel)]="password"
          required
          minlength="12"
          maxlength="128"
          [attr.autocomplete]="register() ? 'new-password' : 'current-password'"
      /></label>
      @if (register()) {
        <p class="muted small">
          Use at least 12 characters, with uppercase and lowercase letters and a number.
        </p>
      }
      <button class="button wide" [disabled]="busy()">
        {{ busy() ? 'Please wait…' : register() ? 'Create account' : 'Sign in' }}
      </button>
      <p class="muted small">
        {{ register() ? 'Already have an account?' : 'New to ' + site.settings().name + '?' }}
        <button
          type="button"
          class="text-button"
          (click)="register.set(!register()); error.set('')"
        >
          {{ register() ? 'Sign in' : 'Create an account' }}
        </button>
      </p>
    </form>
  </div>`,
})
export class AuthPage {
  readonly site = inject(Site);
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly register = signal(false);
  readonly error = signal('');
  readonly busy = signal(false);
  email = '';
  password = '';
  displayName = '';
  async submit() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.refreshCsrf();
      await this.api.post('/auth/' + (this.register() ? 'register' : 'login'), {
        email: this.email,
        password: this.password,
        ...(this.register() ? { displayName: this.displayName } : {}),
      });
      await this.api.refreshSession();
      await this.router.navigateByUrl('/dashboard');
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
