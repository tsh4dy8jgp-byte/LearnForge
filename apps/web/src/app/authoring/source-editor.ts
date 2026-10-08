import { DOCUMENT } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthoringPreviewDto } from '../api-contracts';
import { DraftEditorState } from './draft-editor-state';
import { questionFor } from './diagnostic-question';

@Component({
  selector: 'lf-source-editor',
  imports: [FormsModule],
  host: { style: 'display: contents' },
  templateUrl: './source-editor.html',
})
export class SourceEditor {
  readonly report = input<AuthoringPreviewDto | null>(null);
  readonly sourceChanged = output<string>();
  readonly validate = output<void>();
  readonly publish = output<void>();
  readonly download = output<void>();
  readonly inspect = output<string>();
  protected readonly editor = inject(DraftEditorState);
  private readonly document = inject(DOCUMENT);
  protected readonly reportStatus = computed(() => {
    const report = this.report();
    if (!report?.success) return 'Content needs attention before it can be published.';
    const count = report.warnings.length;
    return count
      ? `Validation passed with ${count} quality warning${count === 1 ? '' : 's'}. Review them and the preview before publishing.`
      : 'Validation passed. Review the preview before publishing.';
  });
  // Warning paths that point into a previewable question, resolved once per report.
  protected readonly warningTargets = computed(() => {
    const report = this.report();
    return new Set(
      (report?.warnings ?? []).map((w) => w.path).filter((path) => questionFor(report, path)),
    );
  });
  protected focusSource(event: Event) {
    event.preventDefault();
    this.document.getElementById('pack-source')?.focus();
  }
  protected inspectWarning(event: Event, path: string) {
    event.preventDefault();
    this.inspect.emit(path);
  }
}
