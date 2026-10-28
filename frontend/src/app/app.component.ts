import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="nav">
      <a routerLink="/" class="brand">🎮 GameHub</a>
      <nav>
        @if (auth.user(); as user) {
          <a routerLink="/games" routerLinkActive="active">Games</a>
          <a routerLink="/profile" routerLinkActive="active">Profile</a>
          <span class="muted">{{ user.displayName || user.email }}</span>
          <button type="button" class="link" (click)="auth.logout()">Log out</button>
        } @else {
          <a routerLink="/login" routerLinkActive="active">Log in</a>
          <a routerLink="/register" routerLinkActive="active">Register</a>
        }
      </nav>
    </header>
    <main class="container">
      <router-outlet />
    </main>
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
}
