import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { Api } from '../api';
import { AuthoringPreviewDto, Diagnostic } from '../models';
import { StudioPage } from './studio';

const preview = (warnings: Diagnostic[]): AuthoringPreviewDto => ({
  success: true,
  hash: 'hash',
  diagnostics: [],
  catalog: {
    id: 'sample',
    title: 'Sample exam',
    description: 'Original practice.',
    version: '1.0.0',
    license: 'CC0-1.0',
    objectives: [{ id: 'http', title: 'HTTP', prerequisites: [] }],
    lessons: [],
    blueprints: [
      {
        id: 'short',
        title: 'Short mock exam',
        count: 1,
        minutes: 5,
        size: 'short',
        objectiveIds: ['http'],
        requiredKinds: [],
        objectiveWeights: { http: 1 },
      },
    ],
    sources: [],
    readiness: {
      shortAttempts: 1,
      fullAttempts: 1,
      threshold: 90,
      lookbackDays: 90,
      minimumFreshPercent: 0,
    },
    goal: 'readiness',
    questionCount: 1,
    profile: 'exam',
    capabilities: { lessons: false, practice: true, assessments: true },
  },
  questions: [
    {
      id: 'q1',
      kind: 'single',
      prompt: 'Which status code marks a permanent move?',
      objectiveIds: ['http'],
      options: [
        { id: 'o1', text: '301' },
        { id: 'o2', text: '302' },
      ],
      slots: [],
      selectCount: 1,
      reuse: false,
      scenarioId: null,
      weight: 1,
      code: null,
    },
  ],
  scenarios: [],
  warnings,
});

async function validate(warnings: Diagnostic[]) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  TestBed.inject(Api).user.set({
    id: 'u1',
    displayName: 'Author',
    email: 'author@example.test',
    publisher: true,
  });
  const fixture = TestBed.createComponent(StudioPage);
  const page = fixture.componentInstance;
  const http = TestBed.inject(HttpTestingController);
  fixture.detectChanges();
  TestBed.tick();
  http.expectOne('/api/authoring/drafts').flush([]);
  page.title.set('Sample');
  page.source.set('{"format":"exam/1"}');
  const pending = page.validate();
  http.expectOne('/api/authoring/preview').flush(preview(warnings));
  await pending;
  fixture.detectChanges();
  await fixture.whenStable();
  return { page, element: fixture.nativeElement as HTMLElement };
}

describe('StudioPage', () => {
  it('lists quality warnings without blocking publishing and links them to the question', async () => {
    const { page, element } = await validate([
      {
        code: 'LF201',
        path: 'q1',
        message: 'The key is 40 characters; the distractors average 12.',
      },
      { code: 'LF222', path: 'readiness', message: 'Readiness needs more question families.' },
    ]);
    expect(element.textContent).toContain('Validation passed with 2 quality warnings.');
    expect(element.textContent).toContain('Quality warnings (2)');
    expect(element.textContent).toContain('LF201 · q1');
    const links = Array.from(
      element.querySelectorAll<HTMLAnchorElement>('ul[aria-labelledby="quality-warnings"] a'),
    );
    expect(links.map((a) => a.getAttribute('href'))).toEqual(['#preview-question', '#pack-source']);
    const publish = Array.from(element.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Publish new release'),
    );
    expect(publish?.disabled).toBe(false);
    expect(page.questionFor('q1.b1')).toBe('q1');
  });

  it('shows no warning section for a clean source', async () => {
    const { element } = await validate([]);
    expect(element.textContent).toContain(
      'Validation passed. Review the preview before publishing.',
    );
    expect(element.textContent).not.toContain('Quality warnings');
  });
});
