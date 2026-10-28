import { inject, Service } from '@angular/core';
import { GameModel } from '../models/game.model';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';

@Service()
export class GameService {
	private readonly games: GameModel[] = [
		{
      id: 3328,
      name: 'The Witcher 3: Wild Hunt',
      background_image: null,
      released: '2015-05-19',
      rating: 4.64,
	  metacritic: 4.64,
	  genres: ['Action', 'RPG'],
      platforms: ['PC', 'PlayStation', 'Xbox']
    },
    {
      id: 4200,
      name: 'Portal 2',
      background_image: null,
      released: '2011-04-18',
      rating: 4.57,
	  metacritic: 4.64,
	  genres: ['Action'],
      platforms: ['PC', 'PlayStation', 'Xbox']
    }
	];

	getGames(): GameModel[] {
		return this.games;
	}
	private readonly http = inject(HttpClient);
	fetchGames(): Observable<GameModel[]> {
		return this.http.get<GameModel[]>('/api/games');
	}
}
