import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Icon, iconNames } from './icon';

describe('Icon', () => {
  it('renders every named icon as decorative inline SVG', async () => {
    for (const name of iconNames) {
      const fixture = TestBed.createComponent(Icon);
      fixture.componentRef.setInput('name', name);
      await fixture.whenStable();
      const host = fixture.nativeElement as HTMLElement;
      expect(host.getAttribute('aria-hidden')).toBe('true');
      expect(host.getAttribute('data-icon')).toBe(name);
      expect(host.querySelectorAll('svg path').length).toBeGreaterThan(0);
      expect(host.textContent?.trim()).toBe('');
    }
  });

  it('fills solid shapes and strokes the rest with the text colour', async () => {
    const fixture = TestBed.createComponent(Icon);
    fixture.componentRef.setInput('name', 'mastery-half');
    await fixture.whenStable();
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg')!;
    expect(svg.getAttribute('stroke')).toBe('currentColor');
    const fills = [...svg.querySelectorAll('path')].map((p) => p.getAttribute('fill'));
    expect(fills).toEqual([null, 'currentColor']);
  });
});
