import { Component } from '@angular/core';
import { GameCard } from '../../shared/game-card/game-card';
import { Game } from '../../core/models/game.model';

@Component({
  imports: [GameCard],
  selector: 'app-games',
  styleUrl: './games.scss',
  templateUrl: './games.html',
})
export class Games {
  games: Game[] = [
  {
    id:3328,
    name: 'The Witcher 3: Wild Hunt',
    background_image: null,
    released: '2015-05-19',
    rating: 4.64,
    metacritic: 4.64,
    genres: ['Action', 'RPG'],
    platforms: ['PC', 'PlayStation', 'Xbox']
  },
  {
    id:3328,
    name: 'The Witcher 3: Wild Hunt',
    background_image: null,
    released: '2015-05-19',
    rating: 4.64,
    metacritic: 4.64,
    genres: ['Action', 'RPG'],
    platforms: ['PC', 'PlayStation', 'Xbox']
  },
  {
    id:3328,
    name: 'The Witcher 3: Wild Hunt',
    background_image: null,
    released: '2015-05-19',
    rating: 4.64,
    metacritic: 4.64,
    genres: ['Action', 'RPG'],
    platforms: ['PC', 'PlayStation', 'Xbox']
  }
]
}
