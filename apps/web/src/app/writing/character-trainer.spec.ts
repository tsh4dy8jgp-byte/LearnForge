import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { WritingCharacter } from '../api-contracts';
import { CharacterTrainer } from './character-trainer';

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
    strokes: ['M 0 0 L 10 10 Z'],
    medians: [
      [
        [0, 0],
        [10, 10],
      ],
    ],
  },
};
const FIRST_STROKE = 'M 1 1 L 2 2 Z';
const SECOND_STROKE = 'M 3 3 L 4 4 Z';
const twoStrokes: WritingCharacter = {
  ...character,
  id: 'two',
  glyph: '二',
  strokeData: {
    strokes: [FIRST_STROKE, SECOND_STROKE],
    medians: [
      [
        [1, 1],
        [2, 2],
      ],
      [
        [3, 3],
        [4, 4],
      ],
    ],
  },
};
async function render(mode: 'watch' | 'sequence', target = character) {
  const fixture = TestBed.createComponent(CharacterTrainer);
  fixture.componentRef.setInput('character', target);
  fixture.componentRef.setInput('mode', mode);
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
}
type Fixture = Awaited<ReturnType<typeof render>>;
function clearButton(fixture: Fixture) {
  return [...fixture.nativeElement.querySelectorAll('button')].find((el: Element) =>
    el.textContent?.includes('Clear / retry'),
  ) as HTMLButtonElement;
}
function strokeOption(fixture: Fixture, stroke: string) {
  return [...fixture.nativeElement.querySelectorAll('.stroke-choices button')].find(
    (el: Element) => el.querySelector('path')?.getAttribute('d') === stroke,
  ) as HTMLButtonElement;
}
async function settle(fixture: Fixture) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}
describe('CharacterTrainer', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
    vi.stubGlobal('matchMedia', () => ({ matches: true }));
  });
  afterEach(() => vi.unstubAllGlobals());
  it('offers non-drawing stroke choices and emits completion only once', async () => {
    const fixture = await render('sequence');
    const finished = vi.fn();
    fixture.componentInstance.finished.subscribe(finished);
    const button = fixture.nativeElement.querySelector(
      '.stroke-choices button',
    ) as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    expect(finished).toHaveBeenCalledExactlyOnceWith({ completed: true, mistakes: 0, hints: 0 });
    expect(button.disabled).toBe(true);
    button.click();
    expect(finished).toHaveBeenCalledTimes(1);
  });
  it('shows a static stroke prefix when reduced motion is selected', async () => {
    const fixture = await render('watch');
    const toggle = [...fixture.nativeElement.querySelectorAll('input')].find((el: Element) =>
      el.parentElement?.textContent?.includes('Static stroke steps'),
    ) as HTMLInputElement;
    expect(toggle.checked).toBe(true);
    const button = [...fixture.nativeElement.querySelectorAll('button')].find(
      (el: Element) => el.textContent?.trim() === 'Stroke 1',
    ) as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.static-model path')).toHaveLength(1);
    expect(fixture.nativeElement.textContent).not.toContain('Play stroke order');
  });
  it('shows a different isolated stroke on each stroke choice', async () => {
    const fixture = await render('sequence', twoStrokes);
    const paths = [...fixture.nativeElement.querySelectorAll('.stroke-choices path')].map((el) =>
      (el as Element).getAttribute('d'),
    );
    expect(paths.sort()).toEqual([FIRST_STROKE, SECOND_STROKE]);
  });
  it('enables Clear / retry only when there is an attempt to clear', async () => {
    const fixture = await render('sequence', twoStrokes);
    expect(clearButton(fixture).disabled).toBe(true);
    strokeOption(fixture, SECOND_STROKE).click();
    await settle(fixture);
    expect(fixture.nativeElement.textContent).toContain('1 retries');
    expect(clearButton(fixture).disabled).toBe(false);
    clearButton(fixture).click();
    await settle(fixture);
    expect(fixture.nativeElement.textContent).toContain('0 retries');
    expect(clearButton(fixture).disabled).toBe(true);
  });
  it('lets a learner retry a character after finishing it', async () => {
    const fixture = await render('sequence', twoStrokes);
    const finished = vi.fn();
    fixture.componentInstance.finished.subscribe(finished);
    strokeOption(fixture, FIRST_STROKE).click();
    await settle(fixture);
    strokeOption(fixture, SECOND_STROKE).click();
    await settle(fixture);
    expect(finished).toHaveBeenCalledTimes(1);
    clearButton(fixture).click();
    await settle(fixture);
    strokeOption(fixture, FIRST_STROKE).click();
    await settle(fixture);
    strokeOption(fixture, SECOND_STROKE).click();
    await settle(fixture);
    expect(finished).toHaveBeenCalledTimes(2);
  });
  it('enables the Watch reset only after stepping away from the full model', async () => {
    const fixture = await render('watch');
    expect(clearButton(fixture).disabled).toBe(true);
    const step = [...fixture.nativeElement.querySelectorAll('button')].find(
      (el: Element) => el.textContent?.trim() === 'Stroke 1',
    ) as HTMLButtonElement;
    step.click();
    await settle(fixture);
    expect(clearButton(fixture).disabled).toBe(false);
    clearButton(fixture).click();
    await settle(fixture);
    expect(fixture.nativeElement.querySelectorAll('.static-model path')).toHaveLength(0);
    expect(clearButton(fixture).disabled).toBe(true);
  });
});
