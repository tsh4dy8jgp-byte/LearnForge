import {
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  afterNextRender,
  viewChild,
  ElementRef,
  Injector,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Session } from '../authentication/session';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import {
  CourseCatalogDto,
  FlashcardSummaryDto,
  FlashcardSessionDto,
  ReviewRating,
} from '../api-contracts';
import { FlashcardExports } from './flashcard-exports';

@Component({
  selector: 'lf-course-flashcards',
  imports: [FormsModule, RouterLink, DatePipe, FlashcardExports],
  templateUrl: './course-flashcards.html',
  styles: [
    `
      .card-text {
        white-space: pre-wrap;
        overflow-wrap: anywhere;
      }
      .recall-card {
        max-width: 48rem;
        margin: 1rem auto;
      }
      .row {
        flex-wrap: wrap;
        justify-content: flex-start;
      }
      details {
        margin-block: 1rem;
      }
      summary {
        cursor: pointer;
      }
      .card-list {
        max-height: 32rem;
        overflow: auto;
      }
    `,
  ],
})
export class CourseFlashcards {
  readonly course = input.required<CourseCatalogDto>();
  protected readonly account = inject(Session);
  private readonly api = inject(ApiClient);
  private readonly injector = inject(Injector);
  private readonly questionHeading = viewChild<ElementRef<HTMLElement>>('questionHeading');
  private readonly answerHeading = viewChild<ElementRef<HTMLElement>>('answerHeading');
  private readonly reviewStatus = viewChild<ElementRef<HTMLElement>>('reviewStatus');
  protected readonly summary = httpResource<FlashcardSummaryDto>(() =>
    this.account.user() ? `/api/me/courses/${this.course().id}/flashcards/` : undefined,
  );
  protected readonly session = signal<FlashcardSessionDto | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly deck = signal('');
  protected readonly objective = signal('');
  protected readonly tag = signal('');
  protected readonly tags = computed(() =>
    [...new Set((this.course().flashcards ?? []).flatMap((c) => c.tags))].sort(),
  );
  protected readonly cards = computed(() =>
    (this.course().flashcards ?? []).filter(
      (c) =>
        (!this.deck() || c.deckId === this.deck()) &&
        (!this.objective() || c.objectiveIds.includes(this.objective())) &&
        (!this.tag() || c.tags.includes(this.tag())),
    ),
  );
  protected readonly states = computed(
    () => new Map((this.summary.value()?.cards ?? []).map((s) => [s.unitId, s])),
  );
  protected readonly errorText = message;
  private pending?: { action: string; path: string; body: unknown };
  constructor() {
    effect(() => {
      this.course().id;
      this.session.set(null);
      this.pending = undefined;
    });
  }
  private base() {
    return `/me/courses/${this.course().id}/flashcards`;
  }
  protected async resume() {
    const id = this.summary.value()?.activeSessionId;
    if (!id) return;
    await this.run(async () => {
      this.session.set(await this.api.get(`${this.base()}/sessions/${id}`));
      this.pending = undefined;
      this.focusCard();
    });
  }
  protected async start() {
    if (!this.pending)
      this.pending = {
        action: 'start',
        path: `${this.base()}/sessions`,
        body: {
          requestId: crypto.randomUUID(),
          deck: this.deck() || null,
          objective: this.objective() || null,
          tag: this.tag() || null,
        },
      };
    await this.retry();
  }
  protected async mutate(action: 'reveal' | 'rate' | 'finish', rating?: ReviewRating) {
    const current = this.session();
    if (!current) return;
    if (!this.pending)
      this.pending = {
        action,
        path: `${this.base()}/sessions/${current.id}/${action}`,
        body: {
          requestId: crypto.randomUUID(),
          revision: current.revision,
          rating: rating ?? null,
        },
      };
    await this.retry();
  }
  protected async retry() {
    const pending = this.pending;
    if (!pending) return;
    await this.run(async () => {
      this.session.set(await this.api.post<FlashcardSessionDto>(pending.path, pending.body));
      this.pending = undefined;
      this.summary.reload();
      this.focusCard();
    });
  }
  protected get retryPending() {
    return !!this.pending;
  }
  private focusCard() {
    afterNextRender(
      () =>
        (this.session()?.completed
          ? this.reviewStatus()
          : this.session()?.revealed
            ? this.answerHeading()
            : this.questionHeading()
        )?.nativeElement.focus(),
      { injector: this.injector },
    );
  }
  protected async suspend(id: string) {
    const state = this.states().get(id);
    if (!state) return;
    await this.run(async () => {
      await this.api.put(`${this.base()}/suspension`, {
        unitId: id,
        suspended: !state.suspended,
        revision: state.revision,
      });
      this.summary.reload();
    });
  }
  private async run(action: () => Promise<void>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
