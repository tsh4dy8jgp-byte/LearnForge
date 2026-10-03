import { DOCUMENT, registerLocaleData } from '@angular/common';
import enGb from '@angular/common/locales/en-GB';
import ru from '@angular/common/locales/ru';
import kk from '@angular/common/locales/kk';
import { inject, Injectable, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { Api, message } from './api';
import { SiteSettings as SiteSettingsDto } from './models';
import { Appearance } from './appearance';

export type SiteSettings = Required<SiteSettingsDto>;

registerLocaleData(enGb, 'en-GB');
registerLocaleData(ru, 'ru-RU');
registerLocaleData(kk, 'kk-KZ');

export const defaultSiteSettings: SiteSettings = {
  name: 'LearnForge', tagline: 'A little further, every day.',
  description: 'Purposeful practice. Measurable progress.', logoPath: null,
  primaryColor: '#315b4b', font: 'system', locale: 'en-US', homePage: 'dashboard',
  layout: 'sidebar', colorScheme: 'brand',
  overviewLabel: 'Overview', libraryLabel: 'Learning library', historyLabel: 'Attempts & results', studioLabel: 'Content studio',
};

@Injectable({ providedIn: 'root' })
export class Site {
  private readonly api = inject(Api);
  private readonly document = inject(DOCUMENT);
  private readonly appearance = inject(Appearance);
  readonly settings = signal<SiteSettings>(defaultSiteSettings);
  readonly error = signal('');

  async initialize() {
    try {
      const settings = { ...defaultSiteSettings, ...(await this.api.get<SiteSettingsDto>('/site-settings')) };
      this.settings.set(settings);
      const root = this.document.documentElement;
      root.style.setProperty('--brand-primary', settings.primaryColor);
      root.style.setProperty('--site-font', settings.font === 'serif' ? 'Georgia, serif' : 'Inter, ui-sans-serif, system-ui, sans-serif');
      this.document.title = settings.name;
      this.document.querySelector('meta[name="description"]')?.setAttribute('content', settings.description);
    } catch (error) {
      this.error.set('Site settings could not be loaded. ' + message(error));
    } finally {
      this.appearance.initialize(this.settings());
    }
  }
}

@Injectable()
export class SiteTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly site = inject(Site);
  override updateTitle(snapshot: RouterStateSnapshot) {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${page} · ${this.site.settings().name}` : this.site.settings().name);
  }
}
