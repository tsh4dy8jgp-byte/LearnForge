import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Api } from '../api';
import { Attempt } from '../models';
import { AttemptPage } from './attempt';

function view(minutes = 60): Attempt {
  const serverTime = Date.parse('2026-10-07T00:00:00Z');
  return {
    id: 'paper', packId: 'istqb-ctfl-4', title: 'ISTQB', version: '1.0.0',
    mode: 'mock', size: 'full', status: 'inProgress', revision: 0,
    startedAt: new Date(serverTime).toISOString(),
    deadline: new Date(serverTime + minutes * 60000).toISOString(),
    completedAt: null, sectionIndex: 0, sections: ['general'], lockSections: false,
    timedOut: false, focus: null, questions: [], scenarios: [], objectives: [],
    answers: {}, feedback: {}, serverTime: new Date(serverTime).toISOString(),
    results: null, summary: null, goal: 'readiness',
  };
}

async function render(attempt: Attempt) {
  TestBed.configureTestingModule({
    providers: [provideRouter([{ path: 'attempts/:id', component: AttemptPage }]),
      { provide: Api, useValue: { user: signal(null), get: vi.fn().mockResolvedValue(attempt) } }],
  });
  const harness = await RouterTestingHarness.create();
  const component = await harness.navigateByUrl('/attempts/paper', AttemptPage);
  await harness.fixture.whenStable();
  harness.detectChanges();
  return { harness, component };
}

afterEach(() => { TestBed.resetTestingModule(); vi.restoreAllMocks(); });

describe('attempt timer and mock result', () => {
  it('uses server time immediately despite a client clock several hours ahead', async () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-10-07T05:00:00Z'));
    const { component, harness } = await render(view(75));
    expect(component.timeLeft()).toBe('75:00');
    expect(harness.routeNativeElement?.querySelector('.timer')?.textContent).toContain('75:00');
  });

  it.each([true, false])('renders a completed mock result with passed=%s', async passed => {
    const attempt = view();
    attempt.status = 'completed';
    attempt.completedAt = attempt.serverTime;
    attempt.results = {};
    attempt.summary = { earned: passed ? 26 : 25, possible: 40, score: passed ? 65 : 62.5,
      correctPercent: passed ? 65 : 62.5, eligible: true, freshPercent: 100, passPoints: 26, passed };
    const { harness } = await render(attempt);
    const status = harness.routeNativeElement?.querySelector('[role="status"]');
    expect(status?.textContent).toContain(passed ? 'Mock pass' : 'Mock fail');
    expect(status?.textContent).toContain('Pass threshold: 26 points');
  });

  it('does not render a pass/fail label when the result has no threshold', async () => {
    const attempt = view();
    attempt.mode = 'learn'; attempt.status = 'completed'; attempt.results = {};
    attempt.summary = { earned: 40, possible: 40, score: 100, correctPercent: 100,
      eligible: false, freshPercent: 100, passPoints: null, passed: null };
    const { harness } = await render(attempt);
    expect(harness.routeNativeElement?.querySelector('[role="status"]')).toBeNull();
  });
});
