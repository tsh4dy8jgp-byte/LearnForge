import { Session } from '../authentication/session';
import { Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { AttemptState } from './attempt-state';
import { QuestionInput } from '../assessment/question-input';
import { describeExpected } from '../assessment/answer-description';

@Component({
  selector: 'lf-active-session',
  imports: [DecimalPipe, QuestionInput],
  host: { style: 'display: contents' },
  templateUrl: './active-session.html',
})
export class ActiveSession {
  protected readonly session = inject(Session);
  protected readonly state = inject(AttemptState);
  protected readonly expected = describeExpected;
  protected kindLabel(kind: string) {
    return (
      (
        {
          single: 'Single choice',
          multiple: 'Select multiple',
          sequence: 'Sequence',
          matching: 'Drag & match',
          dropdown: 'Dropdown blanks',
          numeric: 'Numeric answer',
          codeOutput: 'Program output',
        } as Record<string, string>
      )[kind] || kind
    );
  }
}
