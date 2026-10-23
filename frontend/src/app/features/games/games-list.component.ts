import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api-error';
import { GameSummary, PagedResult } from './games.models';
import { GamesService } from './games.service';

type ListState =
  | { status: 'loading' }
  | { status: 'ready'; data: PagedResult<GameSummary> }
  | { status: 'error'; message: string };

@Component({
  selector: 'app-games-list',
  imports: [RouterLink],
  template: `
    <h1>Games</h1>
    <input
      class="search"
      type="search"
      placeholder="Search games…"
      [value]="searchText()"
      (input)="onSearch($any($event.target).value)"
    />

    @switch (state().status) {
      @case ('loading') {
        <p class="muted">Loading…</p>
      }
      @case ('error') {
        <p class="error" role="alert">{{ errorMessage() }}</p>
      }
      @case ('ready') {
        @if (data(); as result) {
          @if (result.items.length === 0) {
            <p class="muted">No games found.</p>
          }
          <div class="grid">
            @for (game of result.items; track game.id) {
              <a class="card game" [routerLink]="[game.id]">
                @if (game.backgroundImage) {
                  <img [src]="game.backgroundImage" [alt]="game.name" loading="lazy" />
                }
                <div class="body">
                  <h3>{{ game.name }}</h3>
                  <p class="muted">
                    {{ game.released ?? 'TBA' }} · ★ {{ game.rating }}
                    @if (game.metacritic) { · MC {{ game.metacritic }} }
                  </p>
                  <p class="muted">{{ game.genres.join(', ') }}</p>
                </div>
              </a>
            }
          </div>

          <div class="pager">
            <button type="button" (click)="goTo(page() - 1)" [disabled]="page() <= 1">Previous</button>
            <span>Page {{ result.page }} of {{ result.totalPages }}</span>
            <button type="button" (click)="goTo(page() + 1)" [disabled]="page() >= result.totalPages">
              Next
            </button>
          </div>
        }
      }
    }
  `,
})
export class GamesListComponent {
  private readonly games = inject(GamesService);

  protected readonly searchText = signal('');
  protected readonly page = signal(1);

  private readonly debouncedSearch = toSignal(
    toObservable(this.searchText).pipe(debounceTime(300), distinctUntilChanged()),
    { initialValue: '' },
  );

  // Any change to the search or page re-runs the request; switchMap cancels the stale one.
  protected readonly state = toSignal(
    toObservable(computed(() => ({ search: this.debouncedSearch(), page: this.page() }))).pipe(
      switchMap(({ search, page }) =>
        this.games.search(search, page).pipe(
          map((data): ListState => ({ status: 'ready', data })),
          startWith<ListState>({ status: 'loading' }),
          catchError((err: unknown) =>
            of<ListState>({ status: 'error', message: apiErrorMessage(err, 'Could not load games.') }),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as ListState },
  );

  protected readonly data = computed(() => {
    const s = this.state();
    return s.status === 'ready' ? s.data : null;
  });

  protected readonly errorMessage = computed(() => {
    const s = this.state();
    return s.status === 'error' ? s.message : '';
  });

  protected onSearch(value: string): void {
    this.searchText.set(value);
    this.page.set(1);
  }

  protected goTo(page: number): void {
    this.page.set(Math.max(1, page));
  }
}
