import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NextStepDto } from './models';

// The API sends reason codes; wording lives here so it can be localized later.
export function describeStep(step: NextStepDto): string {
  const objective = step.objectiveTitle ?? 'this objective';
  switch (step.reason) {
    case 'startObjective':
      return `Begin "${objective}" by reading "${step.lessonTitle}".`;
    case 'continueReading':
      return `Finish reading "${step.lessonTitle}" for "${objective}".`;
    case 'needsEvidence':
      return `Practise "${objective}" so your progress can be measured.`;
    case 'belowProficient':
      return `Keep practising "${objective}" until it becomes proficient.`;
    case 'reviewDue':
      return `Review "${objective}": it has been a while since you practised it.`;
    case 'readyForMock':
      return 'Every objective is proficient. Take a timed mock to build readiness evidence.';
    case 'assessmentAvailable':
      return 'Take an assessment to demonstrate what you know and build objective evidence.';
  }
}

export function stepLink(packId: string, step: NextStepDto): { path: string[]; query: Record<string, string> } {
  const path = ['/courses', packId];
  switch (step.kind) {
    case 'readLesson':
      return { path, query: { tab: 'learn', lesson: step.lessonId ?? '' } };
    case 'takeMock':
      return { path, query: { tab: 'practice', mode: 'mock', blueprint: step.blueprintId ?? '' } };
    default:
      // Objectives practised only inside case studies come with a blueprint for a balanced learning session.
      return step.blueprintId
        ? { path, query: { tab: 'practice', blueprint: step.blueprintId } }
        : { path, query: { tab: 'practice', objective: step.objectiveId ?? '' } };
  }
}

const actions: Record<NextStepDto['kind'], string> = {
  readLesson: 'Open lesson',
  practise: 'Practise',
  review: 'Review',
  takeMock: 'Start a mock',
};

@Component({
  selector: 'lf-next-steps',
  imports: [RouterLink],
  template: `<ol class="next-steps">
    @for (step of steps(); track $index) {
      <li>
        <span>{{ describe(step) }}</span>
        <a class="text-link" [routerLink]="link(step).path" [queryParams]="link(step).query"
          >{{ action(step) }} →</a
        >
      </li>
    } @empty {
      <li class="muted">You have completed every suggested step for this course.</li>
    }
  </ol>`,
})
export class NextSteps {
  readonly packId = input.required<string>();
  readonly steps = input.required<NextStepDto[]>();
  readonly describe = describeStep;
  link(step: NextStepDto) {
    return stepLink(this.packId(), step);
  }
  action(step: NextStepDto) {
    return actions[step.kind];
  }
}
