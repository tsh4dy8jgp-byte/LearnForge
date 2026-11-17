import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { MasteryBadge, masteryLabel } from './mastery-badge';
import { MasteryState } from '../api-contracts';

describe('MasteryBadge', () => {
  it('names every state in words', () => {
    const states: MasteryState[] = ['notStarted', 'emerging', 'developing', 'proficient'];
    expect(states.map(masteryLabel)).toEqual([
      'Not started',
      'Getting started',
      'Developing',
      'Proficient',
    ]);
  });

  it('shows the evidence behind the state, not only a colour', async () => {
    const fixture = TestBed.createComponent(MasteryBadge);
    fixture.componentRef.setInput('state', 'developing');
    fixture.componentRef.setInput('correct', 3);
    fixture.componentRef.setInput('considered', 5);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Developing');
    expect(text).toContain('3 of last 5 correct');
  });

  it('pairs each state with its own ring and tone, separated from the label', async () => {
    const rings: Record<MasteryState, string> = {
      notStarted: 'mastery-none',
      emerging: 'mastery-quarter',
      developing: 'mastery-half',
      proficient: 'mastery-full',
    };
    for (const [state, ring] of Object.entries(rings)) {
      const fixture = TestBed.createComponent(MasteryBadge);
      fixture.componentRef.setInput('state', state);
      await fixture.whenStable();
      const pill = (fixture.nativeElement as HTMLElement).querySelector('.pill.mastery')!;
      expect(pill.getAttribute('data-state')).toBe(state);
      const icon = pill.querySelector('lf-icon')!;
      expect(icon.getAttribute('data-icon')).toBe(ring);
      expect(icon.getAttribute('aria-hidden')).toBe('true');
      expect(pill.textContent?.trim()).toBe(masteryLabel(state as MasteryState));
    }
  });

  it('says when there is no evidence and when a review is due', async () => {
    const fixture = TestBed.createComponent(MasteryBadge);
    fixture.componentRef.setInput('state', 'proficient');
    fixture.componentRef.setInput('reviewDue', true);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Proficient · review due');
    expect(text).toContain('No evidence yet');
  });
});
