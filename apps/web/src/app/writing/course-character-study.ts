import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Session } from '../authentication/session';
import { ApiClient } from '../http/api-client';
import { ApiError, message } from '../http/api-error';
import type {
  CharacterReading,
  CourseCatalogDto,
  FlashcardSessionDto,
  FlashcardSummaryDto,
  ReviewRating,
  WritingSummaryDto,
} from '../api-contracts';
import {
  introducedCharacters,
  missingCharacters,
  recommendReading,
  reviewAvailability,
} from './character-study-plan';

type StudyStep = { kind: 'recognition' | 'confusion'; deck: string; limit: number; tag?: string };
type PendingReview = {
  path: string;
  body: unknown;
  action: 'start' | 'reveal' | 'rate' | 'finish';
};

@Component({
  selector: 'lf-course-character-study',
  imports: [RouterLink],
  templateUrl: './course-character-study.html',
  styleUrl: './course-character-study.scss',
})
export class CourseCharacterStudy {
  readonly course = input.required<CourseCatalogDto>();
  protected readonly account = inject(Session);
  private readonly api = inject(ApiClient);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly activityHeading = viewChild<ElementRef<HTMLElement>>('activityHeading');
  private readonly answerHeading = viewChild<ElementRef<HTMLElement>>('answerHeading');
  protected readonly summary = httpResource<FlashcardSummaryDto>(() =>
    this.account.user() ? `/api/me/courses/${this.course().id}/flashcards/` : undefined,
  );
  protected readonly writing = httpResource<WritingSummaryDto>(() =>
    this.account.user() ? `/api/me/courses/${this.course().id}/writing/` : undefined,
  );
  protected readonly config = computed(() => this.course().characterStudy!);
  protected readonly summaryData = computed(() =>
    this.summary.hasValue() ? this.summary.value() : undefined,
  );
  protected readonly writingData = computed(() =>
    this.writing.hasValue() ? this.writing.value() : undefined,
  );
  protected readonly phase = signal<'overview' | 'review' | 'reading' | 'done'>('overview');
  protected readonly steps = signal<StudyStep[]>([]);
  protected readonly stepIndex = signal(0);
  protected readonly step = computed(() => this.steps()[this.stepIndex()]);
  protected readonly review = signal<FlashcardSessionDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly pending = signal<PendingReview | null>(null);
  protected readonly chosen = signal('');
  protected readonly reviewed = signal<ReadonlySet<string>>(new Set());
  protected readonly savedCount = signal(0);
  protected readonly reading = signal<CharacterReading | null>(null);
  protected readonly activeWord = signal('');
  protected readonly showPinyin = signal(false);
  protected readonly showTranslation = signal(false);
  protected readonly readingOnly = signal(false);
  protected readonly availability = computed(() => ({
    recognition: reviewAvailability(
      this.course(),
      this.summaryData(),
      this.config().recognitionDeckId,
    ),
    confusion: reviewAvailability(this.course(), this.summaryData(), this.config().confusionDeckId),
  }));
  protected readonly introduced = computed(() =>
    introducedCharacters(this.course(), this.summaryData(), this.writingData(), this.reviewed()),
  );
  protected readonly suggestedReading = computed(() =>
    recommendReading(this.config().readings, this.introduced()),
  );
  protected readonly progressUnavailable = computed(
    () =>
      this.account.user() &&
      (!this.summary.hasValue() || this.summary.isLoading() || !!this.summary.error()),
  );
  protected readonly contrast = computed(() => {
    const card = this.review()?.card;
    return card?.deckId === this.config().confusionDeckId
      ? this.config().contrasts.find((c) => c.noteIds.includes(card.noteId))
      : undefined;
  });
  protected readonly choices = computed(() => {
    const pair = this.contrast();
    const session = this.review();
    if (!pair || !session?.card) return [];
    const characters = pair.characterIds.map(
      (id) => this.course().characters!.find((c) => c.id === id)!,
    );
    // Vary the positions while keeping them stable through reveal/retry/re-render.
    const seed = session.id + session.card.id;
    return [...seed].reduce((n, c) => n + c.charCodeAt(0), 0) % 2
      ? characters.reverse()
      : characters;
  });
  protected readonly checkedChoice = computed(() => {
    const current = this.review();
    if (!current?.revealed || !this.chosen()) return null;
    // Use the pinned session answer, including when a course was republished meanwhile.
    const answer = current.card?.back?.trim();
    if (!this.choices().some((c) => c.glyph === answer)) return null;
    return this.chosen() === answer;
  });
  protected readonly errorText = message;

  constructor() {
    effect(() => {
      this.course().id;
      this.account.user()?.id;
      this.phase.set('overview');
      this.review.set(null);
      this.steps.set([]);
      this.pending.set(null);
      this.chosen.set('');
      this.error.set('');
      this.savedCount.set(0);
      this.reviewed.set(new Set());
      this.reading.set(null);
    });
  }

