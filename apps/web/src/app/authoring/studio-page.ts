import { DOCUMENT } from '@angular/common';
import { questionFor } from './diagnostic-question';
import { Session } from '../authentication/session';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { AuthoringPreviewDto, DraftDto } from '../api-contracts';
import { DraftEditorState } from './draft-editor-state';
import { SourceEditor } from './source-editor';
import { LearnerPreview } from './learner-preview';
import { SiteSettingsStore } from '../site/site-settings-store';

@Component({
  imports: [FormsModule, RouterLink, SourceEditor, LearnerPreview],
  providers: [DraftEditorState],
  host: { '(window:beforeunload)': 'editor.beforeUnload($event)' },
  templateUrl: './studio-page.html',
})
export class StudioPage {
  protected readonly session = inject(Session);
  private readonly api = inject(ApiClient);
  protected readonly site = inject(SiteSettingsStore);
  protected readonly editor = inject(DraftEditorState);
  private readonly document = inject(DOCUMENT);
  protected readonly error = signal('');
  protected readonly published = signal(false);
  protected readonly report = signal<AuthoringPreviewDto | null>(null);
  protected readonly questionId = linkedSignal(() => this.report()?.questions[0]?.id ?? '');
  protected readonly errorText = message;
  canLeave() {
    return this.editor.canLeave();
  }
  protected editSource(source: string) {
    this.editor.source.set(source);
    this.report.set(null);
    this.published.set(false);
  }
  private reset(draft: DraftDto | null, title: string, source: string) {
    this.editor.reset(draft, title, source);
    this.error.set('');
    this.report.set(null);
    this.published.set(false);
  }
  protected async createStarter() {
    if (!(await this.editor.canLeave())) return;
    this.editor.busy.set(true);
    this.error.set('');
    try {
      const pack = await this.api.get<{ title: string }>(
        `/authoring/starters/${this.editor.profile()}`,
      );
      this.reset(null, pack.title, JSON.stringify(pack, null, 2));
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.editor.busy.set(false);
    }
  }
  protected async openDraft() {
    const id = this.editor.selectedDraft();
    if (!id || !(await this.editor.canLeave())) return;
    this.editor.busy.set(true);
    this.error.set('');
    try {
      const draft = await this.api.get<DraftDto>(`/authoring/drafts/${id}`);
      this.reset(draft, draft.title, draft.source);
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.editor.busy.set(false);
    }
  }
  protected async loadFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 2_000_000) {
      this.error.set('Source files must be under 2 MB.');
      return;
    }
    if (!(await this.editor.canLeave())) return;
    this.editor.busy.set(true);
    try {
      this.reset(null, file.name.slice(0, 120), await file.text());
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.editor.busy.set(false);
    }
  }
  protected async validate() {
    this.editor.busy.set(true);
    this.error.set('');
    this.published.set(false);
    try {
      this.report.set(
        await this.api.post<AuthoringPreviewDto>(
          '/authoring/preview',
          JSON.parse(this.editor.source()),
        ),
      );
    } catch (error) {
      this.report.set(null);
      this.error.set(message(error));
    } finally {
      this.editor.busy.set(false);
    }
  }
  protected async publish() {
    if (!this.report()?.success) return;
    this.editor.busy.set(true);
    this.error.set('');
    this.published.set(false);
    try {
      if (this.editor.dirty() && !(await this.editor.save())) return;
      await this.api.post('/authoring/publish', JSON.parse(this.editor.source()));
      this.published.set(true);
      this.report.set(null);
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.editor.busy.set(false);
    }
  }
  protected inspect(path: string) {
    const id = questionFor(this.report(), path);
    if (!id) return this.document.getElementById('pack-source')?.focus();
    this.questionId.set(id);
    this.document.getElementById('preview-question')?.focus();
  }
  protected download() {
    const url = URL.createObjectURL(new Blob([this.editor.source()], { type: 'application/json' }));
    const link = this.document.createElement('a');
    link.href = url;
    link.download = 'pack-source.private.json';
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
