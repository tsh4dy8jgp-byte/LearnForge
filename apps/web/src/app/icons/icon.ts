import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

type Shape = { d: string; fill?: true };

// Original 24 × 24 outline shapes. Stroked shapes use the current text colour; `fill` shapes
// are solid. Keep every icon optically centred so labels line up whatever icon precedes them.
const shapes = {
  overview: [
    {
      d: 'M5.5 4h3A1.5 1.5 0 0 1 10 5.5v3A1.5 1.5 0 0 1 8.5 10h-3A1.5 1.5 0 0 1 4 8.5v-3A1.5 1.5 0 0 1 5.5 4z',
    },
    {
      d: 'M15.5 4h3A1.5 1.5 0 0 1 20 5.5v3a1.5 1.5 0 0 1-1.5 1.5h-3A1.5 1.5 0 0 1 14 8.5v-3A1.5 1.5 0 0 1 15.5 4z',
    },
    {
      d: 'M5.5 14h3a1.5 1.5 0 0 1 1.5 1.5v3A1.5 1.5 0 0 1 8.5 20h-3A1.5 1.5 0 0 1 4 18.5v-3A1.5 1.5 0 0 1 5.5 14z',
    },
    {
      d: 'M15.5 14h3a1.5 1.5 0 0 1 1.5 1.5v3a1.5 1.5 0 0 1-1.5 1.5h-3a1.5 1.5 0 0 1-1.5-1.5v-3a1.5 1.5 0 0 1 1.5-1.5z',
    },
  ],
  library: [
    {
      d: 'M3.5 5.5c2.9-1.2 5.7-1.1 8.5.7 2.8-1.8 5.6-1.9 8.5-.7v13c-2.9-1.2-5.7-1.1-8.5.7-2.8-1.8-5.6-1.9-8.5-.7z',
    },
    { d: 'M12 6.2v13' },
  ],
  history: [{ d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' }, { d: 'M12 7.5V12l3 2' }],
  studio: [
    { d: 'M10.5 3.5l1.7 4.8 4.8 1.7-4.8 1.7-1.7 4.8-1.7-4.8L4 10l4.8-1.7z' },
    { d: 'M17.5 14.5l.8 2.2 2.2.8-2.2.8-.8 2.2-.8-2.2-2.2-.8 2.2-.8z' },
  ],
  account: [
    { d: 'M15.5 8a3.5 3.5 0 1 1-7 0 3.5 3.5 0 0 1 7 0z' },
    { d: 'M5 20c.9-3.5 3.6-5.5 7-5.5s6.1 2 7 5.5' },
  ],
  appearance: [
    { d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' },
    { d: 'M12 3.5a8.5 8.5 0 0 1 0 17z', fill: true },
  ],
  'arrow-right': [{ d: 'M5 12h14' }, { d: 'M13 6l6 6-6 6' }],
  'arrow-left': [{ d: 'M19 12H5' }, { d: 'M11 6l-6 6 6 6' }],
  'arrow-up': [{ d: 'M12 19V5' }, { d: 'M6 11l6-6 6 6' }],
  'arrow-down': [{ d: 'M12 5v14' }, { d: 'M6 13l6 6 6-6' }],
  external: [{ d: 'M7 17L17 7' }, { d: 'M8.5 7H17v8.5' }],
  download: [{ d: 'M12 4v11' }, { d: 'M7 10.5l5 4.5 5-4.5' }, { d: 'M5 19.5h14' }],
  check: [{ d: 'M5 12.5l4.5 4.5L19 7.5' }],
  circle: [{ d: 'M20 12a8 8 0 1 1-16 0 8 8 0 0 1 16 0z' }],
  grip: [
    { d: 'M9 4.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
    { d: 'M15 4.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
    { d: 'M9 10.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
    { d: 'M15 10.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
    { d: 'M9 16.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
    { d: 'M15 16.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8z', fill: true },
  ],
  // Mastery rings: an outer track with an inner disc filled to 0, ¼, ½ or all of the way.
  'mastery-none': [{ d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' }],
  'mastery-quarter': [
    { d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' },
    { d: 'M12 12V6.5a5.5 5.5 0 0 1 5.5 5.5z', fill: true },
  ],
  'mastery-half': [
    { d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' },
    { d: 'M12 6.5a5.5 5.5 0 0 1 0 11z', fill: true },
  ],
  'mastery-full': [
    { d: 'M20.5 12a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0z' },
    { d: 'M17.5 12a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z', fill: true },
  ],
} as const satisfies Record<string, readonly Shape[]>;

export type IconName = keyof typeof shapes;
export const iconNames = Object.keys(shapes) as IconName[];

/**
 * A decorative inline icon sized to the surrounding text (1em). It is always hidden from
 * assistive technology, so the adjacent text or the control's aria-label must carry the meaning.
 */
@Component({
  selector: 'lf-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[attr.data-icon]': 'name()' },
  template: `<svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    focusable="false"
  >
    @for (shape of paths(); track $index) {
      <path [attr.d]="shape.d" [attr.fill]="shape.fill ? 'currentColor' : null" />
    }
  </svg>`,
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
      width: 1em;
      height: 1em;
      vertical-align: -0.125em;
    }
    svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  protected readonly paths = computed<readonly Shape[]>(() => shapes[this.name()]);
}
