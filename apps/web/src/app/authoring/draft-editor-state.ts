import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Session } from '../authentication/session';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { DraftDto, DraftSummaryDto, ProductProfile } from '../api-contracts';

@Injectable()
export class DraftEditorState {
  private readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  readonly drafts = httpResource<DraftSummaryDto[]>(() =>
    this.session.user()?.publisher ? '/api/authoring/drafts' : undefined,
  );
  readonly activeDraft = signal<DraftDto | null>(null);
  readonly selectedDraft = signal('');
  readonly profile = signal<ProductProfile>('hybrid');
  readonly title = signal('');
  readonly source = signal('');
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly dirty = computed(() =>
    this.activeDraft()
      ? this.title() !== this.activeDraft()!.title || this.source() !== this.activeDraft()!.source
      : !!(this.source() || this.title()),
  );
  private saveTask?: Promise<boolean>;

  constructor() {
    effect((onCleanup) => {
      // Edits during a request remain dirty and are saved after that request completes.
      if (!this.dirty() || !this.title().trim() || this.busy() || this.saving() || this.saveError())
        return;
      this.source();
      const timer = setTimeout(() => void this.save(), 1200);
      onCleanup(() => clearTimeout(timer));
    });
  }
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
  async canLeave() {
    if (this.busy()) return false;
    if (this.saveTask) await this.saveTask;
    return !this.dirty() || ((await this.save()) && !this.dirty());
  }
  save(asCopy = false): Promise<boolean> {
    if (this.saveTask) return this.saveTask;
    const task = this.saveDraft(asCopy);
    this.saveTask = task;
    void task.finally(() => {
      this.saveTask = undefined;
    });
    return task;
  }
  private async saveDraft(asCopy: boolean) {
    if (!this.title().trim()) {
      this.saveError.set('Give the draft a title.');
      return false;
    }
    this.saving.set(true);
    this.saveError.set('');
    const active = asCopy ? null : this.activeDraft();
    const payload = { title: this.title(), source: this.source(), revision: active?.revision ?? 0 };
    try {
      const draft = active
        ? await this.api.put<DraftDto>(`/authoring/drafts/${active.id}`, payload)
        : await this.api.post<DraftDto>('/authoring/drafts', payload);
      this.activeDraft.set(draft);
      this.selectedDraft.set(draft.id);
      if (this.title() === payload.title) this.title.set(draft.title);
      this.drafts.reload();
      return true;
    } catch (error) {
      this.saveError.set(message(error));
      return false;
    } finally {
      this.saving.set(false);
    }
  }
  reset(draft: DraftDto | null, title: string, source: string) {
    this.activeDraft.set(draft);
    this.title.set(title);
    this.source.set(source);
    this.selectedDraft.set(draft?.id ?? '');
    this.saveError.set('');
  }
}
