import { Injectable, signal } from '@angular/core';
import { UserRole } from '../shared/models/models';

export interface NavItem { key: string; icon: string; route: string; roles?: UserRole[]; }
export interface NavGroup { key: string; items: NavItem[]; }
export const OPS: UserRole[] = ['Admin', 'Manager', 'Analyst'];
export const NAV: NavGroup[] = [
  { key: 'nav.groups.overview', items: [{ key: 'nav.dashboard', icon: 'space_dashboard', route: '/dashboard' }] },
  {
    key: 'nav.groups.management', items: [
      { key: 'nav.clients', icon: 'apartment', route: '/clients' },
      { key: 'nav.campaigns', icon: 'campaign', route: '/campaigns' },
      { key: 'nav.ads', icon: 'ad_units', route: '/ads' },
      { key: 'nav.creatives', icon: 'photo_library', route: '/creatives' },
      { key: 'nav.budgets', icon: 'account_balance_wallet', route: '/budgets' }
    ]
  },
  {
    key: 'nav.groups.crm', items: [
      { key: 'nav.leads', icon: 'people_alt', route: '/leads' },
      { key: 'nav.conversions', icon: 'shopping_cart_checkout', route: '/conversions' }
    ]
  },
  {
    key: 'nav.groups.analytics', items: [
      { key: 'nav.metrics', icon: 'query_stats', route: '/metrics' },
      { key: 'nav.compare', icon: 'compare_arrows', route: '/compare' },
      { key: 'nav.reports', icon: 'summarize', route: '/reports' }
    ]
  },
  { key: 'nav.groups.operations', items: [{ key: 'nav.tasks', icon: 'event_note', route: '/tasks', roles: OPS }] },
  { key: 'nav.groups.learning', items: [{ key: 'nav.learn', icon: 'menu_book', route: '/learn' }] },
  {
    key: 'nav.groups.admin', items: [
      { key: 'nav.users', icon: 'manage_accounts', route: '/users', roles: ['Admin'] },
      { key: 'nav.settings', icon: 'settings', route: '/settings' }
    ]
  }
];
/** Rótulo dinâmico do último item do breadcrumb (ex.: nome do cliente na página de detalhes). */
@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  readonly label = signal<string | null>(null);
  set(label: string | null): void { this.label.set(label); }
}
export interface Crumb { label: string; url: string; raw: boolean; }
