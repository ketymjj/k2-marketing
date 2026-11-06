import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UI } from '../../../shared/ui';
import { LearnNavComponent } from '../learn-nav/learn-nav';
import { CalcResultsComponent } from '../calc-results/calc-results';
import { EXAMPLES, LearningExample } from '../learning.shared';

interface FunnelStage { key: string; value: number; rate: number | null; width: number; }

/** Exemplos educativos com dados fictícios. Nenhum dado é gravado. */
@Component({
  imports: [UI, LearnNavComponent, CalcResultsComponent],
  templateUrl: './examples-page.html',
  styleUrl: './examples-page.css'
})
export class ExamplesPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly examples = EXAMPLES;
  readonly selected = signal<LearningExample>(EXAMPLES[0]);
  readonly explainKeys = ['ctr', 'cpc', 'cpl', 'cpa', 'roas'];

  /** Funil impressões → cliques → leads → conversões, com a taxa de passagem entre etapas. */
  readonly funnel = computed<FunnelStage[]>(() => {
    const d = this.selected().data;
    const stages = [
      { key: 'impressions', value: d.impressions }, { key: 'clicks', value: d.clicks },
      { key: 'leads', value: d.leads }, { key: 'conversions', value: d.conversions }
    ];
    return stages.map((s, i) => ({
      ...s, width: 100 - i * 18,
      rate: i === 0 || !stages[i - 1].value ? null : (s.value / stages[i - 1].value) * 100
    }));
  });

  ngOnInit(): void {
    const key = this.route.snapshot.queryParamMap.get('example');
    const ex = EXAMPLES.find(e => e.key === key);
    if (ex) this.selected.set(ex);
  }

  select(e: LearningExample): void { this.selected.set(e); }
}
