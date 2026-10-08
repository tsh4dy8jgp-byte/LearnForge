import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppearancePreferences, appearanceStorageKey } from './appearance-preferences';
import { ApiClient } from '../http/api-client';
import { SiteSettingsStore } from '../site/site-settings-store';

describe('Appearance preferences', () => {
  let appearance: AppearancePreferences;
  let storage: Storage;
  let root: HTMLElement;
  beforeEach(() => {
    appearance = TestBed.inject(AppearancePreferences);
    const document = TestBed.inject(DOCUMENT);
    storage = document.defaultView!.localStorage;
    root = document.documentElement;
    storage.removeItem(appearanceStorageKey);
  });
  afterEach(() => {
    vi.restoreAllMocks();
    storage.removeItem(appearanceStorageKey);
    delete root.dataset['layout'];
    delete root.dataset['colorScheme'];
  });

  it('uses deployment defaults without creating a browser override', () => {
    appearance.initialize({ layout: 'header', colorScheme: 'plum' });
    expect(root.dataset['layout']).toBe('header');
    expect(root.dataset['colorScheme']).toBe('plum');
    expect(appearance.customized()).toBe(false);
    expect(storage.getItem(appearanceStorageKey)).toBeNull();
  });

  it('persists explicit choices while unspecified choices follow new site defaults', () => {
    appearance.initialize({ layout: 'sidebar', colorScheme: 'brand' });
    appearance.setColorScheme('midnight');
    appearance.initialize({ layout: 'header', colorScheme: 'ocean' });
    expect(appearance.layout()).toBe('header');
    expect(appearance.colorScheme()).toBe('midnight');
    appearance.setLayout('focus');
    const nextVisit = TestBed.runInInjectionContext(() => new AppearancePreferences());
    nextVisit.initialize({ layout: 'sidebar', colorScheme: 'brand' });
    expect(nextVisit.layout()).toBe('focus');
    expect(nextVisit.colorScheme()).toBe('midnight');
  });

  it.each(['{broken', 'null', '42', '[]', '{"layout":"retired","colorScheme":"unknown"}'])(
    'recovers from invalid saved data: %s',
    (value) => {
      storage.setItem(appearanceStorageKey, value);
      appearance.initialize({ layout: 'focus', colorScheme: 'terracotta' });
      expect(appearance.layout()).toBe('focus');
      expect(appearance.colorScheme()).toBe('terracotta');
      expect(appearance.customized()).toBe(false);
    },
  );

  it('keeps a valid preference when another saved field is obsolete', () => {
    storage.setItem(appearanceStorageKey, '{"layout":"old-layout","colorScheme":"ocean"}');
    appearance.initialize({ layout: 'header', colorScheme: 'brand' });
    expect(appearance.layout()).toBe('header');
    expect(appearance.colorScheme()).toBe('ocean');
  });

  it('removes the override when returning to deployment defaults', () => {
    appearance.initialize({ layout: 'header', colorScheme: 'plum' });
    appearance.setLayout('focus');
    appearance.setColorScheme('midnight');
    appearance.reset();
    expect(storage.getItem(appearanceStorageKey)).toBeNull();
    expect(root.dataset['layout']).toBe('header');
    expect(root.dataset['colorScheme']).toBe('plum');
    expect(appearance.customized()).toBe(false);
  });

  it('still applies choices if browser storage is blocked, and explains that saving failed', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    appearance.initialize({ layout: 'sidebar', colorScheme: 'brand' });
    appearance.setColorScheme('midnight');
    expect(root.dataset['colorScheme']).toBe('midnight');
    expect(appearance.storageNotice()).toContain('could not save');
  });
});

it('applies saved appearance even when site settings cannot be fetched', async () => {
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: { get: vi.fn().mockRejectedValue(new Error('offline')) } },
    ],
  });
  const document = TestBed.inject(DOCUMENT);
  const storage = document.defaultView!.localStorage;
  storage.setItem(appearanceStorageKey, '{"layout":"focus","colorScheme":"midnight"}');
  try {
    const site = TestBed.inject(SiteSettingsStore);
    await site.initialize();
    expect(site.error()).toContain('Site settings could not be loaded');
    expect(document.documentElement.dataset['colorScheme']).toBe('midnight');
    expect(document.documentElement.dataset['layout']).toBe('focus');
  } finally {
    storage.removeItem(appearanceStorageKey);
    delete document.documentElement.dataset['layout'];
    delete document.documentElement.dataset['colorScheme'];
  }
});
