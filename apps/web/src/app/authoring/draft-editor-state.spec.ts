import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DraftEditorState } from './draft-editor-state';
import { StudioPage } from './studio-page';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
  });
  const fixture = TestBed.createComponent(StudioPage);
  const editor = fixture.debugElement.injector.get(DraftEditorState);
  editor.title.set('Original title');
  editor.source.set('{"version":"1.0.0"}');
  return { fixture, editor, http: TestBed.inject(HttpTestingController) };
}

afterEach(() => vi.useRealTimers());

describe('page-scoped draft coordination', () => {
  it('shares an in-flight save and preserves edits made during that request', async () => {
    const { editor, http } = setup();
    const first = editor.save();
    expect(editor.save()).toBe(first);
    const request = http.expectOne('/api/authoring/drafts');
    const payload = request.request.body;
    editor.source.set('{"version":"2.0.0"}');
    request.flush({ id: 'draft-1', ...payload, revision: 1 });
    expect(await first).toBe(true);
    expect(editor.source()).toContain('2.0.0');
    expect(editor.dirty()).toBe(true);
    const next = editor.save();
    const update = http.expectOne('/api/authoring/drafts/draft-1');
    expect(update.request.body).toMatchObject({ revision: 1, source: '{"version":"2.0.0"}' });
    update.flush({ id: 'draft-1', ...update.request.body, revision: 2 });
    await next;
    expect(editor.dirty()).toBe(false);
    http.verify();
  });

  it('keeps unsaved source and blocks leaving when a save fails', async () => {
    const { editor, http } = setup();
    const leaving = editor.canLeave();
    http
      .expectOne('/api/authoring/drafts')
      .flush({ detail: 'Revision conflict.' }, { status: 409, statusText: 'Conflict' });
    expect(await leaving).toBe(false);
    expect(editor.source()).toContain('1.0.0');
    expect(editor.dirty()).toBe(true);
    expect(editor.saveError()).toBe('Revision conflict.');
    http.verify();
  });

  it('cancels scheduled autosave when the route component is destroyed', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { fixture, editor, http } = setup();
    fixture.detectChanges();
    TestBed.tick();
    expect(editor.dirty()).toBe(true);
    fixture.destroy();
    await vi.advanceTimersByTimeAsync(1500);
    http.expectNone('/api/authoring/drafts');
    http.verify();
  });
});
