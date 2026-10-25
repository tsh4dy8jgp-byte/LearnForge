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
async function render(mode: 'watch' | 'sequence') {
  const fixture = TestBed.createComponent(CharacterTrainer);
  fixture.componentRef.setInput('character', character);
  fixture.componentRef.setInput('mode', mode);
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
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
});
