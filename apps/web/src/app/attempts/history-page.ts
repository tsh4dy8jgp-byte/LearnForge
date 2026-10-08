import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { AttemptSummary } from '../api-contracts';
import { SiteSettingsStore } from '../site/site-settings-store';
@Component({
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink],
  templateUrl: './history-page.html',
})
export class HistoryPage {
  protected readonly site = inject(SiteSettingsStore);
  private readonly api = inject(ApiClient);
  protected readonly attempts = signal<AttemptSummary[]>([]);
  protected readonly hasReadiness = computed(() =>
    this.attempts().some((a) => a.goal === 'readiness'),
  );
  protected readonly error = signal('');
  protected readonly query = signal('');
  protected readonly mode = signal('');
  protected readonly size = signal('');
  protected readonly filtered = computed(() =>
    this.attempts().filter(
      (a) =>
        (!this.mode() || a.mode === this.mode()) &&
        (!this.size() || a.size === this.size()) &&
        a.title.toLowerCase().includes(this.query().toLowerCase()),
    ),
  );
  constructor() {
    this.api
      .get<AttemptSummary[]>('/me/attempts')
      .then((a) => this.attempts.set(a))
      .catch((e) => this.error.set(message(e)));
  }
}
