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
