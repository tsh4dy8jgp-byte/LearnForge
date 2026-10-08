import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Session } from './session';

export const authenticated: CanActivateFn = () =>
  inject(Session).user() ? true : inject(Router).parseUrl('/sign-in');
