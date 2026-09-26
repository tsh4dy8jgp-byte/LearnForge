import { describe, expect, it } from 'vitest';
import { describeAnswer, describeExpected, hasResponse } from './answers';
import { Grade, Question } from './models';

const question = (overrides: Partial<Question>): Question => ({
  id: 'q',
  kind: 'single',
  prompt: 'Prompt?',
  objectiveIds: ['sets'],
  options: [
    { id: 'a', text: 'Alpha' },
    { id: 'b', text: 'Beta' },
  ],
  slots: [],
  selectCount: 1,
  reuse: false,
  scenarioId: null,
  weight: 1,
  ...overrides,
});

const grade = (correct: string[]): Grade => ({
  earned: 1,
  possible: 1,
  fullyCorrect: true,
  correct,
  matches: null,
  explanation: 'Because.',
});

describe('answers', () => {
  it('treats selections, slots and typed text as responses', () => {
    expect(hasResponse(undefined)).toBe(false);
    expect(hasResponse({ selected: [], slots: {} })).toBe(false);
    expect(hasResponse({ selected: [], slots: {}, text: '   ' })).toBe(false);
    expect(hasResponse({ selected: ['a'], slots: {} })).toBe(true);
    expect(hasResponse({ selected: [], slots: { s: 'a' } })).toBe(true);
    expect(hasResponse({ selected: [], slots: {}, text: '4' })).toBe(true);
  });

  it('describes choices by their text and typed answers verbatim', () => {
    expect(describeAnswer(question({}), { selected: ['b'], slots: {} })).toBe('Beta');
    const numeric = question({ kind: 'numeric', options: [], selectCount: 0 });
    expect(describeAnswer(numeric, { selected: [], slots: {}, text: '4.5' })).toBe('4.5');
    expect(describeAnswer(numeric, { selected: [], slots: {} })).toBe('Unanswered');
  });

  it('lists every accepted value for typed questions', () => {
    const output = question({ kind: 'codeOutput', options: [], selectCount: 0 });
    expect(describeExpected(output, grade(['3 [1, 2]', '3 [1,2]']))).toBe('3 [1, 2] or 3 [1,2]');
    expect(describeExpected(question({}), grade(['a']))).toBe('Alpha');
  });
});
