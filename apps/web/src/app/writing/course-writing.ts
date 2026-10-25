import { Component, computed, inject, input, linkedSignal, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Session } from '../authentication/session';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import type {
  CourseCatalogDto,
  WritingMode,
  WritingSessionDto,
  WritingSummaryDto,
} from '../api-contracts';
import { CharacterTrainer, PracticeResult } from './character-trainer';

@Component({
  selector: 'lf-course-writing',
  imports: [FormsModule, RouterLink, DatePipe, CharacterTrainer],
  templateUrl: './course-writing.html',
  styleUrl: './course-writing.scss',
})
export class CourseWriting {
  readonly course = input.required<CourseCatalogDto>();
  readonly preview = input(false);
  protected readonly account = inject(Session);
  private readonly api = inject(ApiClient);
  private readonly trainer = viewChild(CharacterTrainer);
  protected readonly summary = httpResource<WritingSummaryDto>(() =>
    this.account.user() && !this.preview()
      ? `/api/me/courses/${this.course().id}/writing/`
      : undefined,
  );
  protected readonly selectedId = linkedSignal(() => this.course().characters?.[0]?.id ?? '');
  protected readonly lessonFilter = signal('');
  protected readonly dueOnly = signal(false);
  protected readonly mode = signal<WritingMode>('watch');
  protected readonly current = signal<WritingSessionDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly pending = signal<PracticeResult | null>(null);
  private resultKey = '';
  protected readonly active = computed(() => !!this.current() && !this.current()?.finishedAt);
  protected readonly writingLessons = computed(() =>
    this.course().lessons.filter((l) => this.course().characters?.some((c) => c.lessonId === l.id)),
  );
  protected readonly characters = computed(() =>
    (this.course().characters ?? []).filter(
      (c) =>
        (!this.lessonFilter() || c.lessonId === this.lessonFilter()) &&
        (!this.dueOnly() || this.isDue(c.id)),
    ),
  );
  protected readonly character = computed(
    () =>
      this.current()?.character ??
      this.course().characters?.find((c) => c.id === this.selectedId()),
  );
  protected readonly progress = computed(() =>
    this.summary.value()?.characters.find((c) => c.characterId === this.character()?.id),
  );
  protected readonly practiceMode = computed(() => this.current()?.mode ?? this.mode());
  protected readonly hideModel = computed(() => this.practiceMode() === 'recall' && this.active());
  protected readonly modes: { id: WritingMode; title: string; description: string }[] = [
    { id: 'watch', title: 'Watch', description: 'See the stroke order and listen.' },
    { id: 'trace', title: 'Trace', description: 'Follow a faint outline.' },
    { id: 'copy', title: 'Copy', description: 'Write beside the model.' },
    { id: 'recall', title: 'Recall', description: 'Write from meaning and Pinyin.' },
    { id: 'sequence', title: 'Stroke choices', description: 'Choose strokes without drawing.' },
  ];
  protected isDue(id: string) {
    const due = this.summary.value()?.characters.find((c) => c.characterId === id)?.dueAt;
    return !!due && new Date(due).getTime() <= Date.now();
  }
  protected select(id: string) {
    this.current.set(null);
    this.selectedId.set(id);
    this.pending.set(null);
    this.error.set('');
    this.notice.set('');
  }
  protected changeMode(mode: WritingMode) {
    this.current.set(null);
    this.pending.set(null);
    this.mode.set(mode);
    this.notice.set('');
  }
  protected async start() {
    const c = this.character();
    if (!c) return;
    await this.run(async () => {
      const session = await this.api.post<WritingSessionDto>(
        `/me/courses/${this.course().id}/writing/sessions`,
        {
          characterId: c.id,
          mode: this.mode(),
          requestId: crypto.randomUUID(),
          version: this.course().version,
        },
      );
      this.current.set(session);
      this.notice.set('Practice started. A reload lets you resume from this character.');
      this.summary.reload();
    });
  }
  protected async resume() {
    const id = this.summary.value()?.activeSessionId;
    if (!id) return;
    await this.run(async () => {
      const session = await this.api.get<WritingSessionDto>(
        `/me/courses/${this.course().id}/writing/sessions/${id}`,
      );
      this.current.set(session);
      this.selectedId.set(session.character.id);
      this.mode.set(session.mode);
      this.notice.set(
        'Resumed the saved character and mode. Draw this character again; unfinished strokes are not stored.',
      );
    });
  }
  protected completed(result: PracticeResult) {
    if (this.preview() || !this.active()) {
      this.notice.set('Practice complete. Sign in and start a saved session to record progress.');
      return;
    }
    this.pending.set(result);
    this.resultKey = crypto.randomUUID();
    void this.save();
  }
  protected finishEarly() {
    this.pending.set(this.trainer()?.result() ?? { completed: false, mistakes: 0, hints: 0 });
    this.resultKey = crypto.randomUUID();
    void this.save();
  }
  protected async save() {
    const current = this.current(),
      result = this.pending();
    if (!current || !result) return;
    await this.run(async () => {
      const saved = await this.api.post<WritingSessionDto>(
        `/me/courses/${this.course().id}/writing/sessions/${current.id}/result`,
        {
          requestId: this.resultKey,
          revision: current.revision,
          ...result,
        },
      );
      this.current.set(saved);
      this.pending.set(null);
      this.summary.reload();
      this.notice.set(
        result.completed
          ? 'Saved. Guided practice, independent recall and stroke choices are tracked separately.'
          : 'Session finished. No completion was recorded.',
      );
    });
  }
  protected print() {
    window.print();
  }
  protected errorText(e: unknown) {
    return message(e);
  }
  private async run(action: () => Promise<void>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (e) {
      this.error.set(message(e));
      this.summary.reload();
    } finally {
      this.busy.set(false);
    }
  }
}
