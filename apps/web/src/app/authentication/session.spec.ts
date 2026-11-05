import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../http/api-client';
import { csrfInterceptor } from '../http/csrf-interceptor';
import { Session } from './session';
import { authenticated } from './authenticated';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([csrfInterceptor])),
      provideHttpClientTesting(),
      provideRouter([]),
    ],
  });
  return { session: TestBed.inject(Session), http: TestBed.inject(HttpTestingController) };
}

describe('session and transport boundaries', () => {
  it('initializes the user and supplies the fetched CSRF token to mutations', async () => {
    const { session, http } = setup();
    const pending = session.initialize();
    http.expectOne('/api/auth/csrf').flush({ token: 'current-token' });
    await vi.waitFor(() =>
      http
        .expectOne('/api/auth/me')
        .flush({ id: 'learner', displayName: 'Learner', publisher: false }),
    );
    await pending;
    expect(session.user()?.id).toBe('learner');
    expect(TestBed.runInInjectionContext(() => authenticated({} as never, {} as never))).toBe(true);
    const save = TestBed.inject(ApiClient).put('/me/example', { value: 1 });
    const request = http.expectOne('/api/me/example');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('current-token');
    request.flush({});
    await save;
    http.verify();
  });

  it('treats a signed-out response as normal and redirects protected navigation', async () => {
    const { session, http } = setup();
    const pending = session.initialize();
    http.expectOne('/api/auth/csrf').flush({ token: 'anonymous-token' });
    await vi.waitFor(() =>
      http.expectOne('/api/auth/me').flush({}, { status: 401, statusText: 'Unauthorized' }),
    );
    await pending;
    expect(session.user()).toBeNull();
    expect(session.unavailable()).toBe(false);
    const redirect = TestBed.runInInjectionContext(() => authenticated({} as never, {} as never));
    expect(TestBed.inject(Router).serializeUrl(redirect as ReturnType<Router['parseUrl']>)).toBe(
      '/sign-in',
    );
    http.verify();
  });

  it('records an unavailable backend without failing application initialization', async () => {
    const { session, http } = setup();
    const pending = session.initialize();
    http.expectOne('/api/auth/csrf').flush({}, { status: 503, statusText: 'Unavailable' });
    await pending;
    expect(session.unavailable()).toBe(true);
    http.expectNone('/api/auth/me');
    http.verify();
  });
});
