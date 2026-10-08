import { Component, computed, input } from '@angular/core';
import { MasteryState } from '../api-contracts';

const labels: Record<MasteryState, { text: string; icon: string }> = {
  notStarted: { text: 'Not started', icon: '○' },
  emerging: { text: 'Getting started', icon: '◔' },
  developing: { text: 'Developing', icon: '◑' },
  proficient: { text: 'Proficient', icon: '●' },
};

export const masteryLabel = (state: MasteryState) => labels[state].text;

// State is always spelled out with its evidence; the icon and colour only reinforce it.
@Component({
  selector: 'lf-mastery-badge',
  template: `<span class="pill mastery" [class.success]="state() === 'proficient'"
      ><span aria-hidden="true">{{ icon() }}</span> {{ label() }}</span
    ><small class="muted">{{ evidence() }}</small>`,
})
export class MasteryBadge {
  readonly state = input.required<MasteryState>();
  readonly correct = input(0);
  readonly considered = input(0);
  readonly reviewDue = input(false);
  protected readonly label = computed(
    () => masteryLabel(this.state()) + (this.reviewDue() ? ' · review due' : ''),
  );
  protected readonly icon = computed(() => labels[this.state()].icon);
  protected readonly evidence = computed(() =>
    this.considered() === 0
      ? 'No evidence yet'
      : `${this.correct()} of last ${this.considered()} correct`,
  );
}
