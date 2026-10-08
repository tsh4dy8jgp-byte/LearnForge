import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';
import type { SiteColorScheme, SiteLayout } from '../api-contracts';

export const layouts = [
  { id: 'sidebar', name: 'Workspace', description: 'A familiar sidebar with everything in reach.' },
  { id: 'header', name: 'Campus', description: 'Navigation across the top. More room to explore.' },
  {
    id: 'focus',
    name: 'Focus',
    description: 'A centred workspace with a quieter, single-column dashboard.',
  },
] as const satisfies ReadonlyArray<{ id: SiteLayout; name: string; description: string }>;

export const colorSchemes = [
  { id: 'brand', name: 'Site brand', description: 'Botanical greens, or your site’s own accent.' },
  { id: 'ocean', name: 'Ocean', description: 'Cool blues and crisp, airy surfaces.' },
  { id: 'plum', name: 'Plum', description: 'Soft lavender with rich violet accents.' },
  { id: 'terracotta', name: 'Terracotta', description: 'Warm clay, cream, and a little sunshine.' },
  { id: 'midnight', name: 'Midnight', description: 'Deep slate with luminous mint accents.' },
] as const satisfies ReadonlyArray<{ id: SiteColorScheme; name: string; description: string }>;

export const appearanceStorageKey = 'learnforge.appearance.v1';
type Preferences = { layout?: SiteLayout; colorScheme?: SiteColorScheme };

@Injectable({ providedIn: 'root' })
export class AppearancePreferences {
  private readonly document = inject(DOCUMENT);
  private readonly defaults = signal<Required<Preferences>>({
    layout: 'sidebar',
    colorScheme: 'brand',
  });
  private readonly preferences = signal<Preferences>({});
  readonly layout = computed(() => this.preferences().layout ?? this.defaults().layout);
  readonly colorScheme = computed(
    () => this.preferences().colorScheme ?? this.defaults().colorScheme,
  );
  readonly customized = computed(() => Object.keys(this.preferences()).length > 0);
  readonly storageNotice = signal('');

  initialize(defaults: Required<Preferences>) {
    this.defaults.set(defaults);
    // Validate each field independently so outdated or corrupt browser data cannot break startup.
    try {
      const value: unknown = JSON.parse(
        this.document.defaultView?.localStorage.getItem(appearanceStorageKey) ?? 'null',
      );
      const preferences: Preferences = {};
      if (value && typeof value === 'object') {
        if ('layout' in value && layouts.some((item) => item.id === value.layout))
          preferences.layout = value.layout as SiteLayout;
        if ('colorScheme' in value && colorSchemes.some((item) => item.id === value.colorScheme))
          preferences.colorScheme = value.colorScheme as SiteColorScheme;
      }
      this.preferences.set(preferences);
    } catch {
      this.preferences.set({});
    }
    this.apply();
  }

  setLayout(layout: SiteLayout) {
    this.save({ ...this.preferences(), layout });
  }
  setColorScheme(colorScheme: SiteColorScheme) {
    this.save({ ...this.preferences(), colorScheme });
  }
  reset() {
    this.save({});
  }

  private save(preferences: Preferences) {
    this.preferences.set(preferences);
    this.apply();
    this.storageNotice.set('');
    try {
      const storage = this.document.defaultView?.localStorage;
      if (!storage) throw new Error('Storage unavailable');
      if (this.customized()) storage.setItem(appearanceStorageKey, JSON.stringify(preferences));
      else storage.removeItem(appearanceStorageKey);
    } catch {
      this.storageNotice.set(
        'Your choice is applied for now. This browser could not save it for your next visit.',
      );
    }
  }

  private apply() {
    const root = this.document.documentElement;
    root.dataset['layout'] = this.layout();
    root.dataset['colorScheme'] = this.colorScheme();
  }
}
