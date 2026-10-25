import { Component, ElementRef, effect, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { WritingCharacter, WritingMode } from '../api-contracts';
import { WritingEngine } from './writing-engine';

export interface PracticeResult {
  completed: boolean;
  mistakes: number;
  hints: number;
}

@Component({
  selector: 'lf-character-trainer',
  imports: [FormsModule],
  templateUrl: './character-trainer.html',
  styleUrl: './character-trainer.scss',
})
export class CharacterTrainer {
  readonly character = input.required<WritingCharacter>();
  readonly mode = input<WritingMode>('watch');
  readonly finished = output<PracticeResult>();
  protected readonly host = viewChild<ElementRef<HTMLElement>>('canvas');
  protected readonly mistakes = signal(0);
  protected readonly hints = signal(0);
  protected readonly stroke = signal(0);
  protected readonly done = signal(false);
  protected readonly feedback = signal('');
  protected readonly speed = signal(1);
  protected readonly grid = signal('cross');
  protected readonly numbers = signal(false);
  protected readonly reduced = signal(
    globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false,
  );
  protected readonly revision = signal(0);
  protected readonly modelShown = signal(false);
  protected readonly staticHint = signal<number | null>(null);
  protected readonly paused = signal(false);
  protected readonly playing = signal(false);
  protected readonly choices = signal<number[]>([]);
  private engine?: WritingEngine;

  constructor() {
    effect((onCleanup) => {
      const character = this.character();
      const mode = this.mode();
      const host = this.host()?.nativeElement;
      const reducedMotion = this.reduced();
      const speed = this.speed();
      this.revision();
      this.mistakes.set(0);
      this.hints.set(0);
      this.stroke.set(0);
      this.done.set(false);
      this.modelShown.set(false);
      this.staticHint.set(null);
      this.paused.set(false);
      this.playing.set(false);
      this.feedback.set(
        mode === 'sequence'
          ? 'Choose the stroke that comes next. Each option shows one isolated stroke.'
          : mode === 'watch'
            ? 'Watch the model, or step through its strokes.'
            : 'Draw one stroke at a time. Lift your finger or mouse between strokes.',
      );
      this.choices.set(
        character.strokeData.strokes.map((_, i) => i).sort(() => Math.random() - 0.5),
      );
      if (!host || (mode === 'watch' && reducedMotion)) return;
      const engine = new WritingEngine(
        host,
        character,
        { mode, reducedMotion, speed },
        {
          mistake: (text) => {
            this.mistakes.update((v) => v + 1);
            this.feedback.set(text);
          },
          stroke: (n) => {
            this.stroke.set(n);
            this.staticHint.set(null);
            this.feedback.set(`Stroke ${n} accepted.`);
          },
          complete: () => this.complete(),
        },
      );
      this.engine = engine;
      onCleanup(() => {
        engine.dispose();
        if (this.engine === engine) this.engine = undefined;
      });
    });
  }
  protected async play() {
    this.playing.set(true);
    this.paused.set(false);
    try {
      await this.engine?.animate();
    } finally {
      this.playing.set(false);
      this.paused.set(false);
    }
  }
  protected pause() {
    if (this.paused()) this.engine?.resume();
    else this.engine?.pause();
    this.paused.update((v) => !v);
  }
  protected reset() {
    this.revision.update((v) => v + 1);
  }
  protected hint() {
    this.hints.update((v) => v + 1);
    if (this.reduced()) this.staticHint.set(this.stroke());
    else this.engine?.hint();
    this.feedback.set(
      `Replay of stroke ${this.stroke() + 1}. Start at its beginning and follow its direction.`,
    );
  }
  protected showModel() {
    this.modelShown.set(true);
    this.hints.update((v) => v + 1);
  }
  protected step(index: number) {
    this.stroke.set(index + 1);
    void this.engine?.step(index);
  }
  protected choose(index: number) {
    if (this.done()) return;
    if (index !== this.stroke()) {
      this.mistakes.update((v) => v + 1);
      this.feedback.set('Try another stroke. Think about which part is written first.');
      return;
    }
    this.stroke.update((v) => v + 1);
    this.feedback.set(`Stroke ${this.stroke()} chosen correctly.`);
    if (this.stroke() === this.character().strokeData.strokes.length) this.complete();
  }
  private complete() {
    this.done.set(true);
    this.feedback.set('Character complete. Take a moment to compare it with the model.');
    this.finished.emit({ completed: true, mistakes: this.mistakes(), hints: this.hints() });
  }
  result(): PracticeResult {
    return { completed: false, mistakes: this.mistakes(), hints: this.hints() };
  }
}
