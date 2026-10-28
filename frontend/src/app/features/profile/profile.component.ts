import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, of, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { SteamGame } from './profile.models';
import { Loadable, ProfileService, toLoadable } from './profile.service';

const PAGE_SIZE = 24;

@Component({
  selector: 'app-profile',
  imports: [DatePipe],
  host: { '(window:keydown.escape)': 'closeAchievements()' },
  styles: `
    .head { display: flex; gap: 1.25rem; align-items: center; padding: 1.25rem; margin-bottom: 1.5rem; }
    .head img.avatar { width: 96px; height: 96px; border-radius: 12px; }
    .head h1 { margin: 0 0 0.25rem; }
    .stats { display: flex; gap: 2rem; margin-top: 0.75rem; }
    .stats strong { display: block; font-size: 1.4rem; }
    h2 { margin: 2rem 0 0.75rem; }
    .banner { padding: 0.75rem 1rem; margin-bottom: 1rem; border-left: 4px solid var(--danger); }
    .tile { padding: 0; text-align: left; background: var(--panel); color: inherit; display: block; width: 100%; }
    .tile:hover { outline: 2px solid var(--accent); }
    .tile img { width: 100%; height: 110px; object-fit: cover; display: block; background: var(--bg); }
    .tile .body { padding: 0.6rem 0.8rem; }
    .tile h3 { margin: 0 0 0.2rem; font-size: 1rem; }
    .friends { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 0.75rem; }
    .friend { display: flex; gap: 0.75rem; align-items: center; padding: 0.6rem; color: inherit; }
    .friend img { width: 44px; height: 44px; border-radius: 6px; background: var(--bg); }
    .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #666; margin-right: 0.35rem; }
    .dot.online { background: #57cbde; }
    .overlay { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.65); display: flex; align-items: center; justify-content: center; padding: 1rem; z-index: 10; }
    .dialog { max-width: 640px; width: 100%; max-height: 85vh; overflow: auto; padding: 1.25rem; }
    .dialog header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
    .achievement { display: flex; flex-direction: column; padding: 0.6rem 0; border-top: 1px solid #2d3354; }
    .achievement.locked { opacity: 0.5; }
    .bar { height: 8px; background: var(--bg); border-radius: 4px; overflow: hidden; margin: 0.75rem 0; }
    .bar span { display: block; height: 100%; background: var(--accent); }
    .toolbar { display: flex; gap: 1rem; align-items: center; margin-bottom: 1rem; }
    .toolbar input { flex: 1; }
  `,
  template: `
    @switch (steam()) {
      @case ('failed') {
        <p class="card banner error" role="alert">Steam sign-in could not be verified. Please try again.</p>
      }
      @case ('conflict') {
        <p class="card banner error" role="alert">
          That Steam account is already linked to a different GameHub account, or your account already has another Steam account.
        </p>
      }
    }

    @if (!linked) {
      <section class="card narrow">
        <h1>Connect Steam</h1>
        <p class="muted">
          Link your Steam account to see your profile, games, playtime, achievements and friends here.
        </p>
        <a class="button steam" [href]="auth.steamLoginUrl">Connect with Steam</a>
      </section>
    } @else {
      @let p = profile();
      @if (p.status === 'loading') {
        <p class="muted">Loading your Steam profile…</p>
      } @else if (p.status === 'error') {
        <p class="error" role="alert">{{ p.message }}</p>
      } @else {
        @let me = p.data;
        <section class="card head">
          <img class="avatar" [src]="me.avatarUrl" [alt]="me.personaName" />
          <div>
            <h1>{{ me.personaName }}</h1>
            <p class="muted">
              <span class="dot" [class.online]="me.status !== 'offline'"></span>
              @if (me.currentGame) { Playing {{ me.currentGame }} } @else { {{ me.status }} }
              @if (me.memberSince) { · Member since {{ me.memberSince | date: 'MMM y' }} }
              @if (me.countryCode) { · {{ me.countryCode }} }
              · <a [href]="me.profileUrl" target="_blank" rel="noopener noreferrer">Steam profile</a>
            </p>
            <div class="stats">
              <span><strong>{{ library()?.count ?? '—' }}</strong><span class="muted">games</span></span>
              <span><strong>{{ library() ? hours(library()!.totalPlaytimeMinutes) + ' h' : '—' }}</strong><span class="muted">played</span></span>
              <span><strong>{{ friendCount() ?? '—' }}</strong><span class="muted">friends</span></span>
            </div>
            @if (!me.isPublic) {
              <p class="muted">🔒 Your Steam profile is not public, so some data below may be hidden.</p>
            }
          </div>
        </section>
      }

      <h2>Recently played</h2>
      @let recent = recentGames();
      @if (recent.status === 'loading') {
        <p class="muted">Loading…</p>
      } @else if (recent.status === 'error') {
        <p class="error" role="alert">{{ recent.message }}</p>
      } @else if (recent.data.status === 'private') {
        <p class="muted">🔒 Hidden by your Steam privacy settings. Set "Game details" to public in Steam, then reload.</p>
      } @else if (!recent.data.data?.length) {
        <p class="muted">Nothing played in the last two weeks.</p>
      } @else {
        <div class="grid">
          @for (game of recent.data.data; track game.appId) {
            <button type="button" class="card tile" (click)="openAchievements(game)">
              <img [src]="game.headerUrl" [alt]="game.name" loading="lazy" />
              <div class="body">
                <h3>{{ game.name }}</h3>
                <p class="muted">{{ hours(game.playtimeLastTwoWeeksMinutes) }} h past 2 weeks · {{ hours(game.playtimeMinutes) }} h total</p>
              </div>
            </button>
          }
        </div>
      }

      <h2>Library</h2>
      @let lib = games();
      @if (lib.status === 'loading') {
        <p class="muted">Loading…</p>
      } @else if (lib.status === 'error') {
        <p class="error" role="alert">{{ lib.message }}</p>
      } @else if (lib.data.status === 'private') {
        <p class="muted">🔒 Hidden by your Steam privacy settings. Set "Game details" to public in Steam, then reload.</p>
      } @else {
        <div class="toolbar">
          <input type="search" placeholder="Filter your games…" [value]="filter()" (input)="setFilter($any($event.target).value)" />
          <span class="muted">{{ filteredGames().length }} shown</span>
        </div>
        <div class="grid">
          @for (game of visibleGames(); track game.appId) {
            <button type="button" class="card tile" (click)="openAchievements(game)">
              <img [src]="game.headerUrl" [alt]="game.name" loading="lazy" />
              <div class="body">
                <h3>{{ game.name }}</h3>
                <p class="muted">
                  {{ hours(game.playtimeMinutes) }} h played
                  @if (game.lastPlayed) { · last {{ game.lastPlayed | date: 'mediumDate' }} }
                </p>
              </div>
            </button>
          }
        </div>
        @if (visibleGames().length < filteredGames().length) {
          <p class="pager"><button type="button" (click)="showMore()">Show more</button></p>
        }
      }

      <h2>Friends</h2>
      @let fr = friends();
      @if (fr.status === 'loading') {
        <p class="muted">Loading…</p>
      } @else if (fr.status === 'error') {
        <p class="error" role="alert">{{ fr.message }}</p>
      } @else if (fr.data.status === 'private') {
        <p class="muted">🔒 Hidden by your Steam privacy settings. Set "Friends list" to public in Steam, then reload.</p>
      } @else if (!fr.data.data?.length) {
        <p class="muted">No friends to show.</p>
      } @else {
        <div class="friends">
          @for (friend of fr.data.data; track friend.steamId) {
            <a class="card friend" [href]="friend.profileUrl" target="_blank" rel="noopener noreferrer">
              <img [src]="friend.avatarUrl" [alt]="friend.personaName" loading="lazy" />
              <div>
                <strong>{{ friend.personaName }}</strong><br />
                <span class="muted"><span class="dot" [class.online]="friend.status !== 'offline'"></span>{{ friend.status }}</span>
              </div>
            </a>
          }
        </div>
      }
    }

    @if (selectedGame(); as game) {
      <div class="overlay" (click)="closeAchievements()">
        <section class="card dialog" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
          <header>
            <h2 style="margin: 0">{{ game.name }}</h2>
            <button type="button" class="link" (click)="closeAchievements()">Close</button>
          </header>
          @let ach = achievements();
          @if (!ach || ach.status === 'loading') {
            <p class="muted">Loading achievements…</p>
          } @else if (ach.status === 'error') {
            <p class="error" role="alert">{{ ach.message }}</p>
          } @else if (ach.data.status === 'private') {
            <p class="muted">🔒 Hidden by your Steam privacy settings. Set "Game details" to public in Steam, then try again.</p>
          } @else if (ach.data.status === 'nodata' || !ach.data.data) {
            <p class="muted">This game has no achievements.</p>
          } @else {
            @let a = ach.data.data;
            <p>{{ a.unlocked }} / {{ a.total }} unlocked</p>
            <div class="bar"><span [style.width.%]="a.total ? (a.unlocked / a.total) * 100 : 0"></span></div>
            @for (item of a.achievements; track item.apiName) {
              <div class="achievement" [class.locked]="!item.achieved">
                <strong>{{ item.achieved ? '🏆' : '🔒' }} {{ item.name }}</strong>
                @if (item.description) { <span class="muted">{{ item.description }}</span> }
                @if (item.unlockedAt) { <span class="muted">Unlocked {{ item.unlockedAt | date: 'medium' }}</span> }
              </div>
            }
          }
        </section>
      </div>
    }
  `,
})
export class ProfileComponent {
  protected readonly auth = inject(AuthService);
  private readonly service = inject(ProfileService);

