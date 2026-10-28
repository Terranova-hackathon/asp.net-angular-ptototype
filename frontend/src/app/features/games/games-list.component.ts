import { Component, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api-error';
import { FavoriteButtonComponent } from '../favorites/favorite-button.component';
import { GamesFiltersComponent } from './games-filters.component';
import { GameFilters, filtersFromQueryParams, filtersToQueryParams } from './games.filters';
import { GameSummary, PagedResult } from './games.models';
import { GamesService } from './games.service';

type ListState =
  | { status: 'loading' }
  | { status: 'ready'; data: PagedResult<GameSummary> }
  | { status: 'error'; message: string };

@Component({
  selector: 'app-games-list',
  imports: [RouterLink, FavoriteButtonComponent, GamesFiltersComponent],
  template: `
    <h1>Games</h1>
    <input
      class="search"
      type="search"
      placeholder="Search games…"
      [value]="searchText()"
      (input)="onSearch($any($event.target).value)"
    />

    <app-games-filters
      [options]="filterOptions()"
      [value]="filters()"
      (changed)="onFilters($event)"
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
              <div class="card game">
                <app-favorite-button [gameId]="game.id" />
                <a [routerLink]="[game.id]">
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
              </div>
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

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // The URL is the saved state: reloading or sharing the link restores the same search.
  private readonly initial = this.route.snapshot.queryParamMap;
  protected readonly searchText = signal(this.initial.get('search') ?? '');
  protected readonly page = signal(Math.max(1, Number(this.initial.get('page')) || 1));
  protected readonly filters = signal<GameFilters>(filtersFromQueryParams((k) => this.initial.get(k)));

  protected readonly filterOptions = toSignal(
    this.games.getFilterOptions().pipe(catchError(() => of(null))),
    { initialValue: null },
  );

  private readonly debouncedSearch = toSignal(
    toObservable(this.searchText).pipe(debounceTime(300), distinctUntilChanged()),
    { initialValue: this.searchText() },
  );

  private readonly syncUrl = effect(() => {
    const search = this.debouncedSearch().trim();
    const page = this.page();
    void this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        search: search || null,
        page: page > 1 ? page : null,
        ...filtersToQueryParams(this.filters()),
      },
    });
  });

  // Any change to the search or page re-runs the request; switchMap cancels the stale one.
  protected readonly state = toSignal(
    toObservable(computed(() => ({ search: this.debouncedSearch(), page: this.page(), filters: this.filters() }))).pipe(
      switchMap(({ search, page, filters }) =>
        this.games.search(search, page, filters).pipe(
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

  protected onFilters(filters: GameFilters): void {
    this.filters.set(filters);
    this.page.set(1);
  }

  protected goTo(page: number): void {
    this.page.set(Math.max(1, page));
  }
}
