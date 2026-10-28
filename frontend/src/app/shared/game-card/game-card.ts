import { Component, input } from '@angular/core';
import { Game } from '../../core/models/game.model';

@Component({
  imports: [],
  selector: 'app-game-card',
  styleUrl: './game-card.scss',
  templateUrl: './game-card.html',
})
export class GameCard {
  game = input.required<Game>();
}
