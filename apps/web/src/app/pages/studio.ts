import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
@Component({
  imports: [FormsModule, RouterLink],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">BUILD SOMETHING WORTH LEARNING</p>
        <h1>Content studio<span class="accent">.</span></h1>
        <p class="lead">
          Create a subject pack from reusable templates. Check the connections before publishing.
        </p>
      </div>
    </div>
    @if (!api.user()?.publisher) {
      <p class="alert">
        Publisher access is required. Ask your LearnForge administrator to grant it to your account.
      </p>
    } @else {
      <div class="practice-layout">
        <section class="panel">
          <label
            >Import a JSON source file<input
              type="file"
              accept="application/json,.json"
              (change)="loadFile($event)" /></label
          ><label
            >Pack source<textarea
              rows="20"
              spellcheck="false"
              [ngModel]="source()"
              (ngModelChange)="source.set($event); report.set(null)"
            ></textarea>
          </label>
          <div class="row">
            <button class="button secondary" [disabled]="busy()" (click)="validate()">
              Validate content</button
            ><button class="button" [disabled]="busy() || !report()?.success" (click)="publish()">
              Publish new release
            </button>
          </div>
        </section>
        <aside>
          <div class="recommendation">
            <p class="eyebrow">ONE SOURCE, CONNECTED CONTENT</p>
            <h2>Write once.<br />Check the whole picture.</h2>
            <p>
              The same compiler checks template expansion, answer keys, prerequisite cycles,
              objective coverage and exam composition.
            </p>
            <p class="small">
              Published releases are immutable. Use a new version to update a course.
            </p>
          </div>
          @if (report(); as r) {
            <div class="panel">
              <h3>{{ r.success ? 'Ready to publish' : 'Some connections need attention' }}</h3>
              @if (r.success) {
                <p>{{ r.lessonCount }} lessons · {{ r.questionCount }} questions</p>
              }
              @for (d of r.diagnostics; track $index) {
                <p class="diagnostic">
                  <strong>{{ d.code }} · {{ d.path }}</strong
                  ><br />{{ d.message }}
                </p>
              }
            </div>
          }
        </aside>
      </div>
    }
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    @if (published()) {
      <p class="alert success">
        Release published. <a routerLink="/courses">Open learning library →</a>
      </p>
    }`,
})
export class StudioPage {
  readonly api = inject(Api);
  readonly source = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly published = signal(false);
  readonly report = signal<{
    success: boolean;
    diagnostics: { code: string; path: string; message: string }[];
    lessonCount: number;
    questionCount: number;
  } | null>(null);
  async loadFile(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 2_000_000) {
      this.error.set('Source files must be under 2 MB.');
      return;
    }
    this.source.set(await file.text());
    this.report.set(null);
  }
  async validate() {
    this.busy.set(true);
    this.error.set('');
    this.published.set(false);
    try {
      this.report.set(await this.api.post('/authoring/validate', JSON.parse(this.source())));
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async publish() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post('/authoring/publish', JSON.parse(this.source()));
      this.published.set(true);
      this.report.set(null);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
