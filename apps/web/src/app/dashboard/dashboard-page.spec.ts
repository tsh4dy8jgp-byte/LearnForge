import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { DashboardDto } from '../api-contracts';
import { DashboardPage } from './dashboard-page';

const sample: DashboardDto = {
  courses: [
    {
      packId: 'reasoning-foundations',
      title: 'Reasoning foundations',
      version: '1.0.0',
      enrollment: 'active',
      lessonCount: 3,
      capabilities: { lessons: true, practice: true, assessments: true },
      completedLessons: ['sets-intro'],
      revisedLessons: [],
      objectives: [
        {
          id: 'sets',
          title: 'Reason about sets',
          prerequisites: [],
          state: 'developing',
          correct: 3,
          considered: 5,
          independent: 0,
          lastEvidenceAt: '2026-09-24T10:00:00Z',
          reviewDue: false,
          lessonIds: ['sets-intro'],
          standalonePractice: true,
        },
      ],
      nextSteps: [
        {
          kind: 'practise',
          reason: 'belowProficient',
          objectiveId: 'sets',
          objectiveTitle: 'Reason about sets',
          lessonId: null,
          lessonTitle: null,
          blueprintId: null,
        },
      ],
      goal: {
        goal: 'mastery',
        met: false,
        proficientObjectives: 0,
        objectiveCount: 3,
        completedLessons: 1,
        lessonCount: 3,
        readiness: null,
      },
      lastActivityAt: '2026-09-24T10:00:00Z',
    },
  ],
  recentAttempts: [],
  completedAttempts: 0,
  activeAttempts: 0,
  completedLessons: 1,
};

async function render(data: DashboardDto) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  const fixture = TestBed.createComponent(DashboardPage);
  fixture.detectChanges();
  TestBed.tick();
  TestBed.inject(HttpTestingController).expectOne('/api/me/dashboard').flush(data);
  await fixture.whenStable();
  return (fixture.nativeElement as HTMLElement).textContent ?? '';
}

describe('DashboardPage', () => {
  it('shows enrolled courses with mastery, next steps and goal progress', async () => {
    const text = await render(sample);
    expect(text).toContain('My courses');
    expect(text).toContain('Reasoning foundations');
    expect(text).toContain('3 of last 5 correct');
    expect(text).toContain('Keep practising "Reason about sets"');
    expect(text).toContain('0 of 3 objectives proficient');
  });

  it('invites learners without courses to the library', async () => {
    const text = await render({ ...sample, courses: [] });
    expect(text).toContain('No courses yet.');
  });
});
