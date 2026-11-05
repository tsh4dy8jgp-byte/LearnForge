import { Component, input, model } from '@angular/core';
import { RouterLink } from '@angular/router';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { CourseCatalogDto } from '../../api-contracts';
import { PracticeSelection } from './practice-selection';

@Component({
  selector: 'lf-practice-setup',
  imports: [RouterLink, FormField],
  templateUrl: './practice-setup.html',
})
export class PracticeSetup {
  readonly course = input.required<CourseCatalogDto>();
  readonly setup = model.required<PracticeSelection>();
  readonly signedIn = input.required<boolean>();
  readonly busy = input(false);
  // Await the page mutation so Signal Forms keeps the submission in flight.
  readonly startSession = input.required<(selection: PracticeSelection) => Promise<void>>();
  protected readonly setupForm = form(this.setup, (path) => {
    required(path.blueprintId);
    required(path.objectiveId, {
      when: ({ valueOf }) => valueOf(path.mode) === 'learn' && valueOf(path.focus) === 'objective',
    });
  });
  protected async beginSession(event: Event) {
    event.preventDefault();
    await submit(this.setupForm, async () => {
      await this.startSession()(this.setup());
      return undefined;
    });
  }
}
