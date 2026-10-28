import { Params } from '@angular/router';

/** What the user picked in the advanced search. Lists are "any of these" (OR). */
export interface GameFilters {
  genres: string[];
  platforms: string[];
  developers: string[];
  publishers: string[];
  ordering: string;
  metacriticMin: number | null;
}

export const NO_FILTERS: GameFilters = {
  genres: [],
  platforms: [],
  developers: [],
  publishers: [],
  ordering: '',
  metacriticMin: null,
};

export function activeFilterCount(f: GameFilters): number {
  return (
    f.genres.length +
    f.platforms.length +
    f.developers.length +
    f.publishers.length +
    (f.ordering ? 1 : 0) +
    (f.metacriticMin !== null ? 1 : 0)
  );
}

/** Only non-empty values go in the URL, so the address bar stays short and shareable. */
export function filtersToQueryParams(f: GameFilters): Params {
  return {
    genres: f.genres.join(',') || null,
    platforms: f.platforms.join(',') || null,
    developers: f.developers.join(',') || null,
    publishers: f.publishers.join(',') || null,
    ordering: f.ordering || null,
    metacriticMin: f.metacriticMin,
  };
}

export function filtersFromQueryParams(get: (key: string) => string | null): GameFilters {
  const list = (key: string) => (get(key) ?? '').split(',').filter(Boolean);
  const min = Number(get('metacriticMin'));
  return {
    genres: list('genres'),
    platforms: list('platforms'),
    developers: list('developers'),
    publishers: list('publishers'),
    ordering: get('ordering') ?? '',
    metacriticMin: get('metacriticMin') !== null && Number.isFinite(min) ? min : null,
  };
}
