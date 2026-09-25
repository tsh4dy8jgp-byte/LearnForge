import { Component, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { CourseGoal, DashboardDto } from '../models';
import { MasteryBadge } from '../mastery-badge';
import { NextSteps } from '../next-steps';

const goalLabels: Record<CourseGoal, string> = {
  readiness: 'EXAM READINESS',
  mastery: 'MASTERY GOAL',
  completion: 'COMPLETION GOAL',
};

@Component({
  imports: [RouterLink, DecimalPipe, DatePipe, MasteryBadge, NextSteps],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">ONE STEP FURTHER</p>
        <h1>Welcome back, {{ api.user()?.displayName }}<span class="accent">.</span></h1>
        <p class="lead">Small steps today. Stronger understanding tomorrow.</p>
      </div>
      <a class="button" routerLink="/courses">Explore your courses <span>↗</span></a>
    </div>
    @if (dashboard.error(); as e) {
      <p class="alert error" role="alert">{{ message(e) }}</p>
    }
    @if (dashboard.hasValue()) {
      @let d = dashboard.value();
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
            <h2>My courses</h2>
            <a routerLink="/courses" class="text-link">Browse the library →</a>
          </div>
          @for (course of d.courses; track course.packId) {
            <article class="panel path-panel">
              <div class="row">
                <span class="tiny-label">CONTINUE LEARNING</span
                ><span class="muted small"
                  >{{ course.completedLessons.length }} / {{ course.lessonCount }} lessons</span
                >
              </div>
              <h2>
                <a [routerLink]="['/courses', course.packId]"
                  >{{ course.title }} <span class="accent">↗</span></a
                >
              </h2>
              <progress
                [value]="course.completedLessons.length"
                [max]="course.lessonCount"
                [attr.aria-label]="course.title + ' completion'"
              ></progress>
              <h3 class="small">Next steps</h3>
              <lf-next-steps [packId]="course.packId" [steps]="course.nextSteps" />
              <div class="objective-list">
                @for (objective of course.objectives; track objective.id) {
                  <div>
                    <span>{{ objective.title }}</span>
                    <lf-mastery-badge
                      [state]="objective.state"
                      [correct]="objective.correct"
                      [considered]="objective.considered"
                      [reviewDue]="objective.reviewDue"
                    />
                  </div>
                }
              </div>
            </article>
          } @empty {
            <div class="panel empty">
              <h3>No courses yet.</h3>
              <p>
                Open a course from the library. It appears here as soon as you read a lesson or
                start a session.
              </p>
              <a routerLink="/courses" class="button">Browse the library →</a>
            </div>
          }
          <div class="section-heading">
            <h2>Recent practice</h2>
            <a routerLink="/attempts" class="text-link">All attempts →</a>
          </div>
          <div class="panel">
            @for (a of d.recentAttempts; track a.id) {
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
          <div class="section-heading"><h2>Course goals</h2></div>
          @for (course of d.courses; track course.packId) {
            <div class="panel readiness">
              <span class="eyebrow">{{ goalLabel(course.goal.goal) }}</span>
              <h3>{{ course.title }}</h3>
              @switch (course.goal.goal) {
                @case ('readiness') {
                  @if (course.goal.readiness; as r) {
                    <p>{{ r.message }}</p>
                    <div class="readiness-track">
                      <strong>{{ r.short.streak }}<small>/{{ r.short.required }}</small></strong
                      ><span>consecutive short mocks</span>
                    </div>
                    <div class="readiness-track">
                      <strong>{{ r.full.streak }}<small>/{{ r.full.required }}</small></strong
                      ><span>consecutive full mocks</span>
                    </div>
                    <p class="small muted">
                      Each result must be strictly above {{ r.threshold }}% fully correct, with
                      fresh questions and independent work. Either streak qualifies.
                    </p>
                  }
                }
                @case ('mastery') {
                  <p>
                    {{ course.goal.proficientObjectives }} of {{ course.goal.objectiveCount }}
                    objectives proficient.
                  </p>
                }
                @default {
                  <p>
                    {{ course.goal.completedLessons }} of {{ course.goal.lessonCount }} lessons
                    completed.
                  </p>
                }
              }
              @if (course.goal.met) {
                <p><span class="pill success">Goal met</span></p>
              }
            </div>
          }
        </aside>
      </div>
    } @else if (dashboard.isLoading()) {
      <p class="empty">Loading your workspace…</p>
    }`,
})
export class DashboardPage {
  readonly api = inject(Api);
  readonly dashboard = httpResource<DashboardDto>(() => '/api/me/dashboard');
  readonly message = message;
  goalLabel(goal: CourseGoal) {
    return goalLabels[goal];
  }
}
