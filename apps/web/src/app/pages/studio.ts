import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Answer, AuthoringPreviewDto, DraftDto, DraftSummaryDto, ProductProfile } from '../models';
import { LessonContent } from '../lesson-content';
import { QuestionInput } from '../question-input';
import { Site } from '../site-settings';

@Component({
  imports: [FormsModule, RouterLink, LessonContent, QuestionInput],
  host: { '(window:beforeunload)': 'beforeUnload($event)' },
  template: `
    <div class="page-heading">
      <div>
        <p class="eyebrow">BUILD SOMETHING WORTH LEARNING</p>
        <h1>{{ site.settings().studioLabel }}<span class="accent">.</span></h1>
        <p class="lead">Save a private draft, preview the learner experience, then publish a new release.</p>
      </div>
    </div>
    @if (!api.user()?.publisher) {
      <p class="alert">Publisher access is required. Ask your administrator to grant it to your account.</p>
    } @else {
      <section class="panel">
        <div class="filter-row">
          <label>Starter profile
            <select [ngModel]="profile()" (ngModelChange)="profile.set($event)" [disabled]="busy() || saving()">
              <option value="course">Course — lessons and completion</option>
              <option value="exam">Exam — practice and mock exams</option>
              <option value="hybrid">Hybrid — lessons, practice and exams</option>
            </select>
          </label>
          <button class="button secondary" [disabled]="busy() || saving()" (click)="createStarter()">Create starter</button>
          <label>Saved drafts
            <select [ngModel]="selectedDraft()" (ngModelChange)="selectedDraft.set($event)" [disabled]="busy() || saving()">
              <option value="">Choose a draft…</option>
              @for (draft of drafts.value() ?? []; track draft.id) {
                <option [value]="draft.id">{{ draft.title }} · revision {{ draft.revision }}</option>
              }
            </select>
          </label>
          <button class="button secondary" [disabled]="!selectedDraft() || busy() || saving()" (click)="openDraft()">Open draft</button>
        </div>
        @if (drafts.error(); as draftError) { <p class="alert error" role="alert">{{ errorText(draftError) }}</p> }
        <label>Import a JSON source file
          <input type="file" accept="application/json,.json" [disabled]="busy() || saving()" (change)="loadFile($event)" />
        </label>
      </section>
      @if (source() || activeDraft()) {
        <div class="practice-layout studio-layout">
          <section class="panel">
            <label>Draft title
              <input maxlength="120" [ngModel]="title()" (ngModelChange)="title.set($event)" [disabled]="busy()" />
            </label>
            <label for="pack-source">Pack source</label>
            <textarea id="pack-source" rows="24" spellcheck="false" [ngModel]="source()"
              (ngModelChange)="editSource($event)" [disabled]="busy()" aria-describedby="source-help"></textarea>
            <p id="source-help" class="muted small">Drafts can contain unfinished JSON. Validation is required before publishing. Source files may contain private answer keys.</p>
            <p role="status" aria-live="polite">{{ saving() ? 'Saving draft…' : dirty() ? 'Unsaved changes' : 'Draft saved' }}</p>
            @if (saveError()) {
              <p class="alert error" role="alert">{{ saveError() }} Your changes are still in the editor. Retry saving, download your source, or save a copy.</p>
            }
            <div class="row">
              <button class="button secondary" [disabled]="busy() || saving() || !title().trim()" (click)="save()">Save draft</button>
              <button class="button secondary" [disabled]="busy() || saving() || !title().trim()" (click)="save(true)">Save as copy</button>
              <button class="button secondary" (click)="download()">Download source</button>
            </div>
            <div class="row">
              <button class="button" [disabled]="busy() || saving()" (click)="validate()">Validate & preview</button>
              <button class="button" [disabled]="busy() || saving() || !report()?.success" (click)="publish()">Publish new release</button>
            </div>
            @if (report(); as report) {
              <div role="status"><p>{{ report.success ? 'Validation passed. Review the preview before publishing.' : 'Content needs attention before it can be published.' }}</p></div>
              @if (report.diagnostics.length) {
                <ul aria-label="Content diagnostics">
                  @for (diagnostic of report.diagnostics; track $index) {
                    <li><a href="#pack-source" (click)="focusSource($event)">{{ diagnostic.code }} · {{ diagnostic.path }}</a>: {{ diagnostic.message }}</li>
                  }
                </ul>
              }
            }
          </section>
          <aside class="panel" aria-label="Learner preview">
            <p class="eyebrow">LEARNER PREVIEW</p>
            @if (report()?.catalog; as catalog) {
              <h2>{{ catalog.title }}</h2>
              <p>{{ catalog.description }}</p>
              <p class="muted small">{{ catalog.profile }} · {{ catalog.goal }} goal · {{ catalog.license }}</p>
              @if (catalog.capabilities.lessons) {
                <label>Preview lesson
                  <select [ngModel]="lessonId()" (ngModelChange)="lessonId.set($event)">
                    @for (lesson of catalog.lessons; track lesson.id) { <option [value]="lesson.id">{{ lesson.title }}</option> }
                  </select>
                </label>
                @if (previewLesson(); as lesson) { <article class="reading"><lf-lesson-content [lesson]="lesson" /></article> }
              }
              @if (catalog.capabilities.practice || catalog.capabilities.assessments) {
                <label>Preview question
                  <select [ngModel]="questionId()" (ngModelChange)="questionId.set($event)">
                    @for (question of report()?.questions ?? []; track question.id; let i = $index) {
                      <option [value]="question.id">{{ i + 1 }} · {{ question.id }}</option>
                    }
                  </select>
                </label>
                @if (previewQuestion(); as question) {
                  @if (previewScenario(); as scenario) { <h3>{{ scenario.title }}</h3><p>{{ scenario.background }}</p> }
                  <h3>{{ question.prompt }}</h3>
                  <lf-question-input [question]="question" [answer]="previewAnswer()" (changed)="previewAnswer.set($event)" />
                  <p class="muted small">Try the interaction here. Preview responses are not graded or saved as learner evidence.</p>
                }
              }
            } @else { <p class="muted">Validate your source to preview it with the same lesson and question components used by learners.</p> }
          </aside>
        </div>
      } @else {
        <p class="empty">Create a starter or import a source file to begin. Only you can access your saved drafts.</p>
      }
    }
    @if (error()) { <p class="alert error" role="alert">{{ error() }}</p> }
    @if (published()) {
      <p class="alert success" role="status">Release published. <a routerLink="/courses">Open {{ site.settings().libraryLabel }} →</a> Change the source version before publishing another release.</p>
    }
  `,
})
export class StudioPage {
  readonly api = inject(Api);
  readonly site = inject(Site);
  readonly drafts = httpResource<DraftSummaryDto[]>(() => this.api.user()?.publisher ? '/api/authoring/drafts' : undefined);
  readonly activeDraft = signal<DraftDto | null>(null);
  readonly selectedDraft = signal('');
  readonly profile = signal<ProductProfile>('hybrid');
  readonly title = signal('');
  readonly source = signal('');
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly saveError = signal('');
  readonly published = signal(false);
  readonly report = signal<AuthoringPreviewDto | null>(null);
  readonly dirty = computed(() => this.activeDraft()
    ? this.title() !== this.activeDraft()!.title || this.source() !== this.activeDraft()!.source
    : !!(this.source() || this.title()));
  readonly lessonId = linkedSignal(() => this.report()?.catalog?.lessons[0]?.id ?? '');
  readonly questionId = linkedSignal(() => this.report()?.questions[0]?.id ?? '');
  readonly previewLesson = computed(() => this.report()?.catalog?.lessons.find(l => l.id === this.lessonId()));
  readonly previewQuestion = computed(() => this.report()?.questions.find(q => q.id === this.questionId()));
  readonly previewScenario = computed(() => this.report()?.scenarios.find(s => s.id === this.previewQuestion()?.scenarioId));
  readonly previewAnswer = linkedSignal<Answer>(() => {
    this.previewQuestion();
    return { selected: [], slots: {} };
  });
  readonly errorText = message;
  private saveTask?: Promise<boolean>;

