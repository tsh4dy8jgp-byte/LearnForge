import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { Tab, TabContent, TabList, TabPanel, Tabs } from '@angular/aria/tabs';
import { Api, message } from '../api';
import { CourseCatalogDto, CourseProgressDto, EnrollmentStatus } from '../models';
import { MasteryBadge } from '../mastery-badge';
import { NextSteps } from '../next-steps';

interface PracticeSetup {
  blueprintId: string;
  mode: string;
  focus: string;
  objectiveId: string;
}

@Component({
  imports: [RouterLink, FormField, Tabs, TabList, Tab, TabPanel, TabContent, MasteryBadge, NextSteps],
  template: ` @if (catalog.error(); as e) {
      <p class="alert error" role="alert">{{ errorText(e) }}</p>
    }
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
      <a routerLink="/attempts" class="text-link">Find your active session →</a>
    }
    @if (course(); as c) {
      <a routerLink="/courses" class="breadcrumb">← Learning library</a>
      <div class="page-heading">
        <div>
          <p class="eyebrow">YOUR LEARNING PATH</p>
          <h1>{{ c.title }}</h1>
          <p class="lead">{{ c.description }}</p>
        </div>
        <div class="row">
          <span class="pill">{{ c.lessons.length }} lessons</span>
          @if (progress.hasValue()) {
            @switch (progress.value().enrollment) {
              @case ('active') {
                <button class="button secondary" [disabled]="busy()" (click)="setEnrollment('archived')">
                  Archive course
                </button>
              }
              @case ('archived') {
                <button class="button" [disabled]="busy()" (click)="setEnrollment('active')">
                  Restore to my courses
                </button>
              }
              @default {
                <button class="button" [disabled]="busy()" (click)="setEnrollment('active')">
                  Add to my courses
                </button>
              }
            }
          }
        </div>
      </div>
      @if (progress.hasValue() && progress.value().nextSteps.length) {
        <section class="panel">
          <p class="eyebrow">A GOOD NEXT STEP</p>
          <lf-next-steps [packId]="c.id" [steps]="progress.value().nextSteps" />
        </section>
      }
      <div ngTabs>
        <div ngTabList class="tabs" selectionMode="follow" [(selectedTab)]="selectedTab" aria-label="Course views">
          @for (t of tabs; track t.id) {
            <button type="button" ngTab [value]="t.id">{{ t.title }}</button>
          }
        </div>
        <div ngTabPanel value="learn" class="tab-panel">
          <ng-template ngTabContent>
            <div class="lesson-layout">
              <nav class="panel lesson-nav" aria-label="Course lessons">
                @for (l of c.lessons; track l.id; let i = $index) {
                  <button [class.active]="current()?.id === l.id" (click)="lessonId.set(l.id)">
                    <span>{{ completed().has(l.id) ? '✓' : (i + 1).toString().padStart(2, '0') }}</span>
                    <div>
                      <strong>{{ l.title }}</strong><small>{{ l.summary }}</small>
                      @if (revised().has(l.id)) {
                        <small class="pill subtle">Updated since you read it</small>
                      }
                    </div>
                  </button>
                }
              </nav>
              @if (current(); as l) {
                <article class="panel reading">
                  <p class="eyebrow">BUILD YOUR UNDERSTANDING</p>
                  <h2>{{ l.title }}</h2>
                  <p class="lead">{{ l.summary }}</p>
                  @for (block of l.blocks; track $index) {
                    <section [class]="'content-block ' + block.kind">
                      @if (block.title) {
                        <h3>{{ block.title }}</h3>
                      }
                      @if (block.kind === 'code') {
                        <pre><code>{{ block.text }}</code></pre>
                      } @else {
                        <p>{{ block.text }}</p>
                      }
                    </section>
                  }
                  <div class="row">
                    <span class="muted small">Reading completion is separate from assessment performance.</span>
                    @if (api.user()) {
                      @if (revised().has(l.id)) {
                        <button class="button" [disabled]="busy()" (click)="complete(l.id)">Mark as re-read</button>
                      } @else {
                        <button class="button" [disabled]="busy() || completed().has(l.id)" (click)="complete(l.id)">
                          {{ completed().has(l.id) ? 'Lesson completed ✓' : 'Mark as complete' }}
                        </button>
                      }
                    } @else {
                      <a routerLink="/sign-in" class="button">Sign in to save progress</a>
                    }
                  </div>
                </article>
              }
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="map" class="tab-panel">
          <ng-template ngTabContent>
            <div class="section-heading">
              <div>
                <h2>How the ideas connect</h2>
                <p class="muted">Follow the prerequisites, then practise the objective.</p>
              </div>
            </div>
            <div class="map-grid">
              @for (o of c.objectives; track o.id; let i = $index) {
                <article class="panel objective-card">
                  <span class="eyebrow">OBJECTIVE {{ i + 1 }}</span>
                  <h3>{{ o.title }}</h3>
                  @if (mastery().get(o.id); as m) {
                    <lf-mastery-badge [state]="m.state" [correct]="m.correct" [considered]="m.considered" [reviewDue]="m.reviewDue" />
                  }
                  <p class="muted small">
                    {{ o.prerequisites.length ? 'Builds on: ' + prerequisiteNames(o.prerequisites) : 'A starting point — no prerequisites' }}
                  </p>
                  @for (l of c.lessons; track l.id) {
                    @if (l.objectiveIds.includes(o.id)) {
                      <button class="text-button" (click)="openLesson(l.id)">Study: {{ l.title }} →</button>
                    }
                  }
                  @if (api.user()) {
                    <button class="text-button" (click)="practise(o.id)">Practise this objective →</button>
                  }
                </article>
              }
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="practice" class="tab-panel">
          <ng-template ngTabContent>
            <div class="practice-layout">
              <form class="panel" (submit)="start($event)">
                <p class="eyebrow">PUT YOUR KNOWLEDGE TO WORK</p>
                <h2>Shape your session</h2>
                <label
                  >Session length<select [formField]="setupForm.blueprintId">
                    @for (b of c.blueprints; track b.id) {
                      <option [value]="b.id" [selected]="b.id === setup().blueprintId">
                        {{ b.title }} · {{ b.count }} questions · {{ b.minutes }} minutes
                      </option>
                    }
                  </select></label
                >
                <label
                  >Feedback mode<select [formField]="setupForm.mode">
                    <option value="learn">Learn — check answers as you go</option>
                    <option value="mock">Mock exam — results after submission</option>
                  </select></label
                >
                @if (setup().mode === 'learn') {
                  <label
                    >Practice focus<select [formField]="setupForm.focus">
                      <option value="">Balanced practice</option>
                      <option value="weak">Weakest objectives</option>
                      <option value="mistakes">Previous mistakes</option>
                      <option value="objective">One objective</option>
                    </select></label
                  >
                  @if (setup().focus === 'objective') {
                    <label
                      >Objective<select [formField]="setupForm.objectiveId">
                        <option value="">Choose an objective…</option>
                        @for (o of c.objectives; track o.id) {
                          <option [value]="o.id" [selected]="o.id === setup().objectiveId">{{ o.title }}</option>
                        }
                      </select></label
                    >
                  }
                }
                <p class="muted">
                  {{
                    setup().mode === 'mock'
                      ? 'The timer starts when you begin. Your responses save automatically; a refresh will not reset the deadline.'
                      : 'Take your time. Check each answer and learn from the explanation. Learning sessions build mastery evidence but do not count toward exam readiness.'
                  }}
                </p>
                @if (api.user()) {
                  <button class="button" [disabled]="busy()">{{ busy() ? 'Preparing…' : 'Begin session →' }}</button>
                } @else {
                  <a routerLink="/sign-in" class="button">Sign in to practise</a>
                }
              </form>
              <aside class="recommendation">
                <p class="eyebrow">PRACTICE WITH PURPOSE</p>
                <h2>Understanding comes<br />from doing.</h2>
                <p>
                  Every session draws from this course's question bank. You will see different ways to
                  demonstrate the same ideas.
                </p>
                <div class="feature-list">
                  <p>Single and multiple choice</p>
                  <p>Matching and dropdown blanks</p>
                  <p>Sequences and case studies</p>
                </div>
                <p class="small">
                  Focused practice may be shorter when your history contains fewer relevant questions.
                </p>
              </aside>
            </div>
          </ng-template>
        </div>
        <div ngTabPanel value="sources" class="tab-panel">
          <ng-template ngTabContent>
            <div class="panel">
              <h2>Sources & attribution</h2>
              <p>Content license: {{ c.license }} · Release {{ c.version }}</p>
              @for (s of c.sources; track s.url) {
                <p>
                  <a [href]="s.url" target="_blank" rel="noopener noreferrer" class="text-link">{{ s.title }} ↗</a>
                </p>
              } @empty {
                <p class="muted">This course contains original demonstration material.</p>
              }
            </div>
          </ng-template>
        </div>
      </div>
    } @else if (catalog.isLoading()) {
      <p class="empty">Loading the course…</p>
    }`,
})
export class CoursePage {
  readonly api = inject(Api);
  private readonly router = inject(Router);
  // Route and query parameters, bound through withComponentInputBinding().
  readonly id = input.required<string>();
  readonly tab = input<string>();
  readonly lesson = input<string>();
  readonly objective = input<string>();
  readonly blueprint = input<string>();
  readonly mode = input<string>();

