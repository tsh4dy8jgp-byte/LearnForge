import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Answer, Question } from './models';
@Component({
  selector: 'lf-question-input',
  imports: [FormsModule, CdkDrag, CdkDropList],
  template: ` @if (question().kind === 'single' || question().kind === 'multiple') {
      <fieldset [disabled]="disabled()">
        <legend>
          {{
            question().kind === 'single'
              ? 'Select one answer.'
              : 'Select ' + question().selectCount + ' answers.'
          }}
        </legend>
        <div class="choices">
          @for (option of question().options; track option.id; let i = $index) {
            <label class="choice" [class.selected]="answer().selected.includes(option.id)"
              ><input
                [type]="question().kind === 'single' ? 'radio' : 'checkbox'"
                [name]="question().id"
                [checked]="answer().selected.includes(option.id)"
                [disabled]="question().kind === 'multiple' && answer().selected.length >= question().selectCount && !answer().selected.includes(option.id)"
                (change)="choose(option.id)"
              /><span class="option-letter">{{ letter(i) }}</span
              ><span>{{ option.text }}</span></label
            >
          }
        </div>
      </fieldset>
    } @else if (question().kind === 'sequence') {
      <p class="muted">Arrange the sequence. Drag items or use the move buttons.</p>
      <div
        cdkDropList
        [cdkDropListDisabled]="disabled()"
        (cdkDropListDropped)="drop($event)"
        class="sequence-list"
      >
        @for (option of sequence(); track option.id; let i = $index) {
          <div class="sequence-item" cdkDrag [cdkDragDisabled]="disabled()">
            <span class="drag-handle" aria-hidden="true">⠿</span
            ><span class="muted">{{ i + 1 }}.</span><strong>{{ option.text }}</strong>
            <div class="row">
              <button
                type="button"
                class="icon-button"
                [disabled]="disabled() || i === 0"
                [attr.aria-label]="'Move ' + option.text + ' up'"
                (click)="move(i, -1)"
              >
                ↑</button
              ><button
                type="button"
                class="icon-button"
                [disabled]="disabled() || i === sequence().length - 1"
                [attr.aria-label]="'Move ' + option.text + ' down'"
                (click)="move(i, 1)"
              >
                ↓
              </button>
            </div>
          </div>
        }
      </div>
      <button
        class="button secondary"
        [disabled]="disabled()"
        (click)="changed.emit({ selected: sequenceIds(), slots: {} })"
      >
        Save this order
      </button>
    } @else if (question().kind === 'matching') {
      <p class="muted">
        Drag a token to its target, or select a token and then a target.
        {{ question().reuse ? 'Tokens may be reused.' : 'Use each token once.' }}
      </p>
      <div
        class="token-bank"
        cdkDropList
        id="token-bank"
        [cdkDropListConnectedTo]="targetIds()"
        [cdkDropListSortingDisabled]="true"
      >
        @for (option of question().options; track option.id) {
          <button
            type="button"
            cdkDrag
            [cdkDragData]="option.id"
            [cdkDragDisabled]="disabled()"
            [disabled]="disabled()"
            [attr.aria-pressed]="picked() === option.id"
            [class.selected]="picked() === option.id"
            class="token"
            (click)="picked.set(option.id)"
          >
            {{ option.text }}
          </button>
        }
      </div>
      <div class="matching-slots">
        @for (slot of question().slots; track slot.id) {
          <div
            class="match-target"
            cdkDropList
            [id]="'target-' + slot.id"
            [cdkDropListDisabled]="disabled()"
            (cdkDropListDropped)="place(slot.id, $event.item.data)"
          >
            <span>{{ slot.text }}</span
            ><button
              type="button"
              class="token target"
              [disabled]="disabled() || !picked()"
              (click)="place(slot.id, picked()!)"
            >
              {{ optionText(answer().slots[slot.id]) || 'Place token here' }}</button
            ><button
              class="text-button"
              [disabled]="disabled() || !answer().slots[slot.id]"
              (click)="setSlot(slot.id, '')"
            >
              Clear
            </button>
          </div>
        }
      </div>
    } @else if (question().kind === 'dropdown') {
      <fieldset [disabled]="disabled()">
        <legend>Choose an option for every blank.</legend>
        @for (slot of question().slots; track slot.id) {
          <label
            >{{ slot.text
            }}<select
              [ngModel]="answer().slots[slot.id] || ''"
              (ngModelChange)="setSlot(slot.id, $event)"
            >
              <option value="">Choose an answer…</option>
              @for (option of slot.options; track option.id) {
                <option [value]="option.id">{{ option.text }}</option>
              }
            </select></label
          >
        }
      </fieldset>
    } @else if (question().kind === 'numeric') {
      <label class="typed-answer"
        >Your answer
        <input
          type="text"
          inputmode="decimal"
          autocomplete="off"
          [disabled]="disabled()"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
          (keydown.enter)="saveText()"
      /></label>
      <p class="muted small">Enter a number such as 4 or -2.5.</p>
      <button class="button secondary" [disabled]="!canSaveText()" (click)="saveText()">
        Save answer
      </button>
    } @else if (question().kind === 'codeOutput') {
      @if (question().code; as code) {
        <figure class="code-sample">
          <figcaption class="muted small">{{ code.language }}</figcaption>
          <pre><code>{{ code.source }}</code></pre>
        </figure>
      }
      <label class="typed-answer"
        >Exact output
        <textarea
          rows="3"
          spellcheck="false"
          [disabled]="disabled()"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
        ></textarea>
      </label>
      <p class="muted small">Spacing and line endings do not affect the mark.</p>
      <button class="button secondary" [disabled]="!canSaveText()" (click)="saveText()">
        Save answer
      </button>
    }
    <p class="sr-only" aria-live="polite">{{ announcement() }}</p>`,
})
export class QuestionInput {
  readonly question = input.required<Question>();
  readonly answer = input<Answer>({ selected: [], slots: {} });
  readonly disabled = input(false);
  readonly changed = output<Answer>();
  readonly picked = signal<string | null>(null);
  readonly announcement = signal('');
  readonly sequence = computed(() => {
    const q = this.question();
    const ids = this.answer().selected;
    return ids.length === q.options.length
      ? ids.map((id) => q.options.find((o) => o.id === id)!).filter(Boolean)
      : q.options;
  });
  readonly targetIds = computed(() => this.question().slots.map((s) => 'target-' + s.id));
  // Typed answers are saved explicitly, like a sequence, so each keystroke is not a revision.
  readonly draft = linkedSignal(() => this.answer().text ?? '');
  readonly canSaveText = computed(
    () => !this.disabled() && !!this.draft().trim() && this.draft() !== (this.answer().text ?? ''),
  );
  saveText() {
    if (!this.canSaveText()) return;
    this.changed.emit({ selected: [], slots: {}, text: this.draft() });
    this.announcement.set('Answer saved');
  }
  letter(i: number) {
    return String.fromCharCode(65 + i);
  }
  sequenceIds() {
    return this.sequence().map((o) => o.id);
  }
  optionText(id: string) {
    return this.question().options.find((o) => o.id === id)?.text;
  }
  choose(id: string) {
    let selected = [...this.answer().selected];
    if (this.question().kind === 'single') selected = [id];
    else if (selected.includes(id)) selected = selected.filter((x) => x !== id);
    else if (selected.length < this.question().selectCount) selected.push(id);
    else return;
    this.changed.emit({ selected, slots: {} });
  }
  setSlot(id: string, value: string) {
    const slots = { ...this.answer().slots };
    if (value) slots[id] = value;
    else delete slots[id];
    this.changed.emit({ selected: [], slots });
  }
  place(id: string, value: string) {
    if (this.disabled() || !value) return;
    const slots = { ...this.answer().slots };
    if (!this.question().reuse)
      for (const key of Object.keys(slots)) if (slots[key] === value) delete slots[key];
    slots[id] = value;
    this.changed.emit({ selected: [], slots });
    this.picked.set(null);
    this.announcement.set('Placed ' + this.optionText(value));
  }
  move(index: number, delta: number) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, index, index + delta);
    this.changed.emit({ selected: ids, slots: {} });
    this.announcement.set('Moved to position ' + (index + delta + 1));
  }
  drop(event: CdkDragDrop<unknown>) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, event.previousIndex, event.currentIndex);
    this.changed.emit({ selected: ids, slots: {} });
  }
}
