import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { LookupService } from '../../../core/services/app-state.services';
import { LanguageService } from '../../../core/i18n/i18n';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { SERIES } from '../../../shared/components/analytics.shared';
import { CampaignLookup, CompareItem, CompareResult, Metrics } from '../../../shared/models/models';
import { barConfig, dayLabels, lineConfig, moneyTick } from '../../../shared/chart-helpers';
import { Row } from '../compare.shared';

@Component({
  imports: [UI, ChartComponent],
  templateUrl: './compare-page.html',
  styleUrl: './compare-page.css'
})
export class ComparePage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly r = signal<CompareResult | null>(null);
  selected: string[] = [];
  from: Date | null = null;
  to: Date | null = null;
  readonly rows: Row[] = [
    { key: 'spend', fmt: 'money' }, { key: 'impressions', fmt: 'num' }, { key: 'clicks', fmt: 'num' }, { key: 'ctr', fmt: 'pct' },
    { key: 'cpc', fmt: 'money' }, { key: 'leads', fmt: 'num' }, { key: 'conversions', fmt: 'num' }, { key: 'cpa', fmt: 'money' },
    { key: 'attributedRevenue', fmt: 'money' }, { key: 'roas', fmt: 'roas' }
  ];

  private names = computed(() => this.r()?.items.map(i => i.name.length > 24 ? i.name.slice(0, 23) + '…' : i.name) ?? []);
  readonly spendChart = computed(() => {
    const it = this.r()!.items; this.lang.lang();
    return barConfig(this.names(), [
      { label: this.t.translate('kpi.spend'), data: it.map(i => i.metrics.spend), color: '#6941C6' },
      { label: this.t.translate('kpi.revenue'), data: it.map(i => i.metrics.attributedRevenue), color: '#12B76A' }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });
  readonly effChart = computed(() => {
    const it = this.r()!.items; this.lang.lang();
    return barConfig(this.names(), [
      { label: 'CPC', data: it.map(i => i.metrics.cpc), color: '#2E90FA' },
      { label: 'CPA', data: it.map(i => i.metrics.cpa), color: '#F79009' }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });
  readonly roasChart = computed(() => {
    const it = this.r()!.items;
    return barConfig(this.names(), [{ label: 'ROAS', data: it.map(i => i.metrics.roas), color: it.map((_, i) => this.color(i)) }],
      { plugins: { legend: { display: false } }, scales: { x: { grid: { display: false } }, y: { ticks: { callback: (v: string | number) => `${v}x` } } } });
  });
  readonly dailyChart = computed(() => {
    const it = this.r()!.items;
    return lineConfig(dayLabels(it[0]?.daily.map(d => d.date) ?? [], this.lang.lang()),
      it.map((i, idx) => ({ label: i.name, data: i.daily.map(d => d.spend), color: this.color(idx) })),
      { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  ngOnInit(): void {
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    const ids = this.route.snapshot.queryParamMap.get('ids');
    if (ids) this.selected = ids.split(',').slice(0, 4);
    if (this.selected.length >= 2) this.run();
  }

  limit(): void { if (this.selected.length > 4) this.selected = this.selected.slice(0, 4); }

  run(): void {
    this.api.post<CompareResult>('/campaigns/compare', { ids: this.selected, from: isoDate(this.from), to: isoDate(this.to) }).subscribe(r => this.r.set(r));
  }

  color(i: number): string { return SERIES[i % SERIES.length]; }
  label(k: keyof Metrics): string { return k === 'attributedRevenue' ? 'revenue' : k; }

  isBest(res: CompareResult, key: keyof Metrics, it: CompareItem): boolean {
    return res.highlights.some(h => h.metric === key && h.campaignId === it.campaignId);
  }
}
