import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { map } from 'rxjs';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { PALETTE } from '../../../shared/components/analytics.shared';
import { UsageBarComponent } from '../../../shared/components/usage-bar/usage-bar';
import { Budget, BudgetDetail } from '../../../shared/models/models';
import { dayLabels, lineConfig, moneyTick } from '../../../shared/chart-helpers';

/** Histórico do orçamento: gasto acumulado × ritmo planejado linear. */
@Component({
  imports: [UI, ChartComponent, UsageBarComponent],
  templateUrl: './budget-detail-dialog.html',
  styleUrl: './budget-detail-dialog.css'
})
export class BudgetDetailDialog implements OnInit {
  readonly data = inject<Budget>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly detail = signal<BudgetDetail | null>(null);
  readonly chart = computed(() => {
    const d = this.detail()!;
    return lineConfig(dayLabels(d.history.map(h => h.date), this.lang.lang()), [
      { label: this.t.translate('budgets.cumulative'), data: d.history.map(h => h.cumulative), color: PALETTE.purple, fill: true },
      { label: this.t.translate('budgets.plannedPace'), data: d.history.map(h => h.plannedCumulative), color: PALETTE.gray, dashed: true },
      { label: this.t.translate('budgets.dailySpend'), data: d.history.map(h => h.spend), color: PALETTE.blue }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  ngOnInit(): void { this.api.get<BudgetDetail>(`/budgets/${this.data.id}`).subscribe(d => this.detail.set(d)); }
}
