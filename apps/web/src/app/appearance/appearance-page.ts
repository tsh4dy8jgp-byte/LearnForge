import { Component, computed, inject, signal } from '@angular/core';
import { AppearancePreferences, colorSchemes, layouts } from './appearance-preferences';
import type { SiteColorScheme, SiteLayout } from '../api-contracts';

@Component({
  selector: 'app-appearance',
  templateUrl: './appearance-page.html',
})
export class AppearancePage {
  protected readonly appearance = inject(AppearancePreferences);
  protected readonly layouts = layouts;
  protected readonly colorSchemes = colorSchemes;
  protected readonly announcement = signal('');
  protected readonly currentLook = computed(
    () =>
      `${layouts.find((item) => item.id === this.appearance.layout())?.name} · ${colorSchemes.find((item) => item.id === this.appearance.colorScheme())?.name}`,
  );

  protected chooseLayout(layout: SiteLayout) {
    this.appearance.setLayout(layout);
    this.announce();
  }
  protected chooseScheme(scheme: SiteColorScheme) {
    this.appearance.setColorScheme(scheme);
    this.announce();
  }
  protected reset() {
    this.appearance.reset();
    this.announcement.set(`Site defaults restored. ${this.currentLook()}.`);
  }
  private announce() {
    this.announcement.set(`${this.currentLook()} applied.`);
  }
}
