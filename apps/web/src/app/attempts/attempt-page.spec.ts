import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Answer, Attempt } from '../api-contracts';
import { AttemptPage } from './attempt-page';
import { AttemptState } from './attempt-state';

const draftKey = 'learnforge.draft.undefined.attempt-1';
const answer: Answer = { selected: ['o2'], slots: {} };
const sample = (): Attempt => ({
  id: 'attempt-1',
  packId: 'sample',
  title: 'Sample exam',
  version: '1.0.0',
  mode: 'mock',
  size: 'short',
  status: 'inProgress',
  revision: 0,
  startedAt: new Date().toISOString(),
  deadline: new Date(Date.now() + 600_000).toISOString(),
  completedAt: null,
  sectionIndex: 0,
  sections: ['general'],
  lockSections: false,
  timedOut: false,
  focus: null,
  scenarios: [],
  objectives: [],
  answers: {},
  feedback: {},
  serverTime: new Date().toISOString(),
  results: null,
  summary: null,
  goal: 'readiness',
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
});

async function render() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  vi.spyOn(TestBed.inject(ActivatedRoute).snapshot.paramMap, 'get').mockReturnValue('attempt-1');
  const fixture = TestBed.createComponent(AttemptPage);
  const http = TestBed.inject(HttpTestingController);
  const attempt = sample();
  http.expectOne('/api/me/attempts/attempt-1').flush(attempt);
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, http, attempt, element: fixture.nativeElement as HTMLElement };
}

afterEach(() => {
  localStorage.removeItem(draftKey);
  vi.restoreAllMocks();
});

describe('AttemptPage answer saving', () => {
  it('destroys its clock when leaving the route', async () => {
    const intervals = vi.spyOn(globalThis, 'setInterval');
    const clear = vi.spyOn(globalThis, 'clearInterval');
    const { fixture } = await render();
    const clock =
      intervals.mock.results[intervals.mock.calls.findIndex((args) => args[1] === 1000)].value;
    clear.mockClear();
    fixture.destroy();
    expect(clear).toHaveBeenCalledWith(clock);
  });

  it('switches from the active session to released results after submission', async () => {
    const { fixture, http, attempt, element } = await render();
    const submitting = fixture.debugElement.injector.get(AttemptState).submit();
    http.expectOne('/api/me/attempts/attempt-1/submit').flush({
      ...attempt,
      status: 'completed',
      completedAt: new Date().toISOString(),
      answers: { q1: answer },
      results: {
        q1: {
          earned: 1,
          possible: 1,
          fullyCorrect: true,
          explanation: 'Released explanation.',
          correct: ['o2'],
          matches: null,
        },
      },
    });
    await submitting;
    fixture.detectChanges();
    expect(element.querySelector('lf-active-session')).toBeNull();
    expect(element.querySelector('lf-attempt-results')?.textContent).toContain(
      'Released explanation.',
    );
    expect(element.querySelector('lf-question-input')).toBeNull();
    http.verify();
  });

  it('keeps the selection visible without inserting a recovery banner while saving', async () => {
    const { fixture, http, attempt, element } = await render();
    const save = vi.spyOn(fixture.debugElement.injector.get(AttemptState), 'save');
    const input = element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1];
    input.checked = true;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(element.querySelector('.choice.selected')?.textContent).toContain('Second response');
    expect(element.querySelector('.alert')).toBeNull();
    expect(element.querySelector('.timer small')?.textContent).toContain('Saving');
    expect(localStorage.getItem(draftKey)).not.toBeNull();

    http
      .expectOne('/api/me/attempts/attempt-1/responses')
      .flush({ ...attempt, revision: 1, answers: { q1: answer } });
    await save.mock.results[0].value;
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.querySelector('.choice.selected')?.textContent).toContain('Second response');
    expect(element.querySelector('.alert')).toBeNull();
    expect(element.querySelector('.timer small')?.textContent).toContain('All responses saved');
    expect(localStorage.getItem(draftKey)).toBeNull();
    http.verify();
  });

  it('retains the selected draft and offers recovery after a save fails', async () => {
    const { fixture, http, attempt, element } = await render();
    const saving = fixture.debugElement.injector.get(AttemptState).save(answer);
    http
      .expectOne('/api/me/attempts/attempt-1/responses')
      .flush({ detail: 'Connection interrupted.' }, { status: 503, statusText: 'Unavailable' });
    await saving;
    fixture.detectChanges();
    expect(element.textContent).toContain('Retry saving');
    expect(element.querySelector('.choice.selected')?.textContent).toContain('Second response');
    expect(localStorage.getItem(draftKey)).not.toBeNull();

    const retrying = fixture.debugElement.injector.get(AttemptState).retry();
    fixture.detectChanges();
    expect(element.textContent).not.toContain('An unsaved answer');
    http
      .expectOne('/api/me/attempts/attempt-1/responses')
      .flush({ ...attempt, revision: 1, answers: { q1: answer } });
    await retrying;
    fixture.detectChanges();
    expect(element.textContent).not.toContain('Retry saving');
    expect(localStorage.getItem(draftKey)).toBeNull();
    http.verify();
  });

  it('shows a recovered local draft and restores the server response when discarded', async () => {
    localStorage.setItem(
      draftKey,
      JSON.stringify({
        questionId: 'q1',
        answer,
        check: false,
        revision: 0,
        requestId: 'request-1',
      }),
    );
    const { fixture, http, element } = await render();
    expect(element.textContent).toContain('Retry saving');
    expect(element.querySelector('.choice.selected')?.textContent).toContain('Second response');
    fixture.debugElement.injector.get(AttemptState).discard();
    fixture.detectChanges();
    expect(element.querySelector('.choice.selected')).toBeNull();
    expect(element.textContent).not.toContain('Retry saving');
    http.verify();
  });
});
