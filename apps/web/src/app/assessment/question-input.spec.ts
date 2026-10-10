import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Answer, Question } from '../api-contracts';
import { QuestionInput } from './question-input';

const typed = (kind: 'numeric' | 'codeOutput' | 'shortAnswer'): Question => ({
  id: 'q-' + kind,
  kind,
  prompt: 'Prompt?',
  objectiveIds: ['sets'],
  options: [],
  slots: [],
  selectCount: 0,
  reuse: false,
  scenarioId: null,
  weight: 1,
  code: kind === 'codeOutput' ? { language: 'python', source: 'print(len({1, 2, 2}))' } : null,
});

async function render(question: Question) {
  const fixture = TestBed.createComponent(QuestionInput);
  fixture.componentRef.setInput('question', question);
  const emitted: Answer[] = [];
  fixture.componentInstance.changed.subscribe((a) => emitted.push(a));
  fixture.detectChanges();
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, emitted, fixture };
}

describe('typed question input', () => {
  it('saves a numeric answer as text when the learner saves it', async () => {
    const { element, emitted, fixture } = await render(typed('numeric'));
    const input = element.querySelector('input')!;
    input.value = ' 4.5 ';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('button')!.click();

    expect(input.getAttribute('inputmode')).toBe('decimal');
    expect(emitted).toEqual([{ selected: [], slots: {}, text: ' 4.5 ' }]);
  });

  it('shows the program and saves the typed output', async () => {
    const { element, emitted, fixture } = await render(typed('codeOutput'));
    expect(element.querySelector('pre')!.textContent).toContain('print(len({1, 2, 2}))');
    const area = element.querySelector('textarea')!;
    area.value = '2';
    area.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('button')!.click();

    expect(emitted).toEqual([{ selected: [], slots: {}, text: '2' }]);
  });

  it('saves a short answer with Enter', async () => {
    const { element, emitted, fixture } = await render(typed('shortAnswer'));
    const input = element.querySelector<HTMLInputElement>('input[type=text]')!;
    expect(input.getAttribute('spellcheck')).toBe('false');
    input.value = 'sommes partis';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(emitted).toEqual([{ selected: [], slots: {}, text: 'sommes partis' }]);
  });

  it('inserts an accented character at the caret from the keyboard-reachable palette', async () => {
    const { element, emitted, fixture } = await render(typed('shortAnswer'));
    const input = element.querySelector<HTMLInputElement>('input[type=text]')!;
    input.value = 'est allee';
    input.dispatchEvent(new Event('input'));
    input.setSelectionRange(7, 8);
    const accent = element.querySelector<HTMLButtonElement>('button[aria-label="Insert é"]')!;
    expect(accent.type).toBe('button');
    expect(accent.closest('details')!.querySelector('summary')!.textContent).toContain('Accents');
    accent.click();
    await fixture.whenStable();

    expect(input.value).toBe('est allée');
    expect(input.selectionStart).toBe(8);
    expect(document.activeElement).toBe(input);
    element.querySelector<HTMLButtonElement>('.button.secondary')!.click();
    expect(emitted).toEqual([{ selected: [], slots: {}, text: 'est allée' }]);
  });

  it('does not save an empty answer', async () => {
    const { element, emitted } = await render(typed('numeric'));
    expect(element.querySelector<HTMLButtonElement>('button')!.disabled).toBe(true);
    expect(emitted).toEqual([]);
  });
});
