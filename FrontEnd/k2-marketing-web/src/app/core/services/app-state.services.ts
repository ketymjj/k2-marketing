import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subscription, interval, shareReplay, startWith, switchMap, catchError, of } from 'rxjs';
import { ApiService } from './api.service';
import { AgencySettings, AppNotification, CampaignLookup, ClientLookup, Paged, UserLookup } from '../../shared/models/models';

/** Central de notificações do header: contador atualizado a cada minuto + lista sob demanda. */
@Injectable({ providedIn: 'root' })
export class NotificationCenter {
  private readonly api = inject(ApiService);
  private sub?: Subscription;
  readonly unread = signal(0);
  readonly recent = signal<AppNotification[]>([]);

  start(): void {
    this.sub?.unsubscribe();
    this.sub = interval(60_000).pipe(
      startWith(0),
      switchMap(() => this.api.get<{ count: number }>('/notifications/unread-count').pipe(catchError(() => of({ count: this.unread() }))))
    ).subscribe(r => this.unread.set(r.count));
  }

  stop(): void {
    this.sub?.unsubscribe();
    this.unread.set(0);
    this.recent.set([]);
  }

  loadRecent(): void {
    this.api.get<Paged<AppNotification>>('/notifications', { pageSize: 8 }).subscribe(r => this.recent.set(r.items));
  }

  markRead(n: AppNotification): void {
    if (n.readAt) return;
    this.api.post(`/notifications/${n.id}/read`).subscribe(() => {
      this.unread.update(c => Math.max(0, c - 1));
      this.recent.update(list => list.map(x => x.id === n.id ? { ...x, readAt: new Date().toISOString() } : x));
    });
  }

  markAllRead(): Observable<void> {
    const r = this.api.post<void>('/notifications/read-all');
    r.subscribe(() => {
      this.unread.set(0);
      this.recent.update(list => list.map(x => ({ ...x, readAt: x.readAt ?? new Date().toISOString() })));
    });
    return r;
  }
}

/** Identidade visual da agência (nome e cor usados no cabeçalho e nos relatórios). */
@Injectable({ providedIn: 'root' })
export class AgencyService {
  private readonly api = inject(ApiService);
  readonly settings = signal<AgencySettings | null>(null);

  load(): void {
    this.api.get<AgencySettings>('/settings/agency').subscribe(s => this.settings.set(s));
  }
}

/** Listas de seleção (clientes, campanhas, responsáveis) com cache curto, invalidado após alterações. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly api = inject(ApiService);
  private clients$?: Observable<ClientLookup[]>;
  private campaigns = new Map<string, Observable<CampaignLookup[]>>();
  private users$?: Observable<UserLookup[]>;

  clients(): Observable<ClientLookup[]> {
    return this.clients$ ??= this.api.get<ClientLookup[]>('/clients/lookup').pipe(shareReplay(1));
  }

  campaignsFor(clientId?: string | null): Observable<CampaignLookup[]> {
    const key = clientId ?? '*';
    if (!this.campaigns.has(key)) this.campaigns.set(key, this.api.get<CampaignLookup[]>('/campaigns/lookup', { clientId }).pipe(shareReplay(1)));
    return this.campaigns.get(key)!;
  }

  assignableUsers(): Observable<UserLookup[]> {
    return this.users$ ??= this.api.get<UserLookup[]>('/users/assignable').pipe(shareReplay(1));
  }

  invalidate(): void {
    this.clients$ = undefined;
    this.campaigns.clear();
    this.users$ = undefined;
  }
}
