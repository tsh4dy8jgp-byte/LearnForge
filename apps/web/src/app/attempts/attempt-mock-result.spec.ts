import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Attempt } from '../api-contracts';
import { AttemptPage } from './attempt-page';

function view(minutes = 60): Attempt {
  const serverTime = Date.parse('2026-10-07T00:00:00Z');
  return {
    id: 'paper',
    packId: 'sample-exam',
    title: 'Sample exam',
    version: '1.0.0',
    mode: 'mock',
    size: 'full',
    status: 'inProgress',
    revision: 0,
    startedAt: new Date(serverTime).toISOString(),
    deadline: new Date(serverTime + minutes * 60000).toISOString(),
    completedAt: null,
    sectionIndex: 0,
    sections: ['general'],
    lockSections: false,
    timedOut: false,
    focus: null,
    questions: [
      {
        id: 'q1',
        kind: 'single',
        prompt: 'Choose a response.',
        objectiveIds: [],
        options: [
          { id: 'o1', text: 'First response' },
          { id: 'o2', text: 'Second response' },
        ],
        slots: [],
        selectCount: 1,
        reuse: false,
        scenarioId: null,
        weight: 1,
      },
    ],
    scenarios: [],
    objectives: [],
    answers: {},
    feedback: {},
    serverTime: new Date(serverTime).toISOString(),
    results: null,
    summary: null,
    goal: 'readiness',
  };
}

async function render(attempt: Attempt) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  vi.spyOn(TestBed.inject(ActivatedRoute).snapshot.paramMap, 'get').mockReturnValue('paper');
  const fixture = TestBed.createComponent(AttemptPage);
  TestBed.inject(HttpTestingController).expectOne('/api/me/attempts/paper').flush(attempt);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

afterEach(() => vi.restoreAllMocks());

describe('attempt timer and mock result', () => {
  it('uses server time immediately despite a client clock several hours ahead', async () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-10-07T05:00:00Z'));
    const element = await render(view(75));
    expect(element.querySelector('.timer')?.textContent).toContain('75:00');
  });

  it.each([true, false])('renders a completed mock result with passed=%s', async (passed) => {
    const attempt = view();
    attempt.status = 'completed';
    attempt.completedAt = attempt.serverTime;
    attempt.results = {};
    attempt.summary = {
      earned: passed ? 26 : 25,
      possible: 40,
      score: passed ? 65 : 62.5,
      correctPercent: passed ? 65 : 62.5,
      eligible: true,
      freshPercent: 100,
      passPoints: 26,
      passed,
    };
    const status = (await render(attempt)).querySelector('[role="status"]');
    expect(status?.textContent).toContain(passed ? 'Mock pass' : 'Mock fail');
    expect(status?.textContent).toContain('Pass threshold: 26 points');
  });

  it('does not render a pass/fail label when the result has no threshold', async () => {
    const attempt = view();
    attempt.mode = 'learn';
    attempt.status = 'completed';
    attempt.results = {};
    attempt.summary = {
      earned: 40,
      possible: 40,
      score: 100,
      correctPercent: 100,
      eligible: false,
      freshPercent: 100,
      passPoints: null,
      passed: null,
    };
    expect((await render(attempt)).querySelector('[role="status"]')).toBeNull();
  });

  it('does not call a short mock readiness evidence when it is not eligible', async () => {
    const attempt = view();
    attempt.size = 'short';
    attempt.status = 'completed';
    attempt.completedAt = attempt.serverTime;
    attempt.results = {};
    attempt.summary = {
      earned: 20,
      possible: 20,
      score: 100,
      correctPercent: 100,
      eligible: false,
      freshPercent: 100,
      passPoints: null,
      passed: null,
    };
    const text = (await render(attempt)).textContent ?? '';
    expect(text).toContain('Not counted toward readiness');
    expect(text).not.toContain('Used for exam readiness');
    expect(text).not.toContain('Repeated question families');
  });

  it('keeps line breaks in the prompt of an active question', async () => {
    const element = await render(view());
    expect(element.querySelector('.question-card h2')?.classList).toContain('prompt-text');
  });
});
