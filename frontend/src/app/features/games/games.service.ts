import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { GameFilters, NO_FILTERS } from './games.filters';
import {
  CompanyKind,
  CompanyOption,
  GameDetails,
  GameFilterOptions,
  GameSummary,
  PagedResult,
} from './games.models';

/** The only place that knows the games API URLs. Components just call methods. */
@Injectable({ providedIn: 'root' })
export class GamesService {
  private readonly http = inject(HttpClient);

  search(
    search: string,
    page: number,
    filters: GameFilters = NO_FILTERS,
    pageSize = 20,
  ): Observable<PagedResult<GameSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    if (filters.genres.length) params = params.set('genres', filters.genres.join(','));
    if (filters.platforms.length) params = params.set('platforms', filters.platforms.join(','));
    if (filters.developers.length) params = params.set('developers', filters.developers.join(','));
    if (filters.publishers.length) params = params.set('publishers', filters.publishers.join(','));
    if (filters.ordering) params = params.set('ordering', filters.ordering);
    if (filters.metacriticMin !== null) params = params.set('metacriticMin', filters.metacriticMin);
    return this.http.get<PagedResult<GameSummary>>('/api/games', { params });
  }

  getById(id: number): Observable<GameDetails> {
    return this.http.get<GameDetails>(`/api/games/${id}`);
  }

  getFilterOptions(): Observable<GameFilterOptions> {
    return this.http.get<GameFilterOptions>('/api/games/filters');
  }

  searchCompanies(kind: CompanyKind, search: string): Observable<CompanyOption[]> {
    const params = new HttpParams().set('kind', kind).set('search', search.trim());
    return this.http.get<CompanyOption[]>('/api/games/companies', { params });
  }
}
