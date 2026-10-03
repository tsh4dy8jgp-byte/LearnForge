import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { Api } from './api';
import { Site } from './site-settings';
import type { StudioPage } from './pages/studio';
const authenticated: CanActivateFn = () =>
  inject(Api).user() ? true : inject(Router).parseUrl('/sign-in');
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: () => inject(Site).settings().homePage ?? 'dashboard' },
  { path: 'sign-in', title: 'Sign in', loadComponent: () => import('./pages/auth').then((m) => m.AuthPage) },
  { path: 'appearance', title: 'Appearance', loadComponent: () => import('./pages/appearance').then((m) => m.AppearancePage) },
  {
    path: 'dashboard',
    title: () => inject(Site).settings().overviewLabel ?? 'Overview',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/dashboard').then((m) => m.DashboardPage),
  },
  { path: 'courses', title: () => inject(Site).settings().libraryLabel ?? 'Learning library', loadComponent: () => import('./pages/courses').then((m) => m.CoursesPage) },
  { path: 'courses/:id', title: 'Learning path', loadComponent: () => import('./pages/course').then((m) => m.CoursePage) },
  {
    path: 'attempts',
    title: () => inject(Site).settings().historyLabel ?? 'Attempts & results',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/history').then((m) => m.HistoryPage),
  },
  {
    path: 'attempts/:id',
    title: 'Session',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/attempt').then((m) => m.AttemptPage),
  },
  {
    path: 'studio',
    title: () => inject(Site).settings().studioLabel ?? 'Content studio',
    canDeactivate: [(component: StudioPage) => component.canLeave()],
    canActivate: [authenticated],
    loadComponent: () => import('./pages/studio').then((m) => m.StudioPage),
  },
  {
    path: 'settings',
    title: 'Account',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/settings').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
