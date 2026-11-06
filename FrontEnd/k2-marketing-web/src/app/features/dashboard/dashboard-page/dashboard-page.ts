import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { AnalyticsFilter, PALETTE, PLATFORM_COLORS, SERIES } from '../../../shared/components/analytics.shared';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { FilterBarComponent } from '../../../shared/components/filter-bar/filter-bar';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { ApiService } from '../../../core/services/api.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { Dashboard } from '../../../shared/models/models';
import { MoneyPipe, NumPipe, PctPipe, RoasPipe, delta } from '../../../shared/pipes/format.pipes';
import { barConfig, dayLabels, doughnutConfig, lineConfig, moneyFmt, moneyTick } from '../../../shared/chart-helpers';

@Component({
  imports: [UI, KpiComponent, ChartComponent, FilterBarComponent],
  providers: [MoneyPipe, NumPipe, PctPipe, RoasPipe],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css'
})
export class DashboardPage {
  private readonly api = inject(ApiService);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly money = inject(MoneyPipe);
  readonly num = inject(NumPipe);
  readonly pct = inject(PctPipe);
  readonly roas = inject(RoasPipe);
  readonly data = signal<Dashboard | null>(null);
  readonly skeleton = Array.from({ length: 15 }, (_, i) => i);

  private tr(key: string): string { this.lang.lang(); return this.t.translate(key); }

  private readonly labels = computed(() => dayLabels(this.data()?.daily.map(p => p.date) ?? [], this.lang.lang()));

  readonly spendChart = computed(() => {
    const d = this.data()!;
    return barConfig(this.labels(), [{ label: this.tr('kpi.spend'), data: d.daily.map(p => p.spend), color: PALETTE.purple }],
      { plugins: { legend: { display: false } }, scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  readonly clicksChart = computed(() => {
    const d = this.data()!;
    return lineConfig(this.labels(), [{ label: this.tr('kpi.clicks'), data: d.daily.map(p => p.clicks), color: PALETTE.blue, fill: true }],
      { plugins: { legend: { display: false } } });
  });

  readonly conversionsChart = computed(() => {
    const d = this.data()!;
    return lineConfig(this.labels(), [
      { label: this.tr('kpi.conversions'), data: d.daily.map(p => p.conversions), color: PALETTE.green, fill: true },
      { label: this.tr('kpi.leads'), data: d.daily.map(p => p.leads), color: PALETTE.pink }
    ]);
  });

  readonly roasChart = computed(() => {
    const d = this.data()!;
    const rows = [...d.roasByCampaign].sort((a, b) => (b.roas ?? -1) - (a.roas ?? -1));
    return barConfig(rows.map(r => r.name.length > 28 ? r.name.slice(0, 27) + '…' : r.name),
      [{ label: 'ROAS', data: rows.map(r => r.roas), color: rows.map(r => (r.roas ?? 0) >= 3 ? PALETTE.green : (r.roas ?? 0) >= 1 ? PALETTE.orange : PALETTE.red) }],
      { plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true, ticks: { callback: (v: string | number) => `${v}x` } }, y: { grid: { display: false } } } }, true);
  });

  readonly platformChart = computed(() => {
    const d = this.data()!;
    return barConfig(d.byPlatform.map(p => this.tr('enums.platform.' + p.platform)), [
      { label: this.tr('kpi.spend'), data: d.byPlatform.map(p => p.metrics.spend), color: PALETTE.purple },
      { label: this.tr('kpi.revenue'), data: d.byPlatform.map(p => p.metrics.attributedRevenue), color: PALETTE.green }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  readonly budgetChart = computed(() => {
    const d = this.data()!;
    const lang = this.lang.lang();
    return doughnutConfig(d.budgetDistribution.map(b => this.tr('enums.platform.' + b.key)), d.budgetDistribution.map(b => b.planned),
      d.budgetDistribution.map((b, i) => PLATFORM_COLORS[b.key] ?? SERIES[i % SERIES.length]),
      { plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: c => `${c.label}: ${moneyFmt(lang, Number(c.raw))}` } } } });
  });

  load(f: AnalyticsFilter): void {
    this.api.get<Dashboard>('/dashboard', { ...f }).subscribe(d => this.data.set(d));
  }

  ch(cur: number | null, prev: number | null): number | null { return delta(cur, prev); }
  platformColor(p: string): string { return PLATFORM_COLORS[p] ?? PALETTE.gray; }
}
