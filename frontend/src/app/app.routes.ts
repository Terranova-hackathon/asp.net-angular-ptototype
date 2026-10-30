import { Routes } from '@angular/router';
import { Home } from './pages/home/home'
import { Games } from './pages/games/games';
import { Favorites } from './pages/favorites/favorites';
import { Login } from './pages/login/login';

export const routes: Routes = [
	{
		path: '',
		component: Home
	},
	{
		path: 'games',
		component: Games
	},
	{
		path: 'favorites',
		component: Favorites
	},
	{
		path: 'login',
		component: Login
	}
];