  constructor() {
    effect((onCleanup) => {
      // Edits during a request remain dirty and are saved after that request completes.
      if (!this.dirty() || !this.title().trim() || this.busy() || this.saving() || this.saveError()) return;
      this.source();
      const timer = setTimeout(() => void this.save(), 1200);
      onCleanup(() => clearTimeout(timer));
    });
  }
  editSource(source: string) {
    this.source.set(source);
    this.report.set(null);
    this.published.set(false);
  }
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty()) { event.preventDefault(); event.returnValue = ''; }
  }
  async canLeave() {
    if (this.busy()) return false;
    if (this.saveTask) await this.saveTask;
    return !this.dirty() || (await this.save() && !this.dirty());
  }
  save(asCopy = false): Promise<boolean> {
    if (this.saveTask) return this.saveTask;
    const task = this.saveDraft(asCopy);
    this.saveTask = task;
    void task.finally(() => { this.saveTask = undefined; });
    return task;
  }
  private async saveDraft(asCopy: boolean) {
    if (!this.title().trim()) { this.saveError.set('Give the draft a title.'); return false; }
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
    } catch (error) { this.saveError.set(message(error)); return false; }
    finally { this.saving.set(false); }
  }
  private reset(draft: DraftDto | null, title: string, source: string) {
    this.activeDraft.set(draft); this.title.set(title); this.source.set(source);
    this.selectedDraft.set(draft?.id ?? '');
    this.error.set(''); this.saveError.set(''); this.report.set(null); this.published.set(false);
  }
  async createStarter() {
    if (!await this.canLeave()) return;
    this.busy.set(true); this.error.set('');
    try {
      const pack = await this.api.get<{ title: string }>(`/authoring/starters/${this.profile()}`);
      this.reset(null, pack.title, JSON.stringify(pack, null, 2));
    } catch (error) { this.error.set(message(error)); }
    finally { this.busy.set(false); }
  }
  async openDraft() {
    const id = this.selectedDraft();
    if (!id || !await this.canLeave()) return;
    this.busy.set(true); this.error.set('');
    try {
      const draft = await this.api.get<DraftDto>(`/authoring/drafts/${id}`);
      this.reset(draft, draft.title, draft.source);
    } catch (error) { this.error.set(message(error)); }
    finally { this.busy.set(false); }
  }
  async loadFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 2_000_000) { this.error.set('Source files must be under 2 MB.'); return; }
    if (!await this.canLeave()) return;
    this.busy.set(true);
    try { this.reset(null, file.name.slice(0, 120), await file.text()); }
    catch (error) { this.error.set(message(error)); }
    finally { this.busy.set(false); }
  }
  async validate() {
    this.busy.set(true); this.error.set(''); this.published.set(false);
    try { this.report.set(await this.api.post<AuthoringPreviewDto>('/authoring/preview', JSON.parse(this.source()))); }
    catch (error) { this.report.set(null); this.error.set(message(error)); }
    finally { this.busy.set(false); }
  }
  async publish() {
    if (!this.report()?.success) return;
    this.busy.set(true); this.error.set(''); this.published.set(false);
    try {
      if (this.dirty() && !await this.save()) return;
      await this.api.post('/authoring/publish', JSON.parse(this.source()));
      this.published.set(true);
      this.report.set(null);
    } catch (error) { this.error.set(message(error)); }
    finally { this.busy.set(false); }
  }
  focusSource(event: Event) {
    event.preventDefault();
    document.getElementById('pack-source')?.focus();
  }
  download() {
    const url = URL.createObjectURL(new Blob([this.source()], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'pack-source.private.json'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}
