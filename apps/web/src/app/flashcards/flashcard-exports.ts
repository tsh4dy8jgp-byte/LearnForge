import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { FlashcardExportFormat, CardPaper, CardPrintMode, CardBinding } from '../api-contracts';

@Component({
  selector: 'lf-flashcard-exports',
  imports: [FormsModule],
  styles: [
    `
      .row,
      .filter-row {
        flex-wrap: wrap;
        justify-content: flex-start;
      }
      .filter-row label {
        flex: 1 1 150px;
        min-width: 0;
      }
    `,
  ],
  template: `<section class="panel">
    <h3>Take your cards with you</h3>
    <p>
      Download an Anki deck or print cards to cut out. Anki and LearnForge keep separate review
      schedules.
    </p>
    <div class="filter-row">
      <label
        >Paper
        <select [(ngModel)]="paper">
          <option value="a4">A4</option>
          <option value="letter">US Letter</option>
        </select></label
      >
      <label
        >Printing
        <select [(ngModel)]="mode">
          <option value="duplex">Duplex, front and back</option>
          <option value="foldable">Single-sided, fold and cut</option>
        </select></label
      >
      @if (mode === 'duplex') {
        <label
          >Flip on
          <select [(ngModel)]="binding">
            <option value="long">Long edge</option>
            <option value="short">Short edge</option>
          </select></label
        >
        <label
          >Back X offset (mm)<input type="number" min="-5" max="5" step="0.5" [(ngModel)]="offsetX"
        /></label>
        <label
          >Back Y offset (mm)<input type="number" min="-5" max="5" step="0.5" [(ngModel)]="offsetY"
        /></label>
      }
    </div>
    <div class="row">
      <button class="button" [disabled]="busy()" (click)="download('apkg')">
        Download Anki deck
      </button>
      <button class="button secondary" [disabled]="busy()" (click)="download('text')">
        Text backup
      </button>
      <button class="button secondary" [disabled]="busy()" (click)="download('pdf')">
        Printable PDF
      </button>
    </div>
    <p class="muted small">
      90 × 60 mm cards. Print at 100% scale. Instructions and calibration are separate from card
      sheets. Text imports require the supplied LearnForge note types.
    </p>
    <p role="status">{{ busy() ? 'Preparing your download…' : notice() }}</p>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
  </section>`,
})
export class FlashcardExports {
  readonly packId = input.required<string>();
  readonly version = input.required<string>();
  readonly source = input<string>();
  readonly deck = input('');
  readonly objective = input('');
  readonly tag = input('');
  private readonly api = inject(ApiClient);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected paper: CardPaper = 'a4';
  protected mode: CardPrintMode = 'duplex';
  protected binding: CardBinding = 'long';
  protected offsetX = 0;
  protected offsetY = 0;
  protected async download(format: FlashcardExportFormat) {
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const query = new URLSearchParams({
        format,
        paper: this.paper,
        mode: this.mode,
        binding: this.binding,
        version: this.version(),
      });
      // Offsets only apply to duplex PDFs; a cleared number field is null.
      if (format === 'pdf' && this.mode === 'duplex') {
        query.set('offsetX', String(this.offsetX || 0));
        query.set('offsetY', String(this.offsetY || 0));
      }
      if (this.deck()) query.set('deck', this.deck());
      if (this.objective()) query.set('objective', this.objective());
      if (this.tag()) query.set('tag', this.tag());
      const source = this.source();
      const blob = await this.api.file(
        source ? 'POST' : 'GET',
        source
          ? `/authoring/flashcards/exports?${query}`
          : `/catalog/${this.packId()}/flashcards/exports?${query}`,
        source ? JSON.parse(source) : undefined,
      );
      const link = document.createElement('a');
      const url = URL.createObjectURL(blob);
      link.href = url;
      link.download = `${this.packId()}-${this.version()}-flashcards.${format === 'text' ? 'zip' : format}`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.notice.set('Download prepared.');
    } catch (error) {
      this.error.set(message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
