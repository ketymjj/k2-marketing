import { Component, OnInit, computed, inject, signal, viewChildren } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { AgencyService } from '../../../core/services/app-state.services';
import { ToastService } from '../../../core/services/toast.service';
import { AnalyticsFilter, PALETTE } from '../../../shared/components/analytics.shared';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { FilterBarComponent } from '../../../shared/components/filter-bar/filter-bar';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { UsageBarComponent } from '../../../shared/components/usage-bar/usage-bar';
import { BudgetSummary, ConversionsReport, ExecutiveReport, LeadsReport, Metrics, Observation, PerformanceReport } from '../../../shared/models/models';
import { DtPipe, MoneyPipe, NumPipe, PctPipe, RoasPipe, delta } from '../../../shared/pipes/format.pipes';
import { barConfig, dayLabels, doughnutConfig, lineConfig, moneyTick } from '../../../shared/chart-helpers';
import { PdfSection, buildPdf } from '../pdf-report';
import { Tab, TABS } from '../reports.shared';

@Component({
  imports: [UI, ChartComponent, FilterBarComponent, KpiComponent, UsageBarComponent],
  providers: [MoneyPipe, NumPipe, PctPipe, RoasPipe, DtPipe],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.css'
})
export class ReportsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  private readonly toast = inject(ToastService);
  readonly agency = inject(AgencyService);
  readonly money = inject(MoneyPipe);
  readonly num = inject(NumPipe);
  readonly pct = inject(PctPipe);
  readonly roas = inject(RoasPipe);
  private readonly dt = inject(DtPipe);
  private readonly charts = viewChildren(ChartComponent);

  readonly tabs = TABS;
  readonly tab = signal<Tab>('executive');
  readonly tabIndex = computed(() => TABS.indexOf(this.tab()));
  readonly exec = signal<ExecutiveReport | null>(null);
  readonly perf = signal<PerformanceReport | null>(null);
  readonly budget = signal<BudgetSummary | null>(null);
  readonly leads = signal<LeadsReport | null>(null);
  readonly conv = signal<ConversionsReport | null>(null);
  readonly generating = signal(false);
  readonly filter = signal<AnalyticsFilter | null>(null);
  groupBy: 'client' | 'campaign' | 'platform' = 'campaign';
  initial: Partial<AnalyticsFilter> | null = null;

  readonly periodLabel = computed(() => {
    const f = this.filter();
    this.lang.lang();
    return f ? `${this.dt.transform(f.from)} – ${this.dt.transform(f.to)}` : '';
  });

  private tr(k: string): string { this.lang.lang(); return this.t.translate(k); }
  private yMoney = () => ({ scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });

  readonly execDaily = computed(() => {
    const e = this.exec()!;
    return lineConfig(dayLabels(e.daily.map(d => d.date), this.lang.lang()), [
      { label: this.tr('kpi.spend'), data: e.daily.map(d => d.spend), color: PALETTE.purple, fill: true },
      { label: this.tr('kpi.revenue'), data: e.daily.map(d => d.revenue), color: PALETTE.green }
    ], this.yMoney());
  });
  readonly execPlatform = computed(() => {
    const e = this.exec()!;
    return barConfig(e.byPlatform.map(p => this.tr('enums.platform.' + p.platform)), [
      { label: this.tr('kpi.spend'), data: e.byPlatform.map(p => p.metrics.spend), color: PALETTE.purple },
      { label: this.tr('kpi.revenue'), data: e.byPlatform.map(p => p.metrics.attributedRevenue), color: PALETTE.green }
    ], this.yMoney());
  });
  readonly perfChart = computed(() => {
    const p = this.perf()!;
    const rows = p.rows.slice(0, 15);
    const label = (l: string) => p.groupBy === 'platform' ? this.tr('enums.platform.' + l) : (l.length > 22 ? l.slice(0, 21) + '…' : l);
    return barConfig(rows.map(r => label(r.label)), [
      { label: this.tr('kpi.spend'), data: rows.map(r => r.metrics.spend), color: PALETTE.purple },
      { label: this.tr('kpi.revenue'), data: rows.map(r => r.metrics.attributedRevenue), color: PALETTE.green }
    ], this.yMoney());
  });
  readonly budgetChart = computed(() => {
    const items = [...this.budget()!.items].sort((a, b) => b.plannedAmount - a.plannedAmount).slice(0, 15);
    return barConfig(items.map(i => i.campaignName.length > 20 ? i.campaignName.slice(0, 19) + '…' : i.campaignName), [
      { label: this.tr('budgets.planned'), data: items.map(i => i.plannedAmount), color: PALETTE.gray + 'aa' },
      { label: this.tr('budgets.spent'), data: items.map(i => i.actualSpend), color: PALETTE.purple }
    ], this.yMoney());
  });
  readonly leadsStatusChart = computed(() => {
    const l = this.leads()!;
    const colors: Record<string, string> = { New: PALETTE.blue, Contacted: PALETTE.indigo, Qualified: PALETTE.orange, Converted: PALETTE.green, Lost: PALETTE.gray };
    return doughnutConfig(l.byStatus.map(s => this.tr('enums.leadStatus.' + s.key)), l.byStatus.map(s => s.count), l.byStatus.map(s => colors[s.key]));
  });
  readonly leadsDailyChart = computed(() => {
    const l = this.leads()!;
    return barConfig(dayLabels(l.daily.map(d => d.date), this.lang.lang()), [{ label: this.tr('nav.leads'), data: l.daily.map(d => d.count), color: PALETTE.pink }],
      { plugins: { legend: { display: false } }, scales: { x: { grid: { display: false } }, y: { ticks: { precision: 0 } } } });
  });
  readonly convDailyChart = computed(() => {
    const c = this.conv()!;
    return lineConfig(dayLabels(c.daily.map(d => d.date), this.lang.lang()), [
      { label: this.tr('nav.conversions'), data: c.daily.map(d => d.count), color: PALETTE.green, yAxisID: 'y' },
      { label: this.tr('conversions.revenue'), data: c.daily.map(d => d.revenue), color: PALETTE.purple, yAxisID: 'y1' }
    ], { scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { precision: 0 } }, y1: { position: 'right', beginAtZero: true, grid: { display: false }, ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });
  readonly convTypeChart = computed(() => {
    const c = this.conv()!;
    return doughnutConfig(c.byType.map(x => this.tr('enums.conversionType.' + x.key)), c.byType.map(x => x.count),
      [PALETTE.green, PALETTE.purple, PALETTE.blue, PALETTE.orange, PALETTE.pink, PALETTE.gray]);
  });

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const tab = qp.get('tab') as Tab | null;
    if (tab && TABS.includes(tab)) this.tab.set(tab);
    const clientId = qp.get('clientId');
    const from = qp.get('from');
    const to = qp.get('to');
    if (clientId || (from && to)) this.initial = { clientId, from, to };
  }

  onFilter(f: AnalyticsFilter): void {
    this.filter.set(f);
    this.load();
  }

  setTab(i: number): void {
    this.tab.set(TABS[i]);
    this.load();
  }

  load(): void {
    const f = this.filter();
    if (!f) return;
    const params = { ...f };
    switch (this.tab()) {
      case 'executive': this.exec.set(null); this.api.get<ExecutiveReport>('/reports/executive', params).subscribe(r => this.exec.set(r)); break;
      case 'performance': this.perf.set(null); this.api.get<PerformanceReport>('/reports/performance', { ...params, groupBy: this.groupBy }).subscribe(r => this.perf.set(r)); break;
      case 'budget': this.budget.set(null); this.api.get<BudgetSummary>('/reports/budget', params).subscribe(r => this.budget.set(r)); break;
      case 'leads': this.leads.set(null); this.api.get<LeadsReport>('/reports/leads', params).subscribe(r => this.leads.set(r)); break;
      case 'conversions': this.conv.set(null); this.api.get<ConversionsReport>('/reports/conversions', params).subscribe(r => this.conv.set(r)); break;
    }
  }

  ch(cur: number | null, prev: number | null): number | null { return delta(cur, prev); }

  /** Observação traduzida com parâmetros formatados no idioma ativo. */
  obs(o: Observation): string {
    const p: Record<string, unknown> = { ...o.params };
    for (const k of ['cpa', 'avg', 'spend']) if (typeof p[k] === 'number') p[k] = this.money.transform(p[k] as number);
    if (typeof p['roas'] === 'number') p['roas'] = this.roas.transform(p['roas'] as number);
    if (typeof p['ctr'] === 'number') p['ctr'] = this.pct.transform(p['ctr'] as number);
    if (typeof p['pct'] === 'number') p['pct'] = this.num.transform(p['pct'] as number, 1);
    return this.t.translate(`reports.obs.${o.code}`, p);
  }

  csv(): void {
    const f = this.filter();
    if (!f) return;
    this.api.download(`/reports/${this.tab()}/export`, { ...f, groupBy: this.groupBy }, `relatorio-${this.tab()}.csv`).subscribe();
  }

  print(): void { window.print(); }

  async pdf(): Promise<void> {
    const f = this.filter();
    const brand = this.exec()?.agency ?? { agencyName: this.agency.settings()?.agencyName ?? 'K2 Digital', primaryColor: this.agency.settings()?.primaryColor ?? '#6941C6',
      contactEmail: null, website: null, reportFooter: this.agency.settings()?.reportFooter ?? null };
    const images = this.charts().map(c => c.toImage()).filter((x): x is string => !!x);
    const sections = this.pdfSections(images);
    if (!sections) { this.toast.info('reports.waitData'); return; }
    this.generating.set(true);
    try {
      await buildPdf({
        title: this.tr('reports.' + this.tab()),
        subtitle: `${this.exec()?.clientName && this.tab() === 'executive' ? this.exec()!.clientName + ' · ' : ''}${this.periodLabel()}`,
        generatedLabel: `${this.tr('reports.generatedAt')} ${this.dt.transform(new Date().toISOString(), 'datetime')}`,
        agency: brand, sections, landscape: this.tab() === 'performance',
        fileName: `k2-${this.tab()}-${f?.from ?? ''}-${f?.to ?? ''}.pdf`
      });
    } finally {
      this.generating.set(false);
    }
  }

  private metricsRow(label: string, m: Metrics): string[] {
    return [label, this.money.transform(m.spend), this.num.transform(m.impressions), this.num.transform(m.clicks), this.pct.transform(m.ctr), this.money.transform(m.cpc),
      this.num.transform(m.conversions), this.money.transform(m.cpa), this.money.transform(m.attributedRevenue), this.roas.transform(m.roas)];
  }

  private metricsHead(): string[] {
    return ['reports.item', 'kpi.spend', 'kpi.impressions', 'kpi.clicks', 'kpi.ctr', 'kpi.cpc', 'kpi.conversions', 'kpi.cpa', 'kpi.revenue', 'kpi.roas'].map(k => this.tr(k));
  }

  private pdfSections(images: string[]): PdfSection[] | null {
    const k = (labelKey: string, value: string, sub?: string) => ({ label: this.tr(labelKey), value, sub });
    const deltaText = (c: number | null, p: number | null) => { const d = delta(c, p); return d === null ? undefined : `${d >= 0 ? '+' : ''}${this.num.transform(d, 1)}% ${this.tr('dashboard.vsPrevious')}`; };
    switch (this.tab()) {
      case 'executive': {
        const e = this.exec(); if (!e) return null;
        return [
          { kpis: [k('kpi.spend', this.money.transform(e.metrics.spend), deltaText(e.metrics.spend, e.previous.spend)),
            k('kpi.impressions', this.num.transform(e.metrics.impressions), deltaText(e.metrics.impressions, e.previous.impressions)),
            k('kpi.clicks', this.num.transform(e.metrics.clicks), deltaText(e.metrics.clicks, e.previous.clicks)),
            k('kpi.ctr', this.pct.transform(e.metrics.ctr)), k('kpi.leads', this.num.transform(e.metrics.leads), deltaText(e.metrics.leads, e.previous.leads)),
            k('kpi.conversions', this.num.transform(e.metrics.conversions), deltaText(e.metrics.conversions, e.previous.conversions)),
            k('kpi.cpa', this.money.transform(e.metrics.cpa)), k('kpi.roas', this.roas.transform(e.metrics.roas), deltaText(e.metrics.roas, e.previous.roas))] },
          { title: this.tr('reports.observations'), bullets: e.observations.map(o => ({ text: this.obs(o), tone: o.tone })) },
          { title: this.tr('reports.charts'), images: images.slice(0, 2).map((d, i) => ({ data: d, caption: this.tr(i === 0 ? 'dashboard.charts.spend' : 'dashboard.charts.byPlatform') })) },
          { title: this.tr('reports.topCampaigns'), table: { head: this.metricsHead(), body: e.topCampaigns.map(r => this.metricsRow(r.label, r.metrics)), foot: this.metricsRow(this.tr('common.total'), e.metrics) } },
          { title: this.tr('reports.budget'), kpis: [k('budgets.planned', this.money.transform(e.budget.planned)), k('budgets.spent', this.money.transform(e.budget.spent)),
            k('budgets.usage', this.pct.transform(e.budget.usagePercent, 1)), k('budgets.over90', String(e.budget.over90))] },
          { title: this.tr('dashboard.crm.title'), kpis: [k('dashboard.crm.leads', this.num.transform(e.crm.leadsRegistered)), k('dashboard.crm.uniqueConverted', this.num.transform(e.crm.uniqueLeadsConverted)),
            k('dashboard.crm.sales', this.num.transform(e.crm.sales)), k('dashboard.crm.salesRevenue', this.money.transform(e.crm.salesRevenue))] },
          { text: this.tr('reports.methodNote') }
        ];
      }
      case 'performance': {
        const p = this.perf(); if (!p) return null;
        const label = (l: string) => p.groupBy === 'platform' ? this.tr('enums.platform.' + l) : l;
        return [
          { images: images.slice(0, 1).map(d => ({ data: d, caption: this.tr('reports.spendVsRevenue') })) },
          { table: { head: this.metricsHead(), body: p.rows.map(r => this.metricsRow(label(r.label), r.metrics)), foot: this.metricsRow(this.tr('common.total'), p.totals) } }
        ];
      }
      case 'budget': {
        const b = this.budget(); if (!b) return null;
        return [
          { kpis: [k('budgets.planned', this.money.transform(b.planned)), k('budgets.spent', this.money.transform(b.spent)), k('budgets.remaining', this.money.transform(b.remaining)), k('budgets.usage', this.pct.transform(b.usagePercent, 1))] },
          { images: images.slice(0, 1).map(d => ({ data: d, caption: this.tr('budgets.plannedVsActual') })) },
          { table: { head: ['filters.campaign', 'filters.client', 'campaigns.period', 'budgets.planned', 'budgets.spent', 'budgets.remaining', 'budgets.usage'].map(x => this.tr(x)),
            body: b.items.map(i => [i.campaignName, i.clientName, `${this.dt.transform(i.startDate)} – ${this.dt.transform(i.endDate)}`, this.money.transform(i.plannedAmount),
              this.money.transform(i.actualSpend), this.money.transform(i.remaining), this.pct.transform(i.usagePercent, 1)]) } }
        ];
      }
      case 'leads': {
        const l = this.leads(); if (!l) return null;
        return [
          { kpis: [k('reports.leadsRegistered', this.num.transform(l.total)), k('reports.leadsConverted', this.num.transform(l.converted)), k('reports.conversionRate', this.pct.transform(l.conversionRate, 1)), k('reports.platformLeads', this.num.transform(l.platformLeads))] },
          { images: images.slice(0, 2).map((d, i) => ({ data: d, caption: this.tr(i === 0 ? 'reports.leadsByStatus' : 'reports.leadsPerDay') })) },
          { title: this.tr('reports.bySource'), table: { head: [this.tr('leads.source'), this.tr('nav.leads'), this.tr('reports.converted')], body: l.bySource.map(s => [s.key, s.count, s.converted]) } },
          { title: this.tr('reports.byCampaign'), table: { head: [this.tr('filters.campaign'), this.tr('nav.leads'), this.tr('reports.converted')], body: l.byCampaign.map(s => [s.label ?? this.tr('reports.noCampaign'), s.count, s.converted]) } }
        ];
      }
      case 'conversions': {
        const c = this.conv(); if (!c) return null;
        return [
          { kpis: [k('conversions.total', this.num.transform(c.total)), k('conversions.uniqueLeads', this.num.transform(c.uniqueLeads)), k('conversions.sales', this.num.transform(c.sales)), k('conversions.salesRevenue', this.money.transform(c.salesRevenue))] },
          { images: images.slice(0, 2).map((d, i) => ({ data: d, caption: this.tr(i === 0 ? 'reports.conversionsPerDay' : 'reports.byType') })) },
          { title: this.tr('reports.byCampaign'), table: { head: [this.tr('filters.campaign'), this.tr('nav.conversions'), this.tr('conversions.sales')], body: c.byCampaign.map(s => [s.label ?? '', s.count, s.converted]) } }
        ];
      }
    }
  }
}
