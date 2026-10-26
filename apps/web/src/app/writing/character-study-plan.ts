import type {
  CharacterReading,
  CourseCatalogDto,
  FlashcardSummaryDto,
  WritingSummaryDto,
} from '../api-contracts';

export function reviewAvailability(
  course: CourseCatalogDto,
  summary: FlashcardSummaryDto | undefined,
  deck: string,
  tag?: string,
  now = Date.now(),
) {
  const states = new Map((summary?.cards ?? []).map((s) => [s.unitId, s]));
  let due = 0;
  let fresh = 0;
  for (const card of course.flashcards ?? []) {
    if (card.deckId !== deck || (tag && !card.tags.includes(tag))) continue;
    const state = states.get(card.id);
    if (state?.suspended) continue;
    if (!state?.introduced) fresh++;
    else if (state.dueAt && Date.parse(state.dueAt) <= now) due++;
  }
  return { due, fresh };
}

export function introducedCharacters(
  course: CourseCatalogDto,
  summary?: FlashcardSummaryDto,
  writing?: WritingSummaryDto,
  justReviewed: ReadonlySet<string> = new Set(),
) {
  const reviewed = new Set([
    ...(summary?.cards ?? []).filter((s) => s.introduced).map((s) => s.unitId),
    ...justReviewed,
  ]);
  const known = new Set(
    (course.flashcards ?? [])
      .filter((c) => c.deckId === course.characterStudy?.recognitionDeckId && reviewed.has(c.id))
      .map((c) => c.noteId),
  );
  for (const state of writing?.characters ?? [])
    if (state.assistedCompletions > 0 || state.independentCompletions > 0)
      known.add(state.characterId);
  return known;
}

export function missingCharacters(reading: CharacterReading, introduced: ReadonlySet<string>) {
  return reading.requiredCharacterIds.filter((id) => !introduced.has(id));
}

export function recommendReading(readings: CharacterReading[], introduced: ReadonlySet<string>) {
  // Prefer the richest available reading, with authored order breaking ties.
  return [...readings]
    .filter((r) => missingCharacters(r, introduced).length === 0)
    .sort((a, b) => b.requiredCharacterIds.length - a.requiredCharacterIds.length)[0];
}
