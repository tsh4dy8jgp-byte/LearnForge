import { Session } from '../../authentication/session';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { Tab, TabContent, TabList, TabPanel, Tabs } from '@angular/aria/tabs';
import { ApiClient } from '../../http/api-client';
import { message } from '../../http/api-error';
import { CourseCatalogDto, CourseProgressDto, EnrollmentStatus } from '../../api-contracts';
import { NextSteps } from '../../learning/next-steps';
import { SiteSettingsStore } from '../../site/site-settings-store';

import { CourseLessons } from './course-lessons';
import { ObjectiveMap } from './objective-map';
import { PracticeSetup } from './practice-setup';
import { PracticeSelection } from './practice-selection';

@Component({
  imports: [
    RouterLink,
    Tabs,
    TabList,
    Tab,
    TabPanel,
    TabContent,
    NextSteps,
    CourseLessons,
    ObjectiveMap,
    PracticeSetup,
  ],
  templateUrl: './course-page.html',
})
export class CoursePage {
  protected readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  protected readonly site = inject(SiteSettingsStore);
  private readonly router = inject(Router);
  // Route and query parameters, bound through withComponentInputBinding().
  readonly id = input.required<string>();
  readonly tab = input<string>();
  readonly lesson = input<string>();
  readonly objective = input<string>();
  readonly blueprint = input<string>();
  readonly mode = input<string>();

  protected readonly catalog = httpResource<CourseCatalogDto>(() => `/api/catalog/${this.id()}`);
  protected readonly progress = httpResource<CourseProgressDto>(() =>
    this.session.user() ? `/api/me/courses/${this.id()}` : undefined,
  );
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly tabs = computed(() => {
    const features = this.course()?.capabilities;
    return [
      ...(features?.lessons ? [{ id: 'learn', title: 'Lessons' }] : []),
      { id: 'map', title: 'Content map' },
      ...(features?.practice || features?.assessments
        ? [
            {
              id: 'practice',
              title:
                features.practice && features.assessments
                  ? 'Practice & exams'
                  : features.practice
                    ? 'Practice'
                    : 'Exams',
            },
          ]
        : []),
      { id: 'sources', title: 'References' },
    ];
  });
  // string | undefined matches the ngTabList selectedTab model for two-way binding.
  protected readonly selectedTab = linkedSignal<string | undefined>(() => {
    const requested = this.tab();
    return requested && this.tabs().some((t) => t.id === requested)
      ? requested
      : this.course()?.capabilities.lessons
        ? 'learn'
        : this.tabs().some((t) => t.id === 'practice')
          ? 'practice'
          : 'map';
  });
  protected readonly lessonId = linkedSignal(() => this.lesson() ?? '');
  protected readonly course = computed(() =>
    this.catalog.hasValue() ? this.catalog.value() : undefined,
  );
  protected readonly completed = computed(
    () => new Set(this.progress.hasValue() ? this.progress.value().completedLessons : []),
  );
  protected readonly revised = computed(
    () => new Set(this.progress.hasValue() ? this.progress.value().revisedLessons : []),
  );
  protected readonly mastery = computed(
    () =>
      new Map(
        (this.progress.hasValue() ? this.progress.value().objectives : []).map(
          (o) => [o.id, o] as const,
        ),
      ),
  );
  // Deep links from next steps preselect the session; the learner can still change every field.
  protected readonly setup = linkedSignal<PracticeSelection>(() => ({
    blueprintId:
      this.course()?.blueprints.find((b) => b.id === this.blueprint())?.id ||
      this.course()?.blueprints[0]?.id ||
      '',
    mode:
      this.course()?.capabilities.assessments &&
      (this.mode() === 'mock' || !this.course()?.capabilities.practice)
        ? 'mock'
        : 'learn',
    focus: this.objective() ? 'objective' : '',
    objectiveId: this.objective() ?? '',
  }));
  protected readonly startSession = (selection: PracticeSelection) => this.start(selection);

  protected errorText(e: unknown) {
    return message(e);
  }
  protected openLesson(id: string) {
    this.lessonId.set(id);
    this.selectedTab.set('learn');
  }
  protected practise(objectiveId: string) {
    this.setup.update((s) => ({ ...s, mode: 'learn', focus: 'objective', objectiveId }));
    this.selectedTab.set('practice');
  }
  protected complete(lessonId: string) {
    return this.run(() => this.api.put(`/me/courses/${this.id()}/lessons/${lessonId}`));
  }
  protected setEnrollment(status: EnrollmentStatus) {
    return this.run(() => this.api.put(`/me/enrollments/${this.id()}`, { status }));
  }
  private async start(s: PracticeSelection) {
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
