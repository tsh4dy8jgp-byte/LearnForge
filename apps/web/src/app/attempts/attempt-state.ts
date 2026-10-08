import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { Answer, Attempt } from '../api-contracts';
import { hasResponse } from '../assessment/answer-description';
import { Session } from '../authentication/session';
import { draftKey } from './attempt-drafts';

@Injectable()
export class AttemptState {
  private readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id');
  private readonly destroy = inject(DestroyRef);
  readonly attempt = signal<Attempt | null>(null);
  readonly index = signal(0);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly confirmSubmit = signal(false);
  readonly now = signal(Date.now());
  readonly unsaved = signal<{
    questionId: string;
    answer: Answer;
    check: boolean;
    revision: number;
    requestId: string;
  } | null>(null);
  readonly empty: Answer = { selected: [], slots: {} };
  readonly current = computed(() => this.attempt()?.questions[this.index()]);
  readonly currentAnswer = computed(() => {
    const id = this.current()?.id;
    const pending = this.unsaved();
    return pending && pending.questionId === id
      ? pending.answer
      : (id && this.attempt()?.answers[id]) || this.empty;
  });
  readonly scenario = computed(() =>
    this.attempt()?.scenarios.find((s) => s.id === this.current()?.scenarioId),
  );
  readonly answered = computed(
    () => Object.values(this.attempt()?.answers || {}).filter(hasResponse).length,
  );
  private offset = 0;
  private refreshExpired = false;
  readonly timeLeft = computed(() => {
    const a = this.attempt();
    const seconds = Math.max(
      0,
      Math.ceil((Date.parse(a?.deadline || '') - this.now() - this.offset) / 1000) || 0,
    );
    return (
      Math.floor(seconds / 60)
        .toString()
        .padStart(2, '0') +
      ':' +
      (seconds % 60).toString().padStart(2, '0')
    );
  });
  constructor() {
    void this.load();
    this.startClock();
  }
  private startClock() {
    const timer = setInterval(() => {
      this.now.set(Date.now());
      const a = this.attempt();
      if (
        a?.mode === 'mock' &&
        a.status === 'inProgress' &&
        this.timeLeft() === '00:00' &&
        !this.busy() &&
        !this.refreshExpired
      ) {
        this.refreshExpired = true;
        void this.load();
      }
    }, 1000);
    this.destroy.onDestroy(() => clearInterval(timer));
  }
  private key() {
    return draftKey(this.session.user()?.id, this.id);
  }
  private accept(a: Attempt) {
    this.attempt.set(a);
    this.offset = Date.parse(a.serverTime) - Date.now();
    if (!this.canGo(this.index()))
      this.index.set(
        a.questions.findIndex(
          (q) => !a.lockSections || (q.scenarioId || 'general') === a.sections[a.sectionIndex],
        ),
      );
  }
  async load() {
    this.error.set('');
    try {
      const a = await this.api.get<Attempt>('/me/attempts/' + this.id);
      this.accept(a);
      const raw = localStorage.getItem(this.key());
      const draft = raw && a.status === 'inProgress' ? JSON.parse(raw) : null;
      const i = draft ? a.questions.findIndex((q) => q.id === draft.questionId) : -1;
      // A draft for a missing or locked question cannot be saved, so it is dropped.
      if (this.canGo(i)) {
        this.unsaved.set(draft);
        this.index.set(i);
      } else {
        this.unsaved.set(null);
        localStorage.removeItem(this.key());
      }
    } catch (e) {
      this.error.set(message(e));
    }
  }
  canGo(i: number) {
    const a = this.attempt();
    if (!a || i < 0 || i >= a.questions.length) return false;
    return (
      a.status === 'completed' ||
      !a.lockSections ||
      (a.questions[i].scenarioId || 'general') === a.sections[a.sectionIndex]
    );
  }
  go(i: number) {
    if (this.canGo(i)) this.index.set(i);
  }
  isAnswered(id: string) {
    return hasResponse(this.attempt()?.answers[id]);
  }
  async save(answer: Answer, check = false) {
    const a = this.attempt()!,
      q = this.current()!;
    this.unsaved.set({
      questionId: q.id,
      answer,
      check,
      revision: a.revision,
      requestId: crypto.randomUUID(),
    });
    try {
      localStorage.setItem(this.key(), JSON.stringify(this.unsaved()));
    } catch {
      /* Server save is still available when device storage is blocked. */
    }
    await this.retry();
  }
  async retry() {
    const pending = this.unsaved();
    if (!pending) return;
    const a = this.attempt()!;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.put<Attempt>('/me/attempts/' + a.id + '/responses', pending);
      this.accept(result);
      this.unsaved.set(null);
      localStorage.removeItem(this.key());
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  discard() {
    this.unsaved.set(null);
    localStorage.removeItem(this.key());
    this.error.set('');
  }
  async submit() {
    this.busy.set(true);
    this.error.set('');
    try {
      const a = this.attempt()!;
      this.accept(
        await this.api.post<Attempt>('/me/attempts/' + a.id + '/submit', { revision: a.revision }),
      );
      this.confirmSubmit.set(false);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async nextSection() {
    if (!confirm('Lock this section and move to the next? You cannot return during this attempt.'))
      return;
    this.busy.set(true);
    try {
      const a = this.attempt()!;
      this.accept(
        await this.api.post<Attempt>('/me/attempts/' + a.id + '/section', { revision: a.revision }),
      );
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
