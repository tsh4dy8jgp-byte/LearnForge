import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { FlashcardEditor } from './flashcard-editor';

describe('flashcard source editing', () => {
  it('preserves wrapped subject content and enables flashcards when adding a deck', () => {
    const fixture = TestBed.createComponent(FlashcardEditor);
    fixture.componentRef.setInput(
      'source',
      JSON.stringify({
        templates: { kept: { value: 42 } },
        pack: {
          id: 'subject',
          objectives: [{ id: 'concept', title: 'Concept' }],
          lessons: [{ id: 'unchanged' }],
          capabilities: { lessons: true, practice: false, assessments: false },
        },
      }),
    );
    let edited = '';
    fixture.componentInstance.changed.subscribe((value) => (edited = value));
    fixture.detectChanges();
    const button = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).find((b) => b.textContent?.includes('Add deck'))!;
    button.click();
    const result = JSON.parse(edited);
    expect(result.templates.kept.value).toBe(42);
    expect(result.pack.lessons).toEqual([{ id: 'unchanged' }]);
    expect(result.pack.capabilities.flashcards).toBe(true);
    expect(result.pack.flashcardDecks[0].id).toBe('recall-1');
  });
  it('rejects malformed note arrays without crashing or replacing the source', () => {
    const fixture = TestBed.createComponent(FlashcardEditor);
    fixture.componentRef.setInput('source', '{"flashcardDecks":[{"id":"d","notes":[null]}]}');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Repair the JSON source');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('button').length).toBe(0);
  });
});
