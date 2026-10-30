import { inject, Service } from '@angular/core';
import { GameModel } from '../models/game.model';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { PagedResult } from '../models/paged-result.model';

@Service()
export class GameService {
	private readonly games: GameModel[] = [
		{
      id: 3328,
      name: 'The Witcher 3: Wild Hunt',
      backgroundImage: null,
      released: '2015-05-19',
      rating: 4.64,
	  metacritic: 4.64,
	  genres: ['Action', 'RPG'],
      platforms: ['PC', 'PlayStation', 'Xbox']
    },
    {
      id: 4200,
      name: 'Portal 2',
      backgroundImage: null,
      released: '2011-04-18',
      rating: 4.57,
	  metacritic: 4.64,
	  genres: ['Action'],
      platforms: ['PC', 'PlayStation', 'Xbox']
    }
	];

  private readonly http = inject(HttpClient);

	getGames(): GameModel[] {
		return this.games;
	}

	fetchGames(): Observable<PagedResult<GameModel>> {
		return this.http.get<PagedResult<GameModel>>(
      '/api/games',
      {
        params: {
          page: 1,
          pageSize: 12
        }
      }
    )
	}
}
