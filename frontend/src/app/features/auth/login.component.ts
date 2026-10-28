import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiErrorMessage } from '../../core/api-error';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Log in</h1>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Email
          <input type="email" formControlName="email" autocomplete="email" />
        </label>
        <label>
          Password
          <input type="password" formControlName="password" autocomplete="current-password" />
        </label>
        @if (error() ?? steamError()) {
          <p class="error" role="alert">{{ error() ?? steamError() }}</p>
        }
        <button type="submit" [disabled]="form.invalid || submitting()">
          {{ submitting() ? 'Signing in…' : 'Log in' }}
        </button>
      </form>
      <p class="divider muted">or</p>
      <a class="button steam" [href]="auth.steamLoginUrl">Sign in with Steam</a>
      <p class="muted">No account? <a routerLink="/register">Register</a></p>
    </section>
  `,
})
export class LoginComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);

  /** Set when the Steam sign-in redirects back here with ?steam=failed. */
  protected readonly steamError = computed(() =>
    this.route.snapshot.queryParamMap.get('steam') === 'failed'
      ? 'Steam sign-in could not be verified. Please try again.'
      : null,
  );

  protected submit(): void {
    if (this.form.invalid) return;

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        // Only follow in-app paths so a crafted link cannot redirect users to another site.
        this.router.navigateByUrl(returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/games');
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not log in.'));
        this.submitting.set(false);
      },
    });
  }
}
