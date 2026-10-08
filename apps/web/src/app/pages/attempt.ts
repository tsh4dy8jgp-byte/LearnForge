import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Answer, Attempt, Grade, Question } from '../models';
import { QuestionInput } from '../question-input';
import { describeAnswer, describeExpected, hasResponse } from '../answers';
@Component({
  imports: [RouterLink, DecimalPipe, DatePipe, QuestionInput],
  template: ` @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
      <button class="button secondary" (click)="load()">Reload saved attempt</button>
    }
    @if (attempt(); as a) {
      <a [routerLink]="['/courses', a.packId]" class="breadcrumb">← {{ a.title }}</a>
      @if (a.status === 'completed') {
        <div class="page-heading">
          <div>
            <p class="eyebrow">A MOMENT TO REFLECT</p>
            <h1>Your results<span class="accent">.</span></h1>
            <p class="lead">
              {{ a.size }} {{ a.mode === 'mock' ? 'mock exam' : 'learning session' }} ·
              {{ a.completedAt | date: 'medium' }}
            </p>
          </div>
          <a routerLink="/dashboard" class="button secondary">Back to overview →</a>
        </div>
        @if (a.summary; as s) {
          @if (s.passed != null) {
            <div class="panel" role="status">
              <h2>{{ s.passed ? 'Mock pass' : 'Mock fail' }}</h2>
              <p>{{ s.earned }} / {{ s.possible }} points · Pass threshold: {{ s.passPoints }} points.</p>
              <p>This simulated result is separate from your exam readiness recommendation.</p>
              @if (a.timedOut) { <p>Time expired; your saved answers were submitted automatically.</p> }
            </div>
          }
          <div class="stat-grid">
            <div class="stat">
              <span>FULLY CORRECT</span
              ><strong>{{ s.correctPercent | number: '1.0-1' }}<em>%</em></strong
              ><small>{{ a.goal === 'readiness' ? 'Used for exam readiness' : 'Independent correct answers' }}</small>
            </div>
            <div class="stat">
              <span>POINTS SCORE</span><strong>{{ s.score | number: '1.0-1' }}<em>%</em></strong
              ><small
                >{{ s.earned | number: '1.0-2' }} / {{ s.possible }} points, including partial
                credit</small
              >
            </div>
            @if (a.goal === 'readiness') { <div class="stat">
              <span>FRESH QUESTIONS</span
              ><strong>{{ s.freshPercent | number: '1.0-0' }}<em>%</em></strong
              ><small>{{
                s.eligible
                  ? 'Eligible readiness evidence'
                  : a.timedOut
                    ? 'Time expired'
                    : a.mode === 'learn'
                      ? 'Learning evidence'
                      : 'Repeated question families'
              }}</small>
            </div> }
          </div>
        }
        <div class="section-heading">
          <h2>Review & understand</h2>
          <span class="muted small">Question revisions are preserved with this attempt.</span>
        </div>
        @for (q of a.questions; track q.id; let i = $index) {
          @if (a.results?.[q.id]; as g) {
            <details class="panel review-item">
              <summary>
                <span class="review-number">{{ i + 1 }}</span
                ><span class="prompt-text">{{ q.prompt }}</span
                ><span class="pill" [class.success]="g.fullyCorrect"
                  >{{ g.earned | number: '1.0-2' }} / {{ g.possible }}</span
                >
              </summary>
              <div class="review-body">
                <p><strong>Your answer:</strong> {{ describe(q, a.answers[q.id]) }}</p>
                <p><strong>Expected:</strong> {{ expected(q, g) }}</p>
                <p class="explanation">{{ g.explanation }}</p>
                <a [routerLink]="['/courses', a.packId]" class="text-link">Revisit the lesson →</a>
              </div>
            </details>
          }
        }
      } @else {
        <div class="page-heading">
          <div>
            <p class="eyebrow">
              {{ a.mode === 'mock' ? 'INDEPENDENT PRACTICE' : 'LEARN BY DOING' }}
            </p>
            <h1>
              {{
                a.focus ? 'Focused practice' : a.size === 'full' ? 'Full session' : 'Short session'
              }}
            </h1>
            <p class="muted">
              {{ answered() }} of {{ a.questions.length }} questions started ·
              {{
                a.mode === 'mock'
                  ? 'Answers released after submission'
                  : 'Check answers for immediate feedback'
              }}
            </p>
          </div>
          <div class="timer">
            <span>{{ a.mode === 'mock' ? 'TIME REMAINING' : 'UNTIMED LEARNING' }}</span
            ><strong>{{ a.mode === 'mock' ? timeLeft() : 'Take your time' }}</strong
            ><small aria-live="polite">{{
              busy() ? 'Saving…' : unsaved() ? 'Not yet saved' : 'All responses saved'
            }}</small>
          </div>
        </div>
        @if (unsaved()) {
          <div class="alert">
            An unsaved answer is stored on this device.
            <button class="text-button" [disabled]="busy()" (click)="retry()">Retry saving</button
            ><button class="text-button" (click)="discard()">Use server answer</button>
          </div>
        }
        @if (a.focus) {
          <p class="alert">
            This session targets {{ a.focus }}. It may be shorter than the usual session.
            @if (a.goal === 'readiness') { It does not count toward exam readiness. }
          </p>
        }
        <div class="exam-layout">
          <section>
            @if (current(); as q) {
              @if (scenario(); as s) {
                <aside class="scenario panel">
                  <p class="eyebrow">CASE STUDY</p>
                  <h2>{{ s.title }}</h2>
                  <p>{{ s.background }}</p>
                </aside>
              }
              <article class="panel question-card">
                <div class="row">
                  <span class="eyebrow">QUESTION {{ index() + 1 }} / {{ a.questions.length }}</span
                  ><span class="pill subtle">{{ kindLabel(q.kind) }}</span>
                </div>
                <h2 class="prompt-text">{{ q.prompt }}</h2>
                <lf-question-input
                  [question]="q"
                  [answer]="a.answers[q.id] || empty"
                  [disabled]="busy() || !!a.feedback[q.id] || !!unsaved()"
                  (changed)="save($event)"
                />
                @if (a.feedback[q.id]; as g) {
                  <div class="feedback" [class.correct]="g.fullyCorrect">
                    <strong
                      >{{ g.fullyCorrect ? 'Correct' : 'Keep building' }} ·
                      {{ g.earned | number: '1.0-2' }} / {{ g.possible }}</strong
                    >
                    <p>{{ g.explanation }}</p>
                    <p class="small">Expected: {{ expected(q, g) }}</p>
                  </div>
                }
                <div class="question-actions">
                  @if (a.mode === 'learn' && !a.feedback[q.id]) {
                    <button
                      class="button"
                      [disabled]="busy() || !!unsaved()"
                      (click)="save(a.answers[q.id] || empty, true)"
                    >
                      Check answer
                    </button>
                  }
                  <div class="row">
                    <button
                      class="button secondary"
                      [disabled]="busy() || !!unsaved() || !canGo(index() - 1)"
                      (click)="go(index() - 1)"
                    >
                      ← Previous</button
                    ><button
                      class="button secondary"
                      [disabled]="busy() || !!unsaved() || !canGo(index() + 1)"
                      (click)="go(index() + 1)"
                    >
                      Next →
                    </button>
                  </div>
                </div>
              </article>
            }
          </section>
          <aside class="panel exam-nav">
            <p class="eyebrow">YOUR SESSION</p>
            <div class="question-grid">
              @for (q of a.questions; track q.id; let i = $index) {
                <button
                  [class.current]="index() === i"
                  [class.answered]="isAnswered(q.id)"
                  [disabled]="busy() || !!unsaved() || !canGo(i)"
                  [attr.aria-label]="'Question ' + (i + 1) + (isAnswered(q.id) ? ', answered' : '')"
                  [attr.aria-current]="index() === i ? 'step' : null"
                  (click)="go(i)"
                >
                  {{ i + 1 }}
                </button>
              }
            </div>
            <p class="muted small">
              Filled numbers have a saved response. You can leave and resume from your attempt
              history.
            </p>
            @if (a.lockSections && a.sectionIndex < a.sections.length - 1) {
              <button
                class="button secondary wide"
                [disabled]="busy() || !!unsaved()"
                (click)="nextSection()"
              >
                Finish this section →
              </button>
              <p class="muted small">Completed sections cannot be reopened during this attempt.</p>
            }
            <button
              class="button wide"
              [disabled]="busy() || !!unsaved()"
              (click)="confirmSubmit.set(true)"
            >
              Submit session
            </button>
            @if (confirmSubmit()) {
              <div class="confirm-box" role="alert">
                <p>Submit all saved responses? Unanswered questions receive zero points.</p>
                <button class="button wide" [disabled]="busy()" (click)="submit()">
                  Confirm submission</button
                ><button class="text-button" (click)="confirmSubmit.set(false)">
                  Keep working
                </button>
              </div>
            }
          </aside>
        </div>
      }
    } @else if (!error()) {
      <p class="empty">Loading your saved session…</p>
    }`,
})
export class AttemptPage {
  private readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
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
  readonly scenario = computed(() =>
    this.attempt()?.scenarios.find((s) => s.id === this.current()?.scenarioId),
  );
  readonly answered = computed(
    () => Object.values(this.attempt()?.answers || {}).filter(hasResponse).length,
  );
  private readonly offset = signal(0);
  private refreshExpired = false;
  readonly timeLeft = computed(() => {
    const a = this.attempt();
    const seconds = Math.max(
      0,
      Math.ceil((Date.parse(a?.deadline || '') - this.now() - this.offset()) / 1000) || 0,
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
    return 'learnforge.draft.' + this.api.user()?.id + '.' + this.route.snapshot.paramMap.get('id');
  }
  private accept(a: Attempt) {
    const clientTime = Date.now();
    this.offset.set(Date.parse(a.serverTime) - clientTime);
    this.now.set(clientTime);
    this.attempt.set(a);
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
      const a = await this.api.get<Attempt>(
        '/me/attempts/' + this.route.snapshot.paramMap.get('id'),
      );
      this.accept(a);
      const raw = localStorage.getItem(this.key());
      if (raw && a.status === 'inProgress') {
        const draft = JSON.parse(raw);
        this.unsaved.set(draft);
        this.index.set(a.questions.findIndex((q) => q.id === draft.questionId));
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
  kindLabel(kind: string) {
    return (
      (
        {
          single: 'Single choice',
          multiple: 'Select multiple',
          sequence: 'Sequence',
          matching: 'Drag & match',
          dropdown: 'Dropdown blanks',
          numeric: 'Numeric answer',
          codeOutput: 'Program output',
        } as Record<string, string>
      )[kind] || kind
    );
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
  describe(q: Question, a?: Answer): string {
    return describeAnswer(q, a);
  }
  expected(q: Question, g: Grade) {
    return describeExpected(q, g);
  }
}
