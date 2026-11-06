import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards/guards';
import { ShellComponent } from './layout/shell/shell';

const OPS = ['Admin', 'Manager', 'Analyst'];

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login-page/login-page').then(m => m.LoginPage) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', data: { crumb: 'nav.dashboard' }, loadComponent: () => import('./features/dashboard/dashboard-page/dashboard-page').then(m => m.DashboardPage) },
      {
        path: 'clients', data: { crumb: 'nav.clients' }, children: [
          { path: '', loadComponent: () => import('./features/clients/clients-page/clients-page').then(m => m.ClientsPage) },
          { path: ':id', data: { crumb: 'clients.detail' }, loadComponent: () => import('./features/clients/client-detail-page/client-detail-page').then(m => m.ClientDetailPage) }
        ]
      },
      {
        path: 'campaigns', data: { crumb: 'nav.campaigns' }, children: [
          { path: '', loadComponent: () => import('./features/campaigns/campaigns-page/campaigns-page').then(m => m.CampaignsPage) },
          { path: ':id', data: { crumb: 'campaigns.detail' }, loadComponent: () => import('./features/campaigns/campaign-detail-page/campaign-detail-page').then(m => m.CampaignDetailPage) }
        ]
      },
      { path: 'ads', data: { crumb: 'nav.ads' }, loadComponent: () => import('./features/ads/ads-page/ads-page').then(m => m.AdsPage) },
      { path: 'creatives', data: { crumb: 'nav.creatives' }, loadComponent: () => import('./features/creatives/creatives-page/creatives-page').then(m => m.CreativesPage) },
      { path: 'budgets', data: { crumb: 'nav.budgets' }, loadComponent: () => import('./features/budgets/budgets-page/budgets-page').then(m => m.BudgetsPage) },
      { path: 'leads', data: { crumb: 'nav.leads' }, loadComponent: () => import('./features/leads/leads-page/leads-page').then(m => m.LeadsPage) },
      { path: 'conversions', data: { crumb: 'nav.conversions' }, loadComponent: () => import('./features/conversions/conversions-page/conversions-page').then(m => m.ConversionsPage) },
      {
        path: 'metrics', data: { crumb: 'nav.metrics' }, children: [
          { path: '', loadComponent: () => import('./features/metrics/metrics-page/metrics-page').then(m => m.MetricsPage) },
          { path: 'import', canActivate: [roleGuard], data: { crumb: 'imports.title', roles: OPS }, loadComponent: () => import('./features/metrics/import-page/import-page').then(m => m.ImportPage) }
        ]
      },
      { path: 'compare', data: { crumb: 'nav.compare' }, loadComponent: () => import('./features/compare/compare-page/compare-page').then(m => m.ComparePage) },
      { path: 'reports', data: { crumb: 'nav.reports' }, loadComponent: () => import('./features/reports/reports-page/reports-page').then(m => m.ReportsPage) },
      { path: 'tasks', canActivate: [roleGuard], data: { crumb: 'nav.tasks', roles: OPS }, loadComponent: () => import('./features/tasks/tasks-page/tasks-page').then(m => m.TasksPage) },
      { path: 'users', canActivate: [roleGuard], data: { crumb: 'nav.users', roles: ['Admin'] }, loadComponent: () => import('./features/users/users-page/users-page').then(m => m.UsersPage) },
      { path: 'notifications', data: { crumb: 'notifications.title' }, loadComponent: () => import('./features/notifications/notifications-page/notifications-page').then(m => m.NotificationsPage) },
      { path: 'settings', data: { crumb: 'nav.settings' }, loadComponent: () => import('./features/settings/settings-page/settings-page').then(m => m.SettingsPage) },
      {
        path: 'learn', data: { crumb: 'nav.learn' }, children: [
          { path: '', loadComponent: () => import('./features/learning/learning-center-page/learning-center-page').then(m => m.LearningCenterPage) },
          { path: 'examples', data: { crumb: 'learn.nav.examples' }, loadComponent: () => import('./features/learning/examples-page/examples-page').then(m => m.ExamplesPage) },
          { path: 'simulator', data: { crumb: 'learn.nav.simulator' }, loadComponent: () => import('./features/learning/simulator-page/simulator-page').then(m => m.SimulatorPage) },
          { path: 'glossary', data: { crumb: 'learn.nav.glossary' }, loadComponent: () => import('./features/learning/glossary-page/glossary-page').then(m => m.GlossaryPage) }
        ]
      },
      { path: 'methodology', data: { crumb: 'methodology.title' }, loadComponent: () => import('./features/settings/methodology-page/methodology-page').then(m => m.MethodologyPage) },
      { path: 'forbidden', loadComponent: () => import('./features/misc/forbidden-page/forbidden-page').then(m => m.ForbiddenPage) },
      { path: '**', loadComponent: () => import('./features/misc/not-found-page/not-found-page').then(m => m.NotFoundPage) }
    ]
  }
];
