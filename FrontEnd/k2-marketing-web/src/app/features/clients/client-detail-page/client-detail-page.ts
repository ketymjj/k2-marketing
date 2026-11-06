import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { ApiService, addDays, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { BreadcrumbService } from '../../../layout/shell.shared';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { PALETTE } from '../../../shared/components/analytics.shared';
import { UsageBarComponent } from '../../../shared/components/usage-bar/usage-bar';
import { Advertisement, CampaignListItem, ClientOverview, Dashboard, HistoryItem, Lead, Paged, PerformanceReport } from '../../../shared/models/models';
import { MoneyPipe, NumPipe, PctPipe, RoasPipe } from '../../../shared/pipes/format.pipes';
import { dayLabels, lineConfig, moneyTick } from '../../../shared/chart-helpers';
import { ClientFormDialog } from '../client-form-dialog/client-form-dialog';

@Component({
  imports: [UI, KpiComponent, ChartComponent, UsageBarComponent],
  providers: [MoneyPipe, NumPipe, PctPipe, RoasPipe],
  templateUrl: './client-detail-page.html',
  styleUrl: './client-detail-page.css'
})
export class ClientDetailPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly breadcrumb = inject(BreadcrumbService);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly auth = inject(AuthService);
  readonly money = inject(MoneyPipe);
  readonly num = inject(NumPipe);
  readonly pct = inject(PctPipe);
  readonly roas = inject(RoasPipe);

  readonly o = signal<ClientOverview | null>(null);
  readonly trend = signal<Dashboard | null>(null);
  readonly campaigns = signal<CampaignListItem[]>([]);
  readonly ads = signal<Advertisement[]>([]);
  readonly leads = signal<Lead[]>([]);
  readonly results = signal<PerformanceReport | null>(null);
  readonly history = signal<HistoryItem[]>([]);
  private loadedTabs = new Set<number>();

  readonly trendChart = computed(() => {
    const d = this.trend()!;
    this.lang.lang();
    return lineConfig(dayLabels(d.daily.map(p => p.date), this.lang.lang()), [
      { label: this.t.translate('kpi.spend'), data: d.daily.map(p => p.spend), color: PALETTE.purple, fill: true },
      { label: this.t.translate('kpi.revenue'), data: d.daily.map(p => p.revenue), color: PALETTE.green }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<ClientOverview>(`/clients/${this.id()}/overview`).subscribe(o => { this.o.set(o); this.breadcrumb.set(o.client.name); });
    this.api.get<Dashboard>('/dashboard', { clientId: this.id() }).subscribe(d => this.trend.set(d));
  }

  onTab(i: number): void {
    if (this.loadedTabs.has(i)) return;
    this.loadedTabs.add(i);
    const clientId = this.id();
    switch (i) {
      case 1: this.api.get<Paged<CampaignListItem>>('/campaigns', { clientId, pageSize: 100 }).subscribe(r => this.campaigns.set(r.items)); break;
      case 2: this.api.get<Paged<Advertisement>>('/advertisements', { clientId, pageSize: 12 }).subscribe(r => this.ads.set(r.items)); break;
      case 3: this.api.get<Paged<Lead>>('/leads', { clientId, pageSize: 20 }).subscribe(r => this.leads.set(r.items)); break;
      case 4: this.api.get<PerformanceReport>('/reports/performance', { clientId, groupBy: 'campaign', from: isoDate(addDays(new Date(), -89)), to: isoDate(new Date()) })
        .subscribe(r => this.results.set(r)); break;
      case 6: this.api.get<HistoryItem[]>(`/clients/${clientId}/history`).subscribe(h => this.history.set(h)); break;
    }
  }

  edit(): void {
    this.dialog.open(ClientFormDialog, { data: { id: this.id() }, width: '720px', maxWidth: '95vw' }).afterClosed().subscribe(s => s && this.load());
  }
}
