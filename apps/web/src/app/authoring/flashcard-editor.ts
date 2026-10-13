import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FlashcardDeck, FlashcardNote, FlashcardKind } from '../api-contracts';

type SubjectSource = {
  format?: string;
  capabilities?: Record<string, boolean>;
  flashcardDecks?: FlashcardDeck[];
  objectives?: { id: string; title: string }[];
  domains?: { id: string; title: string }[];
};

@Component({
  selector: 'lf-flashcard-editor',
  imports: [FormsModule],
  templateUrl: './flashcard-editor.html',
})
export class FlashcardEditor {
  protected readonly clozeExample = '{{c1::answer::optional hint}}';
  readonly source = input.required<string>();
  readonly disabled = input(false);
  readonly changed = output<string>();
  protected readonly parsed = computed(() => {
    try {
      const root = JSON.parse(this.source());
      const body = (root.pack ?? root) as SubjectSource;
      if (!body || typeof body !== 'object' || Array.isArray(body)) return null;
      const objectives = body.objectives ?? body.domains ?? [];
      if (!Array.isArray(objectives) || objectives.some((o) => !o || typeof o.id !== 'string'))
        return null;
      if (
        body.flashcardDecks &&
        (!Array.isArray(body.flashcardDecks) ||
          body.flashcardDecks.some((d) => !d || !Array.isArray(d.notes)))
      )
        return null;
      if (
        body.flashcardDecks?.some((d) =>
          d.notes.some(
            (n) =>
              !n ||
              !Array.isArray(n.objectiveIds) ||
              (n.tags != null && !Array.isArray(n.tags)) ||
              (n.sources != null &&
                (!Array.isArray(n.sources) ||
                  n.sources.some(
                    (s) => !s || typeof s.title !== 'string' || typeof s.url !== 'string',
                  ))),
          ),
        )
      )
        return null;
      return { root, body };
    } catch {
      return null;
    }
  });
  protected readonly decks = computed(() => this.parsed()?.body.flashcardDecks ?? []);
  protected readonly objectives = computed(
    () => this.parsed()?.body.objectives ?? this.parsed()?.body.domains ?? [],
  );
  protected coverage(id: string) {
    return this.decks()
      .flatMap((d) => d.notes)
      .filter((n) => n.objectiveIds?.includes(id)).length;
  }
  private edit(action: (body: SubjectSource) => void) {
    if (this.disabled()) return;
    const parsed = this.parsed();
    if (!parsed) return;
    const root = structuredClone(parsed.root);
    const body = (root.pack ?? root) as SubjectSource;
    action(body);
    if (!body.format && body.capabilities)
      body.capabilities['flashcards'] = !!body.flashcardDecks?.length;
    this.changed.emit(JSON.stringify(root, null, 2));
  }
  protected addDeck() {
    this.edit((body) => {
      const decks = (body.flashcardDecks ??= []);
      let i = decks.length + 1;
      while (decks.some((d) => d.id === `recall-${i}`)) i++;
      decks.push({ id: `recall-${i}`, title: 'Recall deck', notes: [] });
    });
  }
  protected updateDeck(index: number, field: 'id' | 'title', value: string) {
    this.edit((b) => {
      b.flashcardDecks![index][field] = value;
    });
  }
  protected removeDeck(index: number) {
    this.edit((b) => {
      b.flashcardDecks!.splice(index, 1);
    });
  }
  protected addNote(deckIndex: number) {
    this.edit((body) => {
      const notes = body.flashcardDecks![deckIndex].notes;
      let i = notes.length + 1;
      while (notes.some((n) => n.id === `card-${i}`)) i++;
      notes.push({
        id: `card-${i}`,
        kind: 'basic',
        objectiveIds: this.objectives()
          .slice(0, 1)
          .map((o) => o.id),
        explanation: '',
        front: '',
        back: '',
        language: 'en',
        tags: [],
        sources: [],
      });
    });
  }
  protected removeNote(d: number, n: number) {
    this.edit((b) => {
      b.flashcardDecks![d].notes.splice(n, 1);
    });
  }
  protected updateNote(
    d: number,
    n: number,
    field: 'id' | 'front' | 'back' | 'text' | 'explanation' | 'language',
    value: string,
  ) {
    this.edit((b) => {
      b.flashcardDecks![d].notes[n][field] = value;
    });
  }
  protected kind(d: number, n: number, value: FlashcardKind) {
    this.edit((body) => {
      const note = body.flashcardDecks![d].notes[n];
      note.kind = value;
      if (value === 'cloze') {
        note.text = note.text ?? '{{c1::answer}} in context.';
        note.front = null;
        note.back = null;
      } else {
        note.front = note.front ?? '';
        note.back = note.back ?? '';
        note.text = null;
      }
    });
  }
  protected list(d: number, n: number, field: 'tags' | 'objectiveIds', value: string) {
    this.edit((b) => {
      b.flashcardDecks![d].notes[n][field] = value
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
    });
  }
  protected sourceText(note: FlashcardNote) {
    return (note.sources ?? []).map((s) => `${s.title} | ${s.url}`).join('\n');
  }
  protected sources(d: number, n: number, value: string) {
    this.edit((b) => {
      b.flashcardDecks![d].notes[n].sources = value
        .split('\n')
        .filter((s) => s.trim())
        .map((line) => {
          const separator = line.indexOf('|');
          return {
            title: separator < 0 ? line.trim() : line.slice(0, separator).trim(),
            url: separator < 0 ? '' : line.slice(separator + 1).trim(),
          };
        });
    });
  }
}