  readonly catalog = httpResource<CourseCatalogDto>(() => `/api/catalog/${this.id()}`);
  readonly progress = httpResource<CourseProgressDto>(() =>
    this.api.user() ? `/api/me/courses/${this.id()}` : undefined,
  );
  readonly error = signal('');
  readonly busy = signal(false);
  readonly tabs = [
    { id: 'learn', title: 'Lessons' },
    { id: 'map', title: 'Content map' },
    { id: 'practice', title: 'Practice & exams' },
    { id: 'sources', title: 'References' },
  ];
  // string | undefined matches the ngTabList selectedTab model for two-way binding.
  readonly selectedTab = linkedSignal<string | undefined>(() => this.tab() || 'learn');
  readonly lessonId = linkedSignal(() => this.lesson() ?? '');
  readonly course = computed(() => (this.catalog.hasValue() ? this.catalog.value() : undefined));
  readonly current = computed(() => {
    const c = this.course();
    return c?.lessons.find((l) => l.id === this.lessonId()) ?? c?.lessons[0];
  });
  readonly completed = computed(() => new Set(this.progress.hasValue() ? this.progress.value().completedLessons : []));
  readonly revised = computed(() => new Set(this.progress.hasValue() ? this.progress.value().revisedLessons : []));
  readonly mastery = computed(
    () => new Map((this.progress.hasValue() ? this.progress.value().objectives : []).map((o) => [o.id, o] as const)),
  );
  // Deep links from next steps preselect the session; the learner can still change every field.
  readonly setup = linkedSignal<PracticeSetup>(() => ({
    blueprintId: this.blueprint() || this.course()?.blueprints[0]?.id || '',
    mode: this.mode() === 'mock' ? 'mock' : 'learn',
    focus: this.objective() ? 'objective' : '',
    objectiveId: this.objective() ?? '',
  }));
  readonly setupForm = form(this.setup, (p) => {
    required(p.blueprintId);
    required(p.objectiveId, {
      when: ({ valueOf }) => valueOf(p.mode) === 'learn' && valueOf(p.focus) === 'objective',
    });
  });

