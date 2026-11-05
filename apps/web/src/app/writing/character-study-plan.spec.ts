import { describe, expect, it } from 'vitest';
import type {
  CharacterReading,
  CourseCatalogDto,
  FlashcardSummaryDto,
  WritingSummaryDto,
} from '../api-contracts';
import {
  introducedCharacters,
  missingCharacters,
  recommendReading,
  reviewAvailability,
} from './character-study-plan';

const cards = ['one', 'two', 'three'].map((id) => ({
  id: 'read/' + id,
  deckId: 'read',
  noteId: id,
  front: id,
  back: id,
  explanation: '',
  objectiveIds: [],
  tags: [],
  language: 'zh-Hans',
  sources: [],
  contentHash: id,
}));
const course = {
  flashcards: [...cards, { ...cards[0], id: 'compare/one', deckId: 'compare', tags: ['pair'] }],
  characterStudy: {
    recognitionDeckId: 'read',
    confusionDeckId: 'compare',
    contrasts: [],
    readings: [],
  },
} as unknown as CourseCatalogDto;
const status = (cards: FlashcardSummaryDto['cards']) => ({ cards }) as FlashcardSummaryDto;
const reading = (id: string, requiredCharacterIds: string[]) =>
  ({ id, requiredCharacterIds }) as CharacterReading;

describe('character study recommendations', () => {
  it('counts only due or new cards in the selected deck and excludes suspended cards', () => {
    const progress = status([
      {
        unitId: 'read/one',
        introduced: true,
        suspended: false,
        dueAt: '2026-10-25T00:00:00Z',
        revision: 1,
      },
      { unitId: 'read/two', introduced: false, suspended: true, dueAt: null, revision: 1 },
      {
        unitId: 'read/three',
        introduced: true,
        suspended: false,
        dueAt: '2026-10-28T00:00:00Z',
        revision: 1,
      },
    ]);
    expect(
      reviewAvailability(course, progress, 'read', undefined, Date.parse('2026-10-26T00:00:00Z')),
    ).toEqual({ due: 1, fresh: 0 });
    expect(reviewAvailability(course, progress, 'compare', 'pair')).toEqual({ due: 0, fresh: 1 });
    expect(reviewAvailability(course, progress, 'compare', 'missing')).toEqual({
      due: 0,
      fresh: 0,
    });
  });
  it('uses recognition and completed handwriting introductions, while comparisons stay separate', () => {
    const progress = status([
      { unitId: 'compare/one', introduced: true, suspended: false, dueAt: null, revision: 1 },
    ]);
    const writing = {
      characters: [
        { characterId: 'two', assistedCompletions: 1, independentCompletions: 0 },
        {
          characterId: 'three',
          assistedCompletions: 0,
          independentCompletions: 0,
          sequenceCompletions: 2,
        },
      ],
    } as WritingSummaryDto;
    expect([...introducedCharacters(course, progress, writing)]).toEqual(['two']);
    expect([...introducedCharacters(course, progress, writing, new Set(['read/one']))]).toEqual([
      'one',
      'two',
    ]);
  });
  it('recommends a reading only after all target characters have been introduced', () => {
    const first = reading('first', ['one']);
    const richer = reading('richer', ['one', 'two']);
    expect(recommendReading([first, richer], new Set())).toBeUndefined();
    expect(recommendReading([first, richer], new Set(['one']))).toBe(first);
    expect(recommendReading([first, richer], new Set(['one', 'two']))).toBe(richer);
    expect(missingCharacters(richer, new Set(['two']))).toEqual(['one']);
  });
});
