import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { TranslocoModule } from '@jsverse/transloco';
import { Subscription, filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AppLang, LanguageService } from '../../core/i18n/i18n';
import { ThemeService } from '../../core/services/theme.service';
import { AgencyService, LookupService, NotificationCenter } from '../../core/services/app-state.services';
import { ApiService } from '../../core/services/api.service';
import { AppNotification, CurrentUser } from '../../shared/models/models';
import { DtPipe } from '../../shared/pipes/format.pipes';
import { NotificationTextPipe } from '../../features/notifications/notification-text.pipe';
import { NAV, BreadcrumbService, Crumb } from '../shell.shared';
import { LearningService } from '../../core/services/learning.service';
import { TourService } from '../../core/services/tour.service';
import { TourOverlayComponent } from '../tour-overlay/tour-overlay';

@Component({
  selector: 'k2-shell',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatButtonModule, MatMenuModule, MatBadgeModule,
    MatTooltipModule, MatDividerModule, TranslocoModule, DtPipe, NotificationTextPipe, TourOverlayComponent],
  templateUrl: './shell.html',
  styleUrl: './shell.css'
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  readonly lang = inject(LanguageService);
  readonly theme = inject(ThemeService);
  readonly notifications = inject(NotificationCenter);
  readonly agency = inject(AgencyService);
  readonly learning = inject(LearningService);
  readonly tour = inject(TourService);
  private readonly lookups = inject(LookupService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly breadcrumb = inject(BreadcrumbService);
  private subs = new Subscription();

  readonly user = this.auth.user;
  readonly collapsed = signal(this.readCollapsed());
  readonly mobile = signal(false);
  readonly mobileOpen = signal(false);
  private readonly baseCrumbs = signal<Crumb[]>([]);
  readonly crumbs = computed(() => {
    const list = [...this.baseCrumbs()];
    const label = this.breadcrumb.label();
    if (label && list.length) list[list.length - 1] = { ...list[list.length - 1], label, raw: true };
    return list;
  });
  readonly nav = computed(() => NAV
    .map(g => ({ ...g, items: g.items.filter(i => !i.roles || this.auth.hasRole(...i.roles)) }))
    .filter(g => g.items.length));
  readonly initials = computed(() => (this.user()?.fullName ?? '?').split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase());

  ngOnInit(): void {
    this.notifications.start();
    this.agency.load();
    this.lookups.invalidate();
    // Idioma salvo no perfil do usuário prevalece no primeiro acesso deste navegador.
    const pref = this.user()?.preferredLanguage as AppLang | undefined;
    if (pref && pref !== this.lang.lang() && !localStorage.getItem('k2m.lang')) this.lang.set(pref);
    this.subs.add(this.breakpoints.observe('(max-width: 960px)').subscribe(r => {
      this.mobile.set(r.matches);
      if (!r.matches) this.mobileOpen.set(false);
    }));
    this.subs.add(this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.buildCrumbs()));
    this.buildCrumbs();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.notifications.stop();
  }

  toggleSidebar(): void {
    if (this.mobile()) { this.mobileOpen.update(v => !v); return; }
    this.collapsed.update(v => !v);
    try { localStorage.setItem('k2m.sidebar', this.collapsed() ? '1' : '0'); } catch { /* ignore */ }
  }

  setLang(lang: AppLang): void {
    this.lang.set(lang);
    this.api.put<CurrentUser>('/auth/me/preferences', { language: lang }).subscribe(u => this.auth.updateUser(u));
  }

  openNotification(n: AppNotification): void {
    this.notifications.markRead(n);
    if (n.link) this.router.navigateByUrl(n.link);
  }

  notifIcon(type: string): string {
    return ({ CampaignEnding: 'event_busy', BudgetThreshold: 'savings', TaskOverdue: 'alarm', NewLead: 'person_add', ReportAvailable: 'description' } as Record<string, string>)[type] ?? 'notifications';
  }

  private buildCrumbs(): void {
    this.breadcrumb.set(null);
    const crumbs: Crumb[] = [];
    let route: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    let url = '';
    while (route) {
      const seg = route.url.map(s => s.path).join('/');
      if (seg) url += '/' + seg;
      const crumb = route.data?.['crumb'] as string | undefined;
      if (crumb && route.routeConfig?.path !== '' && !crumbs.some(c => c.url === url)) crumbs.push({ label: crumb, url, raw: false });
      route = route.firstChild;
    }
    this.baseCrumbs.set(crumbs.filter(c => c.url !== '/dashboard'));
  }

  private readCollapsed(): boolean {
    try { return localStorage.getItem('k2m.sidebar') === '1'; } catch { return false; }
  }
}