  /** Bound from the ?steam= query parameter after a failed or conflicting Steam link. */
  readonly steam = input<string>();

  // Without a linked Steam account the API answers 409, so we do not even ask.
  protected readonly linked = !!this.auth.user()?.steamId;

  protected readonly profile = this.sectionSignal(() => this.service.profile());
  protected readonly games = this.sectionSignal(() => this.service.games());
  protected readonly recentGames = this.sectionSignal(() => this.service.recentGames());
  protected readonly friends = this.sectionSignal(() => this.service.friends());

  protected readonly filter = signal('');
  private readonly shown = signal(PAGE_SIZE);
  protected readonly selectedGame = signal<SteamGame | null>(null);

  protected readonly library = computed(() => {
    const state = this.games();
    return state.status === 'ready' ? state.data.data : null;
  });

  protected readonly friendCount = computed(() => {
    const state = this.friends();
    return state.status === 'ready' && state.data.status === 'ok' ? (state.data.data?.length ?? 0) : null;
  });

  protected readonly filteredGames = computed(() => {
    const term = this.filter().trim().toLowerCase();
    const games = this.library()?.games ?? [];
    return term ? games.filter((game) => game.name.toLowerCase().includes(term)) : games;
  });

  protected readonly visibleGames = computed(() => this.filteredGames().slice(0, this.shown()));

  protected readonly achievements = toSignal(
    toObservable(this.selectedGame).pipe(
      switchMap((game) => (game ? toLoadable(this.service.achievements(game.appId)) : of(null))),
    ),
    { initialValue: null },
  );

  protected hours(minutes: number): number {
    return Math.round(minutes / 6) / 10;
  }

  protected setFilter(value: string): void {
    this.filter.set(value);
    this.shown.set(PAGE_SIZE);
  }

  protected showMore(): void {
    this.shown.update((count) => count + PAGE_SIZE);
  }

  protected openAchievements(game: SteamGame): void {
    this.selectedGame.set(game);
  }

  protected closeAchievements(): void {
    this.selectedGame.set(null);
  }

  private sectionSignal<T>(load: () => Observable<T>) {
    return toSignal(this.linked ? toLoadable(load()) : of<Loadable<T>>({ status: 'loading' }), {
      requireSync: true,
    });
  }
}
