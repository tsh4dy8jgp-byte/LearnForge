import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Answer, Question } from '../api-contracts';
@Component({
  selector: 'lf-question-input',
  imports: [FormsModule, CdkDrag, CdkDropList],
  templateUrl: './question-input.html',
})
export class QuestionInput {
  readonly question = input.required<Question>();
  readonly answer = input<Answer>({ selected: [], slots: {} });
  readonly disabled = input(false);
  readonly changed = output<Answer>();
  protected readonly picked = signal<string | null>(null);
  protected readonly announcement = signal('');
  protected readonly sequence = computed(() => {
    const q = this.question();
    const ids = this.answer().selected;
    return ids.length === q.options.length
      ? ids.map((id) => q.options.find((o) => o.id === id)!).filter(Boolean)
      : q.options;
  });
  protected readonly targetIds = computed(() => this.question().slots.map((s) => 'target-' + s.id));
  // Typed answers are saved explicitly, like a sequence, so each keystroke is not a revision.
  protected readonly draft = linkedSignal(() => this.answer().text ?? '');
  protected readonly canSaveText = computed(
    () => !this.disabled() && !!this.draft().trim() && this.draft() !== (this.answer().text ?? ''),
  );
  protected updateText(event: Event) {
    this.draft.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }
  protected saveText() {
    if (!this.canSaveText()) return;
    this.changed.emit({ selected: [], slots: {}, text: this.draft() });
    this.announcement.set('Answer saved');
  }
  protected letter(i: number) {
    return String.fromCharCode(65 + i);
  }
  protected sequenceIds() {
    return this.sequence().map((o) => o.id);
  }
  protected optionText(id: string) {
    return this.question().options.find((o) => o.id === id)?.text;
  }
  protected choose(id: string) {
    let selected = [...this.answer().selected];
    if (this.question().kind === 'single') selected = [id];
    else if (selected.includes(id)) selected = selected.filter((x) => x !== id);
    else if (selected.length < this.question().selectCount) selected.push(id);
    else return;
    this.changed.emit({ selected, slots: {} });
  }
  protected setSlot(id: string, value: string) {
    const slots = { ...this.answer().slots };
    if (value) slots[id] = value;
    else delete slots[id];
    this.changed.emit({ selected: [], slots });
  }
  protected place(id: string, value: string) {
    if (this.disabled() || !value) return;
    const slots = { ...this.answer().slots };
    if (!this.question().reuse)
      for (const key of Object.keys(slots)) if (slots[key] === value) delete slots[key];
    slots[id] = value;
    this.changed.emit({ selected: [], slots });
    this.picked.set(null);
    this.announcement.set('Placed ' + this.optionText(value));
  }
  protected move(index: number, delta: number) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, index, index + delta);
    this.changed.emit({ selected: ids, slots: {} });
    this.announcement.set('Moved to position ' + (index + delta + 1));
  }
  protected drop(event: CdkDragDrop<unknown>) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, event.previousIndex, event.currentIndex);
    this.changed.emit({ selected: ids, slots: {} });
  }
}