  errorText(e: unknown) {
    return message(e);
  }
  prerequisiteNames(ids: string[]) {
    return ids.map((id) => this.course()?.objectives.find((o) => o.id === id)?.title ?? id).join(', ');
  }
  openLesson(id: string) {
    this.lessonId.set(id);
    this.selectedTab.set('learn');
  }
  practise(objectiveId: string) {
    this.setup.update((s) => ({ ...s, mode: 'learn', focus: 'objective', objectiveId }));
    this.selectedTab.set('practice');
  }
  complete(lessonId: string) {
    return this.run(() => this.api.put(`/me/courses/${this.id()}/lessons/${lessonId}`));
  }
  setEnrollment(status: EnrollmentStatus) {
    return this.run(() => this.api.put(`/me/enrollments/${this.id()}`, { status }));
  }
  async start(event: Event) {
    event.preventDefault();
    await submit(this.setupForm, async () => {
      const s = this.setup();
      const learn = s.mode === 'learn';
      this.busy.set(true);
      this.error.set('');
      try {
        const attempt = await this.api.post<{ id: string }>('/me/attempts', {
          packId: this.id(),
          blueprintId: s.blueprintId,
          mode: s.mode,
          requestId: crypto.randomUUID(),
          focus: learn && s.focus ? s.focus : null,
          objectiveId: learn && s.focus === 'objective' ? s.objectiveId : null,
        });
        await this.router.navigate(['/attempts', attempt.id]);
      } catch (e) {
        this.error.set(message(e));
      } finally {
        this.busy.set(false);
      }
      return undefined;
    });
  }
  private async run(action: () => Promise<unknown>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
      this.progress.reload();
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
