import { Component, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api-error';
import { GameDetails } from './games.models';
import { GamesService } from './games.service';

type DetailState =
  | { status: 'loading' }
  | { status: 'ready'; game: GameDetails }
  | { status: 'error'; message: string };

@Component({
  selector: 'app-game-detail',
  imports: [RouterLink],
  template: `
    <a routerLink="/games">← Back to games</a>

    @let s = state();
    @if (s.status === 'loading') {
      <p class="muted">Loading…</p>
    } @else if (s.status === 'error') {
      <p class="error" role="alert">{{ s.message }}</p>
    } @else {
      @let game = s.game;
      <article class="card detail">
        @if (game.backgroundImage) {
          <img [src]="game.backgroundImage" [alt]="game.name" />
        }
        <h1>{{ game.name }}</h1>
        <p class="muted">
          Released {{ game.released ?? 'TBA' }} · ★ {{ game.rating }}
          @if (game.metacritic) { · Metacritic {{ game.metacritic }} }
        </p>
        <p><strong>Genres:</strong> {{ game.genres.join(', ') || '—' }}</p>
        <p><strong>Platforms:</strong> {{ game.platforms.join(', ') || '—' }}</p>
        <p><strong>Developers:</strong> {{ game.developers.join(', ') || '—' }}</p>
        <p><strong>Publishers:</strong> {{ game.publishers.join(', ') || '—' }}</p>
        @if (game.description) {
          <p class="description">{{ game.description }}</p>
        }
        @if (game.website) {
          <p><a [href]="game.website" target="_blank" rel="noopener noreferrer">Official website</a></p>
        }
      </article>
    }
  `,
})
export class GameDetailComponent {
  private readonly games = inject(GamesService);

  /** Bound from the route (:id) thanks to withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly state = toSignal(
    toObservable(this.id).pipe(
      switchMap((id) =>
        this.games.getById(Number(id)).pipe(
          map((game): DetailState => ({ status: 'ready', game })),
          startWith<DetailState>({ status: 'loading' }),
          catchError((err: unknown) =>
            of<DetailState>({ status: 'error', message: apiErrorMessage(err, 'Could not load the game.') }),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as DetailState },
  );
}
