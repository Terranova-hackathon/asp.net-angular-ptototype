import { Routes } from '@angular/router';

export const GAMES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./games-list.component').then((m) => m.GamesListComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./game-detail.component').then((m) => m.GameDetailComponent),
  },
];
