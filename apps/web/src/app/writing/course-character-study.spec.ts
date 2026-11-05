import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import type { CourseCatalogDto, FlashcardSessionDto, FlashcardSummaryDto } from '../api-contracts';
import { Session } from '../authentication/session';
import { CourseCharacterStudy } from './course-character-study';

const course = {
  id: 'test-study',
  version: '1.0.0',
  characters: [
    { id: 'two', glyph: '二' },
    { id: 'three', glyph: '三' },
  ],
  flashcards: ['two', 'three']
    .map((id) => ({
      id: 'read/' + id,
      deckId: 'read',
      noteId: id,
      tags: [] as string[],
    }))
    .concat(
      ['a', 'b'].map((id) => ({
        id: 'compare/' + id,
        deckId: 'compare',
        noteId: id,
        tags: ['pair'],
      })),
    ),
  characterStudy: {
    recognitionDeckId: 'read',
    confusionDeckId: 'compare',
    contrasts: [
      {
        id: 'pair',
        characterIds: ['two', 'three'],
        noteIds: ['a', 'b'],
        explanation: 'Count the strokes.',
      },
    ],
    readings: [
      {
        id: 'numbers',
        title: 'Two and three',
        requiredCharacterIds: ['two', 'three'],
        note: 'Read the numbers.',
        supportWords: [],
        lines: [
          {
            words: [
              { text: '二', pinyin: 'èr', meaning: 'two' },
              { text: '三', pinyin: 'sān', meaning: 'three' },
            ],
            translation: 'Two, three.',
          },
        ],
      },
    ],
  },
} as unknown as CourseCatalogDto;
const summary: FlashcardSummaryDto = {
  total: 4,
  new: 4,
  due: 0,
  suspended: 0,
  introduced: 0,
  upToDate: false,
  activeSessionId: null,
  cards: [],
};
const session: FlashcardSessionDto = {
  id: 's1',
  packId: course.id,
  releaseId: 'r1',
  revision: 0,
  position: 0,
  count: 2,
  completed: false,
  revealed: false,
  card: {
    id: 'compare/a',
    deckId: 'compare',
    noteId: 'a',
    front: 'Choose two.',
    language: 'zh-Hans',
    back: null,
    explanation: null,
    sources: [],
  },
};
const base = '/api/me/courses/test-study/flashcards';

async function render(signedIn = true) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  if (signedIn)
    TestBed.inject(Session).user.set({
      id: 'u1',
      email: 'u@example.test',
      displayName: 'Learner',
      publisher: false,
    });
  const fixture = TestBed.createComponent(CourseCharacterStudy);
  fixture.componentRef.setInput('course', course);
  fixture.detectChanges();
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);
  const refresh = async () => {
    await fixture.whenStable();
    fixture.detectChanges();
  };
  const flushSummary = () => {
    TestBed.tick();
    for (const req of http.match(base + '/')) req.flush(summary);
  };
  if (signedIn) {
    http.expectOne(base + '/').flush(summary);
    http
      .expectOne('/api/me/courses/test-study/writing/')
      .flush({ characters: [], due: 0, activeSessionId: null });
  }
  await refresh();
  return {
    fixture,
    http,
    refresh,
    flushSummary,
    component: fixture.componentInstance,
    element: fixture.nativeElement as HTMLElement,
  };
}

describe('guided character study', () => {
  it('does not restore an in-flight review after the learner signs out', async () => {
    const { component, http, refresh } = await render();
    const start = component['start']();
    const request = http.expectOne(base + '/sessions');
    TestBed.inject(Session).user.set(null);
    TestBed.tick();
    request.flush(session);
    await start;
    await refresh();
    expect(component['phase']()).toBe('overview');
    expect(component['review']()).toBeNull();
    http.verify();
  });
  it('saves a wrong comparison as Again, preserving the request through a failed save', async () => {
    const { component, http, refresh, flushSummary, element } = await render();
    const start = component['start']('pair');
    const request = http.expectOne(base + '/sessions');
    expect(request.request.body).toMatchObject({ deck: 'compare', tag: 'pair', limit: 2 });
    request.flush(session);
    await start;
    flushSummary();
    await refresh();
    const choose = component['choose']('三');
    http.expectOne(base + '/sessions/s1/reveal').flush({
      ...session,
      revealed: true,
      revision: 1,
      card: { ...session.card!, back: '二', explanation: 'Two strokes.' },
    });
    await choose;
    flushSummary();
    await refresh();
    expect(element.textContent).toContain('Look at the difference');
    expect(component['checkedChoice']()).toBe(false);
    const save = component['mutate']('rate', component['checkedChoice']() ? 'good' : 'again');
    const failed = http.expectOne(base + '/sessions/s1/rate');
    expect(failed.request.body.rating).toBe('again');
    failed.flush({ detail: 'Unavailable.' }, { status: 503, statusText: 'Unavailable' });
    await save;
    await refresh();
    expect(element.textContent).toContain('Retry saving this action');
    const retry = component['retry']();
    const retried = http.expectOne(base + '/sessions/s1/rate');
    expect(retried.request.body).toEqual(failed.request.body);
    retried.flush({ ...session, revision: 2, position: 1 });
    await retry;
    flushSummary();
    await refresh();
    expect(component['savedCount']()).toBe(1);
    expect(component['chosen']()).toBe('');
    http.verify();
  });
  it('offers a supported reading preview without writing review or assessment progress', async () => {
    const { component, element, http, refresh } = await render(false);
    component['openReading'](course.characterStudy!.readings[0]);
    await refresh();
    expect(element.textContent).toContain('Preview with help');
    expect(element.textContent).not.toContain('Two, three.');
    const word = element.querySelector<HTMLButtonElement>('.reading-word')!;
    word.click();
    await refresh();
    expect(word.getAttribute('aria-expanded')).toBe('true');
    expect(element.textContent).toContain('èr · two');
    component['showTranslation'].set(true);
    await refresh();
    expect(element.textContent).toContain('Two, three.');
    component['finishReading']();
    await refresh();
    expect(element.textContent).toContain('Reading complete');
    http.expectNone((r) => r.method !== 'GET');
    http.verify();
  });
  it('recovers a failed progress load without assuming the learner is new', async () => {
    const { component, http, refresh, element } = await render();
    component['summary'].reload();
    TestBed.tick();
    http
      .expectOne(base + '/')
      .flush({ detail: 'Progress is unavailable.' }, { status: 503, statusText: 'Unavailable' });
    await refresh();
    expect(element.textContent).toContain('Progress is unavailable.');
    expect(element.textContent).toContain('Reload review progress');
    await component['start']();
    http.expectNone(base + '/sessions');
    http.verify();
  });
  it('resumes a revealed comparison with self-rating when the original choice was not saved', async () => {
    const { component, http, refresh, flushSummary, element } = await render();
    component['summary'].reload();
    TestBed.tick();
    http.expectOne(base + '/').flush({ ...summary, activeSessionId: 's1' });
    await refresh();
    const resume = component['resume']();
    http.expectOne(base + '/sessions/s1').flush({
      ...session,
      revealed: true,
      revision: 1,
      card: { ...session.card!, back: '二', explanation: 'Two strokes.' },
    });
    await resume;
    await refresh();
    expect(element.textContent).toContain('How well did you recall it?');
    expect(element.textContent).not.toContain('You spotted it');
    expect(component['checkedChoice']()).toBeNull();
    component['overview']();
    flushSummary();
    TestBed.tick();
    for (const request of http.match('/api/me/courses/test-study/writing/'))
      request.flush({ characters: [], due: 0 });
    await refresh();
    http.verify();
  });
});
