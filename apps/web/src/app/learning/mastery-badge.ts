import { Component, computed, input } from '@angular/core';
import { MasteryState } from '../api-contracts';
import { Icon, IconName } from '../icons/icon';

const labels: Record<MasteryState, { text: string; icon: IconName }> = {
  notStarted: { text: 'Not started', icon: 'mastery-none' },
  emerging: { text: 'Getting started', icon: 'mastery-quarter' },
  developing: { text: 'Developing', icon: 'mastery-half' },
  proficient: { text: 'Proficient', icon: 'mastery-full' },
};

export const masteryLabel = (state: MasteryState) => labels[state].text;

// State is always spelled out with its evidence; the ring and tone only reinforce it.
@Component({
  selector: 'lf-mastery-badge',
  imports: [Icon],
  template: `<span
      class="pill mastery"
      [attr.data-state]="state()"
      [class.success]="state() === 'proficient'"
      ><lf-icon [name]="icon()" />{{ label() }}</span
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
