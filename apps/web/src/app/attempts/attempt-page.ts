import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AttemptState } from './attempt-state';
import { ActiveSession } from './active-session';
import { AttemptResults } from './attempt-results';

@Component({
  imports: [RouterLink, ActiveSession, AttemptResults],
  providers: [AttemptState],
  templateUrl: './attempt-page.html',
})
export class AttemptPage {
  protected readonly state = inject(AttemptState);
}
