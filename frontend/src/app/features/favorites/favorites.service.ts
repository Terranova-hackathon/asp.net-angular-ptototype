import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

export interface Favorite {
  id: number;
  name: string;
  imageUrl: string | null;
  addedAtUtc: string;
}

/** The only place that knows the favorites API URLs; also keeps the set of favorite ids for the heart buttons. */
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly _ids = signal<ReadonlySet<number>>(new Set());
  readonly ids = this._ids.asReadonly();
  readonly count = computed(() => this._ids().size);

  constructor() {
    // Load on login, clear on logout, so one user's hearts never show for another.
    effect(() => {
      if (this.auth.user()) {
        this.http.get<number[]>('/api/favorites/ids').subscribe({
          next: (ids) => this._ids.set(new Set(ids)),
          error: () => this._ids.set(new Set()),
        });
      } else {
        this._ids.set(new Set());
      }
    });
  }

  isFavorite(id: number): boolean {
    return this._ids().has(id);
  }

  list(): Observable<Favorite[]> {
    return this.http.get<Favorite[]>('/api/favorites');
  }

  add(id: number): Observable<void> {
    return this.http.put<void>(`/api/favorites/${id}`, {}).pipe(tap(() => this.update(id, true)));
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`/api/favorites/${id}`).pipe(tap(() => this.update(id, false)));
  }

  private update(id: number, present: boolean): void {
    this._ids.update((current) => {
      const next = new Set(current);
      if (present) next.add(id);
      else next.delete(id);
      return next;
    });
  }
}
