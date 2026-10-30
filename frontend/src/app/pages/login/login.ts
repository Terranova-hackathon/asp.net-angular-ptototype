import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  imports: [FormsModule],
  selector: 'app-login',
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class Login {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  email = '';
  password = '';

  loading = signal(false);
  error = signal('');

  onSubmit(): void {
    if (this.loading()) return;

    this.loading.set(true);
    this.error.set('');

    this.authService.login({
      email: this.email,
      password: this.password
    }).subscribe({
      next: () => {
        this.loading.set(false);
        void this.router.navigate(['/games']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);

        if (err.status === 401) {
          this.error.set('Invalid email or password.');
        } else {
          this.error.set('Unable to log in. Please try again.');
        }
      }
    });
  }
}
