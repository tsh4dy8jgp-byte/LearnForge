import { authenticated } from './authentication/authenticated';
import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { SiteSettingsStore } from './site/site-settings-store';
import type { StudioPage } from './authoring/studio-page';
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: () => inject(SiteSettingsStore).settings().homePage ?? 'dashboard',
  },
  {
    path: 'sign-in',
    title: 'Sign in',
    loadComponent: () => import('./authentication/auth-page').then((m) => m.AuthPage),
  },
  {
    path: 'appearance',
    title: 'Appearance',
    loadComponent: () => import('./appearance/appearance-page').then((m) => m.AppearancePage),
  },
  {
    path: 'dashboard',
    title: () => inject(SiteSettingsStore).settings().overviewLabel ?? 'Overview',
    canActivate: [authenticated],
    loadComponent: () => import('./dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'courses',
    title: () => inject(SiteSettingsStore).settings().libraryLabel ?? 'Learning library',
    loadComponent: () => import('./learning-library/courses-page').then((m) => m.CoursesPage),
  },
  {
    path: 'courses/:id',
    title: 'Learning path',
    loadComponent: () => import('./learning-library/course/course-page').then((m) => m.CoursePage),
  },
  {
    path: 'attempts',
    title: () => inject(SiteSettingsStore).settings().historyLabel ?? 'Attempts & results',
    canActivate: [authenticated],
    loadComponent: () => import('./attempts/history-page').then((m) => m.HistoryPage),
  },
  {
    path: 'attempts/:id',
    title: 'Session',
    canActivate: [authenticated],
    loadComponent: () => import('./attempts/attempt-page').then((m) => m.AttemptPage),
  },
  {
    path: 'studio',
    title: () => inject(SiteSettingsStore).settings().studioLabel ?? 'Content studio',
    canDeactivate: [(component: StudioPage) => component.canLeave()],
    canActivate: [authenticated],
    loadComponent: () => import('./authoring/studio-page').then((m) => m.StudioPage),
  },
  {
    path: 'settings',
    title: 'Account',
    canActivate: [authenticated],
    loadComponent: () => import('./account/settings-page').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
