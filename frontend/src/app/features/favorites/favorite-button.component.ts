import { Component, inject, input, signal } from '@angular/core';
import { FavoritesService } from './favorites.service';

@Component({
  selector: 'app-favorite-button',
  template: `
    <button
      type="button"
      class="heart"
      [class.on]="favorites.isFavorite(gameId())"
      [disabled]="busy()"
      [attr.aria-pressed]="favorites.isFavorite(gameId())"
      [attr.aria-label]="favorites.isFavorite(gameId()) ? 'Remove from favorites' : 'Add to favorites'"
      [title]="error() || (favorites.isFavorite(gameId()) ? 'Remove from favorites' : 'Add to favorites')"
      (click)="toggle()"
    >
      {{ favorites.isFavorite(gameId()) ? '♥' : '♡' }}
    </button>
  `,
})
export class FavoriteButtonComponent {
  protected readonly favorites = inject(FavoritesService);

  readonly gameId = input.required<number>();

  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected toggle(): void {
    const id = this.gameId();
    const request = this.favorites.isFavorite(id) ? this.favorites.remove(id) : this.favorites.add(id);
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => this.busy.set(false),
      error: () => {
        this.busy.set(false);
        this.error.set('Could not update favorites. Try again.');
      },
    });
  }
}
