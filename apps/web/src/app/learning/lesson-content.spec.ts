import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Lesson } from '../api-contracts';
import { LessonContent } from './lesson-content';

function render(blocks: Lesson['blocks']) {
  const fixture = TestBed.createComponent(LessonContent);
  fixture.componentRef.setInput('lesson', {
    id: 'l1',
    title: 'Decision tables',
    summary: 'Summary',
    objectiveIds: [],
    blocks,
  });
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('LessonContent', () => {
  it('renders a table block with a caption, column headers and a note', () => {
    const element = render([
      {
        kind: 'table',
        title: 'Delivery rules',
        text: 'X means the action occurs.',
        rows: [
          ['Rule', 'Member?', 'Free delivery'],
          ['R1', 'T', 'X'],
          ['R2', 'F', ''],
        ],
      },
    ]);
    const table = element.querySelector('table')!;
    expect(table.querySelector('caption')?.textContent).toContain('Delivery rules');
    const headers = Array.from(table.querySelectorAll('thead th'));
    expect(headers.map((h) => h.textContent?.trim())).toEqual(['Rule', 'Member?', 'Free delivery']);
    expect(headers.every((h) => h.getAttribute('scope') === 'col')).toBe(true);
    expect(table.querySelectorAll('tbody tr').length).toBe(2);
    // The first cell of each body row labels the row.
    expect(table.querySelector('tbody tr th')?.getAttribute('scope')).toBe('row');
    expect(element.querySelector('.content-block.table p')?.textContent).toContain('X means');
    // The scroll container is reachable by keyboard and named, so wide tables can be scrolled on a phone.
    const region = element.querySelector('.table-wrap')!;
    expect(region.getAttribute('tabindex')).toBe('0');
    expect(region.getAttribute('role')).toBe('region');
    expect(region.getAttribute('aria-label')).toBe('Delivery rules');
  });

  it('omits the note of a table without text and keeps text blocks as paragraphs', () => {
    const element = render([
      { kind: 'table', title: null, text: '', rows: [['A', 'B'], ['1', '2']] },
      { kind: 'text', title: 'Understand', text: 'Plain text.' },
    ]);
    expect(element.querySelector('.content-block.table p')).toBeNull();
    expect(element.querySelector('.table-wrap')?.getAttribute('aria-label')).toBe('Table');
    expect(element.querySelector('.content-block.text p')?.textContent).toBe('Plain text.');
    expect(element.querySelectorAll('table').length).toBe(1);
  });
});
