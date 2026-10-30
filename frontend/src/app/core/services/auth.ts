import { Service, Inject, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { LoginRequest, User } from '../models/auth.model';
import { Observable } from 'rxjs';

@Service()
export class AuthService {
	private readonly http = inject(HttpClient);

	login(credentials: LoginRequest): Observable<User> {
		return this.http.post<User>(
			'/api/auth/login',
			credentials
		);
	}

	getCurrentUser(): Observable<User> {
		return this.http.get<User>('/api/auth/me');
	}

	logout(): Observable<void> {
		return this.http.post<void>('/api/auth/logout', {});
	}

}
