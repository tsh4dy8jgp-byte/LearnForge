import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { NextStepDto } from '../api-contracts';
import { NextSteps } from './next-steps';
import { describeStep, stepLink } from './next-step-description';

const step = (overrides: Partial<NextStepDto>): NextStepDto => ({
  kind: 'practise',
  reason: 'needsEvidence',
  objectiveId: 'sets',
  objectiveTitle: 'Reason about sets',
  lessonId: null,
  lessonTitle: null,
  blueprintId: null,
  ...overrides,
});

describe('next steps', () => {
  it('phrases every reason for a learner', () => {
    expect(
      describeStep(
        step({
          kind: 'readLesson',
          reason: 'startObjective',
          lessonId: 'sets-intro',
          lessonTitle: 'Think in sets',
        }),
      ),
    ).toBe('Begin "Reason about sets" by reading "Think in sets".');
    expect(describeStep(step({ reason: 'belowProficient' }))).toContain(
      'until it becomes proficient',
    );
    expect(describeStep(step({ kind: 'review', reason: 'reviewDue' }))).toContain(
      'Review "Reason about sets"',
    );
    expect(
      describeStep(
        step({
          kind: 'takeMock',
          reason: 'readyForMock',
          objectiveId: null,
          objectiveTitle: null,
          blueprintId: 'short',
        }),
      ),
    ).toContain('Take a timed mock');
  });

  it('links each kind of step to the right course view', () => {
    expect(stepLink('demo', step({ kind: 'readLesson', lessonId: 'sets-intro' }))).toEqual({
      path: ['/courses', 'demo'],
      query: { tab: 'learn', lesson: 'sets-intro' },
    });
    expect(stepLink('demo', step({ kind: 'review' }))).toEqual({
      path: ['/courses', 'demo'],
      query: { tab: 'practice', objective: 'sets' },
    });
    expect(stepLink('demo', step({ kind: 'takeMock', blueprintId: 'short' }))).toEqual({
      path: ['/courses', 'demo'],
      query: { tab: 'practice', mode: 'mock', blueprint: 'short' },
    });
  });

  it('sends practice on case-study-only objectives to a learning session on the suggested blueprint', () => {
    expect(stepLink('demo', step({ kind: 'practise', blueprintId: 'short' }))).toEqual({
      path: ['/courses', 'demo'],
      query: { tab: 'practice', blueprint: 'short' },
    });
    expect(stepLink('demo', step({ kind: 'review', blueprintId: 'short' }))).toEqual({
      path: ['/courses', 'demo'],
      query: { tab: 'practice', blueprint: 'short' },
    });
  });

  it('renders an action link per step and a message when nothing is left', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(NextSteps);
    fixture.componentRef.setInput('packId', 'demo');
    fixture.componentRef.setInput('steps', [step({})]);
    await fixture.whenStable();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a')!;
    expect(link.textContent).toContain('Practise');
    expect(link.getAttribute('href')).toBe('/courses/demo?tab=practice&objective=sets');
    fixture.componentRef.setInput('steps', []);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'You have completed every suggested step',
    );
  });
});
