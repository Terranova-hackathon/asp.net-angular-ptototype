import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { GameDetails, GameSummary, PagedResult } from './games.models';

/** The only place that knows the games API URLs. Components just call methods. */
@Injectable({ providedIn: 'root' })
export class GamesService {
  private readonly http = inject(HttpClient);

  search(search: string, page: number, pageSize = 20): Observable<PagedResult<GameSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<GameSummary>>('/api/games', { params });
  }

  getById(id: number): Observable<GameDetails> {
    return this.http.get<GameDetails>(`/api/games/${id}`);
  }
}
