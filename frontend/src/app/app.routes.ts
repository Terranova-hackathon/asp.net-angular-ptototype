import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';

// Every feature is lazy-loaded: its code is only downloaded when the user visits it.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'games' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'games',
    canActivate: [authGuard],
    loadChildren: () => import('./features/games/games.routes').then((m) => m.GAMES_ROUTES),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/profile/profile.component').then((m) => m.ProfileComponent),
  },
  { path: '**', redirectTo: 'games' },
];
