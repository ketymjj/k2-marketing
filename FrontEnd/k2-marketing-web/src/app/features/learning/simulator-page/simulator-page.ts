import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UI } from '../../../shared/ui';
import { TipComponent } from '../../../shared/components/tip/tip';
import { LearnNavComponent } from '../learn-nav/learn-nav';
import { CalcResultsComponent } from '../calc-results/calc-results';
import { EXAMPLES, MetricInputs } from '../learning.shared';

type Field = 'spend' | 'impressions' | 'clicks' | 'leads' | 'conversions' | 'revenue';

/**
 * Simulador educativo: calcula os indicadores no navegador. Não chama a API e não altera
 * campanhas, métricas ou dados financeiros do sistema.
 */
@Component({
  imports: [UI, LearnNavComponent, CalcResultsComponent, TipComponent],
  templateUrl: './simulator-page.html',
  styleUrl: './simulator-page.css'
})
export class SimulatorPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly examples = EXAMPLES;
  readonly fields: { key: Field; money: boolean; icon: string }[] = [
    { key: 'spend', money: true, icon: 'payments' },
    { key: 'impressions', money: false, icon: 'visibility' },
    { key: 'clicks', money: false, icon: 'ads_click' },
    { key: 'leads', money: false, icon: 'person_add' },
    { key: 'conversions', money: false, icon: 'task_alt' },
    { key: 'revenue', money: true, icon: 'trending_up' }
  ];
  readonly values = signal<MetricInputs>({ spend: 500, impressions: 10_000, clicks: 400, leads: 40, conversions: 10, revenue: 1_250 });

  readonly warnings = computed(() => {
    const v = this.values();
    const w: string[] = [];
    if (v.clicks > v.impressions) w.push('learn.simulator.warnClicks');
    if (v.conversions > v.clicks && v.clicks > 0) w.push('learn.simulator.warnConversions');
    if (Object.values(v).some(x => x < 0)) w.push('learn.simulator.warnNegative');
    return w;
  });

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const ex = EXAMPLES.find(e => e.key === q.get('example'));
    if (ex) this.load(ex.key);
  }

  get(key: Field): number { return this.values()[key]; }

  set(key: Field, raw: unknown): void {
    const n = Number(raw);
    this.values.update(v => ({ ...v, [key]: Number.isFinite(n) ? n : 0 }));
  }

  load(key: string): void {
    const ex = EXAMPLES.find(e => e.key === key);
    if (!ex) return;
    const { spend, impressions, clicks, leads, conversions, revenue } = ex.data;
    this.values.set({ spend, impressions, clicks, leads, conversions, revenue });
  }

  clear(): void { this.values.set({ spend: 0, impressions: 0, clicks: 0, leads: 0, conversions: 0, revenue: 0 }); }
}
