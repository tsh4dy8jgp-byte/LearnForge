import { DOCUMENT, registerLocaleData } from '@angular/common';
import enGb from '@angular/common/locales/en-GB';
import ru from '@angular/common/locales/ru';
import kk from '@angular/common/locales/kk';
import { inject, Injectable, signal } from '@angular/core';
import { ApiClient } from '../http/api-client';
import { message } from '../http/api-error';
import { SiteSettings as SiteSettingsDto } from '../api-contracts';
import { AppearancePreferences } from '../appearance/appearance-preferences';

export type SiteSettings = Required<SiteSettingsDto>;

registerLocaleData(enGb, 'en-GB');
registerLocaleData(ru, 'ru-RU');
registerLocaleData(kk, 'kk-KZ');

export const defaultSiteSettings: SiteSettings = {
  name: 'LearnForge',
  tagline: 'A little further, every day.',
  description: 'Purposeful practice. Measurable progress.',
  logoPath: null,
  primaryColor: '#315b4b',
  font: 'system',
  locale: 'en-US',
  homePage: 'dashboard',
  layout: 'sidebar',
  colorScheme: 'brand',
  overviewLabel: 'Overview',
  libraryLabel: 'Learning library',
  historyLabel: 'Attempts & results',
  studioLabel: 'Content studio',
};

@Injectable({ providedIn: 'root' })
export class SiteSettingsStore {
  private readonly api = inject(ApiClient);
  private readonly document = inject(DOCUMENT);
  private readonly appearance = inject(AppearancePreferences);
  readonly settings = signal<SiteSettings>(defaultSiteSettings);
  readonly error = signal('');

  async initialize() {
    try {
      const settings = {
        ...defaultSiteSettings,
        ...(await this.api.get<SiteSettingsDto>('/site-settings')),
      };
      this.settings.set(settings);
      const root = this.document.documentElement;
      root.style.setProperty('--brand-primary', settings.primaryColor);
      root.style.setProperty(
        '--site-font',
        settings.font === 'serif'
          ? 'Georgia, serif'
          : 'Inter, ui-sans-serif, system-ui, sans-serif',
      );
      this.document.title = settings.name;
      this.document
        .querySelector('meta[name="description"]')
        ?.setAttribute('content', settings.description);
    } catch (error) {
      this.error.set('Site settings could not be loaded. ' + message(error));
    } finally {
      this.appearance.initialize(this.settings());
    }
  }
}
