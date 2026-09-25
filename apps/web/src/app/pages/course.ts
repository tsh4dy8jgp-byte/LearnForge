import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Attempt, Course } from '../models';
@Component({
  imports: [FormsModule, RouterLink],
  template: ` @if (error()) {
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
        <span class="pill">{{ c.lessons.length }} lessons</span>
      </div>
      <div class="tabs" role="tablist" aria-label="Course views">
        @for (t of tabs; track t.id) {
          <button
            role="tab"
            [attr.aria-selected]="tab() === t.id"
            [class.active]="tab() === t.id"
            (click)="tab.set(t.id)"
          >
            {{ t.title }}
          </button>
        }
      </div>
      @if (tab() === 'learn') {
        <div class="lesson-layout">
          <nav class="panel lesson-nav" aria-label="Course lessons">
            @for (l of c.lessons; track l.id; let i = $index) {
              <button [class.active]="lesson()?.id === l.id" (click)="lessonId.set(l.id)">
                <span>{{
                  c.completedLessons.includes(l.id) ? '✓' : (i + 1).toString().padStart(2, '0')
                }}</span>
                <div>
                  <strong>{{ l.title }}</strong
                  ><small>{{ l.summary }}</small>
                </div>
              </button>
            }
          </nav>
          @if (lesson(); as l) {
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
                    <pre><code>{{block.text}}</code></pre>
                  } @else {
                    <p>{{ block.text }}</p>
                  }
                </section>
              }
              <div class="row">
                <span class="muted small"
                  >Reading completion is separate from assessment performance.</span
                >
                @if (api.user()) {
                  <button
                    class="button"
                    [disabled]="busy() || c.completedLessons.includes(l.id)"
                    (click)="complete(l.id)"
                  >
                    {{
                      c.completedLessons.includes(l.id) ? 'Lesson completed ✓' : 'Mark as complete'
                    }}
                  </button>
                } @else {
                  <a routerLink="/sign-in" class="button">Sign in to save progress</a>
                }
              </div>
            </article>
          }
        </div>
      }
      @if (tab() === 'map') {
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
              <p class="muted small">
                {{
                  o.prerequisites.length
                    ? 'Builds on: ' + prerequisiteNames(o.prerequisites)
                    : 'A starting point — no prerequisites'
                }}
              </p>
              @for (l of c.lessons; track l.id) {
                @if (l.objectiveIds.includes(o.id)) {
                  <button class="text-button" (click)="lessonId.set(l.id); tab.set('learn')">
                    Study: {{ l.title }} →
                  </button>
                }
              }
            </article>
          }
        </div>
      }
      @if (tab() === 'practice') {
        <div class="practice-layout">
          <form class="panel" (ngSubmit)="start()">
            <p class="eyebrow">PUT YOUR KNOWLEDGE TO WORK</p>
            <h2>Shape your session</h2>
            <label
              >Session length<select name="blueprint" [(ngModel)]="blueprintId">
                @for (b of c.blueprints; track b.id) {
                  <option [value]="b.id">
                    {{ b.title }} · {{ b.count }} questions · {{ b.minutes }} minutes
                  </option>
                }
              </select></label
            ><label
              >Feedback mode<select name="mode" [(ngModel)]="mode" (ngModelChange)="focus = ''">
                <option value="learn">Learn — check answers as you go</option>
                <option value="mock">Mock exam — results after submission</option>
              </select></label
            >
            @if (mode === 'learn') {
              <label
                >Practice focus<select name="focus" [(ngModel)]="focus">
                  <option value="">Balanced practice</option>
                  <option value="weak">Weakest objectives</option>
                  <option value="mistakes">Previous mistakes</option>
                </select></label
              >
            }
            <p class="muted">
              {{
                mode === 'mock'
                  ? 'The timer starts when you begin. Your responses save automatically; a refresh will not reset the deadline.'
                  : 'Take your time. Check each answer and learn from the explanation. Learning sessions do not count toward exam readiness.'
              }}
            </p>
            @if (api.user()) {
              <button class="button" [disabled]="busy()">
                {{ busy() ? 'Preparing…' : 'Begin session →' }}
              </button>
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
      }
      @if (tab() === 'sources') {
        <div class="panel">
          <h2>Sources & attribution</h2>
          <p>Content license: {{ c.license }} · Release {{ c.version }}</p>
          @for (s of c.sources; track s.url) {
            <p>
              <a [href]="s.url" target="_blank" rel="noopener noreferrer" class="text-link"
                >{{ s.title }} ↗</a
              >
            </p>
          } @empty {
            <p class="muted">This course contains original demonstration material.</p>
          }
        </div>
      }
    } @else if (!error()) {
      <p class="empty">Loading the course…</p>
    }`,
})
export class CoursePage {
  readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly course = signal<Course | null>(null);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly tab = signal('learn');
  readonly lessonId = signal('');
  readonly lesson = computed(
    () => this.course()?.lessons.find((l) => l.id === this.lessonId()) ?? this.course()?.lessons[0],
  );
  readonly tabs = [
    { id: 'learn', title: 'Lessons' },
    { id: 'map', title: 'Content map' },
    { id: 'practice', title: 'Practice & exams' },
    { id: 'sources', title: 'References' },
  ];
  blueprintId = '';
  mode = 'learn';
  focus = '';
  constructor() {
    this.route.paramMap.subscribe((p) => {
      this.api
        .get<Course>('/catalog/' + p.get('id'))
        .then((c) => {
          this.course.set(c);
          this.blueprintId = c.blueprints[0]?.id;
          this.lessonId.set(this.route.snapshot.queryParamMap.get('lesson') || c.lessons[0]?.id);
        })
        .catch((e) => this.error.set(message(e)));
    });
  }
  prerequisiteNames(ids: string[]) {
    return ids
      .map((id) => this.course()?.objectives.find((o) => o.id === id)?.title ?? id)
      .join(', ');
  }
  async complete(id: string) {
    this.busy.set(true);
    try {
      await this.api.put('/me/courses/' + this.course()!.id + '/lessons/' + id);
      this.course.update((c) => (c ? { ...c, completedLessons: [...c.completedLessons, id] } : c));
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async start() {
    this.busy.set(true);
    this.error.set('');
    try {
      const a = await this.api.post<Attempt>('/me/attempts', {
        packId: this.course()!.id,
        blueprintId: this.blueprintId,
        mode: this.mode,
        requestId: crypto.randomUUID(),
        focus: this.focus || null,
      });
      await this.router.navigate(['/attempts', a.id]);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
