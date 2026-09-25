import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { Api } from './api';
const authenticated: CanActivateFn = () =>
  inject(Api).user() ? true : inject(Router).parseUrl('/sign-in');
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'sign-in', loadComponent: () => import('./pages/auth').then((m) => m.AuthPage) },
  {
    path: 'dashboard',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/dashboard').then((m) => m.DashboardPage),
  },
  { path: 'courses', loadComponent: () => import('./pages/courses').then((m) => m.CoursesPage) },
  { path: 'courses/:id', loadComponent: () => import('./pages/course').then((m) => m.CoursePage) },
  {
    path: 'attempts',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/history').then((m) => m.HistoryPage),
  },
  {
    path: 'attempts/:id',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/attempt').then((m) => m.AttemptPage),
  },
  {
    path: 'studio',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/studio').then((m) => m.StudioPage),
  },
  {
    path: 'settings',
    canActivate: [authenticated],
    loadComponent: () => import('./pages/settings').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
