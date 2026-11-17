import { Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Attempt } from '../api-contracts';
import { describeAnswer, describeExpected } from '../assessment/answer-description';
import { Icon } from '../icons/icon';

@Component({
  selector: 'lf-attempt-results',
  imports: [RouterLink, DatePipe, DecimalPipe, Icon],
  host: { style: 'display: contents' },
  templateUrl: './attempt-results.html',
})
export class AttemptResults {
  readonly attempt = input.required<Attempt>();
  protected readonly describe = describeAnswer;
  protected readonly expected = describeExpected;
}
