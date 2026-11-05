import { Session } from './authentication/session';
import {
  ApplicationConfig,
  inject,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, TitleStrategy } from '@angular/router';
import { routes } from './app.routes';
import { csrfInterceptor } from './http/csrf-interceptor';
import { SiteSettingsStore } from './site/site-settings-store';
import { SiteTitleStrategy } from './site/site-title-strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([csrfInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    provideAppInitializer(() =>
      Promise.all([inject(Session).initialize(), inject(SiteSettingsStore).initialize()]),
    ),
    { provide: LOCALE_ID, useFactory: () => inject(SiteSettingsStore).settings().locale },
    { provide: TitleStrategy, useClass: SiteTitleStrategy },
  ],
};
