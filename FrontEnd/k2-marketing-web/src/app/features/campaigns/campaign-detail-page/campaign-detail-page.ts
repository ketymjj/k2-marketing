import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { BreadcrumbService } from '../../../layout/shell.shared';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { PALETTE } from '../../../shared/components/analytics.shared';
import { UsageBarComponent } from '../../../shared/components/usage-bar/usage-bar';
import { Budget, Campaign, CampaignPerformance, CampaignStatus, Paged } from '../../../shared/models/models';
import { MoneyPipe, NumPipe, PctPipe, RoasPipe } from '../../../shared/pipes/format.pipes';
import { dayLabels, lineConfig, moneyTick } from '../../../shared/chart-helpers';
import { confirm } from '../../../shared/paged-list';
import { STATUS_ACTION, changeCampaignStatus, openCampaignForm } from '../campaign-shared';
import { openAdForm } from '../../ads/ad-shared';
import { openBudgetForm } from '../../budgets/budget-shared';

@Component({
  imports: [UI, KpiComponent, ChartComponent, UsageBarComponent],
  providers: [MoneyPipe, NumPipe, PctPipe, RoasPipe],
  templateUrl: './campaign-detail-page.html',
  styleUrl: './campaign-detail-page.css'
})
export class CampaignDetailPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly breadcrumb = inject(BreadcrumbService);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly auth = inject(AuthService);
  readonly money = inject(MoneyPipe);
  readonly num = inject(NumPipe);
  readonly pct = inject(PctPipe);
  readonly roas = inject(RoasPipe);

  readonly c = signal<Campaign | null>(null);
  readonly p = signal<CampaignPerformance | null>(null);
  readonly budgets = signal<Budget[]>([]);
  from: Date | null = null;
  to: Date | null = null;

  private readonly labels = computed(() => dayLabels(this.p()?.daily.map(d => d.date) ?? [], this.lang.lang()));
  readonly spendChart = computed(() => {
    const p = this.p()!;
    this.lang.lang();
    return lineConfig(this.labels(), [
      { label: this.t.translate('kpi.spend'), data: p.daily.map(d => d.spend), color: PALETTE.purple, fill: true },
      { label: this.t.translate('kpi.revenue'), data: p.daily.map(d => d.revenue), color: PALETTE.green }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });
  readonly clicksChart = computed(() => {
    const p = this.p()!;
    this.lang.lang();
    return lineConfig(this.labels(), [
      { label: this.t.translate('kpi.clicks'), data: p.daily.map(d => d.clicks), color: PALETTE.blue, yAxisID: 'y' },
      { label: this.t.translate('kpi.conversions'), data: p.daily.map(d => d.conversions), color: PALETTE.green, yAxisID: 'y1' }
    ], { scales: { x: { grid: { display: false } }, y: { position: 'left', beginAtZero: true }, y1: { position: 'right', beginAtZero: true, grid: { display: false } } } });
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<Campaign>(`/campaigns/${this.id()}`).subscribe(c => { this.c.set(c); this.breadcrumb.set(c.name); });
    this.loadPerformance();
    this.api.get<Paged<Budget>>('/budgets', { campaignId: this.id(), pageSize: 50 }).subscribe(r => this.budgets.set(r.items));
  }

  loadPerformance(): void {
    this.api.get<CampaignPerformance>(`/campaigns/${this.id()}/performance`, { from: isoDate(this.from), to: isoDate(this.to) }).subscribe(p => this.p.set(p));
  }

  action(s: CampaignStatus) { return STATUS_ACTION[s]; }

  setStatus(s: CampaignStatus): void {
    changeCampaignStatus(this.dialog, this.api, this.toast, this.c()!, s).subscribe(c => this.c.set(c));
  }

  edit(): void { openCampaignForm(this.dialog, { id: this.id() }).subscribe(c => c && this.load()); }

  duplicate(): void {
    confirm(this.dialog, { title: 'common.duplicate', message: 'campaigns.confirmDuplicate', params: { name: this.c()!.name } })
      .subscribe(() => this.api.post<Campaign>(`/campaigns/${this.id()}/duplicate`).subscribe(copy => {
        this.toast.success('campaigns.duplicated');
        this.router.navigate(['/campaigns', copy.id]).then(() => this.load());
      }));
  }

  newAd(): void { openAdForm(this.dialog, { campaignId: this.id() }).subscribe(a => a && this.load()); }
  newBudget(): void { openBudgetForm(this.dialog, { campaignId: this.id() }).subscribe(b => b && this.load()); }
}
