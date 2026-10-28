import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, startWith } from 'rxjs';
import { apiErrorMessage } from '../../core/api-error';
import {
  SteamAchievements,
  SteamFriend,
  SteamGame,
  SteamGameLibrary,
  SteamProfile,
  SteamSection,
} from './profile.models';

/** The only place that knows the Steam API URLs. Components never talk to Steam directly. */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);

  profile(): Observable<SteamProfile> {
    return this.http.get<SteamProfile>('/api/steam/profile');
  }

  games(): Observable<SteamSection<SteamGameLibrary>> {
    return this.http.get<SteamSection<SteamGameLibrary>>('/api/steam/games');
  }

  recentGames(): Observable<SteamSection<SteamGame[]>> {
    return this.http.get<SteamSection<SteamGame[]>>('/api/steam/games/recent');
  }

  friends(): Observable<SteamSection<SteamFriend[]>> {
    return this.http.get<SteamSection<SteamFriend[]>>('/api/steam/friends');
  }

  achievements(appId: number): Observable<SteamSection<SteamAchievements>> {
    return this.http.get<SteamSection<SteamAchievements>>(`/api/steam/games/${appId}/achievements`);
  }
}

export type Loadable<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T };

/** Gives each page section its own loading/error state so one failure does not blank the page. */
export function toLoadable<T>(source: Observable<T>): Observable<Loadable<T>> {
  return source.pipe(
    map((data): Loadable<T> => ({ status: 'ready', data })),
    startWith<Loadable<T>>({ status: 'loading' }),
    catchError((err: unknown) =>
      of<Loadable<T>>({ status: 'error', message: apiErrorMessage(err, 'Could not load this section.') }),
    ),
  );
}