  protected glyphs(ids: string[]) {
    return ids
      .map((id) => this.course().characters?.find((c) => c.id === id)?.glyph ?? '')
      .join(' ');
  }
  protected missing(reading: CharacterReading) {
    return missingCharacters(reading, this.introduced());
  }
  protected pairAvailable(id: string) {
    const { due, fresh } = reviewAvailability(
      this.course(),
      this.summaryData(),
      this.config().confusionDeckId,
      id,
    );
    return due + fresh > 0;
  }
  protected async start(pairId?: string) {
    if (!this.account.user() || this.progressUnavailable() || this.busy() || this.pending()) return;
    const available = this.availability();
    const steps: StudyStep[] = [];
    if (!pairId && available.recognition.due + available.recognition.fresh > 0)
      steps.push({ kind: 'recognition', deck: this.config().recognitionDeckId, limit: 3 });
    if (
      (pairId && this.pairAvailable(pairId)) ||
      (!pairId && available.confusion.due + available.confusion.fresh > 0)
    )
      steps.push({
        kind: 'confusion',
        deck: this.config().confusionDeckId,
        limit: 2,
        ...(pairId ? { tag: pairId } : {}),
      });
    this.steps.set(steps);
    this.stepIndex.set(0);
    this.savedCount.set(0);
    this.readingOnly.set(false);
    if (!steps.length) return this.openReading();
    this.phase.set('review');
    await this.startStep();
  }
  private async startStep() {
    const step = this.step();
    if (!step) return;
    this.review.set(null);
    this.chosen.set('');
    this.pending.set({
      path: `${this.base()}/sessions`,
      body: {
        requestId: crypto.randomUUID(),
        deck: step.deck,
        tag: step.tag ?? null,
        limit: step.limit,
      },
      action: 'start',
    });
    await this.retry();
  }
  protected async resume() {
    const id = this.summaryData()?.activeSessionId;
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    const context = { courseId: this.course().id, userId: this.account.user()?.id };
    try {
      const current = await this.api.get<FlashcardSessionDto>(`${this.base()}/sessions/${id}`);
      if (!this.isCurrent(context)) return;
      const deck = current.card?.deckId ?? this.config().recognitionDeckId;
      const kind = deck === this.config().confusionDeckId ? 'confusion' : 'recognition';
      const steps: StudyStep[] = [{ kind, deck, limit: current.count }];
      if (
        kind === 'recognition' &&
        this.availability().confusion.due + this.availability().confusion.fresh > 0
      )
        steps.push({ kind: 'confusion', deck: this.config().confusionDeckId, limit: 2 });
      this.steps.set(steps);
      this.stepIndex.set(0);
      this.savedCount.set(0);
      this.chosen.set('');
      this.review.set(current);
      this.phase.set('review');
      this.focusActivity();
    } catch (e) {
      if (!this.isCurrent(context)) return;
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  protected async choose(glyph: string) {
    if (this.busy() || this.pending() || this.review()?.revealed) return;
    this.chosen.set(glyph);
    await this.mutate('reveal');
  }
  protected async mutate(action: 'reveal' | 'rate' | 'finish', rating?: ReviewRating) {
    const current = this.review();
    if (!current || current.completed || this.busy() || this.pending()) return;
    this.pending.set({
      path: `${this.base()}/sessions/${current.id}/${action}`,
      body: { requestId: crypto.randomUUID(), revision: current.revision, rating: rating ?? null },
      action,
    });
    await this.retry();
  }
  protected async retry() {
    const pending = this.pending();
    if (!pending || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    const context = { courseId: this.course().id, userId: this.account.user()?.id };
    try {
      const previous = this.review();
      const current = await this.api.post<FlashcardSessionDto>(pending.path, pending.body);
      if (!this.isCurrent(context)) return;
      this.review.set(current);
      this.pending.set(null);
      if (pending.action === 'rate') {
        this.savedCount.update((n) => n + 1);
        if (previous?.card) this.reviewed.update((ids) => new Set([...ids, previous.card!.id]));
        this.chosen.set('');
      }
      this.summary.reload();
      this.focusActivity();
    } catch (e) {
      if (!this.isCurrent(context)) return;
      if (
        e instanceof ApiError &&
        e.status >= 400 &&
        e.status < 500 &&
        e.status !== 408 &&
        e.status !== 429
      ) {
        this.pending.set(null);
        this.summary.reload();
      }
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  protected async next() {
    if (!this.review()?.completed || this.busy()) return;
    if (this.stepIndex() + 1 < this.steps().length) {
      this.stepIndex.update((n) => n + 1);
      await this.startStep();
    } else this.openReading();
  }
  protected openReading(reading?: CharacterReading) {
    this.readingOnly.set(this.phase() === 'overview');
    this.reading.set(reading ?? this.suggestedReading() ?? this.config().readings[0]);
    this.activeWord.set('');
    this.showPinyin.set(false);
    this.showTranslation.set(false);
    this.phase.set('reading');
    this.focusActivity();
  }
  protected finishReading() {
    this.phase.set('done');
    this.focusActivity();
  }
  protected overview() {
    if (this.busy() || this.pending()) return;
    this.error.set('');
    this.phase.set('overview');
    this.summary.reload();
    this.writing.reload();
    this.focusActivity();
  }
  private base() {
    return `/me/courses/${this.course().id}/flashcards`;
  }
  private focusActivity() {
    if (this.destroyRef.destroyed) return;
    afterNextRender(
      () => {
        const heading =
          this.review()?.revealed && this.phase() === 'review'
            ? this.answerHeading()
            : this.activityHeading();
        heading?.nativeElement.focus();
      },
      { injector: this.injector },
    );
  }
  private isCurrent(context: { courseId: string; userId: string | undefined }) {
    return (
      !this.destroyRef.destroyed &&
      this.course().id === context.courseId &&
      this.account.user()?.id === context.userId
    );
  }
}
