import { Component, inject } from '@angular/core';
import { GameCard } from '../../shared/game-card/game-card';
import { GameModel } from '../../core/models/game.model';
import { GameService } from '../../core/services/game';

@Component({
  imports: [GameCard],
  selector: 'app-games',
  styleUrl: './games.scss',
  templateUrl: './games.html',
})
export class Games {
  private readonly gameService = inject(GameService);

  games: GameModel[] = this.gameService.getGames();
}
