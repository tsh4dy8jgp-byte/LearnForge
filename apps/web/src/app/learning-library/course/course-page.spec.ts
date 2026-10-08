import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { CourseCatalogDto } from '../../api-contracts';
import { Session } from '../../authentication/session';
import { CoursePage } from './course-page';

const catalog: CourseCatalogDto = {
  id: 'arbitrary-subject',
  title: 'A generic subject',
  description: 'Example learning material',
  version: '1.0.0',
  license: 'CC0-1.0',
  profile: 'hybrid',
  goal: 'mastery',
  capabilities: { lessons: true, practice: true, assessments: true },
  objectives: [{ id: 'intro', title: 'Introduction', prerequisites: [] }],
  lessons: [
    {
      id: 'first',
      title: 'First lesson',
      summary: 'Read this first',
      objectiveIds: ['intro'],
      blocks: [],
    },
  ],
  sources: [],
  questionCount: 1,
  blueprints: [
    {
      id: 'short',
      title: 'Short session',
      count: 1,
      minutes: 5,
      size: 'short',
      objectiveIds: ['intro'],
      requiredKinds: [],
      objectiveWeights: {},
    },
  ],
  readiness: {
    shortAttempts: 5,
    fullAttempts: 3,
    threshold: 90,
    lookbackDays: 90,
    minimumFreshPercent: 100,
  },
};

async function render(
  data: CourseCatalogDto,
  query: Record<string, string> = {},
  signedIn = false,
) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  if (signedIn)
    TestBed.inject(Session).user.set({
      id: 'learner',
      email: 'learner@example.test',
      displayName: 'Learner',
      publisher: false,
    });
  const fixture = TestBed.createComponent(CoursePage);
  fixture.componentRef.setInput('id', data.id);
  for (const [name, value] of Object.entries(query)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);
  http.expectOne('/api/catalog/' + data.id).flush(data);
  if (signedIn)
    http
      .expectOne('/api/me/courses/' + data.id)
      .flush({
        completedLessons: [],
        revisedLessons: [],
        objectives: [],
        nextSteps: [],
        enrollment: 'none',
      });
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, http, element: fixture.nativeElement as HTMLElement };
}

describe('course presentation boundaries', () => {
  it('renders a lesson-only course without practice or exam controls', async () => {
    const { element, http } = await render({
      ...catalog,
      profile: 'course',
      goal: 'completion',
      capabilities: { lessons: true, practice: false, assessments: false },
      blueprints: [],
      questionCount: 0,
    });
    expect(
      Array.from(element.querySelectorAll('[role=tab]')).map((tab) => tab.textContent?.trim()),
    ).toEqual(['Lessons', 'Content map', 'References']);
    expect(element.querySelector('.reading')?.textContent).toContain('First lesson');
    expect(element.textContent).toContain('Sign in to save progress');
    http.verify();
  });

  it('defaults an exam-only pack to assessment setup without lesson or learn controls', async () => {
    const { element, http } = await render({
      ...catalog,
      profile: 'exam',
      goal: 'readiness',
      capabilities: { lessons: false, practice: false, assessments: true },
      lessons: [],
    });
    expect(element.querySelector('[role=tab][aria-selected=true]')?.textContent).toContain('Exams');
    expect(element.querySelector('lf-course-lessons')).toBeNull();
    const mode = Array.from(element.querySelectorAll('select')).find((select) =>
      select.closest('label')?.textContent?.includes('Feedback mode'),
    )!;
    expect(mode.value).toBe('mock');
    expect(Array.from(mode.options).map((option) => option.value)).toEqual(['mock']);
    http.verify();
  });

  it('honors objective deep links and sends the selected setup when starting a session', async () => {
    const { element, http, fixture } = await render(
      catalog,
      { tab: 'practice', objective: 'intro', blueprint: 'short', mode: 'learn' },
      true,
    );
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const selects = Array.from(element.querySelectorAll('select'));
    const value = (label: string) =>
      selects.find((select) => select.closest('label')?.textContent?.includes(label))?.value;
    expect(value('Feedback mode')).toBe('learn');
    expect(value('Practice focus')).toBe('objective');
    expect(value('Objective')).toBe('intro');
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await Promise.resolve();
    const request = http.expectOne('/api/me/attempts');
    expect(request.request.body).toMatchObject({
      packId: catalog.id,
      blueprintId: 'short',
      mode: 'learn',
      focus: 'objective',
      objectiveId: 'intro',
    });
    request.flush({ id: 'new-attempt' });
    await fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith(['/attempts', 'new-attempt']);
    http.verify();
  });
});
