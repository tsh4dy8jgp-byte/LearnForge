import HanziWriter from 'hanzi-writer';
import { pointerTarget } from './pointer-target';
import type { WritingCharacter, WritingMode } from '../api-contracts';

export interface WritingFeedback {
  mistake(message: string): void;
  stroke(index: number): void;
  complete(): void;
}

/** Owns the third-party renderer; all character data comes from the pinned course release. */
export class WritingEngine {
  private writer: HanziWriter;
  private abort = new AbortController();
  private observer: ResizeObserver;
  private disposed = false;
  private currentStroke = 0;
  private options: { mode: WritingMode; reducedMotion: boolean; speed: number };

  constructor(
    private host: HTMLElement,
    character: WritingCharacter,
    options: { mode: WritingMode; reducedMotion: boolean; speed: number },
    private feedback: WritingFeedback,
  ) {
    this.options = options;
    const style = getComputedStyle(host);
    this.writer = HanziWriter.create(host, character.glyph, {
      rendererOverride: { createRenderTarget: pointerTarget(this.abort) },
      width: host.clientWidth || 300,
      height: host.clientWidth || 300,
      padding: 24,
      charDataLoader: () => character.strokeData,
      showCharacter: options.mode === 'watch' || options.mode === 'sequence',
      showOutline: options.mode === 'trace',
      strokeColor: style.getPropertyValue('--ink').trim(),
      drawingColor: style.getPropertyValue('--ink').trim(),
      outlineColor: style.getPropertyValue('--border-strong').trim(),
      highlightColor: style.getPropertyValue('--orange').trim(),
      strokeAnimationSpeed: options.speed,
      delayBetweenStrokes: options.reducedMotion ? 0 : 350,
      strokeFadeDuration: options.reducedMotion ? 0 : 200,
      highlightOnComplete: false,
    });
    this.observer = new ResizeObserver(() => {
      if (!this.disposed)
        this.writer.updateDimensions({ width: host.clientWidth, height: host.clientWidth });
    });
    this.observer.observe(host);
    if (['trace', 'copy', 'recall'].includes(options.mode)) this.quiz();
  }
  private quiz() {
    void this.writer.quiz({
      leniency: 1.4,
      acceptBackwardsStrokes: false,
      showHintAfterMisses: false,
      highlightOnComplete: false,
      onMistake: (data) => {
        if (this.disposed) return;
        this.feedback.mistake(
          data.isBackwards
            ? 'Try the opposite direction. Use Hint to see the start and direction.'
            : `Check stroke ${data.strokeNum + 1}: its order, starting point and approximate shape. Use Hint for a replay.`,
        );
      },
      onCorrectStroke: (data) => {
        if (this.disposed) return;
        this.currentStroke = data.strokeNum + 1;
        this.feedback.stroke(this.currentStroke);
      },
      onComplete: () => {
        if (!this.disposed) this.feedback.complete();
      },
    });
  }
  async animate() {
    if (this.options.reducedMotion) return;
    await this.writer.animateCharacter();
  }
  pause() {
    void this.writer.pauseAnimation();
  }
  resume() {
    void this.writer.resumeAnimation();
  }
  hint() {
    // Highlight supplies a direction-aware replay without accepting or skipping the stroke.
    void this.writer.highlightStroke(this.currentStroke);
  }
  async step(index: number) {
    if (this.options.reducedMotion) {
      this.writer.cancelQuiz();
      await this.writer.hideCharacter({ duration: 0 });
      // A static prefix of the model is rendered by the component in reduced-motion mode.
    } else await this.writer.animateStroke(index);
  }
  dispose() {
    this.disposed = true;
    this.abort.abort();
    this.observer.disconnect();
    this.writer.cancelQuiz();
    void this.writer.pauseAnimation();
    this.host.replaceChildren();
  }
}
