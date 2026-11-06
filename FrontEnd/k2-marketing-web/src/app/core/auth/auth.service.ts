import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { AuthResponse, CurrentUser, UserRole } from '../../shared/models/models';
import { API_URL } from '../services/api.service';

const STORAGE_KEY = 'k2m.auth';

interface StoredAuth { accessToken: string; refreshToken: string; user: CurrentUser; }

/** Grupos de perfis usados para exibir/ocultar ações (o backend aplica as mesmas regras). */
export const ROLE_GROUPS = {
  admin: ['Admin'] as UserRole[],
  management: ['Admin', 'Manager'] as UserRole[],
  operations: ['Admin', 'Manager', 'Analyst'] as UserRole[]
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly state = signal<StoredAuth | null>(this.read());
  private refreshing$: Observable<string> | null = null;

  readonly user = computed(() => this.state()?.user ?? null);
  readonly isAuthenticated = computed(() => this.state() !== null);
  readonly role = computed(() => this.state()?.user.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly canManage = computed(() => ROLE_GROUPS.management.includes(this.role()!));
  readonly canOperate = computed(() => ROLE_GROUPS.operations.includes(this.role()!));
  readonly isClientViewer = computed(() => this.role() === 'ClientViewer');

  get accessToken(): string | null { return this.state()?.accessToken ?? null; }

  login(email: string, password: string): Observable<CurrentUser> {
    // Erros do login são exibidos no próprio formulário (sem toast).
    return this.http.post<AuthResponse>(`${API_URL}/auth/login`, { email, password }, { headers: { 'X-Silent': '1' } })
      .pipe(tap(r => this.store(r)), map(r => r.user));
  }

  /** Rotação do refresh token. Requisições simultâneas compartilham a mesma renovação. */
  refresh(): Observable<string> {
    if (!this.refreshing$) {
      const refreshToken = this.state()?.refreshToken ?? '';
      this.refreshing$ = this.http.post<AuthResponse>(`${API_URL}/auth/refresh`, { refreshToken }).pipe(
        tap(r => this.store(r)), map(r => r.accessToken), finalize(() => (this.refreshing$ = null)), shareReplay(1));
    }
    return this.refreshing$;
  }

  updateUser(user: CurrentUser): void {
    const s = this.state();
    if (s) this.persist({ ...s, user });
  }

  logout(redirect = true): void {
    const refreshToken = this.state()?.refreshToken;
    if (refreshToken) this.http.post(`${API_URL}/auth/logout`, { refreshToken }).subscribe({ error: () => undefined });
    this.persist(null);
    if (redirect) this.router.navigate(['/login']);
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.role();
    return !!role && (roles.length === 0 || roles.includes(role));
  }

  private store(r: AuthResponse): void {
    this.persist({ accessToken: r.accessToken, refreshToken: r.refreshToken, user: r.user });
  }

  private persist(value: StoredAuth | null): void {
    this.state.set(value);
    try {
      if (value) localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
      else localStorage.removeItem(STORAGE_KEY);
    } catch { /* armazenamento indisponível */ }
  }

  private read(): StoredAuth | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) as StoredAuth : null;
    } catch {
      return null;
    }
  }
}
