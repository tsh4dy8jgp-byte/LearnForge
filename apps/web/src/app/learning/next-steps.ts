import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../icons/icon';
import { NextStepDto } from '../api-contracts';
import { describeStep, stepLink, stepActions } from './next-step-description';

@Component({
  selector: 'lf-next-steps',
  imports: [RouterLink, Icon],
  template: `<ol class="next-steps" role="list">
    @for (entry of entries(); track $index) {
      <li>
        <span>{{ entry.description }}</span>
        <a class="text-link" [routerLink]="entry.link.path" [queryParams]="entry.link.query"
          >{{ entry.action }} <lf-icon name="arrow-right"
        /></a>
      </li>
    } @empty {
      <li class="muted done">You have completed every suggested step for this course.</li>
    }
  </ol>`,
})
export class NextSteps {
  readonly packId = input.required<string>();
  readonly steps = input.required<NextStepDto[]>();
  protected readonly entries = computed(() =>
    this.steps().map((step) => ({
      description: describeStep(step),
      link: stepLink(this.packId(), step),
      action: stepActions[step.kind],
    })),
  );
}
