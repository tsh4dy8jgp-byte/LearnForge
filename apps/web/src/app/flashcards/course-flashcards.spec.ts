import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CourseCatalogDto, FlashcardSessionDto, FlashcardSummaryDto } from '../api-contracts';
import { Session } from '../authentication/session';
import { CourseFlashcards } from './course-flashcards';

const summaryUrl = '/api/me/courses/recall/flashcards/';
const startUrl = '/api/me/courses/recall/flashcards/sessions';
const course: CourseCatalogDto = {
  id: 'recall',
  title: 'Recall subject',
  description: 'Cards only',
  version: '1.0.0',
  license: 'CC0-1.0',
  profile: 'flashcards',
  goal: 'review',
  capabilities: { lessons: false, practice: false, assessments: false, flashcards: true },
  objectives: [{ id: 'terms', title: 'Terms', prerequisites: [] }],
  lessons: [],
  sources: [],
  questionCount: 0,
  blueprints: [],
  readiness: {
    shortAttempts: 0,
    fullAttempts: 0,
    threshold: 0,
    lookbackDays: 0,
    minimumFreshPercent: 0,
  },
  flashcardDecks: [
    { id: 'first', title: 'First deck', notes: [] },
    { id: 'second', title: 'Second deck', notes: [] },
  ],
  flashcards: [
    {
      id: 'first.retrieval',
      deckId: 'first',
      noteId: 'retrieval',
      front: 'What is retrieval practice?',
      back: 'Recalling from memory.',
      explanation: 'Try before revealing.',
      objectiveIds: ['terms'],
      tags: ['terms'],
      language: 'en',
      sources: [],
      contentHash: 'hash',
    },
  ],
};
const summary: FlashcardSummaryDto = {
  total: 1,
  new: 1,
  due: 0,
  suspended: 0,
  introduced: 0,
  upToDate: false,
  activeSessionId: null,
  cards: [],
};
const session: FlashcardSessionDto = {
  id: 'session-1',
  packId: 'recall',
  releaseId: 'release-1',
  revision: 0,
  position: 0,
  count: 1,
  revealed: false,
  completed: false,
  card: {
    id: 'first.retrieval',
    deckId: 'first',
    noteId: 'retrieval',
    front: 'What is retrieval practice?',
    language: 'en',
    back: null,
    explanation: null,
    sources: [],
  },
};

async function render() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  TestBed.inject(Session).user.set({
    id: 'learner',
    email: 'learner@example.test',
    displayName: 'Learner',
    publisher: false,
  });
  const fixture = TestBed.createComponent(CourseFlashcards);
  fixture.componentRef.setInput('course', course);
  fixture.detectChanges();
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);
  const element = fixture.nativeElement as HTMLElement;
  const refresh = async () => {
    await fixture.whenStable();
    fixture.detectChanges();
  };
  return { fixture, http, element, component: fixture.componentInstance, refresh };
}

describe('CourseFlashcards', () => {
  it('keeps the tab usable and offers a reload when review progress fails to load', async () => {
    const { http, element, refresh } = await render();
    http.expectOne(summaryUrl).flush({ detail: 'Unavailable.' }, { status: 503, statusText: 'x' });
    await refresh();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Unavailable.');
    expect(element.textContent).toContain('What is retrieval practice?');

    const reload = [...element.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Reload review progress'),
    )!;
    reload.click();
    TestBed.tick();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();
    expect(element.textContent).toContain('0 due · 1 new');
    http.verify();
  });

  it('starts with the current filters after the server rejects a start', async () => {
    const { http, element, component, refresh } = await render();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();

    component['deck'].set('first');
    const rejected = component['start']();
    const first = http.expectOne(startUrl);
    first.flush(
      { detail: 'No new or due cards match your filters.' },
      { status: 400, statusText: 'Bad Request' },
    );
    await rejected;
    TestBed.tick();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();
    expect(element.textContent).toContain('No new or due cards match your filters.');
    expect(element.textContent).not.toContain('Retry the same action');

    component['deck'].set('second');
    const started = component['start']();
    const second = http.expectOne(startUrl);
    expect(second.request.body.deck).toBe('second');
    expect(second.request.body.requestId).not.toBe(first.request.body.requestId);
    second.flush(session);
    await started;
    TestBed.tick();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();
    expect(element.querySelector('h3.card-text')?.textContent).toContain(
      'What is retrieval practice?',
    );
    http.verify();
  });

  it('resends the same request ID after a transport failure', async () => {
    const { http, element, component, refresh } = await render();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();

    const failed = component['start']();
    const first = http.expectOne(startUrl);
    first.flush({ detail: 'Try again later.' }, { status: 503, statusText: 'Unavailable' });
    await failed;
    await refresh();
    expect(element.textContent).toContain('Retry the same action');

    const retried = component['start']();
    const second = http.expectOne(startUrl);
    expect(second.request.body).toEqual(first.request.body);
    second.flush(session);
    await retried;
    TestBed.tick();
    http.expectOne(summaryUrl).flush(summary);
    await refresh();
    expect(element.textContent).not.toContain('Retry the same action');
    http.verify();
  });
});
