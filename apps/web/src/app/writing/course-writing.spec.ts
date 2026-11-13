import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { CourseCatalogDto, WritingCharacter, WritingSessionDto } from '../api-contracts';
import { Session } from '../authentication/session';
import { CharacterTrainer } from './character-trainer';
import { CourseWriting } from './course-writing';

const character: WritingCharacter = {
  id: 'one',
  glyph: '一',
  pinyin: 'yī',
  meaning: 'one',
  lessonId: 'first',
  objectiveIds: ['first'],
  components: 'One stroke.',
  tip: 'Left to right.',
  words: [{ text: '一个', pinyin: 'yí ge', meaning: 'one item' }],
  sentence: '一个人。',
  sentencePinyin: 'Yí ge rén.',
  translation: 'One person.',
  glossary: '个: counter.',
  audioUrl: '/writing/audio/u4e00.wav',
  audioTranscript: '一 · yī',
  strokeData: {
    strokes: ['M 0 0 L 900 0 L 900 50 Z'],
    medians: [
      [
        [0, 25],
        [900, 25],
      ],
    ],
  },
};
const course = {
  id: 'test-writing',
  version: '1.0.0',
  title: 'Writing',
  lessons: [{ id: 'first', title: 'First' }],
  characters: [character],
} as unknown as CourseCatalogDto;
const base = '/api/me/courses/test-writing/writing';
const summary = {
  packId: course.id,
  version: course.version,
  due: 0,
  activeSessionId: null,
  characters: [],
};
// The server returns its own copy of the pinned character, so object identity differs.
const session = (overrides: Partial<WritingSessionDto> = {}): WritingSessionDto =>
  ({
    id: 's1',
    packId: course.id,
    version: course.version,
    character: structuredClone(character),
    mode: 'trace',
    revision: 0,
    startedAt: '2026-11-07T00:00:00Z',
    finishedAt: null,
    completed: false,
    mistakes: 0,
    hints: 0,
    ...overrides,
  }) as WritingSessionDto;
const result = { completed: true, mistakes: 0, hints: 0 };

async function render() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  TestBed.inject(Session).user.set({
    id: 'u1',
    email: 'u@example.test',
    displayName: 'Learner',
    publisher: false,
  });
  const fixture = TestBed.createComponent(CourseWriting);
  fixture.componentRef.setInput('course', course);
  fixture.detectChanges();
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);
  http.expectOne(base + '/').flush(summary);
  const refresh = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };
  await refresh();
  const button = (name: string) =>
    [...fixture.nativeElement.querySelectorAll('button')].find(
      (el) => (el as Element).textContent?.trim() === name,
    ) as HTMLButtonElement | undefined;
  const board = () => fixture.nativeElement.querySelector('.writing-canvas svg') as Element | null;
  const choose = async (mode: string) => {
    button(mode)!.click();
    await refresh();
  };
  // A response is applied a few microtasks after its flush and then reloads the summary, which
  // would keep whenStable() pending until it is answered.
  const until = (ready: () => void) =>
    vi.waitFor(() => {
      TestBed.tick();
      for (const req of http.match(base + '/')) req.flush(summary);
      fixture.detectChanges();
      ready();
    });
  const startSaved = async () => {
    button('Start saved practice')!.click();
    await refresh();
    http.expectOne(base + '/sessions').flush(session());
    await until(() => expect(button('Finish session')).toBeTruthy());
    await refresh();
  };
  const finish = () =>
    fixture.debugElement
      .query(By.directive(CharacterTrainer))
      .componentInstance.finished.emit(result);
  return { fixture, http, refresh, until, button, board, choose, startSaved, finish };
}

describe('CourseWriting for a signed-in learner', () => {
  beforeEach(() => {
    // The DOM test environment has neither of these browser layout APIs; the renderer stays real.
    vi.stubGlobal(
      'ResizeObserver',
      class {
        observe() {}
        disconnect() {}
      },
    );
    vi.stubGlobal('getComputedStyle', () => ({ getPropertyValue: () => '#123456' }));
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
  });
  afterEach(() => vi.unstubAllGlobals());

  it('provides a drawing board in Trace before any saved session', async () => {
    const { fixture, choose, http } = await render();
    await choose('Trace');
    expect(fixture.nativeElement.querySelector('.writing-board.drawing')).toBeTruthy();
    http.expectNone(base + '/sessions');
  });

  it('offers saved practice in drawing modes but not in Watch', async () => {
    const { button, choose } = await render();
    expect(button('Start saved practice')).toBeUndefined();
    await choose('Trace');
    expect(button('Start saved practice')).toBeTruthy();
    await choose('Watch');
    expect(button('Start saved practice')).toBeUndefined();
  });

  it('clears unsaved strokes when saved practice starts', async () => {
    const { board, choose, startSaved } = await render();
    await choose('Trace');
    const unsaved = board();
    expect(unsaved).toBeTruthy();
    await startSaved();
    expect(board()).toBeTruthy();
    expect(board()).not.toBe(unsaved);
  });

  it('keeps the finished character on the board when its result is saved', async () => {
    const { fixture, board, choose, finish, http, refresh, startSaved, until } = await render();
    await choose('Trace');
    await startSaved();
    const drawn = board();
    expect(drawn).toBeTruthy();
    finish();
    await refresh();
    http
      .expectOne(base + '/sessions/s1/result')
      .flush(session({ finishedAt: '2026-11-07T00:01:00Z', completed: true, revision: 1 }));
    await until(() => expect(fixture.nativeElement.textContent).toContain('Saved.'));
    expect(board()).toBe(drawn);
  });

  it('says an attempt without a session was not saved, without asking to sign in', async () => {
    const { fixture, choose, finish, http, refresh } = await render();
    await choose('Trace');
    finish();
    await refresh();
    http.expectNone(base + '/sessions/s1/result');
    const notice = fixture.nativeElement.querySelector('.alert.success').textContent;
    expect(notice).toContain('not saved');
    expect(notice).not.toContain('Sign in');
  });

  it('does not post a second result while one is waiting to be retried', async () => {
    const { button, choose, finish, http, refresh, startSaved, until } = await render();
    await choose('Trace');
    await startSaved();
    finish();
    await refresh();
    http
      .expectOne(base + '/sessions/s1/result')
      .flush({ title: 'Unavailable' }, { status: 503, statusText: 'Unavailable' });
    await until(() => expect(button('Retry saving result')?.disabled).toBe(false));
    finish();
    http.expectNone(base + '/sessions/s1/result');
  });
});
