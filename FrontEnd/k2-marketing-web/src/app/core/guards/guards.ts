import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { UserRole } from '../../shared/models/models';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
};

/** Rotas com `data: { roles: [...] }` só abrem para os perfis informados. */
export const roleGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const roles = (route.data?.['roles'] ?? []) as UserRole[];
  return auth.hasRole(...roles) ? true : inject(Router).createUrlTree(['/forbidden']);
};
