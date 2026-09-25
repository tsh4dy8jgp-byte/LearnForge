import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Dashboard } from '../models';
@Component({
  imports: [RouterLink, DecimalPipe, DatePipe],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">ONE STEP FURTHER</p>
        <h1>Welcome back, {{ api.user()?.displayName }}<span class="accent">.</span></h1>
        <p class="lead">Small steps today. Stronger understanding tomorrow.</p>
      </div>
      <a class="button" routerLink="/courses">Explore your courses <span>↗</span></a>
    </div>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    @if (data(); as d) {
      <div class="stat-grid">
        <div class="stat">
          <span>LESSONS COMPLETED</span><strong>{{ d.completedLessons | number }}</strong
          ><small>Ideas you have explored</small>
        </div>
        <div class="stat">
          <span>PRACTICE SESSIONS</span><strong>{{ d.completedAttempts | number }}</strong
          ><small>Completed and saved</small>
        </div>
        <div class="stat">
          <span>IN PROGRESS</span><strong>{{ d.activeAttempts }}</strong
          ><small>Ready when you are</small>
        </div>
      </div>
      <div class="dashboard-grid">
        <section>
          <div class="section-heading">
            <h2>Your learning paths</h2>
            <a routerLink="/courses" class="text-link">View library →</a>
          </div>
          @for (course of d.courses; track course.id) {
            <article class="panel path-panel">
              <div class="row">
                <span class="tiny-label">CONTINUE LEARNING</span
                ><span class="muted small"
                  >{{ course.completedLessons }} / {{ course.lessonCount }} lessons</span
                >
              </div>
              <h2>
                <a [routerLink]="['/courses', course.id]"
                  >{{ course.title }} <span class="accent">↗</span></a
                >
              </h2>
              <progress
                [value]="course.completedLessons"
                [max]="course.lessonCount"
                [attr.aria-label]="course.title + ' completion'"
              ></progress>
              <div class="objective-list">
                @for (objective of course.objectives; track objective.id) {
                  <div>
                    <span>{{ objective.title }}</span
                    ><span class="pill subtle">{{
                      objective.correctPercent === null
                        ? 'Needs evidence'
                        : (objective.correctPercent | number: '1.0-0') + '% correct'
                    }}</span>
                  </div>
                }
              </div>
            </article>
          }
          <div class="section-heading">
            <h2>Recent practice</h2>
            <a routerLink="/attempts" class="text-link">All attempts →</a>
          </div>
          <div class="panel">
            @for (a of d.attempts.slice(0, 4); track a.id) {
              <a class="attempt-row" [routerLink]="['/attempts', a.id]"
                ><div>
                  <strong>{{ a.title }}</strong
                  ><small
                    >{{ a.size }} · {{ a.mode }} · {{ a.startedAt | date: 'mediumDate' }}</small
                  >
                </div>
                <span class="pill" [class.success]="a.status === 'completed'">{{
                  a.status === 'completed'
                    ? (a.correctPercent | number: '1.0-0') + '% correct'
                    : 'Resume →'
                }}</span></a
              >
            } @empty {
              <div class="empty">
                <h3>Your first session is ahead.</h3>
                <p>Start a practice session from any course. Your results will appear here.</p>
              </div>
            }
          </div>
        </section>
        <aside>
          <div class="section-heading"><h2>A good next step</h2></div>
          @for (course of d.courses; track course.id) {
            <div class="recommendation">
              <span class="eyebrow">YOUR STUDY COMPASS</span>
              @for (r of course.recommendations.slice(0, 1); track r.objectiveId) {
                <h2>{{ r.title }}</h2>
                <p>{{ r.reason }}</p>
                <a
                  [routerLink]="['/courses', course.id]"
                  [queryParams]="{ lesson: r.lessonId }"
                  class="text-link"
                  >Open the lesson →</a
                >
              }
            </div>
            <div class="panel readiness">
              <span class="eyebrow">EXAM READINESS</span>
              <h3>
                {{ course.readiness.ready ? 'Consistency is showing.' : 'Build your confidence.' }}
              </h3>
              <p>{{ course.readiness.message }}</p>
              <div class="readiness-track">
                <strong
                  >{{ course.readiness.short.streak
                  }}<small>/{{ course.readiness.short.required }}</small></strong
                ><span>consecutive short mocks</span>
              </div>
              <div class="readiness-track">
                <strong
                  >{{ course.readiness.full.streak
                  }}<small>/{{ course.readiness.full.required }}</small></strong
                ><span>consecutive full mocks</span>
              </div>
              <p class="small muted">
                Each result must be strictly above {{ course.readiness.threshold }}% fully correct,
                with fresh questions and independent work. Either streak qualifies.
              </p>
              <a routerLink="/attempts" class="text-link">See the evidence →</a>
            </div>
          }
        </aside>
      </div>
    } @else if (!error()) {
      <p class="empty">Loading your workspace…</p>
    }`,
})
export class DashboardPage {
  readonly api = inject(Api);
  readonly data = signal<Dashboard | null>(null);
  readonly error = signal('');
  constructor() {
    this.api
      .get<Dashboard>('/me/dashboard')
      .then((d) => this.data.set(d))
      .catch((e) => this.error.set(message(e)));
  }
}
