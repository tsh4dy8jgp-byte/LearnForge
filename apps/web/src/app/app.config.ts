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
import { Api, csrfInterceptor } from './api';
import { Site, SiteTitleStrategy } from './site-settings';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([csrfInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    provideAppInitializer(() => Promise.all([inject(Api).initialize(), inject(Site).initialize()])),
    { provide: LOCALE_ID, useFactory: () => inject(Site).settings().locale },
    { provide: TitleStrategy, useClass: SiteTitleStrategy },
  ],
};
