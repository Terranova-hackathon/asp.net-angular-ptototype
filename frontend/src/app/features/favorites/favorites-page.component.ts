import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { apiErrorMessage } from '../../core/api-error';
import { FavoriteButtonComponent } from './favorite-button.component';
import { Favorite, FavoritesService } from './favorites.service';

type PageState =
  | { status: 'loading' }
  | { status: 'ready'; items: Favorite[] }
  | { status: 'error'; message: string };

@Component({
  selector: 'app-favorites-page',
  imports: [RouterLink, FavoriteButtonComponent],
  template: `
    <h1>Favorites</h1>
    @let s = state();
    @if (s.status === 'loading') {
      <p class="muted">Loading…</p>
    } @else if (s.status === 'error') {
      <p class="error" role="alert">{{ s.message }}</p>
    } @else {
      @if (visible().length === 0) {
        <p class="muted">No favorites yet. Tap the ♡ on a game to save it here.</p>
      }
      <div class="grid">
        @for (game of visible(); track game.id) {
          <div class="card game">
            <app-favorite-button [gameId]="game.id" />
            <a [routerLink]="['/games', game.id]">
              @if (game.imageUrl) {
                <img [src]="game.imageUrl" [alt]="game.name" loading="lazy" />
              }
              <div class="body">
                <h3>{{ game.name }}</h3>
              </div>
            </a>
          </div>
        }
      </div>
    }
  `,
})
export class FavoritesPageComponent {
  private readonly favorites = inject(FavoritesService);

  protected readonly state = toSignal(
    this.favorites.list().pipe(
      map((items): PageState => ({ status: 'ready', items })),
      startWith<PageState>({ status: 'loading' }),
      catchError((err: unknown) =>
        of<PageState>({ status: 'error', message: apiErrorMessage(err, 'Could not load favorites.') }),
      ),
    ),
    { initialValue: { status: 'loading' } as PageState },
  );

  // Items un-hearted on this page disappear immediately, without another request.
  protected readonly visible = computed(() => {
    const s = this.state();
    return s.status === 'ready' ? s.items.filter((f) => this.favorites.ids().has(f.id)) : [];
  });
}
